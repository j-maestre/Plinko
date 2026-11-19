using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Partículas y ondas de impacto ligeras hechas con SpriteRenderers en pool
    /// (sin ParticleSystem para no depender de materiales/assets). Un único Update.
    /// </summary>
    public class FxSystem : MonoBehaviour
    {
        class Particle
        {
            public SpriteRenderer Sr;
            public Vector2 Pos, Vel;
            public float Life, MaxLife, Size, EndSize, Gravity, Drag;
            public Color Color;
        }

        const int MaxParticles = 450;

        class BoltFx { public LineRenderer Lr; public float Life; public Color Color; public Vector3[] Points; }
        const float BoltDuration = 0.22f;
        readonly List<BoltFx> bolts = new List<BoltFx>();
        readonly Stack<BoltFx> boltPool = new Stack<BoltFx>();
        Material lineMaterial;

        readonly List<Particle> active = new List<Particle>();
        readonly Stack<Particle> pool = new Stack<Particle>();
        Transform root;

        void Awake()
        {
            root = new GameObject("Fx").transform;
            root.SetParent(transform, false);
            // material de sprites por defecto del renderer 2D, reutilizado para las líneas
            var probe = new GameObject("Probe").AddComponent<SpriteRenderer>();
            lineMaterial = probe.sharedMaterial;
            Destroy(probe.gameObject);
        }

        /// <summary>Explosión de puntitos.</summary>
        public void Burst(Vector2 pos, Color color, int count, float speed, float size = 0.08f, float life = 0.5f, float gravity = -7f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Get(SpriteFactory.Circle, 6);
                if (p == null) return;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float spd = speed * Random.Range(0.4f, 1f);
                p.Pos = pos;
                p.Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
                p.MaxLife = life * Random.Range(0.7f, 1.2f);
                p.Size = size * Random.Range(0.7f, 1.3f);
                p.EndSize = 0;
                p.Gravity = gravity;
                p.Drag = 2.5f;
                p.Color = color;
            }
        }

        /// <summary>Chorro hacia arriba (para los cubos).</summary>
        public void Fountain(Vector2 pos, Color color, int count, float speed, float size = 0.09f)
        {
            for (int i = 0; i < count; i++)
            {
                var p = Get(SpriteFactory.Circle, 6);
                if (p == null) return;
                p.Pos = pos + new Vector2(Random.Range(-0.3f, 0.3f), 0);
                p.Vel = new Vector2(Random.Range(-1.2f, 1.2f), speed * Random.Range(0.6f, 1.1f));
                p.MaxLife = Random.Range(0.45f, 0.8f);
                p.Size = size * Random.Range(0.7f, 1.3f);
                p.EndSize = 0;
                p.Gravity = -12f;
                p.Drag = 1f;
                p.Color = color;
            }
        }

        /// <summary>Onda expansiva.</summary>
        public void Ring(Vector2 pos, Color color, float maxSize = 0.6f, float life = 0.3f)
        {
            var p = Get(SpriteFactory.Ring, 4);
            if (p == null) return;
            p.Pos = pos;
            p.Vel = Vector2.zero;
            p.MaxLife = life;
            p.Size = maxSize * 0.2f;
            p.EndSize = maxSize;
            p.Gravity = 0;
            p.Drag = 0;
            p.Color = color;
        }

        /// <summary>Destello suave que se apaga.</summary>
        public void Flash(Vector2 pos, Color color, float size = 1f, float life = 0.2f)
        {
            var p = Get(SpriteFactory.Glow, 3);
            if (p == null) return;
            p.Pos = pos;
            p.Vel = Vector2.zero;
            p.MaxLife = life;
            p.Size = size;
            p.EndSize = size * 1.3f;
            p.Gravity = 0;
            p.Drag = 0;
            p.Color = color;
        }

        /// <summary>Rayo en zigzag que pasa por los puntos dados.</summary>
        public void Bolt(List<Vector2> points, Color color)
        {
            if (points.Count < 2 || bolts.Count > 30) return;
            var b = boltPool.Count > 0 ? boltPool.Pop() : NewBolt();
            b.Lr.gameObject.SetActive(true);

            const int sub = 4;
            int count = (points.Count - 1) * sub + 1;
            if (b.Points == null || b.Points.Length != count) b.Points = new Vector3[count];
            int idx = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i], c = points[i + 1];
                Vector2 perp = new Vector2(-(c - a).y, (c - a).x).normalized;
                for (int j = 0; j < sub; j++)
                {
                    float t = (float)j / sub;
                    float off = j == 0 ? 0 : Random.Range(-0.12f, 0.12f);
                    b.Points[idx++] = Vector2.Lerp(a, c, t) + perp * off;
                }
            }
            b.Points[idx] = points[points.Count - 1];

            b.Lr.positionCount = count;
            b.Lr.SetPositions(b.Points);
            b.Life = 0;
            b.Color = color;
            bolts.Add(b);
        }

        BoltFx NewBolt()
        {
            var go = new GameObject("Bolt");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = lineMaterial;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.07f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.sortingOrder = 7;
            return new BoltFx { Lr = lr };
        }

        Particle Get(Sprite sprite, int order)
        {
            if (active.Count >= MaxParticles) return null;
            Particle p;
            if (pool.Count > 0)
            {
                p = pool.Pop();
                p.Sr.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("P");
                go.transform.SetParent(root, false);
                p = new Particle { Sr = go.AddComponent<SpriteRenderer>() };
            }
            p.Sr.sprite = sprite;
            p.Sr.sortingOrder = order;
            p.Life = 0;
            active.Add(p);
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                var b = bolts[i];
                b.Life += dt;
                float t = b.Life / BoltDuration;
                if (t >= 1f)
                {
                    b.Lr.gameObject.SetActive(false);
                    boltPool.Push(b);
                    bolts.RemoveAt(i);
                    continue;
                }
                // chisporroteo: la línea tiembla un poco mientras se apaga
                for (int p = 1; p < b.Points.Length - 1; p++)
                    b.Lr.SetPosition(p, b.Points[p] + (Vector3)(Random.insideUnitCircle * 0.03f));
                var c = Color.Lerp(Color.white, b.Color, t);
                c.a = 1f - t;
                b.Lr.startColor = c;
                b.Lr.endColor = c;
                b.Lr.widthMultiplier = 0.08f * (1f - t * 0.6f);
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                p.Life += dt;
                float t = p.Life / p.MaxLife;
                if (t >= 1f)
                {
                    p.Sr.gameObject.SetActive(false);
                    pool.Push(p);
                    int last = active.Count - 1;
                    active[i] = active[last];
                    active.RemoveAt(last);
                    continue;
                }
                p.Vel.y += p.Gravity * dt;
                p.Vel *= 1f / (1f + p.Drag * dt);
                p.Pos += p.Vel * dt;
                p.Sr.transform.localPosition = p.Pos;
                p.Sr.transform.localScale = Vector3.one * Mathf.Lerp(p.Size, p.EndSize, EaseOut(t));
                var c = p.Color;
                c.a *= 1f - t * t;
                p.Sr.color = c;
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
