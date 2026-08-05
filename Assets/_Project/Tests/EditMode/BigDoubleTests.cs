using System;
using NUnit.Framework;
using Onikiri.Core;

namespace Onikiri.Tests
{
    public class BigDoubleTests
    {
        const double Tolerance = 1e-9;

        [Test]
        public void Create_NormalizesMantissaIntoRange()
        {
            var v = BigDouble.Create(12345d, 0L);
            Assert.AreEqual(4L, v.Exponent);
            Assert.AreEqual(1.2345d, v.Mantissa, Tolerance);
        }

        [Test]
        public void Create_NormalizesSmallMantissa()
        {
            var v = BigDouble.Create(0.00042d, 0L);
            Assert.AreEqual(-4L, v.Exponent);
            Assert.AreEqual(4.2d, v.Mantissa, 1e-12);
        }

        [Test]
        public void Zero_StaysZeroThroughArithmetic()
        {
            Assert.IsTrue(BigDouble.Zero.IsZero);
            Assert.IsTrue((BigDouble.Zero * BigDouble.FromDouble(1e100)).IsZero);
            Assert.IsTrue((BigDouble.FromDouble(5d) - BigDouble.FromDouble(5d)).IsZero);
        }

        [Test]
        public void Add_SameMagnitude()
        {
            var sum = BigDouble.Create(1d, 100L) + BigDouble.Create(1d, 100L);
            Assert.AreEqual(100L, sum.Exponent);
            Assert.AreEqual(2d, sum.Mantissa, Tolerance);
        }

        [Test]
        public void Add_TinyTermIsAbsorbedByHugeTerm()
        {
            // 이 타입의 존재 이유 그 자체다. 1e100 골드에 1 골드를 더해도 큰 값이
            // 손상되지 않아야 하고 정밀도도 잃지 않아야 한다
            var big = BigDouble.Create(1d, 100L);
            var sum = big + BigDouble.One;
            Assert.AreEqual(100L, sum.Exponent);
            Assert.AreEqual(1d, sum.Mantissa, Tolerance);
        }

        [Test]
        public void Add_CarriesIntoNextExponent()
        {
            var sum = BigDouble.Create(9.5d, 10L) + BigDouble.Create(9.5d, 10L);
            Assert.AreEqual(11L, sum.Exponent);
            Assert.AreEqual(1.9d, sum.Mantissa, 1e-12);
        }

        [Test]
        public void Subtract_ProducesNegative()
        {
            var result = BigDouble.FromDouble(3d) - BigDouble.FromDouble(10d);
            Assert.IsTrue(result.IsNegative);
            Assert.AreEqual(-7d, result.ToDouble(), 1e-9);
        }

        [Test]
        public void Multiply_AddsExponents()
        {
            var product = BigDouble.Create(1.5d, 10L) * BigDouble.Create(2d, 5L);
            Assert.AreEqual(15L, product.Exponent);
            Assert.AreEqual(3d, product.Mantissa, Tolerance);
        }

        [Test]
        public void Divide_SubtractsExponents()
        {
            var quotient = BigDouble.Create(6d, 20L) / BigDouble.Create(2d, 5L);
            Assert.AreEqual(15L, quotient.Exponent);
            Assert.AreEqual(3d, quotient.Mantissa, Tolerance);
        }

        [Test]
        public void Divide_ByZeroThrows()
        {
            Assert.Throws<DivideByZeroException>(() =>
            {
                var _ = BigDouble.One / BigDouble.Zero;
            });
        }

        [Test]
        public void Compare_OrdersByMagnitudeThenMantissa()
        {
            Assert.IsTrue(BigDouble.Create(1d, 50L) > BigDouble.Create(9d, 49L));
            Assert.IsTrue(BigDouble.Create(2d, 10L) > BigDouble.Create(1d, 10L));
            Assert.IsTrue(BigDouble.FromDouble(-5d) < BigDouble.Zero);
            Assert.IsTrue(BigDouble.Zero < BigDouble.One);
        }

        [Test]
        public void Compare_NegativesOrderCorrectly()
        {
            // 지수부가 더 큰데도 -1e50이 -1e10보다 작다
            Assert.IsTrue(BigDouble.Create(-1d, 50L) < BigDouble.Create(-1d, 10L));
            Assert.IsTrue(BigDouble.FromDouble(-2d) < BigDouble.FromDouble(-1d));
        }

        [Test]
        public void Pow_SmallIntegerPowerIsExact()
        {
            var result = BigDouble.Pow(BigDouble.FromDouble(2d), 10d);
            Assert.AreEqual(1024d, result.ToDouble(), 1e-6);
        }

