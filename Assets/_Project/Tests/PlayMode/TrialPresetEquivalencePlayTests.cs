using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 밴드 프리셋이 **게임에서도 같은 빌드인가** (승급 5.0단계 §2 동등성).
     *
     * ## EditMode가 못 하는 것
     *
     * `TrialPresetTests`는 세이브가 시뮬레이션 줄의 모든 축을 **담았는지**를 잰다.
     * 그것으로는 "게임이 그 세이브를 읽었을 때 같은 화력이 나오는가"를 말할 수
     * 없다 - 복원 순서가 어긋나거나(`UpgradeSystem.ApplyAll`을 다시 안 부르거나)
     * 어느 시스템이 세이브의 필드를 안 읽으면, 담긴 값은 맞는데 전투가 다른 힘으로
     * 돈다. 그 어긋남은 세이브 어디에도 안 적힌다.
     *
     * 그래서 여기서는 **실제 복원 경로**(`GameSession.Apply`)를 지나 `PlayerCombat`이
     * 실제로 내는 `ReadTrialPower()`를 읽는다.
     *
     * ## 사용자 세이브를 쓰지도 덮지도 않는다
     *
     * `GameSession.Load()`는 `SaveSystem.Load()`를 지나고 에디터에서 그 경로는
     * 실사용 세이브다. 그래서 프리셋 JSON을 **검사가 직접 파싱**해 `Apply`에
     * 넘긴다 - 빠지는 것은 디스크 읽기 한 줄뿐이고, 그 한 줄이 정확히 위험한
     * 부분이다.
     *
     * 그 위에 `PromotionTrialPlayTests`가 세운 그물 셋을 그대로 쓴다: 씬을 열기
     * **전에** 원본 바이트를 뜨고, 검사가 끝나면 `GameSession`을 파괴하고(저장
     * 경로 넷이 전부 그 컴포넌트에 있다), 바이트를 되돌린 뒤 해시를 맞춘다.
     *
     * ## 기대값은 프리셋과 같은 실행에서 나온 파일이다
     *
     * `Builds/Step5/presets/expected.json`을 읽는다. 그 파일은 프리셋 12벌과
     * **같은 메뉴 실행**에서 나오므로 둘이 다른 런에서 나올 수 없다. 없으면
     * `Assert.Ignore`가 아니라 **실패**다 - 검증하지 않은 검사가 통과로 집계되면
     * 그 숫자가 거짓이 된다.
     */
    public class TrialPresetEquivalencePlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string PresetFolder = "Builds/Step5/presets";

        GameSession session;
        PlayerCombat combat;
        StageProgress progress;

        string savePath;
        byte[] savedBytes;
        string savedHash;

        // ------------------------------------------------------------ 기대값 표

        /**
         * @brief `TrialPresetForge.Expected`의 거울. **필드 이름이 계약이다.**
         *
         * 그쪽 어셈블리는 `includePlatforms: ["Editor"]`라 이 검사가 참조할 수 없다.
         * 이름이 갈리면 `JsonUtility`가 0을 채우고 아래 검사가 즉시 실패하므로
         * 조용히 어긋날 수는 없다.
         */
        [System.Serializable]
        public struct Expected
        {
            public string name;
            public int gate;
            public int stage;

            public double bandDps;
            public double presetDps;

            public double damage;
            public double attacksPerSecond;
            public double critRate;
            public double critMultiplier;
            public double transcendMultiplier;
            public double comboChance;
            public double petBonus;

            public double skillRateLowerBound;
            public double spiritRate;

            public double autoAttack;
            public double expectedTotal;
        }

        [System.Serializable]
        public class ExpectedTable
        {
            public Expected[] presets = new Expected[0];
        }

        static string ProjectPath(string relative)
        {
            // Application.dataPath = <project>/Assets
            return System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName, relative);
        }

        static List<Expected> LoadExpected()
        {
            string path = ProjectPath(System.IO.Path.Combine(PresetFolder, "expected.json"));

            Assert.IsTrue(System.IO.File.Exists(path), string.Format(
                "기대값 표가 없다: {0}\n"
                + "`Onikiri/Build/귀문 밴드 프리셋 생성` 메뉴를 한 번 돌려야 한다 - "
                + "프리셋과 기대값은 같은 실행에서 나온다", path));

            var table = JsonUtility.FromJson<ExpectedTable>(System.IO.File.ReadAllText(path));

            Assert.IsNotNull(table, "기대값 표를 읽지 못했다");
            Assert.AreEqual(12, table.presets.Length,
                "기대값이 12벌(문 여섯 x 프로필 둘)이 아니다");

            return new List<Expected>(table.presets);
        }

        static SaveData LoadPreset(string name)
        {
            string path = ProjectPath(System.IO.Path.Combine(PresetFolder, name + ".json"));

            Assert.IsTrue(System.IO.File.Exists(path), "프리셋 파일이 없다: " + path);

            var data = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(path));
            Assert.IsNotNull(data, "프리셋을 파싱하지 못했다: " + name);
            return data;
        }

        // ------------------------------------------------------------ 준비

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // 씬을 열기 **전에** 뜬다. 여는 순간 GameSession이 로드·저장할 수 있다
            savePath = System.IO.Path.Combine(Application.persistentDataPath, SaveSystem.FileName);
            savedBytes = System.IO.File.Exists(savePath)
                ? System.IO.File.ReadAllBytes(savePath) : null;
            savedHash = HashOf(savedBytes);

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            combat = Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            progress = Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);

            Assert.IsNotNull(session, "씬에 GameSession이 없다");
            Assert.IsNotNull(combat, "씬에 PlayerCombat이 없다");
            Assert.IsNotNull(progress, "씬에 StageProgress가 없다");
        }

        /**
         * @brief **`GameSession`을 파괴한 뒤** 세이브를 되돌린다. 순서가 중요하다.
         *
         * `OnApplicationQuit`은 `TearDown` **뒤에** 온다. 컴포넌트가 살아 있으면
         * 플레이 모드를 나가는 순간 프리셋 상태가 실사용 세이브 위에 저장된다 -
         * `PromotionTrialPlayTests`가 그 순서에 두 번 물렸다.
         */
        [TearDown]
        public void Restore()
        {
            if (session != null) Object.DestroyImmediate(session);

            if (!string.IsNullOrEmpty(savePath))
            {
                if (savedBytes != null) System.IO.File.WriteAllBytes(savePath, savedBytes);
                else if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);
            }

            var now = System.IO.File.Exists(savePath)
                ? HashOf(System.IO.File.ReadAllBytes(savePath)) : HashOf(null);

            Assert.AreEqual(savedHash, now,
                "복구 뒤에도 세이브 해시가 원본과 다르다 - 사용자 세이브가 오염됐다");
        }

        static string HashOf(byte[] bytes)
        {
            if (bytes == null) return "(none)";
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return System.BitConverter.ToString(md5.ComputeHash(bytes));
        }

        // ------------------------------------------------------------ 검사

        /**
         * @brief 12벌 전부: **런타임 화력이 프리셋의 기대값과 맞는다.**
         *
         * ## 무엇을 등호로 재고 무엇을 범위로 재는가
         *
         *   `AutoAttack`   **등호**(상대 1e-3). 세이브의 레벨에서만 유도되는 항이라
         *                  축 하나가 복원되지 않으면 여기서 갈린다. 오차를 0으로
         *                  두지 않는 이유는 런타임이 `float`으로 치명타 계수를
         *                  만들기 때문이다(`PlayerCombat.ReadTrialPower`)
         *   `Skills`       **하한**. 시전율은 구매 뒤 값이 `StageResult`에 없다(§2.5)
         *   `Specials`     요도 티어가 0인 문에서는 0이 정상이다. 티어가 있으면 > 0
         *   `Companions`   보유 동료가 있으면 > 0
         *   `Total`        `presetDps` 이상 · 그 1.25배 이하. 큰 축이 통째로 빠지면
         *                  아래로 벗어나고, 두 번 곱해지면 위로 벗어난다
         */
        [UnityTest]
        public IEnumerator EveryPreset_ReproducesItsExpectedPowerInTheRuntime()
        {
            var expectedRows = LoadExpected();
            var failures = new List<string>();

            // 표를 통째로 남긴다. 실패했을 때만 찍으면 "왜 1.5% 모자란가"를
            // 물을 때마다 검사를 다시 고쳐야 한다 - 보고서가 이 표를 싣는다
            var table = new System.Text.StringBuilder();
            table.AppendLine("[Trial5] preset\tautoAttack(rt/exp)\tskillRate(rt/exp)\tspiritRate(rt/exp)"
                             + "\tpetBonus(rt/exp)\ttotal(rt/expLowerBound)");

            foreach (var expected in expectedRows)
            {
                var data = LoadPreset(expected.name);

                // ---- 실제 복원 경로
                session.Apply(data);
                yield return null;

                var power = combat.ReadTrialPower();

                // 런타임이 실제로 쓴 시전율을 역산한다. `unit`이 AutoAttack/공격속도다
                double unit = expected.attacksPerSecond > 0d
                    ? power.AutoAttack / expected.attacksPerSecond : 0d;
                double runtimeSkillRate = unit > 0d ? power.Skills / unit : 0d;
                double runtimeSpiritRate = unit > 0d ? power.BossApplicableSpecials / unit : 0d;
                double runtimePetBonus = (power.AutoAttack + power.Skills + power.BossApplicableSpecials) > 0d
                    ? power.Companions / (power.AutoAttack + power.Skills + power.BossApplicableSpecials) : 0d;

                table.AppendLine(string.Format(
                    "[Trial5] {0}\t{1:E4}/{2:E4}\t{3:F4}/{4:F4}\t{5:F4}/{6:F4}\t{7:F4}/{8:F4}\t{9:E4}/{10:E4}",
                    expected.name, power.AutoAttack, expected.autoAttack,
                    runtimeSkillRate, expected.skillRateLowerBound,
                    runtimeSpiritRate, expected.spiritRate,
                    runtimePetBonus, expected.petBonus,
                    power.Total, expected.expectedTotal));

                // 진행 상태도 함께 복원됐는지 - 프리셋의 뜻이 "그 문 앞"이다
                if (progress.Stage != expected.stage)
                    failures.Add(string.Format("{0}: 스테이지가 {1}이다 (기대 {2})",
                        expected.name, progress.Stage, expected.stage));

                // ---- AutoAttack: 등호
                double relative = System.Math.Abs(power.AutoAttack - expected.autoAttack)
                                / System.Math.Max(1e-300d, expected.autoAttack);
                if (relative > 1e-3d)
                    failures.Add(string.Format(
                        "{0}: AutoAttack {1:E6} != 기대 {2:E6} (상대오차 {3:P3}). "
                        + "공격력·공격속도·치명타·초월·연격 중 하나가 복원되지 않았다",
                        expected.name, power.AutoAttack, expected.autoAttack, relative));

                // ---- Skills: 하한이고 0이면 안 된다 (오의는 레벨 1도 시전한다)
                if (power.Skills <= 0d)
                    failures.Add(string.Format(
                        "{0}: Skills가 0이다 - 오의 레벨·장착이 복원되지 않았다", expected.name));

                // ---- Specials: **등호다.** 같은 함수(YodoSpiritCurve.RateFor)에
                // 같은 입력(프리셋의 요도 배열)을 넣으므로 값이 같아야 한다
                bool expectsSpirit = expected.spiritRate > 0d;
                if (expectsSpirit && power.BossApplicableSpecials <= 0d)
                    failures.Add(string.Format(
                        "{0}: 영체 몫이 0이다 - 요도 티어가 복원되지 않았다", expected.name));

                double spiritError = expected.spiritRate > 0d
                    ? System.Math.Abs(runtimeSpiritRate - expected.spiritRate) / expected.spiritRate
                    : System.Math.Abs(runtimeSpiritRate);
                if (spiritError > 1e-3d)
                    failures.Add(string.Format(
                        "{0}: 영체 시전율 {1:F4} != 기대 {2:F4} - 요도 티어·혼격·전설 중 "
                        + "하나가 복원되지 않았다", expected.name, runtimeSpiritRate, expected.spiritRate));

                // ---- Companions: 동료 보너스가 있으면 돈다
                bool expectsPets = expected.petBonus > 0d;
                if (expectsPets && power.Companions <= 0d)
                    failures.Add(string.Format(
                        "{0}: 동료 몫이 0이다 - 동료 보유·레벨이 복원되지 않았다", expected.name));
                if (!expectsPets && power.Companions > 0d)
                    failures.Add(string.Format(
                        "{0}: 동료가 없어야 하는데 몫이 {1:E3}이다", expected.name, power.Companions));

                // ---- Total: 기대값 이상, 1.10배 이하.
                //
                // 하한인 이유는 오의 시전율 하나뿐이다 - 프리셋의 오의 레벨이
                // 구매 뒤 값이라 시전율이 시뮬레이션의 구매 전 값보다 조금 높다
                // (실측 초과폭 1~4%). 그 폭을 1.10으로 잡으면 축이 통째로
                // 빠지거나 두 번 곱해지는 결함은 그대로 걸린다
                if (power.Total < expected.expectedTotal * 0.999d)
                    failures.Add(string.Format(
                        "{0}: 총 화력 {1:E6}이 기대 {2:E6}보다 작다 - 성장 축이 빠졌다",
                        expected.name, power.Total, expected.expectedTotal));
                if (power.Total > expected.expectedTotal * 1.10d)
                    failures.Add(string.Format(
                        "{0}: 총 화력 {1:E6}이 기대 {2:E6}의 1.10배를 넘는다 - "
                        + "어느 축이 두 번 곱해졌을 수 있다",
                        expected.name, power.Total, expected.expectedTotal));
            }

            Debug.Log(table.ToString());

            if (failures.Count > 0)
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine("프리셋 동등성 실패 " + failures.Count + "건:");
                foreach (var line in failures) text.AppendLine("  " + line);
                text.AppendLine();
                text.Append(table.ToString());
                Assert.Fail(text.ToString());
            }
        }

        /**
         * @brief 프리셋을 얹으면 **그 문이 열린 상태**가 된다 (D-4).
         *
         * EditMode가 `PendingGate`의 산수를 재고, 여기서는 그 산수를 실제
         * `StageProgress`가 복원된 상태에서 확인한다 - 앱을 켜자마자 「귀문 도전」이
         * 뜨는 것이 프리셋의 목적이므로, 이것이 깨지면 실기 측정이 시작조차 못 한다.
         */
        [UnityTest]
        public IEnumerator EveryPreset_StandsAtItsGateInTheRuntime()
        {
            var expectedRows = LoadExpected();
            var failures = new List<string>();

            foreach (var expected in expectedRows)
            {
                session.Apply(LoadPreset(expected.name));
                yield return null;

                if (progress.PendingTrialGate != expected.gate)
                    failures.Add(string.Format("{0}: 대기 문이 {1}이다 (기대 {2})",
                        expected.name, progress.PendingTrialGate, expected.gate));
            }

            if (failures.Count > 0) Assert.Fail(string.Join("\n", failures.ToArray()));
        }
    }
}
