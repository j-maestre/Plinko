using System;
using System.Collections.Generic;

namespace Plinko
{
    /// <summary>Mejora permanente de la tienda de prestigio (se paga con lingotes y no se pierde al reiniciar).</summary>
    public class PrestigeDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public double BaseCost;
        public double Growth;
        public int MaxLevel;
        public Func<int, string> Effect;

        public double CostAt(int level) => Math.Ceiling(BaseCost * Math.Pow(Growth, level));
    }

    public static class PrestigeIds
    {
        public const string Multiplier = "p_mult";
        public const string BallTier = "p_tier";
        public const string RushDuration = "p_rush_time";
        public const string RushFrequency = "p_rush_freq";
        public const string StartMoney = "p_start_money";
        public const string StartAuto = "p_start_auto";
        public const string GoldenBall = "p_golden";
        public const string Overload = "p_overload";
    }

    /// <summary>Fórmulas del prestigio en un solo sitio.</summary>
    public static class PrestigeBalance
    {
        /// <summary>Ganancias de la partida necesarias para el primer lingote.</summary>
        public const double FirstCoreAt = 1e6;

        /// <summary>Lingotes que da reiniciar con estas ganancias (raíz cuadrada: crece pero cada vez cuesta más).</summary>
        public static double CoresFor(double runEarned) => Math.Floor(Math.Sqrt(Math.Max(0, runEarned) / FirstCoreAt));

        /// <summary>Ganancias necesarias para tener n lingotes.</summary>
        public static double EarnedForCores(double n) => n * n * FirstCoreAt;

        public const double UnspentCoreBonus = 0.01;           // cada lingote sin gastar: +1% de dinero

        /// <summary>Precio en dinero de hacer prestigio: obliga a llegar lejos y sube con cada prestigio.</summary>
        public static double PrestigeCost(int prestigeCount) => 10e6 * Math.Pow(6, prestigeCount); // la 1ª partida solo tiene Bolas y Pinchos

        /// <summary>
        /// Lingotes fijos por prestigio (no dependen de cuánto pases del precio): incentiva reiniciar en cuanto se pueda.
        /// 5, 7, 10, 14, 19, 27, 38...
        /// </summary>
        public static double Reward(int prestigeCount) => Math.Round(5 * Math.Pow(1.4, prestigeCount));
        public static double Multiplier(int lvl) => 1.0 + 0.25 * lvl;
        public static float RushDuration(int lvl) => Balance.RushDuration + 3f * lvl;
        public static float RushDelayFactor(int lvl) => 1f - 0.1f * lvl;
        public static double StartMoney(int lvl) => lvl <= 0 ? 0 : 100 * Math.Pow(10, lvl);
        public static int StartAuto(int lvl) => 2 * lvl;
        public static double GoldenBallMultiplier(int lvl) => Balance.GoldenBallMultiplier + lvl;
        public static double OverloadBonus(int lvl) => 0.5 * lvl;
    }

    public static class PrestigeCatalog
    {
        public static readonly List<PrestigeDef> All = new List<PrestigeDef>
        {
            new PrestigeDef
            {
                Id = PrestigeIds.BallTier, Name = "Calidad de bola", Desc = "Bolas mucho más valiosas, pero caben menos y salen más despacio",
                BaseCost = 3, Growth = 2.5, MaxLevel = Balance.Tiers.Length - 1,
                Effect = l => Balance.Tiers[l].Name + " (valor " + NumberFormat.Format(Balance.Tiers[l].Value) + ", " +
                              (int)Math.Round(Balance.TierCapacityFactor(l) * 100) + "% bolas)"
            },
            new PrestigeDef
            {
                Id = PrestigeIds.Multiplier, Name = "Riqueza", Desc = "Más dinero de todas las fuentes",
                BaseCost = 1, Growth = 1.6, MaxLevel = 100,
                Effect = l => "Dinero " + NumberFormat.Multiplier(PrestigeBalance.Multiplier(l))
            },
            new PrestigeDef
            {
                Id = PrestigeIds.RushDuration, Name = "Avalancha larga", Desc = "La Avalancha dura 3 segundos más",
                BaseCost = 2, Growth = 2.0, MaxLevel = 5,
                Effect = l => PrestigeBalance.RushDuration(l).ToString("0") + " s"
            },
            new PrestigeDef
            {
                Id = PrestigeIds.RushFrequency, Name = "Avalancha frecuente", Desc = "La Avalancha llega más a menudo",
                BaseCost = 3, Growth = 2.0, MaxLevel = 5,
                Effect = l => "Cada " + (Balance.RushMinDelay * PrestigeBalance.RushDelayFactor(l) / 60f).ToString("0.#") + "-" +
                              (Balance.RushMaxDelay * PrestigeBalance.RushDelayFactor(l) / 60f).ToString("0.#") + " min"
            },
            new PrestigeDef
            {
                Id = PrestigeIds.StartMoney, Name = "Capital inicial", Desc = "Empiezas cada partida con dinero",
                BaseCost = 1, Growth = 2.2, MaxLevel = 8,
                Effect = l => NumberFormat.Money(PrestigeBalance.StartMoney(l))
            },
            new PrestigeDef
            {
                Id = PrestigeIds.StartAuto, Name = "Arranque automático", Desc = "Empiezas con niveles de auto-lanzador",
                BaseCost = 2, Growth = 1.9, MaxLevel = 10,
                Effect = l => "Auto-lanzador nv " + PrestigeBalance.StartAuto(l)
            },
            new PrestigeDef
            {
                Id = PrestigeIds.GoldenBall, Name = "Oro puro", Desc = "La bola dorada vale más",
                BaseCost = 3, Growth = 2.0, MaxLevel = 10,
                Effect = l => "Bola dorada x" + PrestigeBalance.GoldenBallMultiplier(l)
            },
            new PrestigeDef
            {
                Id = PrestigeIds.Overload, Name = "Sobrecarga innata", Desc = "La Sobrecarga multiplica más, siempre",
                BaseCost = 2, Growth = 1.9, MaxLevel = 10,
                Effect = l => "+" + PrestigeBalance.OverloadBonus(l).ToString("0.#") + " al multiplicador"
            },
        };
    }
}
