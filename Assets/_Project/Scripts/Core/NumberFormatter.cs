using System;
using System.Globalization;

namespace Onikiri.Core
{
    /// <summary>
    /// Turns <see cref="BigDouble"/> values into the short player-facing notation used
    /// throughout the HUD and upgrade panel: 1.5K, 3.2M, 7.8B, 1.2aa.
    ///
    /// Tiers step every 3 decimal digits. The first four have conventional letters
    /// (K/M/B/T), and past 10^15 it switches to two-letter tags (aa, ab, ... zz) which
    /// is the standard idle-game convention and stays readable to ~10^2042.
    /// </summary>
    public static class NumberFormatter
    {
        private const int DefaultDecimals = 1;

        /// <summary>Tier index 1..4. Tier 0 is the unsuffixed sub-1000 range.</summary>
        private static readonly string[] ShortSuffixes = { "", "K", "M", "B", "T" };

        /// <summary>First tier that uses the alphabetic scheme (10^15).</summary>
        private const long AlphabeticTierStart = 5;

        private const int LettersInAlphabet = 26;

        public static string Format(BigDouble value)
        {
            return Format(value, DefaultDecimals);
        }

        public static string Format(BigDouble value, int decimals)
        {
            if (value.IsZero) return "0";
            if (decimals < 0) decimals = 0;

            string sign = value.IsNegative ? "-" : string.Empty;
            BigDouble abs = value.Abs();

            // Under 1000 there is no suffix — currencies read as plain integers.
            if (abs.Exponent < 3)
            {
                double plain = Math.Round(abs.ToDouble());
                if (plain < 1000d)
                    return sign + plain.ToString("F0", CultureInfo.InvariantCulture);
                // Rounding pushed it up to exactly 1000; fall through so it becomes "1.0K".
                abs = BigDouble.FromDouble(plain);
            }

            long tier = abs.Exponent / 3;
            int withinTier = (int)(abs.Exponent - tier * 3);

            // Scale the mantissa into [1, 1000) for display.
            double display = abs.Mantissa * Math.Pow(10d, withinTier);
            display = Math.Round(display, decimals);

            // Rounding can carry into the next tier (999.95 -> 1000.0 -> 1.0 of next tier).
            if (display >= 1000d)
            {
                display /= 1000d;
                tier++;
            }

            string suffix = SuffixForTier(tier);
            if (suffix == null) return FormatScientific(value, decimals);

            return sign + display.ToString("F" + decimals, CultureInfo.InvariantCulture) + suffix;
        }

        public static string Format(double value)
        {
            return Format(BigDouble.FromDouble(value), DefaultDecimals);
        }

        /// <summary>
        /// Suffix for a 10^(3*tier) magnitude, or null when the value has outgrown the
        /// two-letter scheme and should fall back to scientific notation.
        /// </summary>
        public static string SuffixForTier(long tier)
        {
            if (tier < 0) return null;
            if (tier < ShortSuffixes.Length) return ShortSuffixes[tier];

            long index = tier - AlphabeticTierStart;
            long first = index / LettersInAlphabet;
            long second = index % LettersInAlphabet;

            if (first >= LettersInAlphabet) return null; // past "zz"

            return string.Concat(
                ((char)('a' + first)).ToString(),
                ((char)('a' + second)).ToString());
        }

        /// <summary>Fallback for magnitudes past the named tiers, e.g. "1.23e2100".</summary>
        public static string FormatScientific(BigDouble value, int decimals)
        {
            if (value.IsZero) return "0";
            return value.Mantissa.ToString("F" + decimals, CultureInfo.InvariantCulture)
                   + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Compact duration used by the offline-reward popup ("3h 12m", "45s").
        /// Lives here so all player-facing number text shares one place.
        /// </summary>
        public static string FormatDuration(TimeSpan span)
        {
            if (span.TotalSeconds < 1d) return "0s";

            if (span.TotalHours >= 1d)
            {
                int hours = (int)span.TotalHours;
                int minutes = span.Minutes;
                return minutes > 0
                    ? hours + "h " + minutes + "m"
                    : hours + "h";
            }

            if (span.TotalMinutes >= 1d)
            {
                int minutes = (int)span.TotalMinutes;
                int seconds = span.Seconds;
                return seconds > 0
                    ? minutes + "m " + seconds + "s"
                    : minutes + "m";
            }

            return (int)span.TotalSeconds + "s";
        }
    }
}
