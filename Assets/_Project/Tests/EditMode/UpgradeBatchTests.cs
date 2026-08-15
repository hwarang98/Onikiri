using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 배수 구매 (#9). 지키는 것은 하나다 - **총액이 같다.**
     *
     * 배수 버튼은 편의 기능이다. 백 번 두드리는 대신 한 번 두드리는 것이고,
     * 그 대가로 싸지는 것은 아무것도 없어야 한다. 방치형에서 구매 편의가
     * 조용히 할인이 되면 곡선 전체가 한 칸씩 밀리고(강화가 예상보다 빨리
     * 쌓인다), 그 어긋남은 몇 시간 방치한 뒤 스테이지 진행 속도로만 드러난다.
     *
     * 그래서 여기서 못 박는다: ×N으로 산 결과는 ×1을 N번 누른 결과와
     * 레벨도 잔액도 정확히 같다.
     */
    public class UpgradeBatchTests
    {
        private static UpgradeTrack AttackPower()
        {
            return new UpgradeTrack(UpgradeSystem.AttackPowerId, "공격력 강화",
                                    BigDouble.FromDouble(10d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(5d), 1.12d);
        }

        /** 상한이 있는 축. 배수가 상한을 넘지 않는지 재는 데 쓴다 */
        private static UpgradeTrack Capped(int maxLevel)
        {
            return new UpgradeTrack(UpgradeSystem.AttackSpeedId, "공격속도 강화",
                                    BigDouble.FromDouble(4d), 1.15d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(1.15d), 1.04d,
                                    maxLevel);
        }

        private static PlayerWallet WalletWith(double gold)
        {
            var go = new GameObject("~TestWallet");
            var wallet = go.AddComponent<PlayerWallet>();
            wallet.SetBalance(BigDouble.FromDouble(gold), BigDouble.FromDouble(gold));
            return wallet;
        }

        /** 이 배치의 계약. 다른 검사들이 전부 이것의 따름정리다 */
        [Test]
        public void BuyingTenAtOnce_CostsExactlyTheSameAsTenSingles()
        {
            const double Start = 1e6d;

            var batch = AttackPower();
            var batchWallet = WalletWith(Start);
            int bought = batch.TryPurchaseMany(batchWallet, 10);

            var singles = AttackPower();
            var singleWallet = WalletWith(Start);
            for (int i = 0; i < 10; i++) singles.TryPurchase(singleWallet);

            Assert.AreEqual(10, bought, "열 칸을 못 샀다");
            Assert.AreEqual(singles.Level, batch.Level, "레벨이 갈렸다");
            Assert.AreEqual(singleWallet.Gold.ToDouble(), batchWallet.Gold.ToDouble(),
                singleWallet.Gold.ToDouble() * 1e-9d,
                "총액이 갈렸다 - 배수가 할인이 됐다");

            Object.DestroyImmediate(batchWallet.gameObject);
            Object.DestroyImmediate(singleWallet.gameObject);
        }

        /** 미리 보여준 가격과 실제로 나가는 골드가 같은가 */
        [Test]
        public void QuotedPrice_MatchesWhatIsActuallySpent()
        {
            var track = AttackPower();
            var wallet = WalletWith(1e6d);

            var quoted = track.CostOfNextLevels(10);
            var before = wallet.Gold;

            track.TryPurchaseMany(wallet, 10);

            double spent = (before - wallet.Gold).ToDouble();
            Assert.AreEqual(quoted.ToDouble(), spent, quoted.ToDouble() * 1e-9d,
                "버튼에 뜬 가격과 실제 결제가 다르다");

            Object.DestroyImmediate(wallet.gameObject);
        }

        /**
         * "최대"는 잔액이 감당하는 데까지다. 한 칸 더 살 수 있는데 멈추거나,
         * 못 사는 칸을 세면 버튼이 거짓말을 한다
         */
        [Test]
        public void Max_BuysEverythingAffordableAndNotOneMore()
        {
            var track = AttackPower();
            var wallet = WalletWith(1e5d);

            // **예전에는 여기가 `AffordableLevels(wallet, 0)`("최대")였다.**
            // 그 경로는 없앴다 - 칸을 육만까지 하나씩 세는데 강화 행 아홉이
            // 골드가 변할 때마다 그것을 불러 화면이 멈췄다(BatchCostTests).
            // 이제는 화면이 고르는 가장 큰 배수로 재고, 계약은 그대로다
            int affordable = track.AffordableLevels(wallet, 100);
            Assert.Greater(affordable, 0, "10골드짜리 첫 칸도 못 산다고 한다");
            Assert.Less(affordable, 100, "이 잔액으로 백 칸이 다 사진다 - 검사 전제가 틀렸다");

            // 센 만큼 정확히 사진다
            Assert.AreEqual(affordable, track.TryPurchaseMany(wallet, affordable));

            // 그리고 한 칸도 더 못 산다
            Assert.IsFalse(track.TryPurchase(wallet), "'최대'가 한 칸을 남겼다");

            Object.DestroyImmediate(wallet.gameObject);
        }

        /** 골드가 모자라면 살 수 있는 데까지. 전부 아니면 전무가 아니다 */
        [Test]
        public void ShortOnGold_BuysAsManyAsItCan()
        {
            var track = AttackPower();
            var wallet = WalletWith(35d); // 10 + 12 = 22는 되고 셋째 칸(13)에서 막힌다

            int bought = track.TryPurchaseMany(wallet, 100);

            Assert.Greater(bought, 0, "×100이 아무것도 안 샀다");
            Assert.Less(bought, 100);
            Assert.AreEqual(1 + bought, track.Level);

            Object.DestroyImmediate(wallet.gameObject);
        }

        /** 상한이 있는 축은 상한에서 멈춘다. 배수가 그것을 넘으면 안 된다 */
        [Test]
        public void Batch_StopsAtMaxLevel()
        {
            const int Max = 20;
            var track = Capped(Max);
            var wallet = WalletWith(1e12d);

            int bought = track.TryPurchaseMany(wallet, 1000);

            Assert.AreEqual(Max, track.Level, "상한을 넘겼다");
            Assert.AreEqual(Max - 1, bought);
            Assert.AreEqual(0, track.AffordableLevels(wallet, 100), "다 산 축이 아직 살 게 있다고 한다");

            Object.DestroyImmediate(wallet.gameObject);
        }

        /** 셈과 구매가 같은 수를 말하는가. 갈리면 화면의 수와 결제가 어긋난다 */
        [Test]
        public void AffordableCount_AgreesWithWhatPurchasingBuys()
        {
            var track = AttackPower();
            var wallet = WalletWith(1234d);

            int counted = track.AffordableLevels(wallet, 100);
            int bought = track.TryPurchaseMany(wallet, 100);

            Assert.AreEqual(counted, bought);

            Object.DestroyImmediate(wallet.gameObject);
        }

        /** 빈 지갑에서 배수를 눌러도 아무 일도 없어야 한다 */
        [Test]
        public void EmptyWallet_BuysNothing()
        {
            var track = AttackPower();
            var wallet = WalletWith(0d);

            // 상한을 양수로 준다. 0을 주면 지갑이 비어서가 아니라 **상한이
            // 0이라서** 0이 나오고, 그러면 이 검사가 아무것도 안 재게 된다
            Assert.AreEqual(0, track.AffordableLevels(wallet, 100));
            Assert.AreEqual(0, track.TryPurchaseMany(wallet, 100));
            Assert.AreEqual(1, track.Level);

            Object.DestroyImmediate(wallet.gameObject);
        }
    }
}
