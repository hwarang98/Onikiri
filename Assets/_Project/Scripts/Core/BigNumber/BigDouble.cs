using System;
using System.Globalization;
using UnityEngine;

namespace Onikiri.Core
{
    /**
     * @brief 가수부 * 10^지수부 형태로 저장하는 임의 크기 숫자.
     *
     * 방치형은 플레이 몇 시간이면 long 범위를 넘어간다. 그래서 ONIKIRI의 모든 재화,
     * 데미지, 비용 값은 long/double 대신 이 타입을 쓴다.
     *
     * 가수부는 항상 1 <= |가수부| < 10 으로 정규화되고(또는 정확히 0), 지수부는 long이라
     * 표현 범위가 대략 10^±9.2e18 이다. 게임이 도달할 수 있는 어떤 값보다도 크다.
     * 정밀도는 크기와 무관하게 double의 유효자리 15자리 수준을 유지하는데, 방치형이
     * 원하는 것이 정확히 이것이다. "1.53 자" 가 중요하지 마지막 자리는 중요하지 않다.
     *
     * Unity 직렬화가 되므로 JsonUtility 세이브 데이터에 그대로 들어간다.
     */
    [Serializable]
    public struct BigDouble : IComparable<BigDouble>, IEquatable<BigDouble>
    {
        [SerializeField] private double m;
        [SerializeField] private long e;

        /** 1 <= |가수부| < 10 으로 정규화된 값. 또는 0 */
        public double Mantissa { get { return m; } }
        public long Exponent { get { return e; } }

        public static readonly BigDouble Zero = new BigDouble(0d, 0L);
        public static readonly BigDouble One = new BigDouble(1d, 0L);

        /**
         * @brief 이보다 크기 차이가 나는 두 값을 더해도 큰 쪽은 바뀌지 않는다.
         *
         * double의 유효자리가 15~17자리라 작은 항이 끝에서 잘려 나가기 때문이다.
         */
        private const int SignificantExponentGap = 17;

        private BigDouble(double mantissa, long exponent)
        {
            m = mantissa;
            e = exponent;
        }

        // ---------------------------------------------------------------- 생성

        /** 임의의 가수부/지수부 쌍에서 정규화된 값을 만든다 */
        public static BigDouble Create(double mantissa, long exponent)
        {
            if (mantissa == 0d || double.IsNaN(mantissa) || double.IsInfinity(mantissa))
                return Zero;

            double abs = Math.Abs(mantissa);
            if (abs >= 1d && abs < 10d)
                return new BigDouble(mantissa, exponent);

            int shift = (int)Math.Floor(Math.Log10(abs));
            double normalized = mantissa / Math.Pow(10d, shift);

            // Log10 반올림으로 [1,10) 밖으로 살짝 벗어날 수 있다. 다시 안으로 밀어 넣는다
            double normAbs = Math.Abs(normalized);
            if (normAbs >= 10d) { normalized /= 10d; shift++; }
            else if (normAbs < 1d) { normalized *= 10d; shift--; }

            return new BigDouble(normalized, exponent + shift);
        }

        public static BigDouble FromDouble(double value)
        {
            return Create(value, 0L);
        }

        /** 저장된 필드로부터의 정확한 복원. 재정규화 비용이 없다 */
        public static BigDouble FromComponents(double mantissa, long exponent)
        {
            return Create(mantissa, exponent);
        }

        // ---------------------------------------------------------------- 상태

        public bool IsZero { get { return m == 0d; } }
        public bool IsNegative { get { return m < 0d; } }
        public bool IsPositive { get { return m > 0d; } }

        /**
         * @brief 평범한 double로 축소한다.
         *
         * double로 담기에 너무 크면 ±Infinity를 반환한다. 게임 상태에는 절대 쓰지 말 것.
         * 비율 계산과 보간에만 쓴다.
         */
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

        /** 절대값의 log10. 0이면 NaN */
        public double Log10()
        {
            if (m == 0d) return double.NaN;
            return e + Math.Log10(Math.Abs(m));
        }

        // ---------------------------------------------------------------- 사칙연산

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

            // 작은 항을 큰 항의 지수부 기준으로 다시 표현한 뒤 정규화한다
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

        /**
         * @brief 거듭제곱.
         *
         * 방치형 비용 곡선이 base * growth^level 형태라, 업그레이드 패널의 핫 패스다.
         */
        public static BigDouble Pow(BigDouble value, double power)
        {
            if (power == 0d) return One;
            if (value.m == 0d) return Zero;
            if (power == 1d) return value;

            bool negativeBase = value.m < 0d;
            bool integerPower = Math.Abs(power % 1d) < 1e-12;

            // 음수 밑은 정수 지수에서만 실수 결과를 갖는다
            if (negativeBase && !integerPower) return Zero;

            // 빠른 경로. 밑과 결과가 모두 double에 담기면 Math.Pow로 바로 간다.
            // log 경로보다 의미 있게 정확하고, 1.15^level 같은 작은 성장 계수라는
            // 가장 흔한 경우를 커버한다
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

            // (-x)의 홀수 제곱은 음수로 남는다
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

        // ---------------------------------------------------------------- 비교

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

            // 음수에서는 지수부가 클수록 더 작은 수다
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

        // ---------------------------------------------------------------- 변환

        public static implicit operator BigDouble(double value) { return FromDouble(value); }
        public static implicit operator BigDouble(float value) { return FromDouble(value); }
        public static implicit operator BigDouble(int value) { return FromDouble(value); }
        public static implicit operator BigDouble(long value) { return FromDouble(value); }

        // ---------------------------------------------------------------- 문자열

        /**
         * @brief 왕복 가능한 정확한 표기 (예: "1.5E+12").
         *
         * 디버그/직렬화용이다. 플레이어에게 보이는 것은 NumberFormatter를 쓸 것.
         */
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
