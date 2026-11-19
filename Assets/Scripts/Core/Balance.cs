using System;
using UnityEngine;

namespace Plinko
{
    public struct BallTier
    {
        public string Name;
        public double Value;      // valor base de la bola
        public double PegBonus;   // multiplica el ingreso fijo por rebote
        public Color Color;

        public BallTier(string name, double value, double pegBonus, Color color)
        {
            Name = name; Value = value; PegBonus = pegBonus; Color = color;
        }
    }

    /// <summary>
    /// Todas las fórmulas de balance en un único sitio. Los niveles de mejora entran, los valores salen.
    /// Regla de diseño: ningún cubo baja de x1 y ninguna tirada cuesta nada (no hay apuesta).
    /// </summary>
    public static class Balance
    {
        // --- Secciones de mejoras (pestañas) ---
        /// <summary>
        /// Lingotes de oro (del prestigio) para desbloquear cada pestaña. Se desbloquean en orden y son permanentes:
        /// la primera partida se juega solo con Bolas y Pinchos.
        /// </summary>
        public static double TabUnlockCost(UpgradeCategory c) => c switch
        {
            UpgradeCategory.Rebotes => 5,
            UpgradeCategory.Sobrecarga => 7,
            UpgradeCategory.Multiplicadores => 10,
            _ => 0,                                    // Bolas y Pinchos, desde el principio
        };

        // --- Tablero ---
        public const int BaseRows = 8;
        public const int RowStep = 2;          // siempre filas pares -> número impar de cubos con uno central
        public const int MaxRowLevels = 2;     // 8 -> 12 filas: solo dos ampliaciones, muy caras
        public static int Rows(int lvl) => BaseRows + RowStep * Math.Min(lvl, MaxRowLevels);

        // --- Lanzamiento ---
        public const int BaseMaxBalls = 3;
        public const float ManualCooldown = 0.35f;
        public static int MaxBalls(int lvl) => BaseMaxBalls + lvl;          // pocas bolas: que merezca la pena mirarlas
        public static float AutoInterval(int lvl) => lvl <= 0 ? 0f : Mathf.Max(0.35f, 2.0f * Mathf.Pow(0.88f, lvl - 1));

        // Mejor calidad de bola = bolas más valiosas pero menos a la vez y lanzadas más despacio
        public static float TierCapacityFactor(int tier) => Mathf.Pow(0.8f, tier);
        public static float TierLaunchFactor(int tier) => 1f + 0.25f * tier;

        /// <summary>
        /// Encarecimiento extra de cada mejora a partir del nivel 5: los primeros niveles van rápidos
        /// y los altos frenan, para que el final no sea un no parar de comprar.
        /// </summary>
        public static double LateLevelFactor(int level) => Math.Pow(1.35, Math.Max(0, level - 4));

        // --- Pinchos ---
        public static double PegFlat(int lvl) => 5 * lvl;                       // +1 por rebote y nivel
        public static double PegPercent(int lvl) => 0.04 * lvl;            // +0.5% del valor de la bola por rebote
        public static double BonusChance(int lvl) => Math.Min(0.54, 0.06 * lvl);
        public static double BonusAmount(int lvl) => 0.06 + 0.06 * lvl;     // multiplicativo sobre el valor de la bola
        public static double CritChance(int lvl) => 0.0125 * lvl;           // hasta 5%
        public const double CritMultiplier = 2.0;
        public const double GoldenPegMultiplier = 1.5;

        // --- Efectos por rebote ---
        public static double LightningChance(int lvl) => 0.03 * lvl;      // hasta 15%
        public static int LightningHops(int lvl) => 3 + lvl;            // pinchos que activa el rayo
        public const double LightningHopValue = 0.10;                       // cada salto paga un 10% del valor de la bola
        public static double ChargeChance(int lvl) => 0.02 * lvl;         // hasta 10%
        public const double ChargedPegMultiplier = 1.5;
        public const float ChargeDuration = 4f;
        public static int MaxChargedPegs(int lvl) => 2 + lvl;
        public static double EchoChance(int lvl) => 0.06 * lvl;             // hasta 30%

