using NUnit.Framework;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 배수 계산이 **부르는 쪽이 말한 만큼만** 돈다는 계약.
     *
     * ## 이 파일은 멈춘 화면에서 나왔다
     *
     * `AffordableLevels(wallet, 0)`은 "잔액이 감당하는 데까지"였고 천장이
     * `MaxBatchLevels`(65536)였다. 그 함수는 칸을 하나씩 세는데, 강화 행
     * 아홉이 **골드가 변할 때마다** 그것을 다시 불렀다. 골드는 요괴 한
     * 마리마다 변한다. 실측(Lv.8540 · 골드 1.17e69 세이브):
     *
     *   ×1   0.09ms      ×100   0.39ms
     *   ×10  0.10ms      최대   **13.68ms**   <- 150배
     *
     * 실제로 최대를 눌러 8882칸을 사면 칸마다 골드 이벤트가 또 떠서 2분
     * 가까이 멈췄다. 스탯 포인트 줄도 같은 값을 읽어(StatPointButton)
     * 밀린 포인트를 한 번에 찍었고, 같은 증상이 났다.
     *
     * 여기서 재는 것은 **비용이 아니라 걸음 수**다. 밀리초를 단언하면
     * 기계가 바뀔 때마다 붉어지지만, "백 칸을 부탁했는데 백 칸만 셌는가"는
     * 어디서 돌려도 같은 답이다.
     *
     * 총액이 같다는 계약은 여기 없다 - 그것은 이미 UpgradeBatchTests의
     * 일이고, 이 파일은 **몇 칸을 세는가**만 본다.
     */
    public class BatchCostTests
    {
        /** 비용이 거의 안 오르는 축. 옛 코드라면 육만 칸까지 셌을 조건이다 */
        private static UpgradeTrack FlatCost()
        {
            return new UpgradeTrack(UpgradeSystem.AttackPowerId, "검사용",
                                    BigDouble.FromDouble(1d), 1.0001d,
                                    UpgradeTrack.Curve.Multiplicative,
                                    BigDouble.FromDouble(1d), 1.01d);
        }

        private static PlayerWallet WalletWith(double gold)
        {
            var go = new GameObject("~BatchCostWallet");
            var wallet = go.AddComponent<PlayerWallet>();
            wallet.SetBalance(BigDouble.FromDouble(gold), BigDouble.FromDouble(gold));
            return wallet;
        }

        /**
         * @brief 상한 없는 호출(`limit <= 0`)은 **0칸**이다.
         *
         * 예전에는 여기가 육만 칸을 세는 문이었다. 0을 돌려주는 것이 맞는
         * 이유는 부르는 쪽이 몇 칸을 원하는지 말하지 않았기 때문이고,
         * 조용히 육만 칸을 세는 것보다 화면에 "0칸"이 뜨는 편이 훨씬 빨리
         * 눈에 띄기 때문이다.
         */
        [Test]
        public void TheAffordableCount_RefusesAnUnboundedRequest()
        {
            var track = FlatCost();
            var wallet = WalletWith(1e30d);
            try
            {
                Assert.AreEqual(0, track.AffordableLevels(wallet, 0),
                    "상한 없는 호출이 아직 칸을 센다 - 이것이 골드가 변할 때마다 "
                    + "육만 칸을 세던 경로다");
                Assert.AreEqual(0, track.AffordableLevels(wallet, -1),
                    "음수 상한이 칸을 센다");
            }
            finally { Object.DestroyImmediate(wallet.gameObject); }
        }

        /**
         * @brief 부탁한 만큼만 센다. **잔액이 아무리 커도.**
         *
         * 비용이 거의 안 오르는 축에 천문학적 잔액을 준다 - 옛 코드라면
         * 육만 칸을 셌을 조건이다. 상한을 지키면 그 수가 그대로 답이다.
         */
        [Test]
        public void TheAffordableCount_StopsAtTheRequestedLimit()
        {
            var track = FlatCost();
            var wallet = WalletWith(1e30d);
            try
            {
                foreach (var limit in new[] { 1, 10, 100 })
                    Assert.AreEqual(limit, track.AffordableLevels(wallet, limit),
                        "×" + limit + " 인데 돌려준 칸 수가 다르다");
            }
            finally { Object.DestroyImmediate(wallet.gameObject); }
        }

        /** 잔액이 모자라면 상한보다 적게 나온다 - 상한은 천장이지 목표가 아니다 */
        [Test]
        public void TheAffordableCount_StillStopsAtTheWallet()
        {
            var track = FlatCost();
            var wallet = WalletWith(2.5d);   // 1골드짜리 두 칸까지
            try
            {
                int counted = track.AffordableLevels(wallet, 100);
                Assert.AreEqual(counted, track.TryPurchaseMany(wallet, 100),
                    "센 수와 실제로 산 수가 다르다");
                Assert.Less(counted, 100, "잔액이 모자란데 상한만큼 셌다");
                Assert.Greater(counted, 0, "첫 칸도 못 산다고 한다");
            }
            finally { Object.DestroyImmediate(wallet.gameObject); }
        }

        /**
         * @brief 화면이 고르는 배수는 **언제나 양수**다.
         *
         * 칩이 0을 다시 들고 오면 위 검사들이 전부 통과하는 채로 증상만
         * 돌아온다 - 막는 자리가 하나 더 필요하다.
         */
        [Test]
        public void TheBatchSelection_IsAlwaysPositive()
        {
            Assert.Greater(Onikiri.UI.UpgradeBatchSelector.Current, 0,
                "배수 기본값이 0 이하다 - 0은 '최대'였고 그것이 화면을 멈췄다");
        }
    }
}
