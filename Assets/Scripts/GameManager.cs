using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Plinko
{
    /// <summary>Punto central: estado, economía, mejoras, auto-lanzador y guardado.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public Stats Stats { get; private set; }
        public BoardView Board { get; private set; }
        public BallSystem Balls { get; private set; }
        public GameUI UI { get; private set; }
        public FxSystem Fx { get; private set; }
        public AudioFx Audio { get; private set; }
        public BackgroundView Background { get; private set; }

        public double Money => State.money;
        public double IncomePerSecond => income.PerSecond;

        /// <summary>Se dispara al cambiar el dinero (puede ser muchas veces por frame: la UI solo marca "sucio").</summary>
        public event Action MoneyChanged;
        public event Action UpgradesChanged;
        public event Action ProgressReset;
        public event Action OverloadStarted, OverloadEnded;
        public event Action RushStarted, RushEnded;

        // --- Avalancha ---
        public bool InRush { get; private set; }
        public float RushTimeLeft { get; private set; }
        float rushTimer;

        // --- Sobrecarga ---
        public float Overload => State.overload;              // 0..1 mientras se llena
        public bool InOverload { get; private set; }
        public float OverloadTimeLeft { get; private set; }
        public double TotalMultiplier => Stats.GlobalMultiplier * (InOverload ? Stats.OverloadMultiplier : 1.0);
        int hitsThisSecond;
        float hitTimer;
        float hitsPerSecond;

        readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        readonly Dictionary<string, int> prestigeLevels = new Dictionary<string, int>();
        public event Action Prestiged;
        readonly IncomeTracker income = new IncomeTracker();
        float autoTimer, manualCooldown, saveTimer;
        const float AutoSaveInterval = 15f;

        // Arranque automático: no hace falta tocar la escena.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance == null) new GameObject("Game").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;

            // Juego de móvil en vertical
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;

            State = SaveSystem.Load();
            foreach (var e in State.upgrades)
            {
                // si el balance bajó el máximo de una mejora, las partidas viejas se ajustan a él
                var def = UpgradeCatalog.Get(e.id);
                if (def != null && e.level > def.MaxLevel) e.level = def.MaxLevel;
                levels[e.id] = e.level;
            }
            foreach (var e in State.prestigeUpgrades) prestigeLevels[e.id] = e.level;
            // partidas anteriores a los candados: abrir las secciones donde ya hay mejoras compradas
            foreach (var e in State.upgrades)
            {
                var def = UpgradeCatalog.Get(e.id);
                if (def != null && e.level > 0 && !State.unlockedTabs.Contains(def.Category.ToString()))
                    State.unlockedTabs.Add(def.Category.ToString());
            }

            Stats = new Stats(this);
            Stats.Recalculate();

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.07f, 0.08f, 0.13f);
            EnsureEventSystem();

            Background = new GameObject("Background").AddComponent<BackgroundView>();
            Background.transform.SetParent(transform, false);

            Board = new GameObject("Board").AddComponent<BoardView>();
            Board.transform.SetParent(transform, false);
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);

            Fx = new GameObject("Fx").AddComponent<FxSystem>();
            Fx.transform.SetParent(transform, false);
            Audio = new GameObject("Audio").AddComponent<AudioFx>();
            Audio.transform.SetParent(transform, false);

            Balls = new GameObject("BallSystem").AddComponent<BallSystem>();
            Balls.transform.SetParent(transform, false);
            Balls.Init(this, Board);

            UI = new GameObject("UI").AddComponent<GameUI>();
            UI.transform.SetParent(transform, false);
            UI.Init(this);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void Update()
        {
            float dt = Time.deltaTime;
            manualCooldown -= dt;
            income.Tick(dt);
            UpdateOverload(dt);
            UpdateRush(dt);

            float interval = LaunchInterval;
            if (interval > 0)
            {
                autoTimer += dt;
                int launched = 0;
                while (autoTimer >= interval && launched < 8) // varias por frame si el ritmo es muy alto
                {
                    if (!CanLaunch) { autoTimer = interval; break; } // espera a que haya hueco
                    Balls.Launch();
                    autoTimer -= interval;
                    launched++;
                }
            }

            saveTimer += dt;
            if (saveTimer >= AutoSaveInterval) Save();

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.isPressed) ManualLaunch();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (kb.mKey.wasPressedThisFrame) DebugAddMoney();
                if (kb.eKey.wasPressedThisFrame && !InRush) StartRush();
