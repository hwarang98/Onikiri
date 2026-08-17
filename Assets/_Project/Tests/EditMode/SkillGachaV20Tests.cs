using System;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 세이브 v20과 이중 천장의 **상태 계약**. 5단계가 지는 빚이다.
     *
     * 여기 있는 검사는 전부 "값이 맞는가"가 아니라 **"상태가 안 새는가"**를
     * 묻는다. 뽑기는 이 게임에서 유일하게 되돌릴 수 없는 축이라(재고가 유한하고
     * 천장이 지불의 기록이다), 상태가 한 칸 새면 플레이어가 지불한 것이 사라진다.
     *
     * 마이그레이션이 특히 그렇다. v19에서 오는 플레이어는 이미 뽑기를 돌린
     * 사람들이고, 그들의 소프트 천장은 **지켜야 하고** 하드 천장은 **없던
     * 것**이다. 두 규칙을 한 마이그레이션에서 동시에 지키는지가 이 파일의 절반이다.
     */
    public class SkillGachaV20Tests
    {
        #region 마이그레이션 - v19에서 오는 사람

        private static SaveData FreshV19()
        {
            var data = SaveData.NewGame();

            // v19 세계를 흉내낸다. 소프트 천장은 이미 쌓여 있고, 하드 천장과
            // 온보딩 칸은 그 세계에 없던 값이라 무엇이 들어 있든 상관없다 -
            // 마이그레이션이 덮어써야 한다
            data.version = 19;
            data.skillGachaPity = 17;
            data.skillGachaTotalPulls = 213;
            data.skillGachaAwakenPity = 99;
            data.skillGachaIntroClaimed = true;
            data.skillGachaIntroEquipDone = true;
            return data;
        }

        /**
         * @brief v19 -> v20이 **세 칸을 전부 기본값으로 덮는가.**
         *
         * `skillGachaAwakenPity`를 누적 뽑기 수로 소급하고 싶어지는데, 그
         * 방식은 아무것도 인정하지 못한다 - 하드 천장이 재는 것은 **마지막 ★5
         * 이후**의 횟수이고 누적은 그 값과 함수 관계가 없다. 213회를 돌며
         * ★5를 세 번 받은 사람과 한 번도 못 받은 사람이 같은 213을 갖는다.
         *
         * 게다가 v19의 ★5는 개안이라 **해금 이력이 저장되지 않았다.** 복원할
         * 원본이 세이브 안에 없다.
         */
        [Test]
        public void TheSaveV20_FillsAllThreeNewFieldsWithDefaults()
        {
            var data = FreshV19();
            Assert.IsTrue(SaveData.Migrate(data), "v19 세이브를 못 읽는다");

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            // v21로 올랐다(승급 재설계). 이 검사는 v19 -> v20의 세 칸을 재므로
            // 최신 버전 숫자만 따라 올린다
            Assert.AreEqual(21, SaveData.CurrentVersion, "세이브 버전이 최신이 아니다");

            Assert.AreEqual(0, data.skillGachaAwakenPity,
                "하드 천장이 소급됐다 - 누적 횟수로는 마지막 ★5 이후를 복원할 수 없다");

            Assert.IsFalse(data.skillGachaIntroClaimed,
                "온보딩 10연이 이미 받은 것으로 온다 - 기존 플레이어가 업데이트 보상을 잃는다");
            Assert.IsFalse(data.skillGachaIntroEquipDone);
        }

        /**
         * @brief **소프트 천장은 지킨다.** 하드와 규칙이 정반대인 자리다.
         *
         * 하드는 v20부터 시작하는 새 약속이라 0에서 출발하는 것이 맞지만,
         * 소프트는 v18부터 있던 값이고 플레이어가 지불한 기록이다. 그것을
         * 함께 0으로 밀면 29회를 채운 사람이 처음부터 다시 도는 셈이 된다 -
         * 47단계가 "지불한 것을 몰수하지 않는다"로 못 박은 자리다.
         */
        [Test]
        public void TheSaveV20_KeepsTheSoftPityItInherited()
        {
            var data = FreshV19();
            SaveData.Migrate(data);

            Assert.AreEqual(17, data.skillGachaPity,
                "소프트 천장이 마이그레이션에서 리셋됐다 - 지불한 것을 몰수한다");
            Assert.AreEqual(213, data.skillGachaTotalPulls,
                "누적 뽑기 수가 사라졌다");
        }

        /**
         * @brief 세이브가 **열다섯 종을 전부 실어 나르는가.**
         *
         * 신규 일곱은 id 병렬 배열이라 마이그레이션이 필요 없다 - 옛 세이브에
         * 없는 id는 Lv.1 미보유로 자연 처리된다. 그런데 "필요 없다"와 "된다"는
         * 다른 말이고, 저장 경로가 열다섯을 다 훑는지는 별개 문제다.
         */
        [Test]
        public void TheSaveV20_RoundTripsAllFifteenSkillIds()
        {
            Assert.AreEqual(15, SkillCatalog.Count, "로스터가 열다섯이 아니다");

            // **v19에서 오는 세이브로 잰다.** 새 게임은 오의 칸이 비어 있고
            // 런타임이 채우므로(SkillSystem), 그것으로는 마이그레이션이 일을
            // 했는지 알 수 없다. v19 세이브는 v16->v17에서 **그때의 여덟**만
            // 받았고 신규 일곱은 아직 칸이 없는 상태다
            var data = FreshV19();
            Assert.IsTrue(SaveData.Migrate(data));

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                string id = SkillCatalog.Skills[i].Id;
                int index = Array.IndexOf(data.skillIds, id);

                Assert.GreaterOrEqual(index, 0,
                    "'" + id + "'의 칸이 v20 세이브에 없다 - 저장에서 조용히 빠진다");
                Assert.AreEqual(1, data.skillLevels[index],
                    "'" + id + "'이 Lv.1로 시작하지 않는다 - 소급해 올려 줬다");
            }

            Assert.AreEqual(data.skillIds.Length, data.skillLevels.Length,
                "id 배열과 레벨 배열의 길이가 다르다 - 병렬 배열이 어긋났다");
        }

        #endregion

        #region 이중 천장 - 두 카운터가 서로를 안 건드린다

        /**
         * @brief ★4는 **하드 카운터를 안 되돌린다.**
         *
         * 이것이 두 천장을 나눈 이유 전부다. 영웅이 전설까지의 거리를 줄이면
         * 두 게이지가 같은 속도로 차고, 그러면 게이지를 둘 그릴 이유가 없다.
         *
         * 곡선 쪽 값으로 재는 이유는 시스템이 MonoBehaviour라 EditMode에서
         * 씬 없이 못 돌리기 때문이다. 대신 `SkillGachaPityModel`이 같은 전이를
         * 들고 있으므로 그쪽으로 잰다 - 두 곳이 갈리면 그 모델의 검사가 잡는다.
         */
        [Test]
        public void TheEpicResult_DoesNotResetTheHardPity()
        {
            var state = new SkillGachaPityModel.State(SkillGachaCurve.PityPulls,
                                                      SkillGachaCurve.AwakenPityPulls);

            // **두 천장이 동시에 코앞인 자리에서 잰다.** 소프트는 다음 한 번에
            // 터지고, 하드는 그 다음 한 번에 터진다. 긴 전파로 재지 않는 이유는
            // 이것이 분포이기 때문이다 - 중간에 자연 ★5가 섞이면 질량이 흩어져
            // "몇 회 뒤에 하드가 온다"를 한 숫자로 말할 수 없다
            state.ResetToSavedCounters(SkillGachaCurve.PityPulls - 1,
                                       SkillGachaCurve.AwakenPityPulls - 2);

            var forced = SkillGachaPityModel.Advance(state);
            Assert.AreEqual(1d - GachaCurve.EffectiveLegendaryChance, forced.UnlockChance, 1e-12d,
                "소프트 천장 회차인데 ★5가 아닌 나머지가 전부 ★4가 아니다");

            // 그 ★4가 하드를 **안 되돌렸다면** 하드는 한 칸 더 차서 다음
            // 회차가 곧 천장이다. 되돌렸다면 다음 회차의 ★5는 표 확률(0.8%)이다
            var next = SkillGachaPityModel.Advance(state);

            Assert.Greater(next.AwakenChance, 0.99d, string.Format(
                "다음 회차의 ★5가 {0:P2}다 - ★4가 하드 카운터를 되돌렸다는 뜻이고, "
                + "그러면 두 천장이 한 카운터를 나눠 쓰는 것이다", next.AwakenChance));
        }

        /**
         * @brief ★5는 **소프트도 함께 되돌린다.** 위와 반대 방향이다.
         *
         * 전설은 영웅 이상이므로 소프트 천장이 재는 "★4 이상"을 충족한다.
         * 이 비대칭이 곧 소프트 천장의 정의이고, 그래서 표준 해금이 30회를
         * 넘길 수 있다 - ★5가 카운터를 가로채기 때문이다.
         */
        [Test]
        public void TheLegendaryResult_ResetsTheSoftPityToo()
        {
            var state = new SkillGachaPityModel.State(SkillGachaCurve.PityPulls,
                                                      SkillGachaCurve.AwakenPityPulls);

            // 하드 천장 직전 + 소프트도 거의 찼다. 다음 한 번은 ★5 확정이다
            state.ResetToSavedCounters(SkillGachaCurve.PityPulls - 2,
                                       SkillGachaCurve.AwakenPityPulls - 1);

            var forced = SkillGachaPityModel.Advance(state);
            Assert.AreEqual(1d, forced.AwakenChance, 1e-12d, "하드 천장 회차가 ★5가 아니다");
            Assert.AreEqual(0d, forced.UnlockChance, 1e-12d,
                "한 회차에 ★4와 ★5가 같이 나왔다");

            // 소프트가 0으로 돌아갔다면 다음 회차는 천장이 아니라 표 확률이다.
            // 안 돌아갔다면 남은 한 칸이 차서 곧바로 ★4 확정이 된다
            var next = SkillGachaPityModel.Advance(state);
            Assert.Less(next.UnlockChance, 0.5d,
                "★5가 소프트 카운터를 안 되돌렸다 - 전설이 영웅 이상이라는 규칙이 깨졌다");
        }

        #endregion

        #region 두 배너가 갈렸다

        /**
         * @brief 상점이 **요도보다 스물일곱 칸 먼저** 열린다.
         *
         * 이 한 줄이 5단계에서 가장 넓게 퍼지는 변경이다 - 뽑기 몫 아홉의
         * 골드 앵커가 함께 내려오고(SkillSpec.UnlockStage), 코리더의 사건
         * 배치에 칸이 하나 들어가며, 보석 소비처가 스물일곱 스테이지 앞당겨진다.
         */
        [Test]
        public void TheShopGate_SitsInsideTheCorridor()
        {
            Assert.AreEqual(14, ShopCurve.UnlockStage);
            Assert.AreEqual(41, GachaCurve.UnlockStage, "요도 배너가 st41에서 움직였다");

            // **배너와 지갑이 다른 칸에 열린다.** 실측이 강제한 분리다 -
            // 보석 구매를 st14에 두면 코리더의 얇은 보석 여유가 빠져나가
            // st52에서 f2p 바닥(1.40)을 1.343으로 뚫는다
            Assert.AreEqual(ShopCurve.UnlockStage, SkillGachaCurve.UnlockStage,
                "배너가 상점과 다른 칸에 선다");
            Assert.AreEqual(GachaCurve.UnlockStage, SkillGachaCurve.PullUnlockStage,
                "보석 구매가 요도 배너와 다른 칸에 열린다 - 지갑이 두 번 흔들린다");

            Assert.IsFalse(SkillGachaCurve.CanBuyAt(40), "st40에 보석 구매가 열렸다");
            Assert.IsTrue(SkillGachaCurve.CanBuyAt(41));

            // 그래도 **배너는 st14에 선다.** 무료 10연과 일일 무료가 그 칸의
            // 값이고, 그것이 이 재설계가 원한 "무과금이 일찍 닿는다"이다
            Assert.IsTrue(SkillGachaCurve.IsUnlockedAt(14),
                "st14에 배너가 안 선다 - 무료 10연을 받을 자리가 없다");

            Assert.IsFalse(ShopCurve.IsUnlockedAt(13));
            Assert.IsTrue(ShopCurve.IsUnlockedAt(14));

            // 뽑기 몫 아홉의 골드 앵커도 함께 내려왔는가
            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                if (!spec.GachaGated) continue;

                Assert.AreEqual(ShopCurve.UnlockStage, spec.UnlockStage, string.Format(
                    "'{0}'의 골드 앵커가 st{1}에 남아 있다 - 비용이 스물일곱 칸 뒤의 "
                    + "수입 규모로 잡힌다", spec.DisplayName, spec.UnlockStage));
            }
        }

        #endregion
    }
}
