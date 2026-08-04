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
