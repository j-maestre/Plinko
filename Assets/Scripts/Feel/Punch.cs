using UnityEngine;

namespace Plinko
{
    /// <summary>Escala "punch" elástica para cualquier Transform (UI o mundo).</summary>
    public class Punch : MonoBehaviour
    {
        public float Amount = 0.15f;
        public float Duration = 0.3f;
        float t = 1f;
        float strength = 1f;
        Vector3 baseScale = Vector3.one;
        bool hasBase;

        public static Punch On(Component c, float amount = 0.15f, float duration = 0.3f)
        {
            var p = c.GetComponent<Punch>();
            if (p == null) p = c.gameObject.AddComponent<Punch>();
            p.Amount = amount;
            p.Duration = duration;
            return p;
        }

        public void Play(float strengthMul = 1f)
        {
            if (!hasBase) { baseScale = transform.localScale; hasBase = true; }
            t = 0f;
            strength = strengthMul;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Duration);
            // oscilación amortiguada: sube rápido, rebota un poco y se asienta
            float s = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * Amount * strength;
            transform.localScale = baseScale * (1f + s);
        }
    }
}
