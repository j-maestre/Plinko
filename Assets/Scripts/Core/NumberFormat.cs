using System;
using System.Globalization;

namespace Plinko
{
    /// <summary>Formatea números grandes con sufijos (K, M, B, T, Qa... y luego aa, ab...).</summary>
    public static class NumberFormat
    {
        static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>
        /// Con sufijo y como mucho un decimal (solo por debajo de 10).
        /// 950 → "950", 1.234 → "1234", 12.345 → "12K", 1.500.000 → "1.5M", 66.300 Qa → "66Qa".
        /// </summary>
        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return "∞";
            if (v < 0) return "-" + Format(-v);
            if (v < 10000) return Math.Floor(v).ToString("0", CI);

            int tier = (int)Math.Floor(Math.Log10(v) / 3.0);
            double scaled = v / Math.Pow(1000, tier);
            // por debajo de 10 se deja un decimal (1.5M, 2M); a partir de 10, entero (12M, 150M)
            if (scaled < 10) return (Math.Floor(scaled * 10) / 10).ToString("0.#", CI) + Suffix(tier);
            return Math.Floor(scaled).ToString("0", CI) + Suffix(tier);
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

        /// <summary>Multiplicadores: x1.5 conserva un decimal (significa algo); a partir de x10, entero.</summary>
        public static string Multiplier(double m) => "x" + m.ToString(m < 10 ? "0.#" : "0", CI);

        /// <summary>Porcentajes enteros (solo por debajo del 1% se deja un decimal: 0.5%).</summary>
        public static string Percent(double p) => (p * 100).ToString(p < 0.01 ? "0.#" : "0", CI) + "%";

        public static string Seconds(float s) => s.ToString(s < 1 ? "0.00" : "0.0", CI) + "s";
    }
}
