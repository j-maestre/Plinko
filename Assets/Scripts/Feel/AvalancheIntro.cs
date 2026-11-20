using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Plinko
{
    /// <summary>
    /// Cinemática de entrada de la Avalancha: una bola cae en un cubo, el tiempo se congela,
    /// la cámara hace zoom al cubo, este se carga de energía (rayos, anillos, partículas que absorbe,
    /// temblor y sonido que sube) y estalla con un destello: se descongela y empieza la lluvia de bolas.
    /// Todo usa tiempo sin escalar porque Time.timeScale está a 0 durante la carga.
    /// </summary>
    public class AvalancheIntro : MonoBehaviour
    {
        const float ZoomIn = 0.45f;
        const float Charge = 1.5f;
        const float ZoomOut = 1.1f;     // vuelta más pausada
        const float Aftermath = 2.4f;   // duración total tras el estallido (cámara lenta incluida)
        const int RayCount = 12;

        static readonly Color Energy = new Color(0.45f, 0.95f, 1f);

        public bool Playing { get; private set; }
        float playingSince;
        Action pendingBurst;   // se invoca aunque la cinemática se interrumpa

        class Spark { public SpriteRenderer Sr; public Vector2 From; public float T, Speed; }

        GameManager gm;
        SpriteRenderer dim, glow;
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<(SpriteRenderer sr, float t)> rings = new List<(SpriteRenderer, float)>();
        readonly List<Spark> sparks = new List<Spark>();
        Image flash;
        Text bigText;
        RectTransform bigTextRt;

        void Awake()
        {
            gm = GetComponentInParent<GameManager>();
            if (gm == null) gm = GameManager.Instance;
        }

        public void Play(int bucket, Action onBurst)
        {
            if (Playing) return;
            playingSince = Time.unscaledTime;
            pendingBurst = onBurst;
            StartCoroutine(Run(bucket, onBurst));
        }

        IEnumerator Run(int bucket, Action onBurst)
        {
            Playing = true;
            var board = gm.Board;
            var bucketSr = board.BucketRenderer(bucket);
            Vector2 bucketPos = bucketSr.transform.position;
            Color bucketColor = board.BucketColor(bucket);
            Vector3 bucketScale = board.BucketBaseScale;

            Vector2 basePos = board.CameraBasePos;
            float baseSize = board.CameraBaseSize;
            float zoomSize = Mathf.Max(2f, baseSize * 0.38f);
            Vector2 zoomPos = board.CameraPosFocusing(bucketPos, zoomSize);

            Time.timeScale = 0f; // se congela todo: bolas, partículas, temporizadores
            gm.Audio.Special(0.6f);
            BuildVisuals(bucketColor);
            bucketSr.sortingOrder = 30;

            // 1) Zoom al cubo
            for (float t = 0; t < ZoomIn && bucketSr != null; t += Time.unscaledDeltaTime)
            {
                float e = EaseOutCubic(t / ZoomIn);
                board.SetCinematic(Vector2.Lerp(basePos, zoomPos, e), Mathf.Lerp(baseSize, zoomSize, e));
                SetDim(0.6f * e, zoomPos, zoomSize);
                yield return null;
            }

            // 2) Carga: cada vez más rápida, más brillante y más temblor
            float ringTimer = 0, sparkTimer = 0, soundTimer = 0, rayAngle = 0;
            for (float t = 0; t < Charge && bucketSr != null; t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                float k = t / Charge;          // 0..1
                float k2 = k * k;

                // temblor creciente alrededor del cubo
                Vector2 shake = UnityEngine.Random.insideUnitCircle * (0.02f + 0.18f * k2) * zoomSize * 0.3f;
                board.SetCinematic(zoomPos + shake, zoomSize * (1f - 0.08f * k));
                SetDim(0.6f + 0.15f * k, zoomPos, zoomSize);

                // el cubo vibra y se hincha
                float wobble = Mathf.Sin(Time.unscaledTime * (20f + 50f * k)) * 0.06f * k;
                bucketSr.transform.localScale = new Vector3(bucketScale.x * (1f + 0.25f * k + wobble), bucketScale.y * (1f + 0.25f * k - wobble), 1);
                bucketSr.color = Color.Lerp(bucketColor, Color.white, 0.5f * k * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 30f)));

                // halo
                glow.transform.position = bucketPos;
                glow.transform.localScale = Vector3.one * (1.2f + 3.5f * k);
                glow.color = new Color(Energy.r, Energy.g, Energy.b, 0.25f + 0.55f * k);

                // rayos que giran y se alargan
                rayAngle += dt * (40f + 400f * k2);
                for (int i = 0; i < rays.Count; i++)
                {
                    float ang = (rayAngle + i * 360f / RayCount) * Mathf.Deg2Rad;
                    float len = 0.4f + 6f * k2;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    rays[i].transform.position = bucketPos + dir * (0.35f + len * 0.5f);
                    rays[i].transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg - 90f);
                    rays[i].transform.localScale = new Vector3(0.06f + 0.05f * k, len, 1);
                    rays[i].color = new Color(1, 1, 1, 0.15f + 0.45f * k);
                }

                // anillos que salen del cubo cada vez más seguidos
                ringTimer -= dt;
                if (ringTimer <= 0) { ringTimer = Mathf.Lerp(0.3f, 0.05f, k); SpawnRing(bucketPos); }
                UpdateRings(dt, bucketPos);

                // partículas que el cubo absorbe
                sparkTimer -= dt;
                if (sparkTimer <= 0) { sparkTimer = Mathf.Lerp(0.06f, 0.01f, k); SpawnSpark(bucketPos, zoomSize); }
                UpdateSparks(dt, bucketPos);

                // zumbido que sube de tono
                soundTimer -= dt;
                if (soundTimer <= 0) { soundTimer = Mathf.Lerp(0.25f, 0.05f, k); gm.Audio.Zap(0.6f + 1.6f * k); }

                yield return null;
            }

            // 3) ¡Estalla!
            if (bucketSr != null)
            {
                bucketSr.sortingOrder = 1;
                bucketSr.color = bucketColor;
                bucketSr.transform.localScale = bucketScale;
            }
            ClearCharge();
            flash.color = Color.white;
            bigText.gameObject.SetActive(true);
            gm.Audio.Record();
            gm.Audio.Crit();
            gm.Background.Pulse(Energy, 1f);
            Time.timeScale = 0.2f;   // arranca en cámara lenta y acelera poco a poco
            pendingBurst = null;
            onBurst?.Invoke();
            gm.Fx.Burst(bucketPos, Energy, 40, 10f, 0.1f, 0.9f, -4f);
            gm.Fx.Ring(bucketPos, Color.white, 4f, 0.5f);

            // 4) Vuelta a la vista normal mientras empieza a llover
            for (float t = 0; t < Aftermath; t += Time.unscaledDeltaTime)
            {
                Time.timeScale = Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, t / (Aftermath * 0.8f)));
                float zo = Mathf.Clamp01(t / ZoomOut);
                float e = EaseInOutCubic(zo);
                if (zo < 1f)
                {
                    Vector2 shake = UnityEngine.Random.insideUnitCircle * 0.25f * (1f - zo);
                    board.SetCinematic(Vector2.Lerp(zoomPos, basePos, e) + shake, Mathf.Lerp(zoomSize, baseSize, e));
                }
                else board.EndCinematic();
                SetDim(0.75f * (1f - Mathf.Clamp01(t / 0.7f)), basePos, baseSize);
                flash.color = new Color(1, 1, 1, 1f - Mathf.Clamp01(t / 0.8f));
                float s = t < 0.3f ? EaseOutBack(t / 0.3f) * 1.2f : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((t - 0.3f) / 1.2f));
                bigTextRt.localScale = Vector3.one * s;
                bigText.color = new Color(1, 1, 1, t < 1.6f ? 1f : Mathf.Clamp01(1f - (t - 1.6f) / 0.8f));
                yield return null;
            }

            Time.timeScale = 1f;
            board.EndCinematic();
            dim.enabled = false;
            flash.color = new Color(1, 1, 1, 0);
            bigText.gameObject.SetActive(false);
            Playing = false;
        }

        /// <summary>
        /// Red de seguridad: si la cinemática se queda colgada (p. ej. por un error), se restaura el tiempo
        /// y la cámara para que el juego nunca se quede congelado.
        /// </summary>
        void Update()
        {
            if (!Playing || Time.unscaledTime - playingSince < 8f) return;
            Debug.LogWarning("[Avalancha] La cinemática no terminó: se restaura el juego");
            StopAllCoroutines();
            if (dim != null) { ClearCharge(); dim.enabled = false; }
            if (flash != null) flash.color = new Color(1, 1, 1, 0);
            if (bigText != null) bigText.gameObject.SetActive(false);
            Time.timeScale = 1f;
            gm.Board.EndCinematic();
            Playing = false;
            var burst = pendingBurst;
            pendingBurst = null;
            burst?.Invoke();
        }

        // ------------------------------------------------------------------ Piezas visuales

        void BuildVisuals(Color bucketColor)
        {
            if (dim == null)
            {
                dim = NewSprite("Dim", SpriteFactory.RoundedRect, 15);
                dim.color = Color.black;
                glow = NewSprite("Glow", SpriteFactory.Glow, 28);
                for (int i = 0; i < RayCount; i++) rays.Add(NewSprite("Ray", SpriteFactory.Glow, 27));
                BuildUI();
            }
            dim.enabled = true;
            glow.enabled = true;
            foreach (var r in rays) r.enabled = true;
        }

        void ClearCharge()
        {
            glow.enabled = false;
            foreach (var r in rays) r.enabled = false;
            foreach (var r in rings) Destroy(r.sr.gameObject);
            rings.Clear();
            foreach (var s in sparks) Destroy(s.Sr.gameObject);
            sparks.Clear();
        }

        SpriteRenderer NewSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        void SetDim(float alpha, Vector2 center, float size)
        {
            dim.transform.position = center;
            float h = size * 2.4f;
            dim.transform.localScale = new Vector3(h * 2f, h, 1);
            dim.color = new Color(0, 0, 0.02f, alpha);
        }

        void SpawnRing(Vector2 pos)
        {
            var sr = NewSprite("Ring", SpriteFactory.Ring, 29);
            sr.transform.position = pos;
            rings.Add((sr, 0f));
        }

        void UpdateRings(float dt, Vector2 pos)
        {
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var (sr, t) = rings[i];
                t += dt / 0.5f;
                if (t >= 1f) { Destroy(sr.gameObject); rings.RemoveAt(i); continue; }
                sr.transform.position = pos;
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 3.5f, t);
                sr.color = new Color(Energy.r, Energy.g, Energy.b, 0.7f * (1f - t));
                rings[i] = (sr, t);
            }
        }

        void SpawnSpark(Vector2 target, float size)
        {
            var sr = NewSprite("Spark", SpriteFactory.Circle, 29);
            float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var from = target + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * size * UnityEngine.Random.Range(0.6f, 1.1f);
            sr.color = Color.Lerp(Energy, Color.white, UnityEngine.Random.value * 0.5f);
            sparks.Add(new Spark { Sr = sr, From = from, Speed = UnityEngine.Random.Range(1.8f, 3f) });
        }

        void UpdateSparks(float dt, Vector2 target)
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.T += dt * s.Speed;
                if (s.T >= 1f) { Destroy(s.Sr.gameObject); sparks.RemoveAt(i); continue; }
                float e = s.T * s.T; // acelera hacia el cubo
                s.Sr.transform.position = Vector2.Lerp(s.From, target, e);
                s.Sr.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.03f, e);
            }
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("IntroCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            flash = UIFactory.Image(UIFactory.Stretch("Flash", root), new Color(1, 1, 1, 0), false);
            flash.raycastTarget = false;

            bigTextRt = UIFactory.Rect("Big", root, new Vector2(0, 0.55f), new Vector2(1, 0.68f));
            bigText = UIFactory.Text(bigTextRt, "¡AVALANCHA!", 110, Color.white, TextAnchor.MiddleCenter, true);
            var outline = bigText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.5f, 0.7f, 0.9f);
            outline.effectDistance = new Vector2(5, -5);
            UIFactory.AddShadow(bigText, 8f, 0.6f);
            bigText.gameObject.SetActive(false);
        }

        static float EaseOutCubic(float t) { t = 1f - Mathf.Clamp01(t); return 1f - t * t * t; }
        static float EaseInOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
