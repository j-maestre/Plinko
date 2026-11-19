using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    public enum PegType { Normal, Golden, Clone }

    /// <summary>
    /// Genera y dibuja el tablero. Disposición tipo Galton: la fila r tiene r+1 pinchos,
    /// y tras la última fila hay rows+1 cubos.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public const float SpacingX = 1f;
        public const float SpacingY = 0.85f;
        public const float PegRadius = 0.13f;
        public const float BallRadius = 0.17f;
        const float BucketHeight = 0.72f;
        const float SpawnHeight = 1.1f;

        // Fracciones de pantalla reservadas para la UI (arriba y abajo)
        public const float TopUI = 0.11f;

        /// <summary>
        /// Hueco superior por la barra de estado / notch (fracción de pantalla), más un pequeño margen.
        /// En el editor la zona segura es toda la pantalla, así que solo queda el margen.
        /// </summary>
        public static float SafeTop => Mathf.Clamp01((Screen.height - Screen.safeArea.yMax) / Mathf.Max(1f, Screen.height)) + 0.01f;

        /// <summary>Borde superior utilizable (1 = arriba del todo).</summary>
        public static float TopEdge => 1f - SafeTop;
        public const float OverloadBar = 0.035f;   // barra de Sobrecarga bajo la barra superior
        public const float BottomUI = 0.44f;


        static readonly Color PegColor = new Color(0.55f, 0.60f, 0.80f);   // apagado en reposo para que el golpe se note
        static readonly Color GoldenColor = new Color(1.00f, 0.80f, 0.28f);
        static readonly Color CloneColor = new Color(0.50f, 0.95f, 0.62f);
        public static readonly Color[] GroupColors =
        {
            new Color(0.25f, 0.58f, 0.92f),   // centrales: azul
            new Color(0.62f, 0.42f, 0.95f),   // intermedios: violeta
            new Color(1.00f, 0.58f, 0.28f),   // extremos: naranja
        };

        public int Rows { get; private set; }
        public int BucketCount => Rows + 1;

        SpriteRenderer[] pegs;
        PegType[] pegTypes;
        float[] pegFlash;
        float[] pegCharge;
        readonly List<int> charged = new List<int>();
        static readonly Color ChargeColor = new Color(0.45f, 0.95f, 1f);
        Color[] pegFlashColor;
        SpriteRenderer[] buckets;
        float[] bucketPop;
        float[] bucketUpgrade;   // animación de "expandirse y volver" al mejorar un cubo
        Vector3 bucketScale;
        Transform pegRoot, bucketRoot;
        SpriteRenderer frame;
        Camera cam;
        Vector2Int lastScreen;
        readonly List<int> activeFlash = new List<int>();

        public static int PegIndex(int r, int k) => r * (r + 1) / 2 + k;

        public Vector2 PegPos(int r, int k) => new Vector2((k - r * 0.5f) * SpacingX, -r * SpacingY);
        public Vector2 PegContact(int r, int k) => PegPos(r, k) + Vector2.up * (PegRadius + BallRadius);
        public Vector2 SpawnPos => PegPos(0, 0) + Vector2.up * SpawnHeight;
        public Vector2 BucketCenterPos(int i) => new Vector2((i - Rows * 0.5f) * SpacingX, -Rows * SpacingY - 0.25f);
        public Vector2 BucketLanding(int i) => BucketCenterPos(i) + Vector2.up * 0.22f; // entra por la boca del cubo
        public int BucketDistance(int i) => Mathf.Abs(i - Rows / 2);
        public PegType GetPegType(int r, int k) => pegTypes[PegIndex(r, k)];

        Vector2[] pegBasePos;
        Vector2 PegPosFromIndex(int i) => pegBasePos[i];

        // --- Temblor de cámara (trauma^2 con ruido Perlin) ---
        float trauma;
        Vector3 camBase;

        public void Shake(float amount) => trauma = Mathf.Min(1f, trauma + amount);

        public void Build(int rows, int golden, int clone)
        {
            cam = Camera.main;
            Rows = rows;
            if (pegRoot != null) Destroy(pegRoot.gameObject);
            if (bucketRoot != null) Destroy(bucketRoot.gameObject);
            activeFlash.Clear();

            pegRoot = new GameObject("Pegs").transform;
            pegRoot.SetParent(transform, false);
            bucketRoot = new GameObject("Buckets").transform;
            bucketRoot.SetParent(transform, false);

            if (frame == null)
            {
                frame = NewSprite("BoardFrame", SpriteFactory.UIPanel, transform, -50);
                frame.drawMode = SpriteDrawMode.Sliced;
                frame.color = new Color(0.55f, 0.6f, 1f, 0.06f);
            }
            float frameTop = SpawnPos.y + 0.35f, frameBottom = -Rows * SpacingY - 0.55f;
            frame.transform.localPosition = new Vector2(0, (frameTop + frameBottom) * 0.5f);
            frame.size = new Vector2((Rows + 1.1f) * SpacingX, frameTop - frameBottom);

            int count = PegIndex(rows, 0);
            pegs = new SpriteRenderer[count];
            pegTypes = new PegType[count];
            pegFlash = new float[count];
            pegCharge = new float[count];
            charged.Clear();
            pegFlashColor = new Color[count];
            pegBasePos = new Vector2[count];
            for (int r = 0; r < rows; r++)
            for (int k = 0; k <= r; k++)
            {
                var sr = NewSprite("Peg", SpriteFactory.Sphere, pegRoot, 0);
                sr.transform.localPosition = PegPos(r, k);
                sr.transform.localScale = Vector3.one * PegRadius * 2f;
                pegs[PegIndex(r, k)] = sr;
                pegBasePos[PegIndex(r, k)] = PegPos(r, k);
            }
            AssignPegTypes(golden, clone);

            buckets = new SpriteRenderer[BucketCount];
            bucketPop = new float[BucketCount];
            bucketUpgrade = new float[BucketCount];
            bucketScale = new Vector3(SpacingX * 0.92f, BucketHeight, 1);
            for (int i = 0; i < BucketCount; i++)
            {
                var sr = NewSprite("Bucket", SpriteFactory.Bucket, bucketRoot, 1);
                sr.transform.localPosition = BucketCenterPos(i);
                sr.transform.localScale = bucketScale;
                sr.color = GroupColors[Balance.BucketGroup(BucketDistance(i))];
                buckets[i] = sr;
            }

            FitCamera();
        }

        /// <summary>Coloca pinchos dorados/clonadores en posiciones fijas (deterministas por número de filas).</summary>
        public void AssignPegTypes(int golden, int clone)
        {
            for (int i = 0; i < pegTypes.Length; i++) pegTypes[i] = PegType.Normal;

            // candidatos: filas 2..rows-1 (no el pincho superior)
            var candidates = new List<int>();
            for (int r = 2; r < Rows; r++)
            for (int k = 0; k <= r; k++) candidates.Add(PegIndex(r, k));

            var rng = new System.Random(Rows * 7919);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int c = 0;
            for (int i = 0; i < golden && c < candidates.Count; i++) pegTypes[candidates[c++]] = PegType.Golden;
            for (int i = 0; i < clone && c < candidates.Count; i++) pegTypes[candidates[c++]] = PegType.Clone;

            for (int i = 0; i < pegs.Length; i++) pegs[i].color = BaseColor(pegTypes[i]);
        }

        static Color BaseColor(PegType t) => t == PegType.Golden ? GoldenColor : t == PegType.Clone ? CloneColor : PegColor;

        SpriteRenderer NewSprite(string name, Sprite sprite, Transform parent, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public Color BucketColor(int i) => buckets[i].color;

        // --- Cámara cinemática (intro de la Avalancha) ---
        bool cinematic;
        Vector2 cinePos;
        float cineSize, baseSize;

        public SpriteRenderer BucketRenderer(int i) => buckets[i];
        public Vector3 BucketBaseScale => bucketScale;
        public Vector2 CameraBasePos => camBase;
        public float CameraBaseSize => baseSize;

        public void SetCinematic(Vector2 camPos, float size)
        {
            cinematic = true;
            cinePos = camPos;
            cineSize = size;
        }

        public void EndCinematic()
        {
            cinematic = false;
            if (cam != null) { cam.orthographicSize = baseSize; cam.transform.position = camBase; }
        }

        /// <summary>Posición de cámara para que un punto quede centrado en la zona visible del tablero con ese zoom.</summary>
        public Vector2 CameraPosFocusing(Vector2 world, float size)
        {
            float frac = TopEdge - TopUI - OverloadBar - BottomUI;
            float screenCenter = BottomUI + frac * 0.5f;
            return new Vector2(world.x, world.y - (screenCenter - 0.5f) * 2f * size);
        }

        public void FlashPeg(int r, int k, Color ballColor)
        {
            int i = PegIndex(r, k);
            if (pegFlash[i] <= 0) activeFlash.Add(i);
            pegFlash[i] = 1f;
            pegFlashColor[i] = Color.Lerp(ballColor, Color.white, 0.5f);
        }

        // --- Pinchos cargados ---
        public int ChargedCount => charged.Count;
        public bool IsCharged(int r, int k) => pegCharge[PegIndex(r, k)] > 0;

        public void Charge(int r, int k, float duration)
        {
            int i = PegIndex(r, k);
            if (pegCharge[i] <= 0) charged.Add(i);
            pegCharge[i] = duration;
        }

        // --- Vecinos (para el relámpago) ---
        static readonly int[] NeighborDr = { -1, -1, 0, 0, 1, 1 };
        static readonly int[] NeighborDk = { -1, 0, -1, 1, 0, 1 };
        readonly List<Vector2Int> candidates = new List<Vector2Int>();

        /// <summary>Cadena de pinchos vecinos distintos empezando junto a (r,k).</summary>
        public void BuildChain(int r, int k, int hops, List<Vector2Int> result)
        {
            result.Clear();
            var cur = new Vector2Int(r, k);
            for (int h = 0; h < hops; h++)
            {
                candidates.Clear();
                for (int n = 0; n < 6; n++)
                {
                    int nr = cur.x + NeighborDr[n], nk = cur.y + NeighborDk[n];
                    if (nr < 0 || nr >= Rows || nk < 0 || nk > nr) continue;
                    var c = new Vector2Int(nr, nk);
                    if ((nr == r && nk == k) || result.Contains(c)) continue;
                    candidates.Add(c);
                }
                if (candidates.Count == 0) break;
                cur = candidates[Random.Range(0, candidates.Count)];
                result.Add(cur);
            }
        }

        public Vector2 PegWorldPos(int r, int k) => (Vector2)transform.TransformPoint(PegPos(r, k));

        public void PopBucket(int i) => bucketPop[i] = 1f;

        /// <summary>Anima los cubos a distancia k del centro (k &lt; 0 = todos).</summary>
        public void PulseBucketGroup(int group)
        {
            for (int i = 0; i < BucketCount; i++)
                if (Balance.BucketGroup(BucketDistance(i)) == group) bucketUpgrade[i] = 1f;
        }

        public void PulseBuckets(int k)
        {
            for (int i = 0; i < BucketCount; i++)
                if (k < 0 || BucketDistance(i) == k) bucketUpgrade[i] = 1f;
        }

        /// <summary>Escala extra de la animación de mejora (1 = reposo). La usa también la etiqueta del cubo.</summary>
        public float BucketPulseScale(int i)
        {
            if (bucketUpgrade == null || i >= bucketUpgrade.Length) return 1f;
            return 1f + 0.35f * Mathf.Sin(Mathf.PI * (1f - bucketUpgrade[i]));
        }

        void Update()
        {
            if (pegs == null) return;
            float dt = Time.deltaTime;

            for (int n = activeFlash.Count - 1; n >= 0; n--)
            {
                int i = activeFlash[n];
                pegFlash[i] = Mathf.Max(0, pegFlash[i] - dt * 5f);
                float f = pegFlash[i];
                pegs[i].color = Color.Lerp(BaseColor(pegTypes[i]), pegFlashColor[i], f);
                pegs[i].transform.localScale = Vector3.one * PegRadius * 2f * (1f + 0.6f * f);
                // el pincho "cede" hacia abajo con el golpe y vuelve con un pequeño rebote elástico
                float wobble = Mathf.Sin((1f - f) * Mathf.PI * 2f) * f;
                pegs[i].transform.localPosition = PegPosFromIndex(i) + Vector2.down * 0.06f * wobble;
                if (f <= 0) activeFlash.RemoveAt(n);
            }

            // Pinchos cargados: parpadeo eléctrico hasta que se agota la carga
            for (int n = charged.Count - 1; n >= 0; n--)
            {
                int i = charged[n];
                pegCharge[i] -= dt;
                if (pegCharge[i] <= 0)
                {
                    charged.RemoveAt(n);
                    pegs[i].color = BaseColor(pegTypes[i]);
                    pegs[i].transform.localScale = Vector3.one * PegRadius * 2f;
                    continue;
                }
                float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 25f + i);
                pegs[i].color = Color.Lerp(BaseColor(pegTypes[i]), ChargeColor, flicker);
                float fade = Mathf.Clamp01(pegCharge[i] / 0.5f); // se apaga al final
                pegs[i].transform.localScale = Vector3.one * PegRadius * 2f * (1f + 0.45f * fade + 0.08f * flicker);
            }

            for (int i = 0; i < buckets.Length; i++)
            {
                if (bucketPop[i] <= 0 && bucketUpgrade[i] <= 0) continue;
                bucketPop[i] = Mathf.Max(0, bucketPop[i] - dt * 4f);
                bucketUpgrade[i] = Mathf.Max(0, bucketUpgrade[i] - dt / 0.35f);
                float f = bucketPop[i];
                float u = BucketPulseScale(i);
                buckets[i].transform.localScale = new Vector3(bucketScale.x * (1 + 0.08f * f) * u, bucketScale.y * (1 - 0.15f * f) * u, 1);
            }

            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) FitCamera();

            if (cam != null && cinematic)
            {
                // la cinemática manda: posición y zoom fijados desde fuera (con tiempo congelado)
                cam.transform.position = new Vector3(cinePos.x, cinePos.y, camBase.z);
                cam.orthographicSize = cineSize;
            }
            else if (cam != null)
            {
                if (trauma > 0)
                {
                    trauma = Mathf.Max(0, trauma - dt * 1.8f);
                    float s = trauma * trauma * 0.25f;
                    float tt = Time.time * 30f;
                    cam.transform.position = camBase + new Vector3(
                        (Mathf.PerlinNoise(tt, 0.3f) * 2f - 1f) * s,
                        (Mathf.PerlinNoise(0.7f, tt) * 2f - 1f) * s, 0);
                }
                else cam.transform.position = camBase;
            }
        }

        /// <summary>Encaja el tablero en la franja de pantalla libre entre la UI superior e inferior.</summary>
        public void FitCamera()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            float width = (Rows + 1.2f) * SpacingX;
            float top = SpawnPos.y + 0.3f;
            float bottom = BucketCenterPos(0).y - BucketHeight * 0.5f - 0.2f;
            float height = top - bottom;

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float frac = TopEdge - TopUI - OverloadBar - BottomUI;
            float size = Mathf.Max(width / (2f * aspect), height / (2f * frac)) * 1.03f;

            cam.orthographic = true;
            cam.orthographicSize = size;
            baseSize = size;
            float boardCenterY = (top + bottom) * 0.5f;
            float screenCenter = BottomUI + frac * 0.5f; // 0..1 desde abajo
            var p = cam.transform.position;
            camBase = new Vector3(0, boardCenterY - (screenCenter - 0.5f) * 2f * size, p.z);
            cam.transform.position = camBase;

        }
    }
}
