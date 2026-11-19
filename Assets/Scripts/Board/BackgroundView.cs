using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Fondo vivo: degradado, nebulosas que derivan despacio, estrellas que parpadean
    /// y un pulso de color cuando pasa algo gordo (crítico, récord, bola dorada).
    /// Sigue a la cámara y siempre la cubre.
    /// </summary>
    public class BackgroundView : MonoBehaviour
    {
        static readonly Color Top = new Color(0.13f, 0.10f, 0.30f);
        static readonly Color Bottom = new Color(0.02f, 0.03f, 0.10f);

        static readonly Color[] NebulaColors =
        {
            new Color(0.55f, 0.30f, 1.00f),
            new Color(0.20f, 0.75f, 0.95f),
            new Color(1.00f, 0.35f, 0.70f),
            new Color(0.25f, 0.95f, 0.70f),
            new Color(0.35f, 0.45f, 1.00f),
        };

        class Nebula { public SpriteRenderer Sr; public Vector2 Anchor; public float Size, Speed, Phase, Alpha; }
        class Star { public SpriteRenderer Sr; public Vector2 Pos; public float Size, Twinkle, Phase, Drift, Alpha; }

        const int StarCount = 80;

        Camera cam;
        SpriteRenderer gradient, pulse;
        readonly List<Nebula> nebulas = new List<Nebula>();
        readonly List<Star> stars = new List<Star>();
        float pulseAmount;
        Color pulseColor;

        void Awake()
        {
            cam = Camera.main;
            if (cam != null) cam.backgroundColor = Bottom;

            gradient = NewSprite("Gradient", SpriteFactory.VerticalGradient(Top, Bottom), -100);

            for (int i = 0; i < NebulaColors.Length; i++)
            {
                var n = new Nebula
                {
                    Sr = NewSprite("Nebula", SpriteFactory.Glow, -90),
                    Anchor = new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, 0.45f)),
                    Size = Random.Range(0.6f, 1.1f),
                    Speed = Random.Range(0.03f, 0.07f),
                    Phase = Random.Range(0f, 100f),
                    Alpha = Random.Range(0.10f, 0.18f),
                };
                n.Sr.color = new Color(NebulaColors[i].r, NebulaColors[i].g, NebulaColors[i].b, n.Alpha);
                nebulas.Add(n);
            }

            for (int i = 0; i < StarCount; i++)
            {
                var s = new Star
                {
                    Sr = NewSprite("Star", SpriteFactory.Circle, -80),
                    Pos = new Vector2(Random.value, Random.value),
                    Size = Random.Range(0.012f, 0.035f),
                    Twinkle = Random.Range(0.8f, 3f),
                    Phase = Random.Range(0f, 10f),
                    Drift = Random.Range(0.002f, 0.01f),
                    Alpha = Random.Range(0.25f, 0.8f),
                };
                stars.Add(s);
            }

            pulse = NewSprite("Pulse", SpriteFactory.Glow, -70);
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

        /// <summary>Destello de color en el fondo.</summary>
        public void Pulse(Color color, float amount = 1f)
        {
            pulseColor = color;
            pulseAmount = Mathf.Min(1f, pulseAmount + amount);
        }

        void LateUpdate()
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; }
            float h = cam.orthographicSize * 2f;
            float w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            float t = Time.time;

            Fit(gradient, c, w * 1.1f, h * 1.1f);

            float big = Mathf.Max(w, h);
            foreach (var n in nebulas)
            {
                var p = c + new Vector2(
                    (n.Anchor.x + 0.25f * Mathf.Sin(t * n.Speed + n.Phase)) * w,
                    (n.Anchor.y + 0.20f * Mathf.Cos(t * n.Speed * 0.8f + n.Phase)) * h);
                n.Sr.transform.position = p;
                float breathe = 1f + 0.08f * Mathf.Sin(t * n.Speed * 3f + n.Phase);
                n.Sr.transform.localScale = Vector3.one * big * n.Size * breathe;
            }

            float dt = Time.deltaTime;
            foreach (var s in stars)
            {
                s.Pos.y += s.Drift * dt;          // suben muy despacio
                if (s.Pos.y > 1f) { s.Pos.y -= 1f; s.Pos.x = Random.value; }
                s.Sr.transform.position = c + new Vector2((s.Pos.x - 0.5f) * w, (s.Pos.y - 0.5f) * h);
                s.Sr.transform.localScale = Vector3.one * s.Size * h * 0.1f;
                float tw = 0.55f + 0.45f * Mathf.Sin(t * s.Twinkle + s.Phase);
                s.Sr.color = new Color(0.85f, 0.9f, 1f, s.Alpha * tw);
            }

            if (pulseAmount > 0)
            {
                pulseAmount = Mathf.Max(0, pulseAmount - dt * 1.5f);
                pulse.enabled = true;
                pulse.transform.position = c;
                pulse.transform.localScale = Vector3.one * big * 1.4f;
                pulse.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, 0.35f * pulseAmount);
            }
            else pulse.enabled = false;
        }

        static void Fit(SpriteRenderer sr, Vector2 center, float w, float h)
        {
            sr.transform.position = center;
            var size = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(w / size.x, h / size.y, 1);
        }
    }
}
