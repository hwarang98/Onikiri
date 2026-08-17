using System;
using NUnit.Framework;
using Onikiri.Core;

namespace Onikiri.Tests
{
    public class NumberFormatterTests
    {
        [Test]
        public void Zero_FormatsAsPlainZero()
        {
            Assert.AreEqual("0", NumberFormatter.Format(BigDouble.Zero));
        }

        [Test]
        public void BelowThousand_HasNoSuffix()
        {
            Assert.AreEqual("1", NumberFormatter.Format(BigDouble.FromDouble(1d)));
            Assert.AreEqual("42", NumberFormatter.Format(BigDouble.FromDouble(42d)));
            Assert.AreEqual("999", NumberFormatter.Format(BigDouble.FromDouble(999d)));
        }

        // 사양서에 명시된 네 가지 표기
        [Test]
        public void SpecExamples_MatchExactly()
        {
            Assert.AreEqual("1.5K", NumberFormatter.Format(BigDouble.FromDouble(1500d)));
            Assert.AreEqual("3.2M", NumberFormatter.Format(BigDouble.Create(3.2d, 6L)));
            Assert.AreEqual("7.8B", NumberFormatter.Format(BigDouble.Create(7.8d, 9L)));
            Assert.AreEqual("1.2aa", NumberFormatter.Format(BigDouble.Create(1.2d, 15L)));
        }

        [Test]
        public void TrillionTier_UsesT()
        {
            Assert.AreEqual("1.5T", NumberFormatter.Format(BigDouble.Create(1.5d, 12L)));
        }

        [Test]
        public void AlphabeticTiers_AdvanceEveryThreeDigits()
        {
            Assert.AreEqual("1.2aa", NumberFormatter.Format(BigDouble.Create(1.2d, 15L)));
            Assert.AreEqual("1.2ab", NumberFormatter.Format(BigDouble.Create(1.2d, 18L)));
            Assert.AreEqual("1.2ac", NumberFormatter.Format(BigDouble.Create(1.2d, 21L)));
        }

        [Test]
        public void MidTierValues_KeepThreeIntegerDigits()
        {
            Assert.AreEqual("15.0K", NumberFormatter.Format(BigDouble.FromDouble(15000d)));
            Assert.AreEqual("150.0K", NumberFormatter.Format(BigDouble.FromDouble(150000d)));
        }

        [Test]
        public void RoundingCarriesIntoNextTier()
        {
            // 999,999는 1000.0K로 반올림되는데, 1.0M으로 보여야 한다
            Assert.AreEqual("1.0M", NumberFormatter.Format(BigDouble.FromDouble(999999d)));
            // 999.6은 1000으로 반올림되는데, "1000"이 아니라 1.0K가 되어야 한다
            Assert.AreEqual("1.0K", NumberFormatter.Format(BigDouble.FromDouble(999.6d)));
        }

        [Test]
        public void ExactTierBoundary()
        {
            Assert.AreEqual("1.0K", NumberFormatter.Format(BigDouble.FromDouble(1000d)));
            Assert.AreEqual("1.0M", NumberFormatter.Format(BigDouble.Create(1d, 6L)));
        }

        [Test]
        public void NegativeValues_KeepSign()
        {
            Assert.AreEqual("-1.5K", NumberFormatter.Format(BigDouble.FromDouble(-1500d)));
            Assert.AreEqual("-42", NumberFormatter.Format(BigDouble.FromDouble(-42d)));
        }

        [Test]
        public void DecimalCount_IsConfigurable()
        {
            Assert.AreEqual("1.50K", NumberFormatter.Format(BigDouble.FromDouble(1500d), 2));
            Assert.AreEqual("2K", NumberFormatter.Format(BigDouble.FromDouble(1500d), 0));
        }

