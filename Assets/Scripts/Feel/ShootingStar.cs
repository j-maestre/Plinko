using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Plinko
{
    /// <summary>
    /// Estrella fugaz: de vez en cuando cruza el tablero y, si el jugador la toca a tiempo, da un premio
    /// (unos segundos de ingresos de golpe). Premia mirar el tablero en vez de estar siempre en las mejoras.
    /// </summary>
    public class ShootingStar : MonoBehaviour
    {
        const float FirstDelay = 30f;
        const float MinDelay = 45f, MaxDelay = 90f;
        const float Duration = 4.2f;        // lo que tarda en cruzar (da tiempo a verla y tocarla)
        const float TouchRadius = 0.9f;     // en unidades del mundo: generoso para el dedo
        const float RewardSeconds = 30f;    // premio = ingresos de 30 s
        static readonly Color StarColor = new Color(1f, 0.92f, 0.55f);

        GameManager gm;
        Transform star;
        SpriteRenderer core, halo;
        TrailRenderer trail;
        float timer = FirstDelay, t, sparkTimer;
        bool flying;
        Vector2 from, to;

        public void Init(GameManager gm)
        {
            this.gm = gm;
            var go = new GameObject("Star");
            go.transform.SetParent(transform, false);
            star = go.transform;
            core = go.AddComponent<SpriteRenderer>();
            core.sprite = StarSprite();
            core.sortingOrder = 40;
            var h = new GameObject("Halo").AddComponent<SpriteRenderer>();
            h.transform.SetParent(star, false);
            h.transform.localScale = Vector3.one * 3.2f;
            h.sprite = SpriteFactory.Glow;
            h.sortingOrder = 39;
            halo = h;
            trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = core.sharedMaterial;
            trail.time = 0.6f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.widthMultiplier = 0.35f;
            trail.startColor = new Color(StarColor.r, StarColor.g, StarColor.b, 0.8f);
            trail.endColor = new Color(StarColor.r, StarColor.g, StarColor.b, 0f);
            trail.sortingOrder = 38;
            go.SetActive(false);
        }

        void Update()
        {
            if (gm == null) return;
            if (!flying)
            {
                // no aparece durante la cinemática de la Avalancha ni con el juego aún vacío
                if (gm.Intro.Playing || gm.Inventory.Count == 0) return;
                timer -= Time.deltaTime;
                if (timer <= 0) Launch();
                return;
            }

            t += Time.deltaTime / Duration;
            if (t >= 1f) { Miss(); return; }

            // trayectoria en arco suave, girando y latiendo para llamar la atención
            Vector2 p = Vector2.Lerp(from, to, t) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 0.8f;
            star.position = p;
            star.rotation = Quaternion.Euler(0, 0, Time.time * 160f);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 12f);
            star.localScale = Vector3.one * 0.55f * pulse;
            halo.color = new Color(StarColor.r, StarColor.g, StarColor.b, 0.45f + 0.2f * Mathf.Sin(Time.time * 9f));

            sparkTimer -= Time.deltaTime;
            if (sparkTimer <= 0)
            {
                sparkTimer = 0.04f;
                gm.Fx.Burst(p, Color.Lerp(StarColor, Color.white, Random.value), 1, 1.5f, 0.06f, 0.5f, -2f);
            }

            if (TappedAt(p)) Collect(p);
        }

        void Launch()
        {
            var cam = Camera.main;
            if (cam == null) { timer = 5f; return; }
            // cruza la franja del tablero de un lado a otro, de arriba hacia abajo
            float bottom = BoardView.BottomUI + 0.06f, top = BoardView.TopEdge - BoardView.TopUI - BoardView.OverloadBar - 0.05f;
            bool leftToRight = Random.value < 0.5f;
            float y0 = Mathf.Lerp(bottom, top, Random.Range(0.55f, 0.9f));
            float y1 = Mathf.Lerp(bottom, top, Random.Range(0.15f, 0.45f));
            from = cam.ViewportToWorldPoint(new Vector3(leftToRight ? -0.05f : 1.05f, y0, 0));
            to = cam.ViewportToWorldPoint(new Vector3(leftToRight ? 1.05f : -0.05f, y1, 0));
            t = 0;
            flying = true;
            star.position = from;
            star.gameObject.SetActive(true);
            trail.Clear();
            gm.Audio.Special(1.6f);
        }

        void Collect(Vector2 p)
        {
            flying = false;
            star.gameObject.SetActive(false);
            timer = Random.Range(MinDelay, MaxDelay);
            double amount = System.Math.Max(gm.IncomePerSecond * RewardSeconds, 50);
            gm.GrantBonus(amount);
            gm.Fx.Burst(p, StarColor, 36, 8f, 0.1f, 0.9f, -4f);
            gm.Fx.Ring(p, Color.white, 2.5f, 0.45f);
            gm.Fx.Flash(p, new Color(1f, 0.95f, 0.7f, 0.7f), 2f, 0.3f);
            gm.Background.Pulse(StarColor, 0.8f);
            gm.Board.Shake(0.3f);
            gm.Audio.Record();
            gm.UI.Popup(p + Vector2.up * 0.4f, "¡ESTRELLA!  +" + NumberFormat.Money(amount), StarColor, 1.3f);
            gm.UI.ShowStarOffer(p, amount);   // x3 viendo un anuncio
        }

        void Miss()
        {
            flying = false;
            star.gameObject.SetActive(false);
            timer = Random.Range(MinDelay, MaxDelay);
        }

        static bool TappedAt(Vector2 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector2 screen;
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return false;
            screen = pointer.position.ReadValue();
#else
            if (!Input.GetMouseButtonDown(0)) return false;
            screen = Input.mousePosition;
#endif
            Vector2 w = cam.ScreenToWorldPoint(screen);
            return (w - world).sqrMagnitude <= TouchRadius * TouchRadius;
        }

        /// <summary>Estrella de cinco puntas blanca (se tiñe) con bordes suaves.</summary>
        static Sprite StarSprite()
        {
            const int N = 96;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.95f : 0.42f;
                pts[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
            }
            var px = new Color32[N * N];
            const int ss = 3;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int hit = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / ss) / N * 2f - 1f, (y + (sy + 0.5f) / ss) / N * 2f - 1f);
                    if (Inside(p, pts)) hit++;
                }
                float a = hit / (float)(ss * ss);
                // centro más claro
                float d = new Vector2(x / (float)N * 2f - 1f, y / (float)N * 2f - 1f).magnitude;
                byte g = (byte)(255 * Mathf.Lerp(1f, 0.85f, d));
                px[y * N + x] = new Color32(255, 255, g, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        }

        static bool Inside(Vector2 pt, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > pt.y) != (poly[j].y > pt.y) &&
                    pt.x < (poly[j].x - poly[i].x) * (pt.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
