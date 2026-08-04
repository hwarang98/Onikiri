using System;
using System.Globalization;

namespace Onikiri.Core
{
    /**
     * @brief BigDouble 값을 플레이어가 보는 축약 표기로 바꾼다.
     *
     * HUD와 업그레이드 패널 전반에서 쓰는 1.5K, 3.2M, 7.8B, 1.2aa 형식이다.
     *
     * 티어는 10진수 3자리마다 올라간다. 처음 넷은 관용 표기(K/M/B/T)를 쓰고,
     * 10^15부터는 두 글자 태그(aa, ab, ... zz)로 넘어간다. 방치형의 표준 관례이며
     * 약 10^2042까지 읽을 수 있다.
     */
    public static class NumberFormatter
    {
        private const int DefaultDecimals = 1;

        /** 티어 1~4. 티어 0은 접미사 없는 1000 미만 구간 */
        private static readonly string[] ShortSuffixes = { "", "K", "M", "B", "T" };

        /** 알파벳 표기가 시작되는 첫 티어 (10^15) */
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

            // 1000 미만은 접미사 없이 정수로 읽힌다
            if (abs.Exponent < 3)
            {
                double plain = Math.Round(abs.ToDouble());
                if (plain < 1000d)
                    return sign + plain.ToString("F0", CultureInfo.InvariantCulture);
                // 반올림으로 정확히 1000이 된 경우. 아래로 흘려보내 "1.0K"가 되게 한다
                abs = BigDouble.FromDouble(plain);
            }

            long tier = abs.Exponent / 3;
            int withinTier = (int)(abs.Exponent - tier * 3);

            // 표시용으로 가수부를 [1, 1000) 범위에 맞춘다
            double display = abs.Mantissa * Math.Pow(10d, withinTier);
            display = Math.Round(display, decimals);

            // 반올림이 다음 티어로 올라갈 수 있다 (999.95 -> 1000.0 -> 다음 티어의 1.0)
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

        /**
         * @brief 10^(3*tier) 크기에 해당하는 접미사.
         *
         * 두 글자 표기 범위를 벗어나면 null을 반환한다. 호출부는 지수 표기로 폴백한다.
         */
        public static string SuffixForTier(long tier)
        {
            if (tier < 0) return null;
            if (tier < ShortSuffixes.Length) return ShortSuffixes[tier];

            long index = tier - AlphabeticTierStart;
            long first = index / LettersInAlphabet;
            long second = index % LettersInAlphabet;

            if (first >= LettersInAlphabet) return null; // "zz" 초과

            return string.Concat(
                ((char)('a' + first)).ToString(),
                ((char)('a' + second)).ToString());
        }

        /** 명명된 티어를 넘어선 크기의 폴백 표기. 예: "1.23e2100" */
        public static string FormatScientific(BigDouble value, int decimals)
        {
            if (value.IsZero) return "0";
            return value.Mantissa.ToString("F" + decimals, CultureInfo.InvariantCulture)
                   + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
        }

        /**
         * @brief 오프라인 보상 팝업이 쓰는 축약 시간 표기 ("3h 12m", "45s").
         *
         * 플레이어가 보는 숫자 텍스트를 한곳에 모으기 위해 여기에 둔다.
         */
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