        /**
         * @brief 강화 버튼이 보여줘야 하는 것은 정확히 1000 미만 구간의 변화다.
         *
         * Format을 쓰면 그 구간이 정수로 뭉개져 버튼이 "1 -> 1"을 보여준다. 화면을
         * 봐야만 알 수 있는 종류의 결함이라 여기서 못 박는다.
         */
        [Test]
        public void FormatStat_KeepsDecimalsBelowThousand()
        {
            Assert.AreEqual("5", NumberFormatter.FormatStat(BigDouble.FromDouble(5d), 2));
            Assert.AreEqual("5.6", NumberFormatter.FormatStat(BigDouble.FromDouble(5.6d), 2));
            Assert.AreEqual("1.15", NumberFormatter.FormatStat(BigDouble.FromDouble(1.15d), 2));
            Assert.AreEqual("1.27", NumberFormatter.FormatStat(BigDouble.FromDouble(1.27d), 2));

            // 같은 값을 Format에 넣으면 무엇이 사라지는지
            Assert.AreEqual("6", NumberFormatter.Format(BigDouble.FromDouble(5.6d), 2));
        }

        [Test]
        public void FormatStat_FallsBackToSuffixesOnceLarge()
        {
            // 후반 공격력은 금방 이 범위로 간다. 거기서는 축약 표기가 맞다
            Assert.AreEqual("1.50K", NumberFormatter.FormatStat(BigDouble.FromDouble(1500d), 2));
            Assert.AreEqual("3.20M", NumberFormatter.FormatStat(BigDouble.Create(3.2d, 6L), 2));

            // 반올림이 1000에 닿는 경계
            Assert.AreEqual("1.00K", NumberFormatter.FormatStat(BigDouble.FromDouble(999.999d), 2));
        }

        [Test]
        public void FormatStat_KeepsSignAndZero()
        {
            Assert.AreEqual("0", NumberFormatter.FormatStat(BigDouble.Zero, 2));
            Assert.AreEqual("-2.5", NumberFormatter.FormatStat(BigDouble.FromDouble(-2.5d), 2));
        }

        [Test]
        public void SuffixForTier_CoversTheWholeAlphabeticRange()
        {
            Assert.AreEqual("K", NumberFormatter.SuffixForTier(1));
            Assert.AreEqual("T", NumberFormatter.SuffixForTier(4));
            Assert.AreEqual("aa", NumberFormatter.SuffixForTier(5));
            Assert.AreEqual("az", NumberFormatter.SuffixForTier(5 + 25));
            Assert.AreEqual("ba", NumberFormatter.SuffixForTier(5 + 26));
            Assert.AreEqual("zz", NumberFormatter.SuffixForTier(5 + 26 * 26 - 1));
            // "zz"를 넘으면 접미사가 없고 호출부가 지수 표기로 폴백한다
            Assert.IsNull(NumberFormatter.SuffixForTier(5 + 26 * 26));
        }

        [Test]
        public void AstronomicalValues_FallBackToScientific()
        {
            // "zz"(약 10^2042)를 한참 넘어선 값
            string formatted = NumberFormatter.Format(BigDouble.Create(1.2d, 5000L));
            Assert.IsTrue(formatted.Contains("e5000"), "Expected scientific fallback, got " + formatted);
        }

        // ------------------------------------------------------------ 전체 자릿수 표기

        [Test]
        public void FormatFull_ShowsEveryDigitWithSeparators()
        {
            Assert.AreEqual("1,234", NumberFormatter.FormatFull(BigDouble.FromDouble(1234d)));
            Assert.AreEqual("291,512,345", NumberFormatter.FormatFull(BigDouble.FromDouble(291512345d)));
        }

        [Test]
        public void FormatFull_BelowThousand_HasNoSeparator()
        {
            Assert.AreEqual("0", NumberFormatter.FormatFull(BigDouble.Zero));
            Assert.AreEqual("5", NumberFormatter.FormatFull(BigDouble.FromDouble(5d)));
            Assert.AreEqual("999", NumberFormatter.FormatFull(BigDouble.FromDouble(999d)));
        }

        /**
         * 축약과 갈라지는 지점. 같은 값이 재화 표기로는 "1.2K", 데미지 팝업으로는
         * "1,234"로 나와야 한다 - 이것이 이번 변경의 전부다
         */
        [Test]
        public void FormatFull_DivergesFromAbbreviated()
        {
            var value = BigDouble.FromDouble(1234d);
            Assert.AreEqual("1.2K", NumberFormatter.Format(value));
            Assert.AreEqual("1,234", NumberFormatter.FormatFull(value));
        }

