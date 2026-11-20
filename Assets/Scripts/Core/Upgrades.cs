using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plinko
{
    public enum UpgradeCategory { Bolas, Pinchos, Rebotes, Sobrecarga, Multiplicadores }

    public class UpgradeDef
    {
        public string Id;
        public string Name;
        /// <summary>Qué hace la mejora, en una frase corta.</summary>
        public string Desc;
        public UpgradeCategory Category;
        public double BaseCost;
        public double Growth;
        public int MaxLevel;
        public string RequiresId;
        public int RequiresLevel = 1;
        /// <summary>Texto del efecto para un nivel dado (se muestra "actual → siguiente").</summary>
        public Func<int, string> Effect;
        /// <summary>Condición extra de visibilidad (opcional).</summary>
        public Func<GameManager, bool> Visible;

        public double CostAt(int level) => BaseCost * Math.Pow(Growth, level) * Balance.LateLevelFactor(level);

        // --- Menos compras pero más gordas ---
        // Las mejoras largas suben de 2 en 2 niveles internos por compra (doble efecto, se paga la suma de ambos).
        // Las cortas (tablero, secciones de pocas compras) siguen de 1 en 1.
        public int Step => MaxLevel >= 8 ? 2 : 1;
        /// <summary>Nivel que ve el jugador (compras hechas).</summary>
        public int Shown(int level) => (level + Step - 1) / Step;
        public int ShownMax => Shown(MaxLevel);
        /// <summary>Nivel interno tras la siguiente compra.</summary>
        public int NextLevel(int level) => Math.Min(MaxLevel, level + Step);
        /// <summary>Precio de la siguiente compra (suma de los niveles internos que sube).</summary>
        public double PurchaseCost(int level)
        {
            double c = 0;
            for (int l = level; l < NextLevel(level); l++) c += CostAt(l);
            return c;
        }
    }

    public static class UpgradeIds
    {
        public const string AutoLauncher = "auto";
        public const string Capacity = "capacity";
        public const string GoldenBall = "golden_ball";
        public const string PegFlat = "peg_flat";
        public const string BonusChance = "bonus_chance";
        public const string BonusAmount = "bonus_amount";
        public const string Crit = "crit";
        public const string Lightning = "lightning";
        public const string Charge = "charge";
        public const string Echo = "echo";
        public const string GoldenPegs = "golden";
        public const string ClonePegs = "clone";
        public const string Rows = "rows";
        public const string Drift = "drift";
        public const string OverloadTime = "overload_time";
        public const string OverloadMult = "overload_mult";
        public const string OverloadFill = "overload_fill";
        public static readonly string[] BucketGroups = { "bucket_center", "bucket_middle", "bucket_edge" };
        public static string BucketGroup(int group) => BucketGroups[group];
    }

    public static class UpgradeCatalog
    {
        public static readonly List<UpgradeDef> All = Build();
        static Dictionary<string, UpgradeDef> byId;

        public static UpgradeDef Get(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, UpgradeDef>();
                foreach (var d in All) byId[d.Id] = d;
            }
            return byId.TryGetValue(id, out var def) ? def : null;
        }

        static List<UpgradeDef> Build()
        {
            var list = new List<UpgradeDef>
            {
                new UpgradeDef
                {
                    Id = UpgradeIds.AutoLauncher, Name = "Caída rápida", Desc = "Las bolas caen más deprisa: más caídas por segundo (tope x1.5)", Category = UpgradeCategory.Bolas,
                    BaseCost = 1500, Growth = 2.6, MaxLevel = 9,
                    Effect = l => "Velocidad x" + Balance.FallSpeed(l).ToString("0.00")
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Capacity, Name = "Huecos", Desc = "Puedes tener más bolas a la vez", Category = UpgradeCategory.Bolas,
                    BaseCost = 400, Growth = 3.0, MaxLevel = 12,
                    Effect = l => Balance.MaxBalls(l) + " huecos"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.GoldenBall, Name = "Bola shiny", Desc = "Probabilidad de que una bola salga shiny: brilla y vale x5 su valor",
                    Category = UpgradeCategory.Bolas,
                    BaseCost = 150000, Growth = 2.7, MaxLevel = 9,
                    Effect = l => NumberFormat.Percent(Balance.GoldenBallChance(l)) + " de bola shiny"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.PegFlat, Name = "Pinchos reforzados", Desc = "Cada rebote suma un % al valor de la bola (se cobra al caer en el cubo)", Category = UpgradeCategory.Pinchos,
                    BaseCost = 200, Growth = 2.5, MaxLevel = 40,
                    Effect = l => NumberFormat.Percent(Balance.BasePegPercent + Balance.PegPercentBonus(l)) + " más de valor por rebote"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.BonusChance, Name = "Rebote potenciador", Desc = "Probabilidad de que un rebote aumente el valor de la bola", Category = UpgradeCategory.Rebotes,
                    BaseCost = 15000, Growth = 2.7, MaxLevel = 9,
                    Effect = l => NumberFormat.Percent(Balance.BonusChance(l)) + " prob. por rebote"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.BonusAmount, Name = "Potencia del rebote", Desc = "Cuánto aumenta la bola cuando el rebote potencia", Category = UpgradeCategory.Rebotes,
                    BaseCost = 60000, Growth = 2.6, MaxLevel = 20,
                    RequiresId = UpgradeIds.BonusChance, RequiresLevel = 1,
                    Effect = l => "+" + NumberFormat.Percent(Balance.BonusAmount(l)) + " valor de bola"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Crit, Name = "Rebote crítico", Desc = "Rebote raro que duplica el valor de la bola", Category = UpgradeCategory.Rebotes,
                    BaseCost = 1500000, Growth = 3.2, MaxLevel = 4,
                    RequiresId = UpgradeIds.BonusChance, RequiresLevel = 5,
                    Effect = l => NumberFormat.Percent(Balance.CritChance(l)) + " de x2"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Echo, Name = "Eco", Desc = "La bola rebota dos veces en el mismo pincho",
                    Category = UpgradeCategory.Rebotes,
                    BaseCost = 120000, Growth = 2.6, MaxLevel = 5,
                    Effect = l => NumberFormat.Percent(Balance.EchoChance(l)) + " de doble rebote"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Lightning, Name = "Relámpago", Desc = "Un rayo salta a pinchos cercanos y los activa",
                    Category = UpgradeCategory.Rebotes,
                    BaseCost = 1000000, Growth = 2.7, MaxLevel = 5,
                    Effect = l => NumberFormat.Percent(Balance.LightningChance(l)) + ", " + Balance.LightningHops(l) + " saltos"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Charge, Name = "Pincho cargado", Desc = "El pincho se electrifica y da x1.5 a las bolas que lo toquen",
                    Category = UpgradeCategory.Rebotes,
                    BaseCost = 8000000, Growth = 2.7, MaxLevel = 6,
                    Effect = l => NumberFormat.Percent(Balance.ChargeChance(l)) + ", máx. " + Balance.MaxChargedPegs(l) + " a la vez"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.GoldenPegs, Name = "Pincho dorado", Desc = "Cada nivel vuelve dorado un pincho más: multiplica el valor de la bola al tocarlo", Category = UpgradeCategory.Pinchos,
                    BaseCost = 50000, Growth = 2.3, MaxLevel = 20,
                    Effect = l => l + (l == 1 ? " pincho (" : " pinchos (") + NumberFormat.Multiplier(Balance.GoldenPegMultiplier) + " valor)"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.ClonePegs, Name = "Pinchos clonadores", Desc = "Pinchos que crean una copia de la bola", Category = UpgradeCategory.Pinchos,
                    BaseCost = 10000000, Growth = 3.7, MaxLevel = 8,
                    RequiresId = UpgradeIds.GoldenPegs, RequiresLevel = 1,
                    Effect = l => l + " pinchos (duplican la bola)"
                },
                new UpgradeDef
                {
                    Id = UpgradeIds.Rows, Name = "Expandir tablero", Desc = "+2 filas de pinchos y +2 cubos en los bordes (multiplican más)", Category = UpgradeCategory.Pinchos,
                    BaseCost = 150000, Growth = 35, MaxLevel = Balance.MaxRowLevels,
                    Effect = l => Balance.Rows(l) + " filas, " + (Balance.Rows(l) + 1) + " cubos"
                },
            };

            string[] groupNames = { "Cubos centrales", "Cubos intermedios", "Cubos extremos" };
            string[] groupDescs =
            {
                "Los 3 cubos del medio, donde cae casi todo",
                "Los 2 cubos siguientes a cada lado",
                "Los cubos de los bordes: raros pero enormes",
            };
            for (int g = 0; g < 3; g++)
            {
                int group = g;
                double[] groupCost = { 2e6, 15e6, 120e6 }; // lo más difícil del juego: centrales -> intermedios -> extremos
                list.Add(new UpgradeDef
                {
                    Id = UpgradeIds.BucketGroup(group),
                    Name = groupNames[group],
                    Desc = groupDescs[group],
                    Category = UpgradeCategory.Multiplicadores,
                    BaseCost = groupCost[group],
                    Growth = 2.8, MaxLevel = 25,
                    Effect = l => "+" + NumberFormat.Multiplier(Balance.BucketBonus(group, l)).Substring(1)
                });
            }
            list.Add(new UpgradeDef
            {
                Id = UpgradeIds.OverloadTime, Name = "Sobrecarga duradera", Desc = "La Sobrecarga dura más tiempo",
                Category = UpgradeCategory.Sobrecarga,
                BaseCost = 15000, Growth = 2.8, MaxLevel = 5,
                Effect = l => Balance.OverloadDuration(l).ToString("0") + " s"
            });
            list.Add(new UpgradeDef
            {
                Id = UpgradeIds.OverloadMult, Name = "Sobrecarga potente", Desc = "Multiplica más el dinero durante la Sobrecarga",
                Category = UpgradeCategory.Sobrecarga,
                BaseCost = 100000, Growth = 3.0, MaxLevel = 5,
                Effect = l => "Todo " + NumberFormat.Multiplier(Balance.OverloadMultiplier(l))
            });
            list.Add(new UpgradeDef
            {
                Id = UpgradeIds.OverloadFill, Name = "Carga rápida", Desc = "El medidor de Sobrecarga se llena antes",
                Category = UpgradeCategory.Sobrecarga,
                BaseCost = 600000, Growth = 2.8, MaxLevel = 5,
                Effect = l => "Necesita " + (int)Math.Round(Balance.OverloadFillFactor(l) * 100) + "% de rebotes"
            });
            list.Add(new UpgradeDef
            {
                Id = UpgradeIds.Drift, Name = "Desvío lateral", Desc = "Las bolas tienden a rebotar hacia los lados",
                Category = UpgradeCategory.Multiplicadores,
                BaseCost = 300e6, Growth = 3.0, MaxLevel = 5,
                Effect = l => NumberFormat.Percent(Balance.DriftChance(l)) + " de rebotes hacia fuera"
            });

            ApplyUnlockChain(list);
            return list;
        }

        /// <summary>
        /// Progresión de desbloqueos: cada mejora aparece al subir otra a cierto nivel.
        /// Al principio solo se ven el auto-lanzador y los pinchos reforzados.
        /// </summary>
        static void ApplyUnlockChain(List<UpgradeDef> list)
        {
            var chain = new Dictionary<string, (string req, int lvl)>
            {
                // Bolas
                { UpgradeIds.AutoLauncher, (UpgradeIds.Capacity, 2) },   // Huecos disponibles desde el inicio; Caída rápida después
                { UpgradeIds.GoldenBall,  (UpgradeIds.Capacity, 4) },
                // Pinchos
                { UpgradeIds.Rows,        (UpgradeIds.PegFlat, 4) },
                { UpgradeIds.GoldenPegs,  (UpgradeIds.PegFlat, 5) },
                { UpgradeIds.ClonePegs,   (UpgradeIds.GoldenPegs, 1) },
                // Rebotes
                { UpgradeIds.BonusChance, (UpgradeIds.PegFlat, 3) },
                { UpgradeIds.BonusAmount, (UpgradeIds.BonusChance, 1) },
                { UpgradeIds.Crit,        (UpgradeIds.BonusChance, 3) },
                { UpgradeIds.Echo,        (UpgradeIds.BonusChance, 2) },
                { UpgradeIds.Lightning,   (UpgradeIds.Echo, 2) },
                { UpgradeIds.Charge,      (UpgradeIds.Lightning, 2) },
                // Multiplicadores
                { UpgradeIds.BucketGroup(0), (UpgradeIds.PegFlat, 2) },
                { UpgradeIds.BucketGroup(1), (UpgradeIds.BucketGroup(0), 2) },
                { UpgradeIds.BucketGroup(2), (UpgradeIds.BucketGroup(1), 2) },
                { UpgradeIds.Drift,          (UpgradeIds.BucketGroup(2), 1) },
                // Sobrecarga
                { UpgradeIds.OverloadTime,   (UpgradeIds.PegFlat, 3) },
                { UpgradeIds.OverloadMult,   (UpgradeIds.OverloadTime, 1) },
                { UpgradeIds.OverloadFill,   (UpgradeIds.OverloadMult, 1) },
            };

            foreach (var def in list)
            {
                if (!chain.TryGetValue(def.Id, out var r)) continue;
                def.RequiresId = r.req;
                def.RequiresLevel = r.lvl;
            }
        }
    }
}
