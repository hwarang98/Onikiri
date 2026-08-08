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
         * @brief 전체 자릿수 + 천 단위 쉼표. 떠오르는 데미지 숫자가 쓴다.
         *
         * 축약 표기(Format)와 나눠 둔 이유는 **읽는 목적이 다르기 때문이다.**
         *
         * 보유 골드나 강화 비용은 "지금 얼마나 있는가"를 재는 값이라 자릿수를 다 보여줘봐야
         * 읽히지 않는다. 54,283,119,006보다 54.2B이 낫다 - 어차피 크기만 알면 되고,
         * 상단 바에서 매 프레임 자릿수가 흔들리면 그 자체가 잡음이다.
         *
         * 타격 순간 떠오르는 숫자는 반대다. 이것은 잔고가 아니라 **방금 일어난 사건의
         * 크기**이고, 플레이어가 강화를 누르는 근거가 정확히 그 변화다. 291.5M은 이번
         * 타격이 291,461,230인지 291,549,880인지 말해주지 않고, 강화를 세 번 눌러도
         * 같은 "291.5M"이 뜰 수 있다. 축약이 숨기는 것이 하필 사람이 보려던 것이다.
         *
         * ## 12자리에서 멈추는 이유
         *
         * 전체 표기는 자릿수가 곧 화면 폭이다. 팝업은 정수배 크기만 쓸 수 있고(비트맵
         * 폰트) 치명타는 그 두 배라, 자릿수가 늘면 다른 수단으로 줄일 방법이 없다.
         * 12자리 + 쉼표 3개 = 15글자가 치명타 크기로 1080 폭에 들어가는 한계다.
         *
         * 그 위는 애초에 읽는 숫자가 아니기도 하다. 1,284,003,551,209는 세어야 크기를
         * 알 수 있고, 그 시점에는 1.28T가 더 정직하다. 상한을 넘으면 조용히 축약으로
         * 돌아간다 - 넘는 순간이 후반이라 눈에 띄는 전환도 아니다.
         */
        public const int FullDigitLimit = 12;

        public static string FormatFull(BigDouble value)
        {
            if (value.IsZero) return "0";

            // Exponent는 10의 거듭제곱 자리수라, 12면 이미 13자리다
            if (value.Abs().Exponent >= FullDigitLimit) return Format(value);

            double plain = Math.Round(value.ToDouble());

            // 반올림이 상한을 넘길 수 있다 (999,999,999,999.7 -> 1e12)
            if (Math.Abs(plain) >= 1e12) return Format(value);

            return plain.ToString("N0", CultureInfo.InvariantCulture);
        }

        /**
         * @brief 강화 패널이 쓰는 스탯 표기. 1000 미만에서도 소수를 지킨다.
         *
         * Format은 1000 미만을 정수로 읽는다. 골드에는 그것이 맞다 - 404.8골드라는
         * 것은 없다. 스탯에는 그 규칙이 치명적이다. 강화 버튼이 보여줘야 하는 것은
         * 정확히 이 구간의 변화인데, 공격력 5 -> 5.6이 "5 -> 6"으로, 공격속도
         * 1.15 -> 1.27이 "1 -> 1"로 뭉개진다. 뒤엣것은 버튼이 무엇을 파는지
         * 아무것도 말해주지 않는다.
         *
         * 의미 없는 0은 떼어낸다. 좁은 폰 화면에서는 "5.00 -> 5.60"보다
         * "5 -> 5.6"이 읽기 쉽다.
         */
        public static string FormatStat(BigDouble value, int decimals)
        {
            if (value.IsZero) return "0";
            if (decimals < 0) decimals = 0;

            // 1000 이상은 축약 표기가 그대로 맞다. 후반 공격력은 금방 그 범위로 간다
            if (value.Abs().Exponent >= 3) return Format(value, decimals);

            double rounded = Math.Round(value.ToDouble(), decimals);

            // 반올림으로 1000에 닿으면 축약 표기로 넘긴다
            if (Math.Abs(rounded) >= 1000d) return Format(BigDouble.FromDouble(rounded), decimals);

            return TrimTrailingZeros(rounded.ToString("F" + decimals, CultureInfo.InvariantCulture));
        }

        private static string TrimTrailingZeros(string text)
        {
            if (text.IndexOf('.') < 0) return text;
            return text.TrimEnd('0').TrimEnd('.');
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
         * @brief 화면에 띄우는 한글 시간 표기 ("3시간 12분", "45초").
         *
         * 축약형(FormatDuration)과 갈라둔 이유는 읽는 상대가 다르기 때문이다.
         * "8h"는 콘솔 로그에서 짧아서 좋지만, 방치 보상 팝업은 **몇 시간을
         * 비웠는지가 보상의 근거**라 한눈에 읽혀야 한다. 영문 축약은 그 한 줄에서
         * 한 번 더 해석을 요구한다.
         *
         * 0초는 "0초"다. 팝업이 그 값으로 뜰 일은 없지만(경과가 0이면 보상 자체가
         * 없다) 빈 문자열을 내면 라벨이 사라져 레이아웃이 흔들린다.
         */
        public static string FormatDurationKo(TimeSpan span)
        {
            if (span.TotalSeconds < 1d) return "0초";

            if (span.TotalHours >= 1d)
            {
                int hours = (int)span.TotalHours;
                int minutes = span.Minutes;
                return minutes > 0
                    ? hours + "시간 " + minutes + "분"
                    : hours + "시간";
            }

            if (span.TotalMinutes >= 1d)
            {
                int minutes = (int)span.TotalMinutes;
                int seconds = span.Seconds;
                return seconds > 0
                    ? minutes + "분 " + seconds + "초"
                    : minutes + "분";
            }

            return (int)span.TotalSeconds + "초";
        }

        /**
         * @brief 로그가 쓰는 축약 시간 표기 ("3h 12m", "45s").
         *
         * 화면에는 FormatDurationKo를 쓴다.
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