        /**
         * @brief 자릿수가 아무리 늘어도 **축약으로 도망가지 않는다.**
         *
         * 한때 12자리에서 멈추고 그 위는 축약으로 되돌렸다. 그 폴백이 실제로 한
         * 일은 후반에 이 기능을 통째로 끄는 것이었다 - 강화가 쌓이면 타격은 금방
         * 10^16을 넘고, 그때부터는 전체 표기를 켠 적이 없는 것과 같아진다.
         *
         * 사용자 지시로 상한을 없앴다. 폭은 폰트로 푼다(ThaleahScale 3 -> 2).
         */
        [Test]
        public void FormatFull_NeverFallsBackToAbbreviated()
        {
            Assert.AreEqual("999,999,999,999",
                            NumberFormatter.FormatFull(BigDouble.FromDouble(999999999999d)));

            // 옛 상한 바로 위. 축약이 아니라 전체 자릿수여야 한다
            Assert.AreEqual("1,000,000,000,000", NumberFormatter.FormatFull(BigDouble.Create(1d, 12L)));
            Assert.AreEqual("1,200,000,000,000,000", NumberFormatter.FormatFull(BigDouble.Create(1.2d, 15L)));

            // 실제로 화면에서 축약이 보이던 자리 (공격력 Lv.2203 무렵)
            Assert.AreEqual("26,400,000,000,000,000",
                            NumberFormatter.FormatFull(BigDouble.Create(2.64d, 16L)));
        }

        /**
         * @brief double 범위(10^308) 밖에서도 자릿수를 짓는다.
         *
         * `ToDouble()`은 그 위에서 무한대가 되므로 가수부 유효자리 + 0으로 만든다.
         * BigDouble은 10^2042까지 가므로 언젠가 닿는 구간이다.
         */
        [Test]
        public void FormatFull_WorksBeyondDoubleRange()
        {
            var huge = NumberFormatter.FormatFull(BigDouble.Create(1d, 400L));

            // 1 뒤에 0이 400개 = 401자리. 401 % 3 = 2라 맨 앞 묶음이 두 자리("10")다
            Assert.AreEqual(401 + 133, huge.Length, "자릿수 401 + 쉼표 133이어야 한다");
            Assert.IsTrue(huge.StartsWith("10,000,"), "맨 앞 묶음이 두 자리여야 한다: " + huge.Substring(0, 8));
            Assert.IsFalse(huge.Contains("E"), "지수 표기로 새면 안 된다");
            Assert.IsFalse(huge.Contains("∞"), "무한대로 새면 안 된다");
        }

        /** 유효자리 아래는 0이다. double이 원래 들고 있지 않은 정보다 */
        [Test]
        public void FormatFull_PadsBeyondSignificantDigitsWithZeroes()
        {
            // 1.2345678901234567 x 10^20 -> 앞 15자리만 진짜(15번째에서 반올림),
            // 나머지 6자리는 0. double이 원래 들고 있지 않은 자리다
            var text = NumberFormatter.FormatFull(BigDouble.Create(1.2345678901234567d, 20L));

            Assert.AreEqual("123,456,789,012,346,000,000", text);
        }

        [Test]
        public void FormatFull_NegativeKeepsSign()
        {
            Assert.AreEqual("-1,234", NumberFormatter.FormatFull(BigDouble.FromDouble(-1234d)));
        }

        [Test]
        public void Duration_FormatsCompactly()
        {
            Assert.AreEqual("45s", NumberFormatter.FormatDuration(TimeSpan.FromSeconds(45)));
            Assert.AreEqual("5m", NumberFormatter.FormatDuration(TimeSpan.FromMinutes(5)));
            Assert.AreEqual("5m 30s", NumberFormatter.FormatDuration(TimeSpan.FromSeconds(330)));
            Assert.AreEqual("3h", NumberFormatter.FormatDuration(TimeSpan.FromHours(3)));
            Assert.AreEqual("3h 12m", NumberFormatter.FormatDuration(TimeSpan.FromMinutes(192)));
            Assert.AreEqual("0s", NumberFormatter.FormatDuration(TimeSpan.Zero));
        }
    }
}