        [Test]
        public void Pow_IdleCostCurveStaysAccurate()
        {
            // 방치형의 표준 업그레이드 곡선. cost = base * 1.15^level
            var result = BigDouble.Pow(BigDouble.FromDouble(1.15d), 100d);
            Assert.AreEqual(1174313.45d, result.ToDouble(), 1d);
        }

        [Test]
        public void Pow_ExceedsDoubleRange()
        {
            // 10^400은 double 범위를 넘지만 여기서는 표현할 수 있어야 한다
            var result = BigDouble.Pow(BigDouble.FromDouble(10d), 400d);
            Assert.AreEqual(400L, result.Exponent);
            Assert.AreEqual(1d, result.Mantissa, 1e-6);
        }

        [Test]
        public void Pow_ZeroExponentIsOne()
        {
            Assert.AreEqual(0L, BigDouble.Pow(BigDouble.Create(5d, 99L), 0d).Exponent);
            Assert.AreEqual(1d, BigDouble.Pow(BigDouble.Create(5d, 99L), 0d).Mantissa, Tolerance);
        }

        [Test]
        public void Log10_MatchesMagnitude()
        {
            Assert.AreEqual(100d, BigDouble.Create(1d, 100L).Log10(), 1e-9);
            Assert.AreEqual(50.3010299957d, BigDouble.Create(2d, 50L).Log10(), 1e-9);
        }

        [Test]
        public void ToDouble_SaturatesInsteadOfWrapping()
        {
            Assert.IsTrue(double.IsPositiveInfinity(BigDouble.Create(1d, 400L).ToDouble()));
            Assert.IsTrue(double.IsNegativeInfinity(BigDouble.Create(-1d, 400L).ToDouble()));
            Assert.AreEqual(0d, BigDouble.Create(1d, -400L).ToDouble());
        }

        [Test]
        public void MaxMin_PickCorrectValue()
        {
            var small = BigDouble.Create(1d, 10L);
            var large = BigDouble.Create(1d, 200L);
            Assert.AreEqual(large, BigDouble.Max(small, large));
            Assert.AreEqual(small, BigDouble.Min(small, large));
        }

        [Test]
        public void Parse_RoundTripsThroughToString()
        {
            var original = BigDouble.Create(1.2345d, 1234L);
            BigDouble parsed;
            Assert.IsTrue(BigDouble.TryParse(original.ToString(), out parsed));
            Assert.AreEqual(original.Exponent, parsed.Exponent);
            Assert.AreEqual(original.Mantissa, parsed.Mantissa, 1e-12);
        }

        [Test]
        public void Parse_HandlesPlainNumbers()
        {
            BigDouble parsed;
            Assert.IsTrue(BigDouble.TryParse("1500", out parsed));
            Assert.AreEqual(3L, parsed.Exponent);
            Assert.AreEqual(1.5d, parsed.Mantissa, 1e-12);
        }

        [Test]
        public void Parse_RejectsGarbage()
        {
            BigDouble parsed;
            Assert.IsFalse(BigDouble.TryParse("not a number", out parsed));
            Assert.IsFalse(BigDouble.TryParse("", out parsed));
        }

        [Test]
        public void ImplicitConversions_Work()
        {
            BigDouble fromInt = 1500;
            BigDouble fromLong = 1500L;
            BigDouble fromDouble = 1500d;
            Assert.AreEqual(3L, fromInt.Exponent);
            Assert.AreEqual(fromInt, fromLong);
            Assert.AreEqual(fromInt, fromDouble);
        }

        [Test]
        public void AccumulatingGold_StaysStableOverManyAdditions()
        {
            // 실제 사용 패턴 재현. 합계에 작은 값을 아주 많이 더한다
            var total = BigDouble.Zero;
            var perKill = BigDouble.FromDouble(12.5d);
            for (int i = 0; i < 10000; i++) total += perKill;
            Assert.AreEqual(125000d, total.ToDouble(), 1e-3);
        }

        [Serializable]
        private class SaveProbe
        {
            public BigDouble gold;
            public int upgradeLevel;
        }

        [Test]
        public void JsonUtility_RoundTripsAsSaveData()
        {
            // MVP 세이브는 PlayerPrefs + JsonUtility라, BigDouble이 Unity 직렬화를
            // 왕복하고도 크기를 그대로 유지해야 한다
            var original = new SaveProbe
            {
                gold = BigDouble.Create(1.2345d, 987L),
                upgradeLevel = 42
            };

            string json = UnityEngine.JsonUtility.ToJson(original);
            var restored = UnityEngine.JsonUtility.FromJson<SaveProbe>(json);

            Assert.AreEqual(987L, restored.gold.Exponent);
            Assert.AreEqual(1.2345d, restored.gold.Mantissa, 1e-12);
            Assert.AreEqual(42, restored.upgradeLevel);
            Assert.AreEqual(original.gold, restored.gold);
        }
    }
}
