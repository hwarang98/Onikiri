using System;
using System.Globalization;
using UnityEngine;

namespace Onikiri.Core
{
    /// <summary>
    /// Arbitrary-magnitude number stored as <c>mantissa * 10^exponent</c>.
    ///
    /// Idle games blow past <see cref="long"/> within hours of play, so every currency,
    /// damage and cost value in ONIKIRI uses this type instead of long/double.
    ///
    /// The mantissa is always normalized to 1 &lt;= |mantissa| &lt; 10 (or exactly 0), and
    /// the exponent is a long, so the representable range is roughly 10^±9.2e18 — far
    /// beyond anything the game can reach. Precision stays at double's ~15 significant
    /// digits regardless of magnitude, which is exactly what an idle game wants: you
    /// care about "1.53 septillion", not the last digit.
    ///
    /// Unity-serializable, so it drops straight into JsonUtility save data.
    /// </summary>
    [Serializable]
    public struct BigDouble : IComparable<BigDouble>, IEquatable<BigDouble>
    {
        [SerializeField] private double m;
        [SerializeField] private long e;

        /// <summary>Normalized to 1 &lt;= |mantissa| &lt; 10, or 0.</summary>
        public double Mantissa { get { return m; } }
        public long Exponent { get { return e; } }

        public static readonly BigDouble Zero = new BigDouble(0d, 0L);
        public static readonly BigDouble One = new BigDouble(1d, 0L);

        /// <summary>
        /// Adding two values further apart than this in magnitude cannot change the
        /// larger one — double has ~15-17 significant digits, so the smaller term
        /// falls off the end.
        /// </summary>
        private const int SignificantExponentGap = 17;

        private BigDouble(double mantissa, long exponent)
        {
            m = mantissa;
            e = exponent;
        }

        // ---------------------------------------------------------------- construction

        /// <summary>Builds a normalized value from an arbitrary mantissa/exponent pair.</summary>
        public static BigDouble Create(double mantissa, long exponent)
        {
            if (mantissa == 0d || double.IsNaN(mantissa) || double.IsInfinity(mantissa))
                return Zero;

            double abs = Math.Abs(mantissa);
            if (abs >= 1d && abs < 10d)
                return new BigDouble(mantissa, exponent);

            int shift = (int)Math.Floor(Math.Log10(abs));
            double normalized = mantissa / Math.Pow(10d, shift);

            // Log10 rounding can land us a hair outside [1,10); nudge back in.
            double normAbs = Math.Abs(normalized);
            if (normAbs >= 10d) { normalized /= 10d; shift++; }
            else if (normAbs < 1d) { normalized *= 10d; shift--; }

            return new BigDouble(normalized, exponent + shift);
        }

        public static BigDouble FromDouble(double value)
        {
            return Create(value, 0L);
        }

        /// <summary>Exact reconstruction from saved fields — no re-normalization cost.</summary>
        public static BigDouble FromComponents(double mantissa, long exponent)
        {
            return Create(mantissa, exponent);
        }

        // ---------------------------------------------------------------- state

        public bool IsZero { get { return m == 0d; } }
        public bool IsNegative { get { return m < 0d; } }
        public bool IsPositive { get { return m > 0d; } }

        /// <summary>
        /// Collapses to a plain double. Returns ±Infinity when the value is too large
        /// for double, so never use this for game state — only for ratios and lerps.
        /// </summary>
        public double ToDouble()
        {
            if (m == 0d) return 0d;
            if (e > 308L) return m > 0d ? double.PositiveInfinity : double.NegativeInfinity;
            if (e < -324L) return 0d;
            return m * Math.Pow(10d, e);
        }

        public BigDouble Abs()
        {
            return m < 0d ? new BigDouble(-m, e) : this;
        }

        /// <summary>log10 of the absolute value. NaN for zero.</summary>
        public double Log10()
        {
            if (m == 0d) return double.NaN;
            return e + Math.Log10(Math.Abs(m));
        }

        // ---------------------------------------------------------------- arithmetic

        public static BigDouble operator -(BigDouble v)
        {
            return v.m == 0d ? Zero : new BigDouble(-v.m, v.e);
        }

        public static BigDouble operator +(BigDouble a, BigDouble b)
        {
            if (a.m == 0d) return b;
            if (b.m == 0d) return a;

            BigDouble larger, smaller;
            if (a.e >= b.e) { larger = a; smaller = b; }
            else { larger = b; smaller = a; }

            long gap = larger.e - smaller.e;
            if (gap > SignificantExponentGap) return larger;

            // Re-express the smaller term in the larger term's exponent, then normalize.
            double combined = larger.m + smaller.m * Math.Pow(10d, -gap);
            return Create(combined, larger.e);
        }

        public static BigDouble operator -(BigDouble a, BigDouble b)
        {
            return a + (-b);
        }

        public static BigDouble operator *(BigDouble a, BigDouble b)
        {
            if (a.m == 0d || b.m == 0d) return Zero;
            return Create(a.m * b.m, a.e + b.e);
        }

