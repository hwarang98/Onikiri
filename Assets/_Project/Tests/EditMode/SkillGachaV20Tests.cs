using System;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 세이브 v20의 **상태 계약**. 5단계가 지는 빚이다.
     *
     * (68단계) 이 파일의 절반이던 이중 천장(★4 소프트 30 · ★5 하드 100)은
     * 사라졌다. 그 두 카운터의 검사는 지우고, 그 자리에 "v19에서 온 누적 뽑기가
     * 사슬 끝(v22 -> v23)에서 소환 경험치가 되는가"를 둔다.
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

            // v19 세계를 흉내낸다. 누적 뽑기는 이미 쌓여 있고, 온보딩 칸은 그
            // 세계에 없던 값이라 무엇이 들어 있든 상관없다 - 마이그레이션이 덮어써야 한다
            data.version = 19;
            data.skillGachaTotalPulls = 213;
            data.skillSummonXp = 999L;      // 그 세계에 없던 칸 - 사슬이 덮어써야 한다
            data.skillGachaIntroClaimed = true;
            data.skillGachaIntroEquipDone = true;
            return data;
        }

        /**
         * @brief v19 -> v20이 **온보딩 두 칸을 기본값으로 덮는가.**
         *
         * 50단계에는 세 칸이었다(★5 하드 천장 포함). 하드 천장 칸은 68단계에
         * 사라졌고, 남은 두 칸의 규칙은 그대로다 - 기존 플레이어 전원이
         * 업데이트 후 무료 10연을 한 번 받는다.
         */
        [Test]
        public void TheSaveV20_FillsTheIntroFieldsWithDefaults()
        {
            var data = FreshV19();
            Assert.IsTrue(SaveData.Migrate(data), "v19 세이브를 못 읽는다");

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            // v23으로 올랐다(68단계). 이 검사는 v19 -> v20의 칸을 재므로
            // 최신 버전 숫자만 따라 올린다
            Assert.AreEqual(23, SaveData.CurrentVersion, "세이브 버전이 최신이 아니다");

            Assert.IsFalse(data.skillGachaIntroClaimed,
                "온보딩 10연이 이미 받은 것으로 온다 - 기존 플레이어가 업데이트 보상을 잃는다");
            Assert.IsFalse(data.skillGachaIntroEquipDone);
        }

        /**
         * @brief v19의 누적 뽑기가 **사슬 끝에서 소환 경험치가 된다** (v22 -> v23).
         *
         * 50단계에는 이 자리가 "소프트 천장은 지킨다"였다 - 지불한 기록을 몰수하지
         * 않는다는 47단계 규칙. 68단계에 천장이 사라졌고, 같은 규칙이 이번에는
         * 누적 뽑기 수를 경험치로 읽는 것으로 지켜진다. 그 세계에 없던 칸에
         * 들어 있던 값(999)은 덮어써진다.
         */
        [Test]
        public void TheSaveV19_CarriesItsPullsIntoSummonXp()
        {
            var data = FreshV19();
            SaveData.Migrate(data);

            Assert.AreEqual(213, data.skillGachaTotalPulls, "누적 뽑기 수가 사라졌다");
            Assert.AreEqual(213L, data.skillSummonXp,
                "누적 213회가 소환 경험치로 안 옮겨졌다 - 이미 뽑은 사람이 Lv.1에서 다시 시작한다");
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
