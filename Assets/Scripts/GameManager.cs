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
        public BallInventory Inventory { get; private set; }
        /// <summary>Se compra o fusiona una bola.</summary>
        public event Action BallsChanged;

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
        /// <summary>Toca avalancha: la siguiente bola que caiga en un cubo la provoca.</summary>
        public bool RushPending { get; private set; }
        /// <summary>Multiplicador de valor de las bolas durante la Avalancha (el del cubo donde cayó la bola brillante).</summary>
        public double RushMultiplier { get; private set; } = 1;
        public AvalancheIntro Intro { get; private set; }
        public float RushTimeLeft { get; private set; }
        float rushTimer;

        // --- Sobrecarga ---
        public float Overload => State.overload;              // 0..1 mientras se llena
        public bool InOverload { get; private set; }
        /// <summary>Durante la Avalancha o la Sobrecarga no se compra nada: toda la atención al tablero.</summary>
        public bool ShopLocked => InRush || InOverload || RushPending || (Intro != null && Intro.Playing);
        public float OverloadTimeLeft { get; private set; }
        public double TotalMultiplier => Stats.GlobalMultiplier * (InOverload ? Stats.OverloadMultiplier : 1.0);
        int hitsThisSecond;
        float hitTimer;
        float hitsPerSecond;

        readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        readonly Dictionary<string, int> prestigeLevels = new Dictionary<string, int>();
        public event Action Prestiged;
        public event Action SummitReached;

        // ---------------------------------------------------------------- Anuncios recompensados
        public AdsService Ads { get; private set; }
        public const float AdSmallCooldown = 60f;    // comprar / fusionar gratis
        public const float AdEventCooldown = 240f;   // Avalancha / Sobrecarga
        float adBuyCd, adMergeCd, adRushCd, adOverloadCd;

        bool EventsIdle => !InRush && !RushPending && !InOverload && (Intro == null || !Intro.Playing);
        public bool AdBuyAvailable => adBuyCd <= 0 && !ShopLocked && Inventory.HasFreeSlot;
        public bool AdMergeAvailable => adMergeCd <= 0 && !ShopLocked && Inventory.MergeableLevel() > 0;
        public bool AdRushAvailable => adRushCd <= 0 && EventsIdle && Inventory.Count > 0;
        public bool AdOverloadAvailable => adOverloadCd <= 0 && EventsIdle && Inventory.Count > 0;

        public void WatchAdBuy() => Ads.ShowRewarded(() => { if (TryBuyBall(true)) adBuyCd = AdSmallCooldown; });
        public void WatchAdMerge() => Ads.ShowRewarded(() => { if (TryMerge(true)) adMergeCd = AdSmallCooldown; });
        public void WatchAdRush() => Ads.ShowRewarded(() => { if (EventsIdle) { BeginRushBall(); adRushCd = AdEventCooldown; } });
        public void WatchAdOverload() => Ads.ShowRewarded(() => { if (EventsIdle) { StartOverload(); adOverloadCd = AdEventCooldown; } });

        void TickAdCooldowns(float dt)
        {
            adBuyCd -= dt; adMergeCd -= dt; adRushCd -= dt; adOverloadCd -= dt;
        }

        /// <summary>Hay algo que comprar con lingotes ahora mismo (mejora de la tienda o sección nueva).</summary>
        public bool CanBuyAnyPrestige
        {
            get
            {
                foreach (var def in PrestigeCatalog.All)
                {
                    int lvl = GetPrestigeLevel(def.Id);
                    if (lvl < def.MaxLevel && State.cores >= def.CostAt(lvl)) return true;
                }
                foreach (UpgradeCategory c in System.Enum.GetValues(typeof(UpgradeCategory)))
                    if (IsTabNextToUnlock(c) && State.cores >= Balance.TabUnlockCost(c)) return true;
                return false;
            }
        }

        public bool CanReachSummit => !State.summitReached && State.money >= PrestigeBalance.SummitCost;

        /// <summary>Meta final: paga el precio de la Cima. El juego sigue después (modo infinito).</summary>
        public bool TryReachSummit()
        {
            if (ShopLocked || !CanReachSummit) return false;
            State.money -= PrestigeBalance.SummitCost;
            State.summitReached = true;
            State.summitPrestiges = State.prestigeCount;
            Save();
            MoneyChanged?.Invoke();
            SummitReached?.Invoke();
            return true;
        }
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
            // Hasta 120 fps (o el refresco de la pantalla si es menor): todo se ve más fluido
            QualitySettings.vSyncCount = 0;
            int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            Application.targetFrameRate = refresh >= 60 ? Mathf.Min(120, refresh) : 120;

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
            foreach (var e in State.prestigeUpgrades)
            {
                // "Riqueza" (multiplicador de dinero) ya no existe: se devuelven los lingotes gastados
                if (e.id == PrestigeIds.Multiplier && e.level > 0)
                {
                    for (int l = 0; l < e.level; l++) State.cores += System.Math.Ceiling(System.Math.Pow(1.6, l));
                    e.level = 0;
                }
                prestigeLevels[e.id] = e.level;
            }
            // partidas anteriores a los candados: abrir las secciones donde ya hay mejoras compradas
            foreach (var e in State.upgrades)
            {
                var def = UpgradeCatalog.Get(e.id);
                if (def != null && e.level > 0 && !State.unlockedTabs.Contains(def.Category.ToString()))
                    State.unlockedTabs.Add(def.Category.ToString());
            }

            Stats = new Stats(this);
            Stats.Recalculate();
            Inventory = new BallInventory(this);
            Inventory.Load(State.balls);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.07f, 0.08f, 0.13f);
            EnsureEventSystem();

            Background = new GameObject("Background").AddComponent<BackgroundView>();
            Background.transform.SetParent(transform, false);

            Board = new GameObject("Board").AddComponent<BoardView>();
            Board.transform.SetParent(transform, false);
            Board.SetPegStage(PegStyles.StageFor(GetLevel(UpgradeIds.PegFlat)), false);
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);

            Fx = new GameObject("Fx").AddComponent<FxSystem>();
            Fx.transform.SetParent(transform, false);
            Audio = new GameObject("Audio").AddComponent<AudioFx>();
            Audio.transform.SetParent(transform, false);

            Intro = new GameObject("AvalancheIntro").AddComponent<AvalancheIntro>();
            Intro.transform.SetParent(transform, false);
            Ads = new GameObject("Ads").AddComponent<AdsService>();
            Ads.transform.SetParent(transform, false);
            Ads.Init();
            var star = new GameObject("ShootingStar").AddComponent<ShootingStar>();
            star.transform.SetParent(transform, false);
            star.Init(this);

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
            TickAdCooldowns(Time.unscaledDeltaTime);
            if (Board != null) Board.Focus = ShopLocked;   // tablero algo más grande durante los eventos
            income.Tick(dt);
            UpdateOverload(dt);
            UpdateRush(dt);
            // nunca quedarse atascado: sin bolas, siempre hay dinero para comprar la siguiente
            if (Inventory.Count == 0 && State.money < Inventory.BuyCost) { State.money = Inventory.BuyCost; MoneyChanged?.Invoke(); }

            // Goteo: las bolas del jugador caen solas, una detrás de otra
            float interval = LaunchInterval;
            autoTimer += dt;
            int launched = 0;
            while (autoTimer >= interval && launched < 8) // varias por frame si el ritmo es muy alto
            {
                var next = Inventory.TakeNextWaiting();
                if (next != null) Balls.Launch(next);
                else if ((InRush || InOverload) && Balls.ActiveCount < MaxBallsNow) Balls.LaunchTemp(Inventory.RandomLevel());
                else { autoTimer = interval; break; } // todas cayendo: espera
                autoTimer -= interval;
                launched++;
            }

            saveTimer += dt;
            if (saveTimer >= AutoSaveInterval) Save();

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (kb.mKey.wasPressedThisFrame) DebugAddMoney();
                if (kb.eKey.wasPressedThisFrame && !InRush && !RushPending && !Intro.Playing) BeginRushBall();
