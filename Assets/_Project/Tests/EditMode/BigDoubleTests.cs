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
            // This is the whole point of the type: adding 1 gold to 1e100 gold must not
            // corrupt the large value, and must not cost precision.
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
            // -1e50 is smaller than -1e10, despite the larger exponent.
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
            // The canonical idle upgrade curve: cost = base * 1.15^level.
            var result = BigDouble.Pow(BigDouble.FromDouble(1.15d), 100d);
            Assert.AreEqual(1174313.45d, result.ToDouble(), 1d);
        }

        [Test]
        public void Pow_ExceedsDoubleRange()
        {
            // 10^400 overflows a double but must be representable here.
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
            // Simulates the real usage pattern: lots of small increments onto a total.
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
            // MVP saves are PlayerPrefs + JsonUtility, so BigDouble has to survive a
            // trip through Unity's serializer with its magnitude intact.
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