#endif
            }
#endif
        }

        // --- Lanzamiento ---

        public int MaxBallsNow
        {
            get
            {
                float max = Stats.MaxBalls;
                if (InOverload) max *= Balance.OverloadCapacityBoost;
                if (InRush) max = Mathf.Min(max * Balance.RushBoost, Mathf.Max(Balance.RushMaxBalls, Stats.MaxBalls));
                return Mathf.RoundToInt(max);
            }
        }

        /// <summary>Segundos entre lanzamientos automáticos (0 = sin auto-lanzador).</summary>
        public float LaunchInterval
        {
            get
            {
                float interval = Stats.AutoInterval;
                if (InRush && interval <= 0) interval = Balance.RushMinInterval;
                if (interval <= 0) return 0;
                if (InOverload) interval /= Balance.OverloadLaunchBoost;
                if (InRush) interval /= Balance.RushBoost;
                return interval;
            }
        }
        public bool CanLaunch => Balls.MainCount < MaxBallsNow;
        public bool ManualReady => manualCooldown <= 0 && CanLaunch;

        public void ManualLaunch()
        {
            if (!ManualReady) return;
            Balls.Launch();
            manualCooldown = Balance.ManualCooldown;
        }

        // --- Economía ---

        public void Earn(double amount)
        {
            amount *= TotalMultiplier;
            State.money += amount;
            State.totalEarned += amount;
            State.runEarned += amount;
            income.Add(amount);
            MoneyChanged?.Invoke();
        }

        /// <summary>Cada rebote en un pincho carga el medidor de Sobrecarga.</summary>
        public void RegisterHit()
        {
            hitsThisSecond++;
            if (InOverload) return;
            float needed = Mathf.Max(Balance.OverloadMinHits, hitsPerSecond * Balance.OverloadFillSeconds) * Stats.OverloadFill;
            State.overload += 1f / needed;
            if (State.overload >= 1f) StartOverload();
        }

        void UpdateOverload(float dt)
        {
            hitTimer += dt;
            if (hitTimer >= 1f)
            {
                hitTimer -= 1f;
                hitsPerSecond = hitsPerSecond <= 0 ? hitsThisSecond : Mathf.Lerp(hitsPerSecond, hitsThisSecond, 0.25f);
                hitsThisSecond = 0;
            }

            if (!InOverload) return;
            OverloadTimeLeft -= dt;
            State.overload = Mathf.Clamp01(OverloadTimeLeft / Stats.OverloadDuration);
            if (OverloadTimeLeft <= 0)
            {
                InOverload = false;
                State.overload = 0;
                OverloadEnded?.Invoke();
            }
        }

        void UpdateRush(float dt)
        {
            if (!InRush)
            {
                if (rushTimer <= 0) rushTimer = UnityEngine.Random.Range(Balance.RushMinDelay, Balance.RushMaxDelay) * Stats.RushDelayFactor;
                rushTimer -= dt;
                if (rushTimer <= 0) StartRush();
                return;
            }
            RushTimeLeft -= dt;
            if (RushTimeLeft <= 0)
            {
                InRush = false;
                rushTimer = UnityEngine.Random.Range(Balance.RushMinDelay, Balance.RushMaxDelay) * Stats.RushDelayFactor;
                RushEnded?.Invoke();
            }
        }

        public void StartRush()
        {
            InRush = true;
            RushTimeLeft = Stats.RushDuration;
            RushStarted?.Invoke();
        }

        void StartOverload()
        {
            InOverload = true;
            OverloadTimeLeft = Stats.OverloadDuration;
            State.overload = 1f;
            OverloadStarted?.Invoke();
        }

        /// <summary>Registra lo ganado por una bola al caer. Devuelve true si es un nuevo récord.</summary>
        public bool RegisterDrop(double amount)
        {
            if (amount <= State.bestDrop) return false;
            State.bestDrop = amount;
            return true;
        }

        // --- Mejoras ---

        public int GetLevel(string id) => levels.TryGetValue(id, out int l) ? l : 0;

        public double CostOf(UpgradeDef def) => def.CostAt(GetLevel(def.Id));

        /// <summary>Requisito de desbloqueo cumplido (o ya tiene niveles comprados).</summary>
        public bool IsUnlocked(UpgradeDef def) =>
            IsTabUnlocked(def.Category) &&
            (def.RequiresId == null || GetLevel(def.RequiresId) >= def.RequiresLevel || GetLevel(def.Id) > 0);

        // --- Secciones (pestañas) con candado ---
        public event Action<UpgradeCategory> TabUnlocked;

        public bool IsTabUnlocked(UpgradeCategory c) =>
            Balance.TabUnlockCost(c) <= 0 || State.unlockedTabs.Contains(c.ToString());

        /// <summary>Solo se puede comprar la siguiente sección a la última desbloqueada.</summary>
        public bool IsTabNextToUnlock(UpgradeCategory c) => !IsTabUnlocked(c) && ((int)c == 0 || IsTabUnlocked((UpgradeCategory)((int)c - 1)));

        public bool TryUnlockTab(UpgradeCategory c)
        {
            if (!IsTabNextToUnlock(c)) return false;
            double cost = Balance.TabUnlockCost(c);
            if (State.cores < cost) return false;   // se paga con lingotes de oro
            State.cores -= cost;
            State.unlockedTabs.Add(c.ToString());
            Stats.Recalculate();                     // los lingotes sin gastar dan bonus
            Audio.Record();
            Save();
            TabUnlocked?.Invoke(c);
            MoneyChanged?.Invoke();
            UpgradesChanged?.Invoke();
            return true;
        }

        /// <summary>Aplica al tablero actual (p. ej. cubos que solo existen con más filas).</summary>
        public bool AppliesToBoard(UpgradeDef def) => def.Visible == null || def.Visible(this);

        public bool IsVisible(UpgradeDef def) => IsUnlocked(def) && AppliesToBoard(def);

        public bool TryBuy(UpgradeDef def)
        {
            int lvl = GetLevel(def.Id);
            if (lvl >= def.MaxLevel || !IsVisible(def)) return false;
            double cost = CostOf(def);
            if (State.money < cost) return false;

            State.money -= cost;
            SetLevel(def.Id, lvl + 1);

            int oldRows = Stats.Rows;
            Stats.Recalculate();

            if (Stats.Rows != oldRows)
            {
                Balls.ClearAll();
                Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            }
            else if (def.Id == UpgradeIds.GoldenPegs || def.Id == UpgradeIds.ClonePegs)
            {
                Board.AssignPegTypes(Stats.GoldenPegs, Stats.ClonePegs);
            }

            int groupIndex = System.Array.IndexOf(UpgradeIds.BucketGroups, def.Id);
            if (groupIndex >= 0) Board.PulseBucketGroup(groupIndex);

            Audio.Buy();
            MoneyChanged?.Invoke();
            UpgradesChanged?.Invoke();
            return true;
        }

        void SetLevel(string id, int level)
        {
            levels[id] = level;
            var entry = State.upgrades.Find(e => e.id == id);
            if (entry == null) State.upgrades.Add(new UpgradeEntry { id = id, level = level });
            else entry.level = level;
        }

        // --- Guardado ---

        public void Save()
        {
            saveTimer = 0;
            SaveSystem.Save(State);
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        // --- Prestigio ---

        public int GetPrestigeLevel(string id) => prestigeLevels.TryGetValue(id, out int l) ? l : 0;

        /// <summary>Lingotes que se ganarían reiniciando ahora.</summary>
        public double CoresOnPrestige => PrestigeBalance.Reward(State.prestigeCount);
        /// <summary>Precio en dinero del próximo prestigio.</summary>
        public double PrestigeCost => PrestigeBalance.PrestigeCost(State.prestigeCount);
        public bool CanPrestige => State.money >= PrestigeCost;

        public bool TryBuyPrestige(PrestigeDef def)
        {
            int lvl = GetPrestigeLevel(def.Id);
            if (lvl >= def.MaxLevel) return false;
            double cost = def.CostAt(lvl);
            if (State.cores < cost) return false;
            State.cores -= cost;
            prestigeLevels[def.Id] = lvl + 1;
            var entry = State.prestigeUpgrades.Find(e => e.id == def.Id);
            if (entry == null) State.prestigeUpgrades.Add(new UpgradeEntry { id = def.Id, level = lvl + 1 });
            else entry.level = lvl + 1;
            Stats.Recalculate();
            Audio.Buy();
            Save();
            UpgradesChanged?.Invoke();
            return true;
        }

        /// <summary>Reinicia la partida a cambio de lingotes de oro. Las mejoras de prestigio se conservan.</summary>
        public void DoPrestige()
        {
            if (!CanPrestige) return;
            State.cores += CoresOnPrestige;
            State.prestigeCount++;
            State.runEarned = 0;
            State.overload = 0;
            State.upgrades.Clear();
            levels.Clear();

            // ventajas de inicio compradas en la tienda
            int startAuto = PrestigeBalance.StartAuto(GetPrestigeLevel(PrestigeIds.StartAuto));
            if (startAuto > 0) SetLevel(UpgradeIds.AutoLauncher, startAuto);
            State.money = PrestigeBalance.StartMoney(GetPrestigeLevel(PrestigeIds.StartMoney));

            if (InOverload) { InOverload = false; OverloadEnded?.Invoke(); }
            if (InRush) { InRush = false; RushEnded?.Invoke(); }
            rushTimer = 0;
            autoTimer = 0;
            Stats.Recalculate();
            Balls.ClearAll();
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            Save();
            ProgressReset?.Invoke();
            Prestiged?.Invoke();
            MoneyChanged?.Invoke();
            UpgradesChanged?.Invoke();
        }

        [ContextMenu("Borrar partida")]
        public void ResetProgress()
        {
            SaveSystem.Delete();
            State = new GameState();
            levels.Clear();
            prestigeLevels.Clear();
            Stats.Recalculate();
            Balls.ClearAll();
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            autoTimer = 0;
            if (InOverload) { InOverload = false; OverloadEnded?.Invoke(); }
            if (InRush) { InRush = false; RushEnded?.Invoke(); }
            rushTimer = 0;
            ProgressReset?.Invoke();
            MoneyChanged?.Invoke();
            UpgradesChanged?.Invoke();
        }

        void DebugAddMoney()
        {
            State.money += Math.Max(100, State.money);
            MoneyChanged?.Invoke();
        }

        /// <summary>Media móvil de ingresos de los últimos 10 segundos.</summary>
        class IncomeTracker
        {
            const int Slots = 10;
            readonly double[] buckets = new double[Slots];
            int current;
            float timer;
            public double PerSecond { get; private set; }

            public void Add(double v) => buckets[current] += v;

            public void Tick(float dt)
            {
                timer += dt;
                if (timer < 1f) return;
                timer -= 1f;
                double sum = 0;
                for (int i = 0; i < Slots; i++) sum += buckets[i];
                PerSecond = sum / Slots;
                current = (current + 1) % Slots;
                buckets[current] = 0;
            }
        }
    }
}
