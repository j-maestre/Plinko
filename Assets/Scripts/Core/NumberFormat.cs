using System;
using System.Globalization;

namespace Plinko
{
    /// <summary>Formatea números grandes con sufijos (K, M, B, T, Qa... y luego aa, ab...).</summary>
    public static class NumberFormat
    {
        static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Format(-v);
            if (v < 1000) return v.ToString(v < 100 ? "0.#" : "0", CI);

            int tier = (int)Math.Floor(Math.Log10(v) / 3.0);
            double scaled = v / Math.Pow(1000, tier);
            if (scaled >= 999.5) { tier++; scaled /= 1000; }

            string fmt = scaled < 10 ? "0.00" : scaled < 100 ? "0.0" : "0";
            return scaled.ToString(fmt, CI) + Suffix(tier);
        }

        static string Suffix(int tier)
        {
            if (tier < Suffixes.Length) return Suffixes[tier];
            int i = tier - Suffixes.Length;
            char a = (char)('a' + (i / 26) % 26);
            char b = (char)('a' + i % 26);
            return new string(new[] { a, b });
        }

        /// <summary>Cantidad de dinero con símbolo: $1.23K</summary>
        public static string Money(double v) => "$" + Format(v);

        public static string Multiplier(double m) => "x" + m.ToString(m < 10 ? "0.0#" : "0.#", CI);

        public static string Percent(double p) => (p * 100).ToString("0.##", CI) + "%";

        public static string Seconds(float s) => s.ToString(s < 1 ? "0.00" : "0.0", CI) + "s";
    }
}