        public static BigDouble operator /(BigDouble a, BigDouble b)
        {
            if (b.m == 0d) throw new DivideByZeroException("BigDouble division by zero.");
            if (a.m == 0d) return Zero;
            return Create(a.m / b.m, a.e - b.e);
        }

        /// <summary>
        /// Exponentiation. Idle cost curves are <c>base * growth^level</c>, so this is
        /// on the hot path for the upgrade panel.
        /// </summary>
        public static BigDouble Pow(BigDouble value, double power)
        {
            if (power == 0d) return One;
            if (value.m == 0d) return Zero;
            if (power == 1d) return value;

            bool negativeBase = value.m < 0d;
            bool integerPower = Math.Abs(power % 1d) < 1e-12;

            // A negative base only has a real result for integer powers.
            if (negativeBase && !integerPower) return Zero;

            // Fast path: when both the base and the result fit in a double, go through
            // Math.Pow directly. It is meaningfully more accurate than the log route,
            // and covers the common case of small growth factors like 1.15^level.
            if (value.e > -60L && value.e < 60L && Math.Abs(power) < 5000d)
            {
                double direct = Math.Pow(value.ToDouble(), power);
                if (!double.IsNaN(direct) && !double.IsInfinity(direct) && direct != 0d)
                    return FromDouble(direct);
            }

            double log = Math.Log10(Math.Abs(value.m)) + value.e;
            double scaled = log * power;

            double floor = Math.Floor(scaled);
            if (floor > long.MaxValue || floor < long.MinValue) return Zero;

            long newExponent = (long)floor;
            double newMantissa = Math.Pow(10d, scaled - floor);

            var result = Create(newMantissa, newExponent);

            // (-x)^odd stays negative.
            if (negativeBase && Math.Abs(power % 2d) > 0.5d) result = -result;
            return result;
        }

        public static BigDouble Max(BigDouble a, BigDouble b)
        {
            return a.CompareTo(b) >= 0 ? a : b;
        }

        public static BigDouble Min(BigDouble a, BigDouble b)
        {
            return a.CompareTo(b) <= 0 ? a : b;
        }

        // ---------------------------------------------------------------- comparison

        public int CompareTo(BigDouble other)
        {
            if (m == 0d)
            {
                if (other.m == 0d) return 0;
                return other.m > 0d ? -1 : 1;
            }
            if (other.m == 0d) return m > 0d ? 1 : -1;

            bool aNegative = m < 0d;
            if (aNegative != (other.m < 0d)) return aNegative ? -1 : 1;

            // For negative values a bigger exponent means a smaller number.
            int sign = aNegative ? -1 : 1;
            if (e != other.e) return e > other.e ? sign : -sign;
            return m.CompareTo(other.m);
        }

        public bool Equals(BigDouble other)
        {
            return m == other.m && e == other.e;
        }

        public override bool Equals(object obj)
        {
            return obj is BigDouble && Equals((BigDouble)obj);
        }

        public override int GetHashCode()
        {
            return m.GetHashCode() ^ e.GetHashCode();
        }

        public static bool operator ==(BigDouble a, BigDouble b) { return a.CompareTo(b) == 0; }
        public static bool operator !=(BigDouble a, BigDouble b) { return a.CompareTo(b) != 0; }
        public static bool operator <(BigDouble a, BigDouble b) { return a.CompareTo(b) < 0; }
        public static bool operator >(BigDouble a, BigDouble b) { return a.CompareTo(b) > 0; }
        public static bool operator <=(BigDouble a, BigDouble b) { return a.CompareTo(b) <= 0; }
        public static bool operator >=(BigDouble a, BigDouble b) { return a.CompareTo(b) >= 0; }

        // ---------------------------------------------------------------- conversion

        public static implicit operator BigDouble(double value) { return FromDouble(value); }
        public static implicit operator BigDouble(float value) { return FromDouble(value); }
        public static implicit operator BigDouble(int value) { return FromDouble(value); }
        public static implicit operator BigDouble(long value) { return FromDouble(value); }

        // ---------------------------------------------------------------- text

        /// <summary>
        /// Round-trippable exact form (e.g. "1.5E+12"). This is the debug/serialization
        /// representation — use <see cref="NumberFormatter"/> for anything player-facing.
        /// </summary>
        public override string ToString()
        {
            if (m == 0d) return "0";
            return m.ToString("R", CultureInfo.InvariantCulture) + "E" + e.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string text, out BigDouble result)
        {
            result = Zero;
            if (string.IsNullOrEmpty(text)) return false;

            text = text.Trim();
            int split = text.IndexOfAny(new[] { 'e', 'E' });

            if (split < 0)
            {
                double plain;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out plain)) return false;
                result = FromDouble(plain);
                return true;
            }

            double mantissa;
            long exponent;
            if (!double.TryParse(text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture, out mantissa)) return false;
            if (!long.TryParse(text.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent)) return false;

            result = Create(mantissa, exponent);
            return true;
        }

        public static BigDouble Parse(string text)
        {
            BigDouble result;
            if (!TryParse(text, out result))
                throw new FormatException("Not a valid BigDouble: '" + text + "'");
            return result;
        }
    }
}
