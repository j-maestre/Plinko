using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Anima las bolas sin física: el camino se decide al lanzar y cada tramo entre pinchos
    /// es un arco parabólico (x lineal, y con parábola), con pequeñas variaciones aleatorias.
    /// Las bolas no colisionan entre sí. Un único Update recorre todas las bolas activas.
    /// </summary>
    public class BallSystem : MonoBehaviour
    {
        class Ball
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public TrailRenderer Trail;
            public bool[] Path;
            public int Row, Col;        // destino del tramo actual (fila == Rows -> cubo)
            public Vector2 From, To;
            public float T, Duration, Arc;
            public bool Dropping;       // primer tramo: caída libre hasta el pincho superior
            public double Value;
            public bool IsClone;
            public float Pulse;
            public Color BaseColor;
            public Vector2 Prev;        // posición del frame anterior (para estirar según velocidad)
            public float Impact;        // aplastamiento al chocar
            public float Spawn;         // escala de aparición 0..1
            public bool IsGolden;       // bola dorada: vale x5, brilla y suelta chispas
            public SpriteRenderer Halo;
            public Transform Shine;     // reflejo blanco fijo arriba a la izquierda
            public float HaloAlpha;     // brillo de base según el tipo de bola
            public bool Echoing;        // está haciendo el rebote extra del eco
            public float SparkTimer;
            public OwnedBall Owner;     // bola del jugador (null = clon o copia temporal)
            public float SizeMul = 1f;  // las de más nivel son algo más grandes
        }

        const float DropDuration = 0.35f;
        const float MinSegment = 0.24f, MaxSegment = 0.30f;
        const float MinArc = 0.18f, MaxArc = 0.32f;
        const float Jitter = 0.04f;

        static readonly Color CritColor = new Color(1f, 0.45f, 0.85f);
        static readonly Color BonusColor = new Color(0.55f, 1f, 0.9f);
        static readonly Color GoldenFx = new Color(1f, 0.82f, 0.3f);
        static readonly Color RecordColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color CloneFx = new Color(0.5f, 0.95f, 0.62f);
        static readonly Color GoldenBallColor = new Color(1f, 0.88f, 0.45f);
        static readonly Color LightningColor = new Color(0.65f, 0.9f, 1f);
        static readonly Color ChargeColor = new Color(0.45f, 0.95f, 1f);
        static readonly Color EchoColor = new Color(0.85f, 0.75f, 1f);
        readonly List<Vector2Int> chain = new List<Vector2Int>();
        readonly List<Vector2> boltPoints = new List<Vector2>();

        GameManager gm;
        BoardView board;
        Transform root;
        readonly List<Ball> active = new List<Ball>();
        readonly Stack<Ball> pool = new Stack<Ball>();

        /// <summary>Bolas del jugador que están cayendo ahora mismo.</summary>
        public int MainCount { get; private set; }
        /// <summary>Todas las bolas en el tablero (incluye clones y copias temporales).</summary>
        public int ActiveCount => active.Count;

        public void Init(GameManager gm, BoardView board)
        {
            this.gm = gm;
            this.board = board;
            root = new GameObject("Balls").transform;
            root.SetParent(transform, false);
        }

        /// <summary>Lanza una bola del jugador desde arriba.</summary>
        public void Launch(OwnedBall owner, bool fanfare = false)
        {
            owner.InFlight = true;
            LaunchLevel(owner, owner.Level, fanfare);
        }

        /// <summary>Copia temporal (Avalancha, Sobrecarga): cae una vez y desaparece.</summary>
        public void LaunchTemp(int level) => LaunchLevel(null, level, false);

        void LaunchLevel(OwnedBall owner, int level, bool fanfare)
        {
            var b = Get();
            b.Owner = owner;
            b.IsClone = false;
            b.Value = Balance.BallValue(gm.Stats.TierIndex, level);
            b.SizeMul = Mathf.Min(1.6f, 1f + 0.07f * (level - 1));
            var color = Balance.LevelColor(level);
            b.BaseColor = color;
            b.Sr.color = color;
            b.Path = PathGenerator.WithDrift(board.Rows, (float)gm.Stats.DriftChance);
            b.HaloAlpha = TierHalo(gm.Stats.TierIndex);
            SetGolden(b, gm.Stats.GoldenBallChance > 0 && Random.value < gm.Stats.GoldenBallChance);
            if (b.IsGolden) b.Value *= gm.Stats.GoldenBallMultiplier;
            b.Row = 0;
            b.Col = 0;
            float spread = gm.InRush ? 0.35f : 0.06f;
            b.From = board.SpawnPos + new Vector2(Random.Range(-spread, spread), Random.Range(0f, gm.InRush ? 0.3f : 0f));
            b.To = board.PegContact(0, 0) + new Vector2(Random.Range(-Jitter, Jitter), 0);
            b.T = 0;
            b.Duration = DropDuration;
            b.Arc = 0;
            b.Dropping = true;
            b.Tr.localPosition = b.From;
            b.Prev = b.From;
            b.Spawn = 0;
            ResetTrail(b);
            active.Add(b);
            if (owner != null) MainCount++;

            gm.Fx.Ring(b.From, Alpha(b.BaseColor, 0.6f), fanfare ? 1.8f : 0.7f, fanfare ? 0.45f : 0.25f);
            if (fanfare) gm.Fx.Burst(b.From, b.BaseColor, 18, 5f, 0.08f, 0.6f);
            gm.Audio.Launch();
            if (b.IsGolden)
            {
                gm.Fx.Burst(b.From, GoldenBallColor, 16, 5f, 0.08f, 0.6f);
                gm.Fx.Ring(b.From, GoldenBallColor, 1.6f, 0.4f);
                gm.Audio.Special(1.5f);
                gm.Background.Pulse(GoldenBallColor, 0.5f);
            }
        }

        /// <summary>Ráfaga de bolas a la vez (inicio de la avalancha).</summary>
        public void LaunchVolley(int count)
        {
            for (int i = 0; i < count; i++) LaunchTemp(gm.Inventory.RandomLevel());
        }

        /// <summary>Quita del tablero la bola de ese dueño si está cayendo (al fusionar).</summary>
        public void RemoveFlying(OwnedBall owner)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].Owner != owner) continue;
                Vector2 pos = active[i].Tr.position;
                gm.Fx.Burst(pos, active[i].BaseColor, 8, 3f, 0.06f, 0.35f);
                Recycle(i, false);
                return;
            }
            owner.InFlight = false;
        }

        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Recycle(i);
            MainCount = 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                b.T += dt / b.Duration;

                if (b.T >= 1f)
                {
                    b.Tr.localPosition = b.To;
                    if (!Arrive(b)) { Recycle(i); continue; }
                }
                else
                {
                    b.Tr.localPosition = Evaluate(b);
                }

                UpdateVisual(b, dt);

                if (!b.IsGolden && b.HaloAlpha > 0)
                    b.Halo.color = Alpha(b.BaseColor, b.HaloAlpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f + i)));

                if (b.IsGolden)
                {
                    b.SparkTimer -= dt;
                    if (b.SparkTimer <= 0)
                    {
                        b.SparkTimer = 0.05f;
                        gm.Fx.Burst(b.Tr.position, GoldenBallColor, 1, 1.2f, 0.06f, 0.4f, -2f);
                    }
                    float glow = 0.5f + 0.15f * Mathf.Sin(Time.time * 12f);
                    b.Halo.color = Alpha(GoldenBallColor, glow);
                }
            }
        }

        static Vector2 Evaluate(Ball b)
        {
            float t = b.T;
            if (b.Dropping)
                return Vector2.LerpUnclamped(b.From, b.To, t * t); // aceleración tipo gravedad

            float x = Mathf.LerpUnclamped(b.From.x, b.To.x, t);
            float y = Mathf.LerpUnclamped(b.From.y, b.To.y, t) + b.Arc * 4f * t * (1f - t);
            return new Vector2(x, y);
        }

        /// <summary>Squash &amp; stretch: se estira según la velocidad y se aplasta al chocar.</summary>
        static void UpdateVisual(Ball b, float dt)
        {
            Vector2 pos = b.Tr.localPosition;
            Vector2 vel = dt > 0 ? (pos - b.Prev) / dt : Vector2.zero;
            b.Prev = pos;

            if (b.Pulse > 0)
            {
                b.Pulse = Mathf.Max(0, b.Pulse - dt * 4f);
                if (b.Pulse <= 0) b.Sr.color = b.BaseColor;
            }
            b.Impact = Mathf.Max(0, b.Impact - dt * 9f);
            b.Spawn = Mathf.Min(1f, b.Spawn + dt * 8f);

            float size = BoardView.BallRadius * 2f * b.SizeMul * (1f + 0.35f * b.Pulse) * EaseOutBack(b.Spawn);
            if (b.Impact > 0)
            {
                b.Tr.localRotation = Quaternion.identity;
                b.Tr.localScale = new Vector3(size * (1f + 0.3f * b.Impact), size * (1f - 0.3f * b.Impact), 1);
            }
            else
            {
                float stretch = Mathf.Clamp(vel.magnitude * 0.035f, 0f, 0.3f);
                float ang = vel.sqrMagnitude > 0.0001f ? Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f : 0f;
                b.Tr.localRotation = Quaternion.Euler(0, 0, ang);
                b.Tr.localScale = new Vector3(size * (1f - stretch * 0.5f), size * (1f + stretch), 1);
            }
            b.Shine.position = (Vector3)pos + new Vector3(-0.28f, 0.3f, 0) * size;
            b.Shine.rotation = Quaternion.identity;
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        /// <summary>Llega al destino del tramo. Devuelve false si la bola ha caído en un cubo.</summary>
        bool Arrive(Ball b)
        {
            if (b.Row >= board.Rows)
            {
                Land(b);
                return false;
            }
            HitPeg(b, b.Row, b.Col);

            // Eco: un saltito extra sobre el mismo pincho que vuelve a contar como golpe
            if (!b.Echoing && gm.Stats.EchoChance > 0 && Random.value < gm.Stats.EchoChance)
            {
                StartEcho(b);
                return true;
            }
            b.Echoing = false;
            Advance(b);
            return true;
        }

        void StartEcho(Ball b)
        {
            b.Echoing = true;
            gm.UI.Popup((Vector2)b.Tr.position + Vector2.up * 0.25f, "ECO", EchoColor, 0.6f, true);
            b.From = b.Tr.localPosition;
            b.To = board.PegContact(b.Row, b.Col) + new Vector2(Random.Range(-0.02f, 0.02f), 0);
            b.T = 0;
            b.Dropping = false;
            b.Duration = 0.2f;
            b.Arc = 0.3f;
            gm.Fx.Ring(b.Tr.position, Alpha(EchoColor, 0.5f), 0.5f, 0.2f);
        }

        void Advance(Ball b)
        {
            int nextRow = b.Row + 1;
            int nextCol = b.Col + (b.Path[b.Row] ? 1 : 0);
            b.From = b.Tr.localPosition;
            b.To = nextRow < board.Rows
                ? board.PegContact(nextRow, nextCol) + new Vector2(Random.Range(-Jitter, Jitter), 0)
                : board.BucketLanding(nextCol);
            b.Row = nextRow;
            b.Col = nextCol;
            b.T = 0;
            b.Dropping = false;
            // con más filas cada tramo va más rápido para que la caída total no se alargue
            float speed = Mathf.Min(1f, Mathf.Pow(8f / board.Rows, 0.75f));
            b.Duration = Random.Range(MinSegment, MaxSegment) * speed;
            b.Arc = Random.Range(MinArc, MaxArc) * Mathf.Lerp(1f, speed, 0.5f);
        }

        void HitPeg(Ball b, int r, int k)
        {
            var s = gm.Stats;
            Vector2 pos = b.Tr.position;
            board.FlashPeg(r, k, b.BaseColor);
            gm.RegisterHit();
            // onda muy tenue en el pincho; con muchas bolas solo sale en una fracción de los golpes
            if (Random.value < 12f / Mathf.Max(12, active.Count))
                gm.Fx.Ring(board.PegWorldPos(r, k), Alpha(b.BaseColor, 0.25f), 0.45f, 0.2f);
            b.Impact = 1f;
            gm.Audio.Peg(r, board.Rows, active.Count);

            // Ingreso por rebote: fijo (escalado por tier) + % del valor actual de la bola
            double income = b.Value * s.PegPercent;
            if (income > 0) gm.Earn(income);

            var type = board.GetPegType(r, k);
            if (type == PegType.Golden)
            {
                b.Value *= Balance.GoldenPegMultiplier;
                Pulse(b, GoldenFx);
                gm.UI.Popup(pos, NumberFormat.Multiplier(Balance.GoldenPegMultiplier), GoldenFx, 0.8f);
                gm.Fx.Burst(pos, GoldenFx, 10, 4f, 0.07f);
                gm.Fx.Flash(pos, Alpha(GoldenFx, 0.5f), 1.0f);
                gm.Audio.Special(1.25f);
            }

            if (s.BonusChance > 0 && Random.value < s.BonusChance)
            {
                b.Value *= 1 + s.BonusAmount;
                Pulse(b, BonusColor);
                gm.Fx.Ring(pos, BonusColor, 0.55f, 0.22f);
                gm.UI.Popup(pos + Vector2.up * 0.25f, "+" + NumberFormat.Percent(s.BonusAmount), BonusColor, 0.6f, true);
            }

            if (s.CritChance > 0 && Random.value < s.CritChance)
            {
                b.Value *= Balance.CritMultiplier;
                Pulse(b, CritColor);
                gm.UI.Popup(pos, "¡CRÍTICO x2!", CritColor, 1.1f);
                gm.Fx.Burst(pos, CritColor, 18, 6f, 0.08f, 0.6f);
                gm.Fx.Ring(pos, CritColor, 1.4f, 0.35f);
                gm.Fx.Flash(pos, Alpha(CritColor, 0.6f), 1.6f, 0.25f);
                board.Shake(0.35f);
                gm.Audio.Crit();
                gm.Background.Pulse(CritColor, 0.6f);
            }

            if (type == PegType.Clone && !b.IsClone)
            {
                SpawnClone(b);
                gm.Fx.Burst(pos, CloneFx, 12, 4.5f, 0.07f);
                gm.Fx.Ring(pos, CloneFx, 0.9f, 0.3f);
                gm.Audio.Special(0.9f);
            }

            // Pincho cargado: multiplica a quien lo toque mientras dure la carga
            if (board.IsCharged(r, k))
            {
                b.Value *= Balance.ChargedPegMultiplier;
                Pulse(b, ChargeColor);
                gm.Fx.Ring(pos, ChargeColor, 0.7f, 0.2f);
                gm.UI.Popup(pos + Vector2.up * 0.25f, NumberFormat.Multiplier(Balance.ChargedPegMultiplier), ChargeColor, 0.6f, true);
            }
            else if (s.ChargeChance > 0 && board.ChargedCount < s.MaxChargedPegs && Random.value < s.ChargeChance)
            {
                board.Charge(r, k, Balance.ChargeDuration);
                Vector2 pegPos = board.PegWorldPos(r, k);
                gm.Fx.Burst(pegPos, ChargeColor, 10, 4f, 0.05f, 0.4f, 0f);
                gm.Fx.Ring(pegPos, ChargeColor, 1.1f, 0.3f);
                gm.UI.Popup(pegPos + Vector2.up * 0.3f, "¡CARGADO!", ChargeColor, 0.65f);
                gm.Audio.Zap(0.7f);
            }

            if (s.LightningChance > 0 && Random.value < s.LightningChance) Lightning(b, r, k);
        }

        /// <summary>Rayo que salta por pinchos vecinos: cada uno se activa y paga.</summary>
        void Lightning(Ball b, int r, int k)
        {
            var s = gm.Stats;
            board.BuildChain(r, k, s.LightningHops, chain);
            if (chain.Count == 0) return;

            boltPoints.Clear();
            boltPoints.Add(board.PegWorldPos(r, k));
            double perHop = b.Value * (s.PegPercent + Balance.LightningHopValue);
            foreach (var c in chain)
            {
                Vector2 p = board.PegWorldPos(c.x, c.y);
                boltPoints.Add(p);
                board.FlashPeg(c.x, c.y, LightningColor);
                gm.Fx.Flash(p, Alpha(LightningColor, 0.4f), 0.6f, 0.18f);
                gm.Earn(perHop);
            }
            gm.Fx.Bolt(boltPoints, LightningColor);
            gm.Audio.Zap(1f);
            gm.Background.Pulse(LightningColor, 0.15f);
            gm.UI.Popup(boltPoints[boltPoints.Count - 1], "+" + NumberFormat.Money(perHop * chain.Count * gm.TotalMultiplier), LightningColor, 0.7f);
        }

        void SpawnClone(Ball parent)
        {
            var c = Get();
            c.IsClone = true;
            c.Owner = null;
            c.SizeMul = parent.SizeMul;
            c.Value = parent.Value;
            c.BaseColor = Color.Lerp(parent.BaseColor, Color.white, 0.4f);
            c.Sr.color = c.BaseColor;
            c.Path = PathGenerator.Reroll(parent.Path, parent.Row, parent.Col, (float)gm.Stats.DriftChance);
            c.Path[parent.Row] = !parent.Path[parent.Row]; // sale hacia el lado contrario
            c.Row = parent.Row;
            c.Col = parent.Col;
            c.Tr.localPosition = parent.Tr.localPosition;
            c.Prev = parent.Tr.localPosition;
            c.Spawn = 0.3f;
            c.HaloAlpha = parent.HaloAlpha;
            SetGolden(c, parent.IsGolden);
            ResetTrail(c);
            Advance(c);
            active.Add(c);
            Pulse(c, CloneFx);
        }

        void Land(Ball b)
        {
            int k = board.BucketDistance(b.Col);
            double mult = gm.Stats.BucketMultiplier(k);
            double amount = b.Value * mult;
            gm.Earn(amount);
            board.PopBucket(b.Col);
            if (gm.RushPending) gm.TriggerRushAt(b.Col);
            double shown = amount * gm.TotalMultiplier;
            bool record = gm.RegisterDrop(shown);

            int maxK = board.Rows / 2;
            float t = maxK == 0 ? 0 : (float)k / maxK;   // 0 centro, 1 borde
            var bc = board.BucketColor(b.Col);
            Vector2 pos = b.Tr.position;
            gm.Fx.Fountain(pos, Color.Lerp(bc, Color.white, 0.3f), 3 + Mathf.RoundToInt(t * 14), 3.5f + 3f * t);
            gm.Fx.Flash(pos, Alpha(bc, 0.45f), 0.9f + t, 0.25f);
            if (t >= 0.7f) board.Shake(0.15f + 0.25f * t);
            gm.Audio.Land(k, maxK);

            if (record && shown > 1)
            {
                gm.Fx.Burst(pos + Vector2.up * 0.3f, RecordColor, 24, 7f, 0.09f, 0.8f, -5f);
                gm.Fx.Ring(pos, RecordColor, 2f, 0.45f);
                board.Shake(0.3f);
                gm.Audio.Record();
                gm.Background.Pulse(RecordColor, 0.8f);
            }

            gm.UI.Popup(pos + Vector2.up * 0.5f, "+" + NumberFormat.Money(shown),
                record ? RecordColor : Color.Lerp(Color.white, bc, 0.35f), (record ? 0.95f : 0.7f) + 0.25f * t);
        }

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static void Pulse(Ball b, Color c)
        {
            b.Pulse = 1f;
            b.Sr.color = c;
        }

        Ball Get()
        {
            Ball b;
            if (pool.Count > 0)
            {
                b = pool.Pop();
                b.Tr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("Ball");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Sphere;
                sr.sortingOrder = 10;
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = sr.sharedMaterial; // material de sprites por defecto del renderer 2D
                trail.time = 0.12f;
                trail.minVertexDistance = 0.03f;
                trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                trail.widthMultiplier = BoardView.BallRadius * 1.6f;
                trail.sortingOrder = 9;
                trail.numCapVertices = 2;
                var halo = new GameObject("Halo").AddComponent<SpriteRenderer>();
                halo.transform.SetParent(go.transform, false);
                halo.transform.localScale = Vector3.one * 3f;
                halo.sprite = SpriteFactory.Glow;
                halo.sortingOrder = 8;
                var shine = new GameObject("Shine").AddComponent<SpriteRenderer>();
                shine.transform.SetParent(go.transform, false);
                shine.transform.localScale = Vector3.one * 0.45f;
                shine.sprite = SpriteFactory.Glow;
                shine.color = new Color(1f, 1f, 1f, 0.75f);
                shine.sortingOrder = 11;
                b = new Ball { Tr = go.transform, Sr = sr, Trail = trail, Halo = halo, Shine = shine.transform };
            }
            b.Pulse = 0;
            b.Impact = 0;
            b.IsGolden = false;
            b.Echoing = false;
            b.Tr.localScale = Vector3.one * BoardView.BallRadius * 2f;
            return b;
        }

        /// <summary>Los tipos de bola altos brillan un poco más (bonus visual por tier).</summary>
        static float TierHalo(int tier) => tier <= 0 ? 0f : Mathf.Min(0.5f, 0.1f + tier * 0.04f);

        void SetGolden(Ball b, bool golden)
        {
            b.IsGolden = golden;
            b.Halo.enabled = golden || b.HaloAlpha > 0;
            b.SparkTimer = 0;
            if (golden)
            {
                b.BaseColor = GoldenBallColor;
                b.Sr.color = GoldenBallColor;
            }
            b.Trail.time = golden ? 0.3f : 0.12f;
        }

        static void ResetTrail(Ball b)
        {
            var c = b.BaseColor;
            b.Trail.startColor = new Color(c.r, c.g, c.b, 0.45f);
            b.Trail.endColor = new Color(c.r, c.g, c.b, 0f);
            b.Trail.Clear();
        }

        void Recycle(int index, bool requeue = true)
        {
            var b = active[index];
            if (b.Owner != null)
            {
                MainCount--;
                var owner = b.Owner;
                b.Owner = null;
                owner.InFlight = false;
                if (requeue) gm.Inventory.ReturnToQueue(owner); // vuelve a la cola de goteo
            }
            int last = active.Count - 1;
            active[index] = active[last];
            active.RemoveAt(last);
            b.Tr.gameObject.SetActive(false);
            pool.Push(b);
        }
    }
}
