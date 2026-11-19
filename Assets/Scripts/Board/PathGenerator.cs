using UnityEngine;

namespace Plinko
{
    /// <summary>
    /// Genera el camino de una bola antes de lanzarla. true = derecha, false = izquierda.
    /// El cubo final es el número de "derechas" (0..rows).
    /// </summary>
    public static class PathGenerator
    {
        /// <summary>50/50 en cada pincho: distribución binomial natural.</summary>
        public static bool[] Binomial(int rows)
        {
            var path = new bool[rows];
            for (int i = 0; i < rows; i++) path[i] = Random.value < 0.5f;
            return path;
        }

        /// <summary>Camino que acaba en un cubo concreto (para cubos elegidos por pesos).</summary>
        public static bool[] ToBucket(int rows, int bucket)
        {
            bucket = Mathf.Clamp(bucket, 0, rows);
            var path = new bool[rows];
            for (int i = 0; i < bucket; i++) path[i] = true;
            for (int i = rows - 1; i > 0; i--) // Fisher-Yates
            {
                int j = Random.Range(0, i + 1);
                (path[i], path[j]) = (path[j], path[i]);
            }
            return path;
        }

        /// <summary>Elige un cubo según pesos arbitrarios y genera su camino.</summary>
        public static bool[] Weighted(int rows, float[] weights)
        {
            float total = 0;
            foreach (var w in weights) total += w;
            float roll = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return ToBucket(rows, i);
            }
            return ToBucket(rows, weights.Length - 1);
        }

        /// <summary>
        /// Como Binomial, pero en cada pincho hay una probabilidad de rebotar "hacia fuera"
        /// (hacia el borde más cercano), lo que abre la distribución hacia los cubos altos.
        /// </summary>
        public static bool[] WithDrift(int rows, float drift, int startRow = 0, int startCol = 0, bool[] source = null)
        {
            var path = source != null ? (bool[])source.Clone() : new bool[rows];
            int col = startCol;
            for (int r = startRow; r < rows; r++)
            {
                bool right;
                float center = r * 0.5f;
                if (drift > 0 && col != center && Random.value < drift) right = col > center; // empujón hacia fuera
                else right = Random.value < 0.5f;
                path[r] = right;
                if (right) col++;
            }
            return path;
        }

        /// <summary>Re-aleatoriza el camino a partir de una fila (clones).</summary>
        public static bool[] Reroll(bool[] source, int fromRow, int col, float drift = 0f)
            => WithDrift(source.Length, drift, fromRow, col, source);
    }
}
