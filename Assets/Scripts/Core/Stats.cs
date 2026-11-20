using System;

namespace Plinko
{
    /// <summary>Valores derivados de los niveles de mejora. Se recalcula al comprar.</summary>
    public class Stats
    {
        readonly GameManager gm;

        public int Rows, MaxBalls, TierIndex, GoldenPegs, ClonePegs;
        public float AutoInterval, FallSpeed;
        public double PegFlat, PegPercent, BonusChance, BonusAmount, CritChance, GoldenBallChance;
        public double LightningChance, ChargeChance, EchoChance, DriftChance;
        public int LightningHops, MaxChargedPegs;
        public float OverloadDuration, OverloadFill;
        public float RushDuration, RushDelayFactor;
        public double GoldenBallMultiplier;
        public double OverloadMultiplier;

        /// <summary>Multiplicador global: prestigio (y más adelante anuncio x2).</summary>
        public double GlobalMultiplier = 1;

        public BallTier Tier => Balance.Tiers[TierIndex];

        public Stats(GameManager gm) { this.gm = gm; }

        public void Recalculate()
        {
            Rows = Balance.Rows(gm.GetLevel(UpgradeIds.Rows));
            int tier = Math.Min(gm.GetPrestigeLevel(PrestigeIds.BallTier), Balance.Tiers.Length - 1); // premio de prestigio
            MaxBalls = Balance.MaxBalls(gm.GetLevel(UpgradeIds.Capacity));     // huecos para bolas
            TierIndex = tier;
            GoldenPegs = gm.GetLevel(UpgradeIds.GoldenPegs);   // un pincho dorado por nivel
            ClonePegs = gm.GetLevel(UpgradeIds.ClonePegs);
            // Caída rápida (usa el id antiguo del Goteo para conservar los niveles guardados);
            // el goteo base se acelera a la par para que las bolas no esperen
            FallSpeed = Balance.FallSpeed(gm.GetLevel(UpgradeIds.AutoLauncher));
            AutoInterval = Balance.AutoInterval(0) / FallSpeed;
            PegFlat = 0;
            PegPercent = Balance.BasePegPercent + Balance.PegPercentBonus(gm.GetLevel(UpgradeIds.PegFlat));
            BonusChance = Balance.BonusChance(gm.GetLevel(UpgradeIds.BonusChance));
            BonusAmount = Balance.BonusAmount(gm.GetLevel(UpgradeIds.BonusAmount));
            CritChance = Balance.CritChance(gm.GetLevel(UpgradeIds.Crit));
            GoldenBallChance = Balance.GoldenBallChance(gm.GetLevel(UpgradeIds.GoldenBall));
            int lightning = gm.GetLevel(UpgradeIds.Lightning), charge = gm.GetLevel(UpgradeIds.Charge);
            LightningChance = Balance.LightningChance(lightning);
            LightningHops = Balance.LightningHops(lightning);
            ChargeChance = Balance.ChargeChance(charge);
            MaxChargedPegs = Balance.MaxChargedPegs(charge);
            EchoChance = Balance.EchoChance(gm.GetLevel(UpgradeIds.Echo));
            DriftChance = Balance.DriftChance(gm.GetLevel(UpgradeIds.Drift));
            OverloadDuration = Balance.OverloadDuration(gm.GetLevel(UpgradeIds.OverloadTime));
            OverloadMultiplier = Balance.OverloadMultiplier(gm.GetLevel(UpgradeIds.OverloadMult))
                                 + PrestigeBalance.OverloadBonus(gm.GetPrestigeLevel(PrestigeIds.Overload));

            // Prestigio
            GlobalMultiplier = PrestigeBalance.PrestigeIncome(gm.State.prestigeCount);   // solo el x0.5 por prestigio
            RushDuration = PrestigeBalance.RushDuration(gm.GetPrestigeLevel(PrestigeIds.RushDuration));
            RushDelayFactor = PrestigeBalance.RushDelayFactor(gm.GetPrestigeLevel(PrestigeIds.RushFrequency));
            GoldenBallMultiplier = PrestigeBalance.GoldenBallMultiplier(gm.GetPrestigeLevel(PrestigeIds.GoldenBall));
            OverloadFill = Balance.OverloadFillFactor(gm.GetLevel(UpgradeIds.OverloadFill));
        }

        /// <summary>Multiplicador final del cubo a distancia k del centro.</summary>
        public double BucketMultiplier(int k)
        {
            double baseMult = Math.Max(Balance.MinBucketMultiplier, Balance.BaseBucketMultiplier(Rows, k));
            int group = Balance.BucketGroup(k);
            return baseMult + Balance.BucketBonus(group, gm.GetLevel(UpgradeIds.BucketGroup(group)));
        }

        /// <summary>Valor esperado del multiplicador de cubo, teniendo en cuenta el desvío lateral.</summary>
        public double ExpectedBucketMultiplier()
        {
            // distribución exacta de la columna fila a fila (como PathGenerator.WithDrift)
            var p = new double[Rows + 1];
            p[0] = 1;
            for (int r = 0; r < Rows; r++)
            {
                var next = new double[Rows + 1];
                double center = r * 0.5;
                for (int c = 0; c <= r; c++)
                {
                    if (p[c] == 0) continue;
                    double right = 0.5;
                    if (c != center) right = c > center ? 0.5 + 0.5 * DriftChance : 0.5 - 0.5 * DriftChance;
                    next[c + 1] += p[c] * right;
                    next[c] += p[c] * (1 - right);
                }
                p = next;
            }
            double ev = 0;
            for (int c = 0; c <= Rows; c++) ev += p[c] * BucketMultiplier(Math.Abs(c - Rows / 2));
            return ev;
        }
    }
}
