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
         * ## 자릿수 상한은 **없다** (사용자 지시)
         *
         * 한때 12자리에서 멈추고 그 위는 축약으로 되돌렸다. 화면 폭 때문이었는데,
         * 그 폴백이 실제로 한 일은 **후반에 이 기능을 통째로 끄는 것**이었다 -
         * 강화가 쌓이면 타격은 금방 10^16을 넘고, 그때부터는 전체 표기를 켠 적이
         * 없는 것과 같아진다. 정확히 그 지점에서 "왜 축약되냐"가 나왔다.
         *
         * 그래서 상한을 없앴다. 폭은 폰트로 푼다 - 데미지 아틀라스를 48에서 32로
         * 다시 구웠다(PixelFontSizes.ThaleahScale). 자릿수가 더 늘면 그때도
         * 폰트나 배수로 풀 일이지, 숫자를 감춰서 풀 일이 아니다.
         *
         * ⚠️ 자릿수는 이제 값이 정하므로 **문자열 길이에 상한이 없다.** 10^100이면
         * 133글자다(숫자 101 + 쉼표 33 - 1). 지금 곡선에서 닿을 자리는 아니지만,
         * 닿으면 화면을 가로지른다.
         *
         * ## double 범위 밖도 찍는다
         *
         * `ToDouble()`은 10^308에서 무한대가 된다. BigDouble은 그보다 훨씬 위까지
         * 가므로(10^2042) 큰 값은 double을 거치지 않고 **가수부 유효자리 + 0**으로
         * 자릿수를 짓는다. 어차피 double의 유효자리가 15~17개라, 그 아래는 원래
         * 정보가 없는 자리다.
         */
        public static string FormatFull(BigDouble value)
        {
            if (value.IsZero) return "0";

            string sign = value.IsNegative ? "-" : string.Empty;
            BigDouble abs = value.Abs();

            // double이 정확히 표현하는 구간은 그대로 반올림해 찍는다.
            // 유효자리 15개 안이라 마지막 자리까지 진짜 값이다
            if (abs.Exponent < SignificantDigits)
            {
                double plain = Math.Round(abs.ToDouble());
                return sign + plain.ToString("N0", CultureInfo.InvariantCulture);
            }

            return sign + GroupDigits(DigitsOf(abs));
        }

        /** double이 믿을 수 있는 유효자리. 그 아래는 0으로 채운다 */
        private const int SignificantDigits = 15;

        /**
         * @brief 정규화된 값(1 <= 가수부 < 10)을 쉼표 없는 자릿수 문자열로.
         *
         * 값은 가수부 x 10^지수이므로 정수 자릿수는 지수+1개다. 앞의 15자리는
         * 가수부에서 나오고 나머지는 0이다.
         */
        private static string DigitsOf(BigDouble abs)
        {
            long totalDigits = abs.Exponent + 1L;

            // 가수부를 15자리 정수로 편다. 1.2345... -> 123450000000000
            double scaled = Math.Round(abs.Mantissa * Math.Pow(10d, SignificantDigits - 1));
            string head = scaled.ToString("F0", CultureInfo.InvariantCulture);

            // 9.999...가 10.0으로 올라가면 자리가 하나 늘어난다
            if (head.Length > SignificantDigits) totalDigits++;

            if (head.Length >= totalDigits) return head.Substring(0, (int)totalDigits);
            return head.PadRight((int)totalDigits, '0');
        }

        /** 뒤에서 세 자리마다 쉼표. "1234567" -> "1,234,567" */
        private static string GroupDigits(string digits)
        {
            int commas = (digits.Length - 1) / 3;
            if (commas <= 0) return digits;

            var chars = new char[digits.Length + commas];
            int write = chars.Length - 1;
            int run = 0;

            for (int read = digits.Length - 1; read >= 0; read--)
            {
                if (run == 3) { chars[write--] = ','; run = 0; }
                chars[write--] = digits[read];
                run++;
            }
            return new string(chars);
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
