using NUnit.Framework;
using Onikiri.DevTools;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 밴드 프리셋 세이브의 계약 (승급 5.0단계 §2).
     *
     * ## 이 검사들이 지키는 것
     *
     * 3단계 실기는 강화 레벨만 손으로 바꾼 합성 빌드로 k를 재려 했고 실패했다.
     * 프리셋 도구의 존재 이유가 그것을 고치는 것이므로, 도구가 **정말로 밴드
     * 빌드를 만드는지**가 곧 실기 숫자의 자격이다. 그 자격을 여기서 잰다.
     *
     * ## `Middle`이 없는 것도 계약이다
     *
     * 중간 프로필은 `PromotionTrialTests.MidAt`이 만드는 기하평균 계산 대리값이고
     * 그 DPS를 내는 강화 레벨 조합은 어디에도 정의돼 있지 않다. 실재하지 않는
     * 빌드를 세이브로 찍으면 그것이 바로 3단계가 폐기한 합성 빌드다 -
     * `Profile`이 둘뿐이라는 사실을 검사가 붙잡는다.
     */
    public class TrialPresetTests
    {
        const int Gates = 6;

        static StageSimulation.StageResult RowAt(TrialPresetForge.Profile profile, int gate)
        {
            int stage = PromotionTrialFixture.GateStages[gate - 1];
            var rows = profile == TrialPresetForge.Profile.Floor
                ? PromotionTrialFixture.GemFloor()
                : PromotionTrialFixture.Lead();
            return rows[stage - 1];
        }

        static TrialPresetForge.Profile[] Profiles()
        {
            return new[] { TrialPresetForge.Profile.Floor, TrialPresetForge.Profile.CurveFollower };
        }

        // ------------------------------------------------------------ 실재하는 것만

        /**
         * @brief 프리셋은 **실재하는 정책 둘**뿐이다.
         *
         * 셋이 되는 날 그 셋째가 무엇인지 물어야 한다 - 새 `Policy`라면 밴드를
         * 다시 유도해야 하고(목표 밴드를 임의로 만들지 않는다), 합성이라면
         * 3단계가 이미 폐기한 방법이다.
         */
        [Test]
        public void OnlyTheTwoRealPolicies_HaveAPreset()
        {
            var values = System.Enum.GetValues(typeof(TrialPresetForge.Profile));

            Assert.AreEqual(2, values.Length,
                "프리셋 프로필이 둘이 아니다. 중간 프로필은 계산 대리값이므로 "
                + "세이브로 찍으면 3단계가 폐기한 합성 빌드가 된다");
        }

        // ------------------------------------------------------------ 결정론·왕복

        /** 같은 줄은 같은 세이브를 낸다 - 실기 숫자를 다시 재려면 이것이 먼저다 */
        [Test]
        public void TheSameSimulationRow_AlwaysProducesTheSameSave()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var row = RowAt(profile, gate);

                    string first = TrialPresetForge.ToJson(TrialPresetForge.Build(row, gate));
                    string second = TrialPresetForge.ToJson(TrialPresetForge.Build(row, gate));

                    Assert.AreEqual(first, second, string.Format(
                        "{0}: 같은 줄에서 두 번 만든 세이브가 다르다", TrialPresetForge.NameOf(profile, gate)));
                }
        }

        /**
         * @brief 저장하고 다시 읽어도 같다. **게임과 같은 직렬화기로 잰다.**
         *
         * `JsonUtility`가 못 담는 형(딕셔너리·프로퍼티)이 세이브에 들어오면
         * 여기서 갈린다 - 그때 기기에 밀어 넣은 파일은 조용히 다른 빌드가 된다.
         */
        [Test]
        public void ThePreset_SurvivesTheJsonRoundTrip()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var row = RowAt(profile, gate);
                    var save = TrialPresetForge.Build(row, gate);

                    string json = TrialPresetForge.ToJson(save);
                    var back = TrialPresetForge.FromJson(json);

                    Assert.AreEqual(json, TrialPresetForge.ToJson(back), string.Format(
                        "{0}: 왕복 후 세이브가 달라졌다", TrialPresetForge.NameOf(profile, gate)));

                    var bad = TrialPresetForge.Audit(back, row, gate);
                    Assert.AreEqual(0, bad.Count, string.Format(
                        "{0}: 왕복 후 {1}개 축이 어긋났다 - 첫 항 [{2}]",
                        TrialPresetForge.NameOf(profile, gate), bad.Count,
                        bad.Count > 0 ? bad[0].ToString() : ""));
                }
        }

        // ------------------------------------------------------------ 같은 빌드인가

        /**
         * @brief 세이브가 그 시뮬레이션 줄의 **모든 성장 축을 담는다.**
         *
         * 축 하나를 안 담으면(요도 티어를 잊으면) 그 축만 빠진 빌드가 기기에
         * 올라가고, 화면에는 아무 표시도 안 난다. 감사가 어긋난 축의 **이름**을
         * 내는 이유가 그것이다 - "느린데 왜 느린지 모르는" 상태를 안 만든다.
         */
        [Test]
        public void EveryPreset_CarriesEveryGrowthAxisOfItsRow()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var row = RowAt(profile, gate);
                    var save = TrialPresetForge.Build(row, gate);
                    var bad = TrialPresetForge.Audit(save, row, gate);

                    if (bad.Count > 0)
                    {
                        var text = new System.Text.StringBuilder();
                        text.AppendLine(TrialPresetForge.NameOf(profile, gate) + ": 어긋난 축 " + bad.Count + "개");
                        foreach (var m in bad) text.AppendLine("  " + m);
                        Assert.Fail(text.ToString());
                    }
                }
        }

        /**
         * @brief 앱을 켠 직후 **그 문에 도전할 수 있다.**
         *
         * 대기 조건 다섯(`PendingGate`)을 여기서 복제하지 않고 그 함수를 부른다 -
         * 판정이 두 벌이 되면 프리셋이 통과시키는 조건과 게임이 요구하는 조건이
         * 갈리고, 그 갈림은 기기에서만 드러난다.
         */
        [Test]
        public void EveryPreset_StandsAtItsGateWithTheDoorOpen()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var save = TrialPresetForge.Build(RowAt(profile, gate), gate);

                    int pending = PromotionTrialCatalog.PendingGate(
                        save.stage, save.maxStageReached, save.killsThisStage,
                        save.bossKillCount, save.evolutionTier);

                    Assert.AreEqual(gate, pending, string.Format(
                        "{0}: 세이브에서 유도한 대기 문이 {1}이다 - 앱을 켜도 문이 안 열린다",
                        TrialPresetForge.NameOf(profile, gate), pending));

                    Assert.AreEqual(gate - 1, save.evolutionTier, string.Format(
                        "{0}: 문 {1}에 서는 플레이어의 경지는 {2}여야 한다",
                        TrialPresetForge.NameOf(profile, gate), gate, gate - 1));
                }
        }

        // ------------------------------------------------------------ 두 시점

        /**
         * @brief 프리셋의 화력이 **밴드 앵커보다 크다.** 5.0단계의 핵심 발견이다.
         *
         * ## 왜 큰가 - 게이트 보스의 보상이 문 앞에서 이미 손에 있다
         *
         * `StageSimulation.Run`은 보스와 싸운 화력(`stats`)을 `ExpectedDps`에 담고,
         * **그 뒤에 한 번 더 사고**(보스 골드·클리어 보너스·업적·요도) 그 시점의
         * 강화 레벨을 `StageResult`에 담는다. 밴드는 앞의 값에 앵커돼 있고 세이브는
         * 뒤의 값을 담는다.
         *
         * 그리고 뒤의 값이 **게임에서 실제로 일어나는 일**이다 -
         * `BossFight.OnBossKilled`이 게이트에서도 보상을 평소대로 주고(멈추는 것은
         * 진행뿐이다), 강화 화면은 문에 들어가기 전까지 열려 있다.
         *
         * 그러므로 실제 플레이어는 두 지점 **사이 어디든** 선다. 하드 계약이 서
         * 있는 프레임은 **즉시 도전**이고(45초 앵커의 뜻이 그것이다), 쇼핑 후의
         * 단축은 성장의 결과다 - `PromotionSoftCapMatrixTests`가 그 구분대로 잰다.
         *
         * 여기서는 그 구간이 실제로 존재한다는 사실과 크기를 붙잡는다. 이 검사가
         * 깨지면(비가 1이 되면) 시뮬레이션의 스냅샷 순서가 바뀐 것이고, 그때
         * 5.0단계의 결론 전부를 다시 읽어야 한다.
         */
        [Test]
        public void ThePresetPower_SitsAboveTheBandAnchor_BecauseTheBossRewardIsAlreadyInHand()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    var row = RowAt(profile, gate);
                    var power = TrialPresetForge.Measure(row, PromotionTrialFixture.ReferencePowerForGate(gate));

                    Assert.Greater(power.PresetDps, power.BandDps, string.Format(
                        "{0}: 프리셋 화력이 밴드 앵커보다 크지 않다 - StageResult의 스냅샷 "
                        + "순서가 바뀌었으면 5.0단계의 결론을 다시 읽어야 한다",
                        TrialPresetForge.NameOf(profile, gate)));

                    Assert.That(power.Ratio, Is.InRange(1.60d, 2.00d), string.Format(
                        "{0}: 두 시점의 화력 비가 x{1:F4}다 (5.0단계 실측 x1.70~1.88). "
                        + "밖으로 나가면 곡선이나 구매 정책이 움직인 것이고, k 판정을 다시 재야 한다",
                        TrialPresetForge.NameOf(profile, gate), power.Ratio));
                }
        }

        /**
         * @brief 밴드 앵커가 **앞 스테이지의 구매 뒤 화력과 이 스테이지의 구매 뒤 화력 사이**다.
         *
         * 위 검사가 "차이가 있다"를 재고 이쪽이 "그 차이가 어디서 오는가"를 잰다 -
         * 한 스테이지 안의 구매 하나 몫이라는 것이 이 부등식의 뜻이고, 그래서
         * 프리셋을 앞 줄에서 만들 수도 없다(그쪽은 밴드보다 **약하다**).
         */
        [Test]
        public void TheBandAnchor_IsBracketedByTheAdjacentPostPurchaseStates()
        {
            foreach (var profile in Profiles())
                for (int gate = 1; gate <= Gates; gate++)
                {
                    int stage = PromotionTrialFixture.GateStages[gate - 1];
                    var rows = profile == TrialPresetForge.Profile.Floor
                        ? PromotionTrialFixture.GemFloor()
                        : PromotionTrialFixture.Lead();

                    double reference = PromotionTrialFixture.ReferencePowerForGate(gate);
                    double previous = TrialPresetForge.Measure(rows[stage - 2], reference).PresetDps;
                    double anchor = rows[stage - 1].ExpectedDps;
                    double here = TrialPresetForge.Measure(rows[stage - 1], reference).PresetDps;

                    Assert.LessOrEqual(previous, anchor * (1d + 1e-9d), string.Format(
                        "문{0}: 앞 스테이지의 구매 뒤 화력이 밴드 앵커보다 크다", gate));
                    Assert.LessOrEqual(anchor, here * (1d + 1e-9d), string.Format(
                        "문{0}: 밴드 앵커가 이 스테이지의 구매 뒤 화력보다 크다", gate));
                }
        }

        // ------------------------------------------------------------ 안전

        /**
         * @brief 프리셋은 **사용자 세이브 폴더를 향하지 않는다.**
         *
         * 에디터에서 `SaveSystem.Path`는 실사용 세이브를 가리킨다. 도구가 그 자리에
         * 쓰면 실기 측정 하나 하려고 실사용 진행을 덮어쓰는 셈이다 - 3단계가
         * 패키지명으로 같은 위험을 피한 것과 같은 판단이고, 여기서는 아예 그 경로를
         * 쓰지 않는다.
         */
        [Test]
        public void ThePresetFolder_IsInsideTheProject_NotTheUserSaveFolder()
        {
            string folder = TrialPresetForge.OutputFolder;

            Assert.IsFalse(System.IO.Path.IsPathRooted(folder),
                "프리셋 출력 경로가 절대 경로다 - 프로젝트 밖을 가리킬 수 있다");

            string full = System.IO.Path.GetFullPath(folder);
            string saveFolder = System.IO.Path.GetDirectoryName(
                System.IO.Path.GetFullPath(SaveSystem.Path));

            Assert.AreNotEqual(saveFolder, full,
                "프리셋 출력 폴더가 실사용 세이브 폴더와 같다");
        }

        /** 모델링하지 않은 것의 목록이 비어 있지 않다 - 빈 값을 조용히 두지 않는다 */
        [Test]
        public void WhatIsNotModelled_IsWrittenDown()
        {
            var lines = TrialPresetForge.NotModelled();

            Assert.Greater(lines.Length, 0,
                "모델링하지 않은 항목이 하나도 없다고 적혀 있다 - 골드·경험치·퀘스트는 "
                + "실제로 0이므로 그 사실이 어딘가에 적혀 있어야 한다");

            foreach (var line in lines)
                Assert.IsNotEmpty(line, "빈 줄이 목록에 있다");
        }
    }
}