        // --- Sobrecarga (frenesí temporal) ---
        public static float OverloadDuration(int lvl) => 12f + 5f * lvl;                // +1 s por nivel
        public static double OverloadMultiplier(int lvl) => 3.0 + 2.5 * lvl;       // x3, +0.5 por nivel
        public static float OverloadFillFactor(int lvl) => 1f / (1f + 0.5f * lvl); // se llena antes
        public const float OverloadLaunchBoost = 2f;      // auto-lanzador el doble de rápido
        public const float OverloadCapacityBoost = 1.5f;  // +50% bolas en pantalla
        public const float OverloadMinHits = 700f;        // rebotes mínimos para llenarla
        public const float OverloadFillSeconds = 100f;    // con mucho ritmo, tarda ~100 s en llenarse

        // --- Avalancha (evento aleatorio): muchas más bolas a la vez, sin tocar el valor por bola ---
        public const float RushMinDelay = 120f, RushMaxDelay = 240f;  // cada 2-4 minutos
        public const float RushDuration = 5f;            // corta a propósito: el prestigio permitirá alargarla
        public const float RushBoost = 5f;            // capacidad x5 y auto-lanzador x5
        public const int RushMaxBalls = 80;           // tope: incluso en avalancha, que se puedan seguir
        public const float RushMinInterval = 0.5f;    // sin auto-lanzador, la avalancha lanza igualmente

        // --- Bola dorada (aparece al lanzar con cierta probabilidad) ---
        public static double GoldenBallChance(int lvl) => 0.06 * lvl;      // hasta 50%
        public const double GoldenBallMultiplier = 5.0;

        // --- Cubos ---
        public const double MinBucketMultiplier = 1.0;                      // ningún cubo baja de x1 (no hay pérdida)
        // Tres tipos de cubo: centrales (los 3 del medio), intermedios (los 2 siguientes a cada lado) y extremos (el resto)
        public const int GroupCenter = 0, GroupMiddle = 1, GroupEdge = 2;
        public static int BucketGroup(int k) => k <= 1 ? GroupCenter : k <= 3 ? GroupMiddle : GroupEdge;
        static readonly double[] GroupBonusPerLevel = { 0.5, 1.25, 2.5 }; // los de los lados suben más por nivel
        public static double BucketBonus(int group, int lvl) => GroupBonusPerLevel[group] * lvl;

        /// <summary>Probabilidad de que una bola caiga en un grupo (sin desvío), para el coste de su mejora.</summary>
        public static double GroupProbability(int rows, int group)
        {
            double p = 0;
            for (int k = 0; k <= rows / 2; k++) if (BucketGroup(k) == group) p += BucketProbability(rows, k);
            return p;
        }

        // --- Desvío hacia los lados ---
        public static double DriftChance(int lvl) => 0.07 * lvl;            // hasta 30% de rebotes forzados hacia fuera

        /// <summary>Multiplicador base del cubo a distancia k del centro. Centro x1, bordes más altos.</summary>
        public static double BaseBucketMultiplier(int rows, int k)
        {
            int half = rows / 2;
            double d = half == 0 ? 0 : (double)k / half;
            double edge = 3.0 + (rows - BaseRows) * 0.5;   // 8 filas: x3 en el borde, 16 filas: x7
            double m = 1.0 + (edge - 1.0) * d * d * d;
            return Math.Round(m, 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>Probabilidad de caer a distancia |k| del centro (suma ambos lados).</summary>
        public static double BucketProbability(int rows, int k)
        {
            int half = rows / 2;
            if (k > half) return 0;
            double p = Binomial(rows, half + k) / Math.Pow(2, rows);
            return k == 0 ? p : 2 * p;
        }

        static double Binomial(int n, int r)
        {
            double res = 1;
            for (int i = 1; i <= r; i++) res = res * (n - r + i) / i;
            return res;
        }

        // --- Tipos de bola (mejora permanente del lanzador, nunca se compran bolas sueltas) ---
        public static readonly BallTier[] Tiers =
        {
            new BallTier("Básica",    4,      1,   new Color(0.92f, 0.94f, 1.00f)),
            new BallTier("Bronce",    32,     3,   new Color(0.85f, 0.55f, 0.32f)),
            new BallTier("Plata",     256,    9,   new Color(0.80f, 0.85f, 0.92f)),
            new BallTier("Esmeralda", 2048,   27,  new Color(0.35f, 0.95f, 0.60f)),
            new BallTier("Rubí",      16384,  81,  new Color(1.00f, 0.35f, 0.45f)),
            new BallTier("Diamante",  131072, 243, new Color(0.55f, 0.85f, 1.00f)),
        };
    }
}