#endif
            }
#endif
        }

        // --- Lanzamiento ---

        public int MaxBallsNow
        {
            get
            {
                // bolas en pantalla permitidas: las tuyas, y en Sobrecarga/Avalancha copias temporales extra
                float max = Mathf.Max(1, Inventory.Count);
                if (InOverload) max *= Balance.OverloadCapacityBoost;
                if (InRush) max = Mathf.Min(max * Balance.RushBoost, Mathf.Max(Balance.RushMaxBalls, Inventory.Count));
                return Mathf.RoundToInt(max);
            }
        }

        /// <summary>Segundos entre lanzamientos automáticos (0 = sin auto-lanzador).</summary>
        public float LaunchInterval
        {
            get
            {
                float interval = Stats.AutoInterval;
                if (InOverload) interval /= Balance.OverloadLaunchBoost;
                if (InRush) interval /= Balance.RushBoost;
                return interval;
            }
        }
        // --- Bolas: comprar y fusionar ---

        public bool TryBuyBall(bool free = false)
        {
            if (ShopLocked || !Inventory.TryBuy(free)) return false;
            Audio.Buy();
            BallsChanged?.Invoke();
            MoneyChanged?.Invoke();
            return true;
        }

        /// <summary>Fusiona las dos bolas de mayor nivel. Si estaban cayendo desaparecen y la nueva cae desde arriba.</summary>
        public bool TryMerge(bool free = false)
        {
            if (ShopLocked || !Inventory.TryMerge(out var result, out var consumed, free)) return false;
            var a = Balls.RemoveFlying(consumed);
            var b = Balls.RemoveFlying(result);
            StartCoroutine(MergeSequence(result, a, b));
            BallsChanged?.Invoke();
            MoneyChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Fusión en el centro de la pantalla: aparecen las dos bolas, giran en espiral una hacia la otra,
        /// chocan con un destello, sale la nueva bola grande con su nombre y sube a lo alto del tablero para caer.
        /// </summary>
        System.Collections.IEnumerator MergeSequence(OwnedBall result, Vector2? fromA, Vector2? fromB)
        {
            int oldLevel = result.Level - 1, newLevel = result.Level;
            var oldColor = Balance.LevelColor(oldLevel);
            var newColor = Balance.LevelColor(newLevel);
            Vector2 spawn = Board.SpawnPos;
            Vector2 center = Vector2.Lerp(spawn, Board.BucketCenterPos(Board.Rows / 2), 0.45f);
            const float big = 1.1f;            // tamaño de las bolas en la animación (en unidades)
            float normal = BoardView.BallRadius * 2f * Mathf.Min(1.6f, 1f + 0.07f * (newLevel - 1));

            var a = MergeSprite(oldLevel);
            var b = MergeSprite(oldLevel);
            Audio.Special(0.7f);

            // 1) aparecen a los lados del centro
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float s = EaseOutBack(t / 0.35f) * big;
                a.transform.position = center + Vector2.left * 1.6f;
                b.transform.position = center + Vector2.right * 1.6f;
                a.transform.localScale = b.transform.localScale = Vector3.one * s;
                yield return null;
            }

            // 2) espiral cada vez más rápida hacia el centro
            const float spin = 0.75f;
            float soundTimer = 0;
            for (float t = 0; t < spin; t += Time.unscaledDeltaTime)
            {
                float k = t / spin, e = k * k;
                float radius = Mathf.Lerp(1.6f, 0f, e);
                float ang = Mathf.PI + e * Mathf.PI * 3f;
                var off = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * radius;
                a.transform.position = center + off;
                b.transform.position = center - off;
                a.transform.Rotate(0, 0, -900f * Time.unscaledDeltaTime * (0.3f + k));
                b.transform.Rotate(0, 0, -900f * Time.unscaledDeltaTime * (0.3f + k));
                if (UnityEngine.Random.value < 0.6f)
                {
                    Fx.Burst(a.transform.position, oldColor, 1, 0.8f, 0.07f, 0.35f, 0f);
                    Fx.Burst(b.transform.position, oldColor, 1, 0.8f, 0.07f, 0.35f, 0f);
                }
                soundTimer -= Time.unscaledDeltaTime;
                if (soundTimer <= 0) { soundTimer = Mathf.Lerp(0.18f, 0.05f, k); Audio.Zap(0.7f + k); }
                yield return null;
            }
            Destroy(a.gameObject);
            Destroy(b.gameObject);

            // 3) ¡choque!
            Fx.Flash(center, new Color(1, 1, 1, 0.95f), 3.2f, 0.35f);
            Fx.Ring(center, newColor, 3.4f, 0.55f);
            Fx.Ring(center, Color.white, 1.8f, 0.3f);
            Fx.Burst(center, newColor, 40, 8f, 0.11f, 0.9f, -3f);
            Board.Shake(0.35f);
            Background.Pulse(newColor, 0.7f);
            Audio.Record();
            UI.Popup(center + Vector2.up * 1.1f, "¡" + Balance.LevelName(newLevel) + "!", newColor, 1.3f);

            // 4) la nueva bola aparece grande y late un momento
            var n = MergeSprite(newLevel);
            n.transform.position = center;
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float s = EaseOutBack(Mathf.Clamp01(t / 0.25f)) * big * 1.25f * (1f + 0.05f * Mathf.Sin(t * 20f));
                n.transform.localScale = Vector3.one * s;
                yield return null;
            }

            // 5) sube a lo alto del tablero encogiendo hasta su tamaño y cae
            Vector2 from = center;
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f, e = k * k;
                n.transform.position = Vector2.Lerp(from, spawn, e);
                n.transform.localScale = Vector3.one * Mathf.Lerp(big * 1.25f, normal, e);
                yield return null;
            }
            Destroy(n.gameObject);
            Balls.Launch(result, true);
        }

        SpriteRenderer MergeSprite(int level)
        {
            var go = new GameObject("MergeBall");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BallMaterials.GetSprite(level);
            sr.sortingOrder = 25;
            go.transform.localScale = Vector3.zero;
            return sr;
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        // --- Economía ---

        /// <summary>Premio directo (ya calculado con multiplicadores), p. ej. la estrella fugaz.</summary>
        public void GrantBonus(double amount)
        {
            State.money += amount;
            State.totalEarned += amount;
            State.runEarned += amount;
            MoneyChanged?.Invoke();
        }

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
                if (RushPending || Intro.Playing)
                {
                    // si la bola brillante desaparece (p. ej. al ampliar el tablero), se lanza otra
                    if (RushPending && !Intro.Playing && !Balls.RushBallActive) Balls.LaunchRushBall();
                    return;
                }
                rushTimer -= dt;
                if (rushTimer <= 0) BeginRushBall();
                return;
            }
            RushTimeLeft -= dt;
            if (RushTimeLeft <= 0)
            {
                InRush = false;
                RushMultiplier = 1;
                rushTimer = UnityEngine.Random.Range(Balance.RushMinDelay, Balance.RushMaxDelay) * Stats.RushDelayFactor;
                RushEnded?.Invoke();
            }
        }

        /// <summary>Toca avalancha: cae la bola brillante que decidirá el multiplicador.</summary>
        void BeginRushBall()
        {
            RushPending = true;
            Balls.LaunchRushBall();
        }

        /// <summary>La bola brillante ha caído en este cubo: empieza la cinemática y la Avalancha con ese multiplicador.</summary>
        public void TriggerRushAt(int bucket, double multiplier = 1)
        {
            if (InRush || Intro.Playing) return;
            RushPending = false;
            RushMultiplier = multiplier;
            Intro.Play(bucket, () =>
            {
                StartRush();
                Balls.LaunchVolley(Mathf.Min(40, MaxBallsNow - Balls.ActiveCount)); // la ráfaga inicial
            });
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

        public double CostOf(UpgradeDef def) => def.PurchaseCost(GetLevel(def.Id));

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
            Stats.Recalculate();                     // (recalcula tras gastar lingotes)
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
            if (ShopLocked) return false;
            int lvl = GetLevel(def.Id);
            if (lvl >= def.MaxLevel || !IsVisible(def)) return false;
            double cost = CostOf(def);
            if (State.money < cost) return false;

            State.money -= cost;
            int newLevel = def.NextLevel(lvl);
            SetLevel(def.Id, newLevel);

            int oldRows = Stats.Rows;
            Stats.Recalculate();

            if (Stats.Rows != oldRows)
            {
                Balls.ClearAll();
                Board.SetPegStage(PegStyles.StageFor(GetLevel(UpgradeIds.PegFlat)), false);
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            }
            else if (def.Id == UpgradeIds.GoldenPegs || def.Id == UpgradeIds.ClonePegs)
            {
                Board.AssignPegTypes(Stats.GoldenPegs, Stats.ClonePegs);
            }

            if (def.Id == UpgradeIds.PegFlat)
            {
                // cada mejora recorre los pinchos con una ola; al cambiar de material, además, se anuncia
                int stage = PegStyles.StageFor(newLevel);
                if (Board.SetPegStage(stage, true))
                {
                    var c = PegStyles.Color(stage);
                    UI.Popup(Board.PegWorldPos(Stats.Rows / 2, Stats.Rows / 4), "¡Pinchos de " + PegStyles.Name(stage) + "!", c, 1.2f);
                    Background.Pulse(c, 0.6f);
                    Board.Shake(0.25f);
                    Audio.Special(1.2f);
                }
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
            if (ShopLocked) return false;
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
            State.balls = new System.Collections.Generic.List<int>();
            State.ballsBought = 0;
            State.runMaxLevel = 0;

            // ventajas de inicio compradas en la tienda
            int startAuto = PrestigeBalance.StartAuto(GetPrestigeLevel(PrestigeIds.StartAuto));
            if (startAuto > 0) SetLevel(UpgradeIds.AutoLauncher, startAuto);
            State.money = Balance.FirstBallCost + PrestigeBalance.StartMoney(GetPrestigeLevel(PrestigeIds.StartMoney)); // siempre alcanza para la primera bola

            if (InOverload) { InOverload = false; OverloadEnded?.Invoke(); }
            if (InRush) { InRush = false; RushMultiplier = 1; RushEnded?.Invoke(); }
            RushPending = false;
            rushTimer = 0;
            autoTimer = 0;
            Stats.Recalculate();
            Balls.ClearAll();
            Inventory.Load(State.balls);
            Board.SetPegStage(PegStyles.StageFor(GetLevel(UpgradeIds.PegFlat)), false);
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            Save();
            ProgressReset?.Invoke();
            Prestiged?.Invoke();
            BallsChanged?.Invoke();
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
            Inventory.Load(State.balls);
            BallsChanged?.Invoke();
            Board.SetPegStage(PegStyles.StageFor(GetLevel(UpgradeIds.PegFlat)), false);
            Board.Build(Stats.Rows, Stats.GoldenPegs, Stats.ClonePegs);
            autoTimer = 0;
            if (InOverload) { InOverload = false; OverloadEnded?.Invoke(); }
            if (InRush) { InRush = false; RushMultiplier = 1; RushEnded?.Invoke(); }
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
