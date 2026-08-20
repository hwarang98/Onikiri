using System.Collections;
using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 귀문(승급전)의 **실제 런타임 검증.**
     *
     * ## 왜 EditMode로는 모자란가
     *
     * EditMode가 재는 것은 순수 함수다 - 게이트 표, D-4 판정, 소프트캡 산식.
     * 그 셋이 다 맞아도 **전투가 안 돌 수 있다**: 적이 안 서거나, 전환에서
     * 시계가 멈추거나, 콜백이 두 번 오거나, 배율이 나갈 때 안 꺼진다.
     *
     * ## 목을 쓰지 않는다
     *
     * `EnemySpawner`·`PlayerCombat`·`BossFight`·`TrialHud`가 서로를 인스펙터
     * 참조로 알고 있어서, 목으로 갈아 끼우면 **배선 자체가 검사 대상에서
     * 빠진다** - 실제로 `TrialHud`는 클래스만 있고 씬에 없는 채로 "완료"가
     * 보고된 적이 있다.
     *
     * ## 결정론 - 플레이어의 강화 상태에 의존하지 않는다
     *
     * 세이브의 강화 레벨이 사람마다 다르므로 "실제로 싸워서 이긴다"에 기대면
     * 그 사람의 지갑이 검사 결과를 바꾼다. 그래서 적은 `KillCurrentFoe()`로
     * **직접 처치**한다 - 상태 머신·스포너·보상 차단·배율은 전부 실제 경로를
     * 지나고, 갈리는 것은 "얼마나 세게 때렸는가"뿐이다.
     *
     * 소프트캡의 산수는 `DamageAudit_*`가 알려진 값으로 따로 잰다.
     *
     * ## 세이브 격리 - **두 번 물렸다**
     *
     *   1차   `BossFight.EndTrial`의 승리 저장이 실제 파일을 덮었다
     *   2차   `GameSession.OnApplicationQuit`이 **TearDown 뒤**에 다시 덮었다
     *
     * 그래서 셋을 겹쳐 둔다: 씬을 열기 **전에** 원본 해시를 뜨고,
     * `GameSession` 컴포넌트를 파괴하고, 끝나면 바이트를 되돌린 뒤 해시를 맞춘다.
     *
     * `Assert.Ignore`를 쓰지 않는다 - 검증하지 않은 검사가 전체 통과로 집계되면
     * 그 숫자가 거짓이 된다.
     */
    public class PromotionTrialPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";

        BossFight fight;
        StageProgress progress;
        EvolutionSystem evolution;
        EnemySpawner spawner;
        PlayerHealth health;
        PlayerCombat combat;
        Onikiri.UI.TrialHud trialHud;

        SaveSandbox sandbox;

        // ------------------------------------------------------------ 준비

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            /**
             * @brief **씬을 열기 전에 세이브 경로를 격리한다** (61단계 S5-0).
             *
             * 60단계까지는 원본 바이트를 떠 두고 TearDown이 되돌렸다 - 그 반창고는
             * 검사가 죽거나 에디터가 행에 걸리면 안 붙었고, 실제로 이 클래스가
             * 그 순서에 두 번 물렸다. 이제 `GameSession`이 보는 경로 자체가 임시
             * 폴더라, 어떤 경로로 저장이 나가도 실사용 파일은 못 만진다.
             */
            sandbox = new SaveSandbox();

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            fight = Object.FindFirstObjectByType<BossFight>(FindObjectsInactive.Include);
            progress = Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);
            evolution = Object.FindFirstObjectByType<EvolutionSystem>(FindObjectsInactive.Include);
            spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            health = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            combat = Object.FindFirstObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            trialHud = Object.FindFirstObjectByType<Onikiri.UI.TrialHud>(FindObjectsInactive.Include);

            Assert.IsNotNull(fight, "씬에 BossFight가 없다");
            Assert.IsNotNull(progress, "씬에 StageProgress가 없다");
            Assert.IsNotNull(evolution, "씬에 EvolutionSystem이 없다");
            Assert.IsNotNull(spawner, "씬에 EnemySpawner가 없다");
            Assert.IsNotNull(combat, "씬에 PlayerCombat이 없다");
            Assert.IsNotNull(health, "씬에 PlayerHealth가 없다");

            /**
             * @brief **`GameSession` 컴포넌트를 파괴한다.** 저장 경로가 넷이다.
             *
             * 자동 30초(`Update`) · `OnApplicationPause` · `OnApplicationFocus` ·
             * `OnApplicationQuit`. 하나씩 막으면 다섯 번째가 생기는 날 실패하므로
             * 컴포넌트를 없앤다. 오브젝트가 아니라 컴포넌트만 지우는 이유는
             * 오브젝트를 끄면 같은 자리의 전투 시스템까지 죽어서 귀문이 아예
             * 안 돌기 때문이다(실측).
             */
            var session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            if (session != null) Object.DestroyImmediate(session);

            // 폐쇄가 180 게임초라 실시간으로는 NUnit의 180초 벽시계를 넘는다.
            // 그러면 "무한 정지"와 "오래 걸림"이 똑같이 타임아웃으로 죽어 진단이
            // 안 된다. 게임 규칙은 그대로 두고 벽시계만 줄인다
            Time.timeScale = 10f;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            TrialDamageScale.Exit();

            /**
             * @brief 장부도 함께 비운다. **정적이라 씬을 다시 열어도 안 지워진다.**
             *
             * `Exit`는 배율만 끄고 장부는 `Enter`가 비운다. 그래서 귀문을 켠 채
             * 끝난 검사 다음에 `OrdinaryCombat_DamageIsUnscaled`가 오면, 그 검사가
             * 보는 "귀문 밖의 장부"에 **앞 검사의 기록**이 남아 있다 - 실제로
             * 4단계에서 검사 순서가 바뀌자 그 조합이 만들어져 실패했다.
             *
             * 검사 하나가 다른 검사의 결과를 바꾸는 것은 그 자체로 결함이므로,
             * 판정을 느슨하게 하는 대신 여기서 상태를 끊는다.
             */
            TrialDamageScale.ResetAudit();

            // 루트를 되돌리고 **실사용 파일이 그대로인지 검사한다** - 격리가
            // 뚫렸으면 여기서 예외로 실패한다 (S5-0 회귀 방지)
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ------------------------------------------------------------ 헬퍼

        /**
         * @brief 게이트 보스를 막 잡은 직후 상태 (D-4 대기).
         *
         * ## 먼저 **필드를 비우고 스폰을 멈춘다**
         *
         * 대기 조건은 `killsThisStage == KillsPerStage`로 **정확히 같아야** 한다
         * (`PromotionTrialCatalog.PendingGate`). 그런데 상태를 적은 뒤 한 프레임만
         * 흘러도 사무라이가 잡몹을 벤다 - `Time.timeScale = 10`에서 한 프레임이
         * 게임 0.17초이고, 상위 스테이지 세이브는 st30 잡몹을 한 방에 벤다.
         * 그러면 할당량을 **넘어서** 대기가 조용히 닫힌다.
         *
         * 4단계에서 이것에 물렸다: 검사 일곱 개가 실행마다 다른 조합으로
         * 떨어졌고, 실패 문구는 "1체가 서지 않았다"라 원인이 스폰인지 대기인지
         * 구분되지 않았다. 원인은 스폰이 아니라 **대기가 이미 닫혀 있었다**였다.
         *
         * `BeginTrial`이 하는 일과 같은 둘이므로 귀문의 세계를 바꾸지 않는다.
         */
        void StandAtPendingGate(int gateStage)
        {
            if (spawner != null)
            {
                spawner.SuspendSpawning();
                spawner.ClearField();
            }

            evolution.DebugSetTier(PromotionTrialCatalog.TierAtFrontier(gateStage));
            progress.SetProgress(gateStage, StageCurve.KillsPerStage, gateStage, gateStage);

            // 못 만들었으면 **여기서** 실패한다. 아래로 흘려보내면 원인이
            // 세 함수 뒤에서 다른 이름으로 나온다
            Assert.Greater(progress.PendingTrialGate, 0,
                "귀문 대기 상태가 안 만들어졌다 (stage=" + progress.Stage
                + " kills=" + progress.KillsThisStage
                + " bossKills=" + progress.BossKillCount
                + " max=" + progress.MaxStageReached
                + " tier=" + evolution.Tier + ")");
        }

        /** 게이트 보스에 도전할 수 있는 상태 (아직 안 잡았다) */
        void StandBeforeGateBoss(int gateStage)
        {
            evolution.DebugSetTier(PromotionTrialCatalog.TierAtFrontier(gateStage));
            progress.SetProgress(gateStage, StageCurve.KillsPerStage, gateStage - 1, gateStage);
        }

        Enemy CurrentFoe()
        {
            foreach (var e in spawner.Active)
                if (e != null && e.IsAlive && e.IsTrialFoe) return e;
            return null;
        }

        /**
         * @brief 지금 적을 **직접 벤다.** 결정론의 핵심이다.
         *
         * 처치 경로는 그대로 `Enemy.TakeDamage`를 지나므로 보상 차단·상태
         * 전이·소프트캡은 전부 실제 코드가 돈다.
         */
        bool KillCurrentFoe()
        {
            var foe = CurrentFoe();
            if (foe == null) return false;

            /**
             * @brief **소프트캡을 되돌린 양**을 때린다.
             *
             * 그냥 `MaxHealth x 1000`을 넣던 시절이 있었는데, 그것은 배율이
             * 1/1000보다 작아지는 순간 조용히 부족해진다 - 배율은 세이브의
             * 강화 상태로 정해지므로 **사람마다 다른 시점에** 그렇게 된다.
             * 4단계에서 실제로 물렸다: 적이 안 죽어 전환에 못 들어갔고,
             * 실패 문구는 "전환에 못 들어갔다"라 원인이 배율이라는 것을
             * 화면 어디에서도 알 수 없었다.
             *
             * 배율로 나누면 "얼마나 세게 때렸는가"가 다시 상수가 된다 -
             * 이 파일이 지키는 결정론이 정확히 그것이다.
             */
            double undo = TrialDamageScale.IsActive && TrialDamageScale.Current > 0d
                ? 1d / TrialDamageScale.Current : 1d;

            foe.TakeDamage(foe.MaxHealth * BigDouble.FromDouble(1000d * undo),
                           TrialDamageScale.Source.AutoAttack);
            return true;
        }

        IEnumerator WaitForTrial(BossFight.TrialState target, float limitSeconds)
        {
            float t = 0f;
            while (fight.Trial != target && t < limitSeconds)
            {
                if (fight.Current != BossFight.Phase.Trial) break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator RunSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        IEnumerator WaitUntilTrialEnds(float limitSeconds)
        {
            float t = 0f;
            while (fight.Current == BossFight.Phase.Trial && t < limitSeconds)
            { t += Time.deltaTime; yield return null; }
        }

        /** 귀문을 열고 1체가 설 때까지 */
        IEnumerator EnterTrialAtFoeOne(int gateStage)
        {
            StandAtPendingGate(gateStage);
            yield return null;

            fight.ChallengeTrial();

            /**
             * @brief 문을 연 **바로 그 프레임에** 플레이어 화력을 멈춘다.
             *
             * 상위 스테이지 세이브는 st30 귀문 적을 한 방에 벤다. 그러면
             * `FightingFoe1`이 한 프레임도 안 서고 지나가 버려서, 프레임마다
             * 확인하는 `WaitForTrial`이 그것을 **놓친다** - 검사는 "1체가 서지
             * 않았다"로 죽지만 실제로 일어난 일은 "이미 이겼다"다.
             *
             * 4단계에서 이것에 물렸다. 실행마다 다른 조합이 떨어졌고 그 무작위성
             * 자체가 원인의 증거였다.
             *
             * 소프트캡 점수는 `ChallengeTrial` 안에서 **이미 읽혔으므로**, 그
             * 다음에 끄는 것은 세계를 바꾸지 않는다(SuspendPlayerDamage 주석의
             * "귀문에 들어간 뒤에 부른다"가 이 순서다). 적은 계속 때리므로
             * 공격 리듬·격노·사망 실패는 그대로 잰다.
             */
            SuspendPlayerDamage();

            yield return WaitForTrial(BossFight.TrialState.FightingFoe1, 30f);

            // 실패했을 때 **왜인지**를 함께 적는다. "Idle이었다"만으로는
            // 안 들어간 것인지 들어갔다 끝난 것인지 구분되지 않는다
            Assert.AreEqual(BossFight.TrialState.FightingFoe1, fight.Trial,
                "1체가 서지 않았다 (phase=" + fight.Current
                + " gate=" + fight.TrialGate
                + " pending=" + fight.PendingTrialGate
                + " scale=" + TrialDamageScale.Current.ToString("G4") + ")");
        }

        /**
         * @brief 플레이어 측 피해를 **멈춘다.** 시계 검사의 전제다.
         *
         * 격노(90초)·폐쇄(180초)·적 공격 간격을 재려면 적이 그때까지 살아
         * 있어야 하는데, 세이브의 강화 상태에 따라 적이 즉사한다 - 실제로
         * st175 세이브에서 네 검사가 "귀문이 이미 끝났다"로 실패했다.
         *
         * 화력을 끄면 그 의존이 사라진다. 적은 계속 때리므로 공격 리듬과
         * 격노는 그대로 재고, 죽지 않으므로 폐쇄까지 간다.
         *
         * **귀문에 들어간 뒤에 부른다** - 소프트캡 점수는 입장 시 한 번
         * 읽으므로, 그 전에 끄면 점수가 0이 되어 다른 세계를 재게 된다.
         */
        void SuspendPlayerDamage()
        {
            if (combat != null) combat.enabled = false;

            /**
             * @brief **죽지도 않는다.** 61단계 경로 격리가 드러낸 숨은 의존이다.
             *
             * 이 헬퍼의 주석은 "죽지 않으므로 폐쇄까지 간다"고 적었지만, 그
             * 생존은 사실 **실사용 세이브의 강화 상태**가 우연히 보장하고
             * 있었다 - 샌드박스(새 게임) 부팅이 되자 st30 귀문 적의 첫 타가
             * 기본 체력(100)을 넘어 2.3초 만에 사망 실패가 났고, 시계 검사
             * 다섯(격노 90초·폐쇄 180초·공격 간격)이 통째로 무너졌다.
             *
             * 검사가 재는 것은 시계이지 생존이 아니므로 체력 풀을 검사 스스로
             * 세운다. 사망 검사는 `Current + 1`을 때리므로 이 값과 무관하게
             * 죽고, 회복 검사는 비율(MaxHealth * 0.4)로 재므로 그대로 성립한다.
             */
            if (health != null) health.MaxHealthStat = 1e12d;

            foreach (var pet in Object.FindObjectsByType<PetCombat>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                pet.enabled = false;

            foreach (var spirit in Object.FindObjectsByType<SpiritSummon>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                spirit.enabled = false;

            foreach (var performer in Object.FindObjectsByType<SkillPerformer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                performer.enabled = false;
        }

        static bool Visible(GameObject go) { return go != null && go.activeInHierarchy; }

        UnityEngine.Object Field(string name)
        {
            var f = typeof(Onikiri.UI.TrialHud).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f == null ? null : f.GetValue(trialHud) as UnityEngine.Object;
        }

        GameObject Root(string name) { return Field(name) as GameObject; }

        static GameObject BossHudRoot(Onikiri.UI.BossHud hud, string name)
        {
            var f = typeof(Onikiri.UI.BossHud).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f == null ? null : f.GetValue(hud) as GameObject;
        }

        // ============================================================ 1. TrialHud 배선

        /**
         * @brief `TrialHud`가 **씬에 실제로 있고 참조가 하나도 비지 않았다.**
         *
         * 클래스를 만든 것은 UI 구현이 아니다. 이 검사가 없으면 컴포넌트가
         * 씬에 없어도 나머지가 전부 통과한다 - 실제로 그 상태로 완료를 보고했다.
         */
        [UnityTest]
        public IEnumerator TrialHud_IsInTheSceneWithEveryReferenceWired()
        {
            Assert.IsNotNull(trialHud, "Main 씬에 TrialHud가 없다 - 클래스만 만들고 배선을 안 했다");

            var names = new[]
            {
                "fight","evolution","bannerRoot","gateLabel","foeLabel","clockLabel",
                "enrageRoot","enrageLabel","noticeRoot","noticeLabel",
                "transitionRoot","transitionLabel","resultRoot","resultTitle","resultDetail",
                // 4단계 §2 - 적 체력 줄
                "healthRoot","healthFill","healthLabel"
            };

            foreach (var name in names)
                Assert.IsNotNull(Field(name), "TrialHud." + name + " 참조가 비어 있다");

            yield return null;
        }

        /** 파밍 중에는 귀문 HUD가 **전부 숨는다** */
        [UnityTest]
        public IEnumerator TrialHud_IsHiddenOutsideTheTrial()
        {
            StandBeforeGateBoss(35);
            yield return null;
            yield return null;

            Assert.IsFalse(Visible(Root("bannerRoot")), "파밍 중에 귀문 띠가 떠 있다");
            Assert.IsFalse(Visible(Root("noticeRoot")), "파밍 중에 귀문 안내가 떠 있다");
            Assert.IsFalse(Visible(Root("transitionRoot")));
            Assert.IsFalse(Visible(Root("resultRoot")));
        }

        /** 상태마다 맞는 것만 뜨고, `BossHud`와 절대 겹치지 않는다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialHud_ShowsEachStateAndNeverOverlapsBossHud()
        {
            var bossHud = Object.FindFirstObjectByType<Onikiri.UI.BossHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(bossHud, "씬에 BossHud가 없다");

            var bossFightRoot = BossHudRoot(bossHud, "fightRoot");
            var bossIntroRoot = BossHudRoot(bossHud, "introRoot");

            StandAtPendingGate(30);
            yield return null;

            fight.ChallengeTrial();

            // 이 검사는 상태를 하나씩 눈으로 확인하므로 **적이 살아 있어야 한다.**
            // 안 멈추면 상위 세이브가 세 적을 프레임 안에 베어 버려서
            // FightingFoe1이 한 프레임도 안 서고, 그 결과가 "띠가 안 떴다"로
            // 나온다 (EnterTrialAtFoeOne 주석과 같은 이유)
            SuspendPlayerDamage();
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Entering, fight.Trial);
            Assert.IsTrue(Visible(Root("noticeRoot")), "진입 안내가 안 떴다");
            Assert.IsFalse(Visible(bossFightRoot), "귀문 중에 보스 전투 HUD가 떠 있다");
            Assert.IsFalse(Visible(bossIntroRoot), "귀문 중에 보스 등장 HUD가 떠 있다");

            yield return WaitForTrial(BossFight.TrialState.FightingFoe1, 30f);
            Assert.IsTrue(Visible(Root("bannerRoot")), "전투 중에 띠가 안 떴다");
            Assert.IsFalse(Visible(Root("noticeRoot")), "전투가 시작됐는데 안내가 남아 있다");
            Assert.IsFalse(Visible(bossFightRoot), "귀문 전투 중에 보스 HUD가 떠 있다");

            var foeLabel = Field("foeLabel") as TMPro.TMP_Text;
            Assert.AreEqual("1/3", foeLabel.text, "적 번호가 1/3이 아니다");

            var clockLabel = Field("clockLabel") as TMPro.TMP_Text;
            Assert.IsNotEmpty(clockLabel.text, "시계가 비어 있다");

            var gateLabel = Field("gateLabel") as TMPro.TMP_Text;
            StringAssert.Contains("일문", gateLabel.text);

            KillCurrentFoe();
            yield return null;
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Transition1, fight.Trial);
            Assert.IsTrue(Visible(Root("transitionRoot")), "전환 표시가 안 떴다");

            yield return WaitForTrial(BossFight.TrialState.FightingFoe2, 30f);
            Assert.AreEqual("2/3", foeLabel.text);
            KillCurrentFoe();
            yield return null;
            yield return WaitForTrial(BossFight.TrialState.FightingFoe3, 30f);
            Assert.AreEqual("3/3", foeLabel.text);
            KillCurrentFoe();
            yield return null;
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Victory, fight.Trial, "승리가 안 났다");
            Assert.IsTrue(Visible(Root("resultRoot")), "결과 화면이 안 떴다");
            Assert.AreEqual("돌파", (Field("resultTitle") as TMPro.TMP_Text).text);

            yield return WaitUntilTrialEnds(60f);

            Assert.IsFalse(Visible(Root("bannerRoot")), "귀문이 끝났는데 띠가 남았다");
            Assert.IsFalse(Visible(Root("resultRoot")), "귀문이 끝났는데 결과가 남았다");
            Assert.IsFalse(Visible(Root("transitionRoot")));
            Assert.IsFalse(Visible(Root("noticeRoot")));
        }

        /** 격노 표시 */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialHud_ShowsEnrageAfterNinetySeconds()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();

            Assert.IsFalse(Visible(Root("enrageRoot")), "90초 전인데 격노가 떠 있다");

            yield return RunSeconds((float)PromotionTrialCatalog.EnrageSeconds + 3f);

            Assert.IsTrue(Visible(Root("enrageRoot")), "90초가 지났는데 격노가 안 떴다");
            StringAssert.Contains("격노", (Field("enrageLabel") as TMPro.TMP_Text).text);
        }

        // ============================================================ 2. 손상 상태

        /**
         * @brief `bossKillCount`가 **stage+1로 부푼** 손상 세이브에서 게이트 보스를 벤다.
         *
         * 절대 일어나면 안 되는 것: 스테이지 상승, 티어 상승, 귀문 건너뛰기.
         * 처음 구현은 등록 실패 시 `AdvanceStage`로 떨어져 문을 통째로 건너뛰었다.
         */
        [UnityTest]
        public IEnumerator CorruptBossKillCount_NeverSkipsTheTrial()
        {
            evolution.DebugSetTier(0);
            progress.SetProgress(30, StageCurve.KillsPerStage, 31, 30);
            yield return null;

            int stageBefore = progress.Stage;
            int tierBefore = evolution.Tier;

            bool registered = progress.RegisterGateBossKill();

            Assert.AreEqual(stageBefore, progress.Stage, "손상 상태에서 스테이지가 올랐다");
            Assert.AreEqual(tierBefore, evolution.Tier, "손상 상태에서 경지가 올랐다");

            // 정규화 정책이므로 등록은 성공하고 귀문이 열려야 한다
            Assert.IsTrue(registered, "실제 처치인데 등록이 실패했다");
            Assert.AreEqual(progress.Stage, progress.BossKillCount,
                "정규화했다면서 bossKillCount가 stage와 다르다");
            Assert.AreEqual(1, progress.PendingTrialGate, "정규화 뒤에도 귀문이 안 열렸다");
            Assert.IsFalse(fight.CanChallenge,
                "귀문이 열렸는데 일반 보스도 도전 가능하다 - 보상 파밍이 열린다");
        }

        // ============================================================ 3. 비활성 → 재활성

        /**
         * @brief 귀문 도중에 껐다 켜면 **시도만 접히고 대기는 남는다.**
         *
         * 배율만 끄면 `phase`가 여전히 `Trial`인데 캡은 꺼진 상태로 다시 켜진다 -
         * 적도 없어서 폐쇄까지 아무 일도 안 일어난다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator DisableThenEnable_AbandonsTheAttemptButKeepsThePendingGate()
        {
            yield return EnterTrialAtFoeOne(30);

            int tierBefore = evolution.Tier;
            int stageBefore = progress.Stage;

            fight.enabled = false;
            yield return null;

            Assert.IsFalse(TrialDamageScale.IsActive, "껐는데 소프트캡이 남았다");
            Assert.AreEqual(BossFight.Phase.Farming, fight.Current, "껐는데 상태가 Trial이다");
            Assert.AreEqual(BossFight.TrialState.Idle, fight.Trial, "껐는데 귀문 상태가 남았다");
            Assert.AreEqual(0, fight.TrialGate, "껐는데 문 번호가 남았다");
            Assert.IsNull(CurrentFoe(), "껐는데 귀문의 적이 필드에 남았다");

            fight.enabled = true;
            yield return null;

            Assert.AreEqual(tierBefore, evolution.Tier, "껐다 켜니 경지가 움직였다");
            Assert.AreEqual(stageBefore, progress.Stage, "껐다 켜니 스테이지가 움직였다");

            Assert.AreEqual(1, progress.PendingTrialGate, "껐다 켜니 귀문 대기가 사라졌다");
            Assert.IsTrue(fight.CanChallengeTrial, "재활성화 뒤 귀문에 도전할 수 없다");
            Assert.IsFalse(fight.CanChallenge, "재활성화 뒤 일반 보스에 도전할 수 있다");

            fight.ChallengeTrial();
            yield return null;
            Assert.AreEqual(BossFight.Phase.Trial, fight.Current, "무료 재도전이 안 열린다");
        }

        // ============================================================ 4. 누락 계약

        /** 사망 실패 → 결과 → 파밍 → 무료 재도전. 일반 보스는 계속 잠긴다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator DeathFailure_ReturnsToFarmingAndAllowsFreeRetryOnly()
        {
            yield return EnterTrialAtFoeOne(30);

            int tierBefore = evolution.Tier;
            int stageBefore = progress.Stage;
            int bossKillsBefore = progress.BossKillCount;
            var goldBefore = PlayerWallet.Instance != null ? PlayerWallet.Instance.Gold : BigDouble.Zero;

            health.TakeDamage(health.Current + 1d);
            yield return null;
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Failure, fight.Trial, "사망이 실패로 안 갔다");

            yield return WaitUntilTrialEnds(60f);

            Assert.AreEqual(BossFight.Phase.Farming, fight.Current, "실패 뒤 파밍으로 안 돌아왔다");
            Assert.IsFalse(TrialDamageScale.IsActive, "실패 뒤 소프트캡이 남았다");

            Assert.AreEqual(tierBefore, evolution.Tier);
            Assert.AreEqual(stageBefore, progress.Stage);
            Assert.AreEqual(bossKillsBefore, progress.BossKillCount);

            if (PlayerWallet.Instance != null)
                Assert.AreEqual(goldBefore.ToDouble(), PlayerWallet.Instance.Gold.ToDouble(),
                    System.Math.Abs(goldBefore.ToDouble()) * 1e-12d,
                    "실패했는데 골드가 움직였다");

            Assert.IsTrue(fight.CanChallengeTrial, "실패 뒤 재도전이 안 열린다");
            Assert.IsFalse(fight.CanChallenge,
                "실패 뒤 일반 보스가 열렸다 - 보상을 반복 파밍할 수 있다");
            Assert.IsTrue(fight.TrialAttempted, "재도전 문구 근거가 안 남았다");
        }

        /** 전환은 **게임 시간 2초**이고 그동안 재생이 실제로 돈다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator Transition_LastsTwoSecondsAndRegenerationContinues()
        {
            yield return EnterTrialAtFoeOne(30);

            health.TakeDamage(health.MaxHealth * 0.4d);
            yield return null;

            KillCurrentFoe();
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Transition1, fight.Trial,
                "전환에 못 들어갔다 - 이 검사는 무시하지 않고 실패시킨다");

            float clockAtStart = fight.TrialClock;
            double healthAtStart = health.Current;

            yield return WaitForTrial(BossFight.TrialState.FightingFoe2, 30f);

            float elapsed = fight.TrialClock - clockAtStart;

            Assert.AreEqual((float)PromotionTrialCatalog.SwapSeconds, elapsed, 0.6f,
                "전환이 2초가 아니다 (실측 " + elapsed.ToString("F2") + "초)");

            Assert.Greater(health.Current, healthAtStart,
                "전환 중에 체력이 안 늘었다 - 재생이 멈췄다");
        }

        /** 전환 중에는 적이 없고 체력이 줄지 않는다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator Transition_SilencesTheFoes()
        {
            yield return EnterTrialAtFoeOne(30);

            KillCurrentFoe();
            yield return null;

            Assert.AreEqual(BossFight.TrialState.Transition1, fight.Trial);
            Assert.IsNull(CurrentFoe(), "전환 중에 적이 서 있다");

            double before = health.Current;
            yield return RunSeconds(1.0f);
            Assert.GreaterOrEqual(health.Current, before, "전환 중에 체력이 줄었다");
        }

        /**
         * @brief 전환 중 오의 쿨타임이 초기화되지 않고 계속 흐른다.
         *
         * ## 이 검사는 한 번 **공허했다** (4단계 §7에서 막았다)
         *
         * 전에는 진행도 합만 비교했다. 그런데 장착 오의가 전부 준비된 상태면
         * 합이 `SlotCount`로 포화돼 있어서, 전환이 쿨타임을 **초기화하든 말든**
         * 전후가 똑같이 최대값이다 - 검사가 통과하지만 아무것도 안 잰 것이다.
         *
         * 그래서 순서를 바꾼다:
         *
         * ```
         * 1. 귀문 1체 전투 진입
         * 2. 실제 시전 경로로 한 칸 시전 (DebugCastNow - Perform을 그대로 탄다)
         * 3. 그 칸의 CooldownFraction < 1 확인          <- 여기가 핵심
         * 4. 적 처치 -> 2초 전환
         * 5. 전환 뒤에도 그 칸이 초기화되지 않았는지 본다
         * ```
         *
         * 3번이 성립하지 않으면 **실패로 떨어진다.** 활성 쿨타임을 못 만들었으면
         * 5번은 잴 것이 없고, 잴 것이 없는 검사가 통과로 집계되는 것이 정확히
         * 이 검사가 고치려는 문제다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator Transition_DoesNotResetSkillCooldowns()
        {
            yield return EnterTrialAtFoeOne(30);

            var performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            Assert.IsNotNull(performer, "씬에 SkillPerformer가 없다");

            var skills = SkillSystem.Instance;
            Assert.IsNotNull(skills, "씬에 SkillSystem이 없다");
            Assert.Greater(skills.SlotCount, 0, "장착 칸이 하나도 없다 - 쿨타임을 만들 수 없다");

            // ---- 2. 실제 시전 경로로 활성 쿨타임을 만든다
            int slot = -1;
            for (int i = 0; i < skills.SlotCount; i++)
                if (skills.DebugCastNow(i)) { slot = i; break; }

            Assert.GreaterOrEqual(slot, 0,
                "어느 칸도 시전되지 않았다 - 활성 쿨타임이 없으면 이 검사는 "
                + "아무것도 재지 못한다(사거리에 적이 없거나 장착 칸이 비었다)");

            // ---- 3. 정말로 쿨타임이 도는가. 여기가 공허함을 막는 자물쇠다
            float castFraction = skills.CooldownFraction(slot);
            Assert.Less(castFraction, 1f,
                "시전 직후인데 " + slot + "번 칸이 이미 준비 상태다 - 쿨타임이 "
                + "돌지 않으면 전환이 초기화해도 알 수 없다 (진행도 "
                + castFraction.ToString("F3") + ")");

            // ---- 4. 적 처치 -> 전환
            KillCurrentFoe();
            yield return null;
            Assert.AreEqual(BossFight.TrialState.Transition1, fight.Trial);

            float slotBefore = skills.CooldownFraction(slot);
            float sumBefore = CooldownProgress();

            yield return WaitForTrial(BossFight.TrialState.FightingFoe2, 30f);

            float slotAfter = skills.CooldownFraction(slot);
            float sumAfter = CooldownProgress();

            // ---- 5. 초기화되면 진행도가 **줄어든다**(0쪽으로 되돌아간다)
            Assert.GreaterOrEqual(slotAfter, slotBefore - 0.01f,
                slot + "번 칸의 쿨타임이 전환을 지나며 되돌아갔다 - 초기화된다 "
                + "(전 " + slotBefore.ToString("F3") + " / 후 " + slotAfter.ToString("F3") + ")");

            Assert.GreaterOrEqual(sumAfter, sumBefore - 0.01f,
                "전환을 지나며 쿨타임 진행도 합이 줄었다 - 어딘가에서 초기화된다 "
                + "(전 " + sumBefore.ToString("F3") + " / 후 " + sumAfter.ToString("F3") + ")");
        }

        /**
         * @brief 장착 오의들의 쿨타임 **진행도 합**. 0에 가까울수록 갓 시전했다.
         *
         * `CooldownFraction`은 1이 "준비됨"이다. 전환을 지나며 쿨타임이
         * 초기화되면 이 합이 **줄어든다**(다시 0쪽으로 간다) - 그래서 검사는
         * "줄지 않았다"를 본다.
         */
        static float CooldownProgress()
        {
            var system = SkillSystem.Instance;
            if (system == null) return 0f;

            float total = 0f;
            for (int i = 0; i < system.SlotCount; i++) total += system.CooldownFraction(i);
            return total;
        }

        /** 격노: 90초 전 0단계, 직후 1단계, 10초마다 누적 */
        [UnityTest, Timeout(300000)]
        public IEnumerator Enrage_StartsAtNinetyAndStacksEveryTenSeconds()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();

            yield return RunSeconds((float)PromotionTrialCatalog.EnrageSeconds - 5f);
            Assert.AreEqual(0, fight.TrialEnrageSteps, "90초 전인데 격노가 올랐다");

            yield return RunSeconds(7f);
            Assert.AreEqual(1, fight.TrialEnrageSteps, "90초 직후 격노가 1단계가 아니다");

            yield return RunSeconds((float)PromotionTrialCatalog.EnrageIntervalSeconds);
            Assert.AreEqual(2, fight.TrialEnrageSteps, "10초 뒤 격노가 2단계가 아니다");

            yield return RunSeconds((float)PromotionTrialCatalog.EnrageIntervalSeconds);
            Assert.AreEqual(3, fight.TrialEnrageSteps, "20초 뒤 격노가 3단계가 아니다");
        }

        /** 180초에 폐쇄로 끝나고 그 뒤 무료 재도전이 열린다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator Close_EndsAtOneEightyAndAllowsFreeRetry()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();

            int tierBefore = evolution.Tier;

            float t = 0f;
            while (fight.Trial != BossFight.TrialState.Closed && t < 220f)
            {
                if (fight.Current != BossFight.Phase.Trial) break;
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(BossFight.TrialState.Closed, fight.Trial, "180초가 지나도 폐쇄되지 않았다");
            Assert.GreaterOrEqual(fight.TrialClock, (float)PromotionTrialCatalog.CloseSeconds - 1f);
            Assert.AreEqual(tierBefore, evolution.Tier, "폐쇄인데 경지가 올랐다");

            yield return WaitUntilTrialEnds(60f);

            Assert.IsFalse(TrialDamageScale.IsActive, "폐쇄 뒤 소프트캡이 남았다");
            Assert.IsTrue(fight.CanChallengeTrial, "폐쇄 뒤 재도전이 안 열린다");
        }

        /**
         * @brief 적의 첫 공격은 **2초 뒤**, 이후 간격도 **2초**.
         *
         * 체력 변화로 재려다 실패했다 - st175 세이브의 재생이 st30 보스의
         * 피해를 즉시 메워서 `Current`가 눈에 띄게 안 줄었다. 그 자는
         * 플레이어의 강화 상태에 의존한다.
         *
         * 그래서 **적의 `Attacked` 사건**을 직접 듣는다. 피해가 얼마든,
         * 재생이 얼마나 빠르든 때린 시각은 같은 값이다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialFoe_FirstAttackAndIntervalAreTwoSeconds()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();

            var foe = CurrentFoe();
            Assert.IsNotNull(foe, "적이 없다");

            var hits = new System.Collections.Generic.List<float>();
            float spawnClock = fight.TrialClock;

            System.Action<Enemy> onAttack = _ => hits.Add(fight.TrialClock - spawnClock);
            foe.Attacked += onAttack;

            float t = 0f;
            while (t < 14f && hits.Count < 3)
            { t += Time.deltaTime; yield return null; }

            foe.Attacked -= onAttack;

            Assert.GreaterOrEqual(hits.Count, 2,
                "적이 두 번도 안 때렸다 (관측 " + hits.Count + "회)");

            Assert.AreEqual((float)PromotionTrialCatalog.FirstAttackDelaySeconds, hits[0], 0.8f,
                "첫 공격이 2초가 아니다 (실측 " + hits[0].ToString("F2") + "초)");

            Assert.AreEqual((float)PromotionTrialCatalog.FoeAttackIntervalSeconds,
                hits[1] - hits[0], 0.8f,
                "공격 간격이 2초가 아니다 (실측 " + (hits[1] - hits[0]).ToString("F2") + "초)");
        }

        /** 귀문 적: 골드·경험치·퀘스트·할당량·보스 처치 수 전부 0 */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialFoes_GrantNoRewardOfAnyKind()
        {
            yield return EnterTrialAtFoeOne(30);

            var wallet = PlayerWallet.Instance;
            var character = CharacterLevel.Instance;
            var quests = QuestSystem.Instance;

            var goldBefore = wallet != null ? wallet.Gold : BigDouble.Zero;
            var expBefore = character != null ? character.Exp : BigDouble.Zero;
            int killsBefore = progress.KillsThisStage;
            int bossKillsBefore = progress.BossKillCount;
            double questBefore = QuestBossProgress(quests);

            KillCurrentFoe();
            yield return null;
            yield return WaitForTrial(BossFight.TrialState.FightingFoe2, 30f);
            KillCurrentFoe();
            yield return null;
            yield return WaitForTrial(BossFight.TrialState.FightingFoe3, 30f);

            if (wallet != null)
                Assert.AreEqual(goldBefore.ToDouble(), wallet.Gold.ToDouble(),
                    System.Math.Abs(goldBefore.ToDouble()) * 1e-12d, "귀문의 적이 골드를 줬다");

            if (character != null)
                Assert.AreEqual(expBefore.ToDouble(), character.Exp.ToDouble(),
                    System.Math.Abs(expBefore.ToDouble()) * 1e-12d, "귀문의 적이 경험치를 줬다");

            Assert.AreEqual(killsBefore, progress.KillsThisStage, "귀문의 적이 할당량에 들어갔다");
            Assert.AreEqual(bossKillsBefore, progress.BossKillCount, "귀문의 적이 보스 처치 수를 올렸다");
            Assert.AreEqual(questBefore, QuestBossProgress(quests), 1e-9d,
                "귀문의 적이 퀘스트 카운터를 올렸다");
        }

        static double QuestBossProgress(QuestSystem quests)
        {
            if (quests == null) return 0d;

            double total = 0d;
            foreach (var kind in new[] { QuestKind.Daily, QuestKind.Repeat })
                for (int i = 0; i < QuestCatalog.Of(kind).Length; i++)
                    total += quests.ProgressOf(kind, i);
            return total;
        }

        // ============================================================ 피해 감사

        /**
         * @brief **표시 피해 = 실제 차감 피해.** 그리고 출처마다 정확히 한 번.
         *
         * 알려진 값으로 잰다 - 실제 전투로 재면 치명타·연격의 난수가 섞여
         * 등식이 성립하는지 아닌지를 구분할 수 없다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator DamageAudit_AppliedEqualsShownAndEachSourceScalesOnce()
        {
            yield return EnterTrialAtFoeOne(30);

            var foe = CurrentFoe();
            Assert.IsNotNull(foe);
            Assert.IsTrue(TrialDamageScale.IsActive, "귀문인데 배율이 꺼져 있다");

            double scale = TrialDamageScale.Current;

            var sources = new[]
            {
                TrialDamageScale.Source.AutoAttack,
                TrialDamageScale.Source.Skill,
                TrialDamageScale.Source.Companion,
                TrialDamageScale.Source.Special
            };

            long baseHits = TrialDamageScale.HitCount;
            var raw = BigDouble.FromDouble(1000d);

            foreach (var source in sources)
            {
                var applied = foe.TakeDamage(raw, source);

                // 반환값이 곧 화면에 뜨는 숫자다 - 배율을 정확히 한 번 지났다
                Assert.AreEqual(1000d * scale, applied.ToDouble(), 1000d * scale * 1e-9d,
                    source + ": 반환된 피해가 배율을 한 번 지나지 않았다");
            }

            Assert.AreEqual(baseHits + 4L, TrialDamageScale.HitCount);
            Assert.AreEqual(0L, TrialDamageScale.HitsOf(TrialDamageScale.Source.Unattributed),
                "출처를 안 밝힌 피해가 있다");

            foreach (var source in sources)
                Assert.GreaterOrEqual(TrialDamageScale.HitsOf(source), 1L,
                    source + "의 타격이 장부에 안 들어갔다");

            // 총합의 비가 배율과 같다 - 어딘가에서 두 번 걸리면 여기서 갈린다
            Assert.AreEqual(scale,
                TrialDamageScale.ScaledTotal.ToDouble() / TrialDamageScale.RawTotal.ToDouble(), 1e-9d,
                "총합의 비가 배율과 다르다 - 이중 적용이나 우회가 있다");
        }

        /** 귀문 **밖**에서는 배율이 안 걸리고 장부도 안 돈다 (회귀) */
        [UnityTest, Timeout(300000)]
        public IEnumerator OrdinaryCombat_DamageIsUnscaled()
        {
            StandBeforeGateBoss(35);
            yield return null;

            Assert.IsFalse(TrialDamageScale.IsActive);

            Enemy mob = null;
            float t = 0f;
            while (mob == null && t < 30f)
            {
                foreach (var e in spawner.Active)
                    if (e != null && e.IsAlive && !e.IsTrialFoe) { mob = e; break; }
                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(mob, "잡몹이 안 나왔다");

            var applied = mob.TakeDamage(BigDouble.FromDouble(1d),
                                         TrialDamageScale.Source.AutoAttack);

            Assert.AreEqual(1d, applied.ToDouble(), 0d, "귀문 밖인데 피해가 보정됐다");
            Assert.AreEqual(0L, TrialDamageScale.HitCount,
                "귀문 밖의 피해가 감사 장부에 들어갔다");
        }

        // ============================================================ 회귀·D-4

        [UnityTest]
        public IEnumerator OrdinaryStage_IsUnchanged()
        {
            evolution.DebugSetTier(1);
            progress.SetProgress(35, StageCurve.KillsPerStage, 34, 35);
            yield return null;

            Assert.AreEqual(0, progress.PendingTrialGate);
            Assert.IsTrue(fight.CanChallenge);
            Assert.IsFalse(fight.CanChallengeTrial);
            Assert.IsFalse(TrialDamageScale.IsActive);

            int stageBefore = progress.Stage;
            int killsBefore = progress.BossKillCount;

            progress.AdvanceStage();

            Assert.AreEqual(stageBefore + 1, progress.Stage);
            Assert.AreEqual(killsBefore + 1, progress.BossKillCount);
        }

        [UnityTest]
        public IEnumerator PendingTrial_IsDerivedFromSavedProgress()
        {
            StandBeforeGateBoss(30);
            yield return null;

            Assert.AreEqual(0, progress.PendingTrialGate);
            Assert.IsTrue(fight.CanChallenge);
            Assert.IsFalse(fight.CanChallengeTrial);

            StandAtPendingGate(30);
            yield return null;

            Assert.AreEqual(1, progress.PendingTrialGate);
            Assert.IsTrue(fight.CanChallengeTrial);
            Assert.IsFalse(fight.CanChallenge, "귀문 대기 중인데 일반 보스에 도전할 수 있다");
        }

        [UnityTest]
        public IEnumerator GateBossRegistration_IsIdempotent()
        {
            StandBeforeGateBoss(40);
            yield return null;

            Assert.IsTrue(progress.RegisterGateBossKill());
            int after = progress.BossKillCount;
            Assert.AreEqual(40, after);

            Assert.IsTrue(progress.RegisterGateBossKill());
            Assert.AreEqual(after, progress.BossKillCount, "중복 콜백이 bossKillCount를 올렸다");
            Assert.AreEqual(2, progress.PendingTrialGate);
        }

        [UnityTest]
        public IEnumerator GateBossRegistration_RefusesWhenTheQuotaIsUnmet()
        {
            evolution.DebugSetTier(0);
            progress.SetProgress(30, 3, 29, 30);
            yield return null;

            Assert.IsFalse(progress.RegisterGateBossKill());
            Assert.AreEqual(29, progress.BossKillCount);
            Assert.AreEqual(0, progress.PendingTrialGate);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Trial_GrantsExactlyOneTierAndOneStage()
        {
            yield return EnterTrialAtFoeOne(30);

            int tierBefore = evolution.Tier;
            int stageBefore = progress.Stage;
            int bossKillsBefore = progress.BossKillCount;

            for (int i = 0; i < PromotionTrialCatalog.FoeCount; i++)
            {
                float t = 0f;
                while (CurrentFoe() == null && t < 30f) { t += Time.deltaTime; yield return null; }
                Assert.IsNotNull(CurrentFoe(), "적 " + (i + 1) + "체가 서지 않았다");

                KillCurrentFoe();
                yield return null;
                yield return null;
            }

            Assert.AreEqual(BossFight.TrialState.Victory, fight.Trial, "승리가 안 났다");
            Assert.AreEqual(tierBefore + 1, evolution.Tier, "경지가 정확히 한 칸 안 올랐다");
            Assert.AreEqual(stageBefore + 1, progress.Stage, "스테이지가 정확히 한 칸 안 올랐다");
            Assert.AreEqual(bossKillsBefore, progress.BossKillCount, "귀문이 bossKillCount를 올렸다");

            yield return WaitUntilTrialEnds(60f);
            Assert.IsFalse(TrialDamageScale.IsActive, "승리 뒤 소프트캡이 남았다");
        }

        [UnityTest]
        public IEnumerator ExitIsIdempotent()
        {
            TrialDamageScale.Enter(2000d, 1000d, 0.45d);
            Assert.IsTrue(TrialDamageScale.IsActive);

            TrialDamageScale.Exit();
            TrialDamageScale.Exit();
            TrialDamageScale.Exit();

            Assert.IsFalse(TrialDamageScale.IsActive);
            Assert.AreEqual(1d, TrialDamageScale.Current, 0d);
            yield return null;
        }

        // ============================================================ 4단계

        // ---------------------------------------------------------- 배선 고정 (§6)

        /** 아무 컴포넌트의 private 직렬화 필드 하나 */
        static UnityEngine.Object Ref(object target, string field)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f == null ? null : f.GetValue(target) as UnityEngine.Object;
        }

        static UnityEngine.Object[] RefArray(object target, string field)
        {
            if (target == null) return null;
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f == null ? null : f.GetValue(target) as UnityEngine.Object[];
        }

        static Onikiri.UI.LockedTab TabNamed(string display)
        {
            foreach (var tab in Object.FindObjectsByType<Onikiri.UI.LockedTab>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var f = typeof(Onikiri.UI.LockedTab).GetField("displayName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null && (string)f.GetValue(tab) == display) return tab;
            }
            return null;
        }

        /**
         * @brief 스킬 탭이 **실제로 스킬 화면을 연다** (4단계 §6).
         *
         * 3단계 실기에서 이 참조가 `null`인 채로 잡혔다. `LockedTab`의 계약이
         * "`screen`이 비면 잠금 표시만 하고 눌리지 않는다"라, 끊기면 **콘솔 한 줄
         * 없이** 탭이 죽는다 - 사람이 눌러 보기 전에는 아무도 모른다.
         *
         * 원인은 `SkillPanelBuilder`만 `RelinkScreenTabs()`를 안 부른 것이었고,
         * 그 호출은 되살렸다. 이 검사는 그것이 **다시 빠지는 날** 울린다.
         */
        [UnityTest]
        public IEnumerator SkillTab_OpensTheSkillPanel()
        {
            var tab = TabNamed("스킬");
            Assert.IsNotNull(tab, "하단 바에 스킬 탭이 없다");

            var screen = Ref(tab, "screen") as GameObject;
            Assert.IsNotNull(screen,
                "스킬 탭의 screen이 비어 있다 - 탭은 밝은데 눌러도 아무 일이 안 일어난다");
            Assert.AreEqual("SkillPanel", screen.name, "스킬 탭이 엉뚱한 화면을 가리킨다");

            yield return null;
        }

        /**
         * @brief 상호 배타 목록에 **구멍이 없다** (4단계 §6).
         *
         * 죽은 참조는 `screen`에만 남지 않는다. 판이 다시 만들어지면 **남들의
         * `otherScreens`**에도 `null` 한 칸이 남고, 그 칸이 원래 가리키던 화면은
         * 다른 탭을 열어도 안 닫힌다 - 두 화면이 겹쳐 뜬다.
         */
        [UnityTest]
        public IEnumerator EveryTab_ClosesEveryOtherScreen()
        {
            var tabs = Object.FindObjectsByType<Onikiri.UI.LockedTab>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(tabs.Length, 0, "씬에 LockedTab이 없다");

            bool sawSkillPanel = false;

            foreach (var tab in tabs)
            {
                var others = RefArray(tab, "otherScreens");
                if (others == null) continue;

                var display = typeof(Onikiri.UI.LockedTab).GetField("displayName",
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance).GetValue(tab) as string;

                for (int i = 0; i < others.Length; i++)
                {
                    Assert.IsNotNull(others[i],
                        "'" + display + "' 탭의 otherScreens[" + i + "]가 비어 있다 - "
                        + "그 화면은 이 탭을 열어도 안 닫힌다");

                    if (others[i].name == "SkillPanel") sawSkillPanel = true;
                }
            }

            Assert.IsTrue(sawSkillPanel,
                "어느 탭도 SkillPanel을 닫지 않는다 - 스킬 화면이 다른 화면 위에 남는다");

            yield return null;
        }

        /**
         * @brief 오의 연출 둘이 배선돼 있다 (4단계 §6).
         *
         * 스킬 탭과 **같은 사고로 함께 끊겼던** 참조다(3단계 실기 §9.9). 증상이
         * 없어서 - 이름 번쩍임과 화면 번쩍임이 그냥 안 나올 뿐이라 - 사용자가
         * 탭을 지적하지 않았으면 묻힐 뻔했다.
         */
        [UnityTest]
        public IEnumerator SkillPerformer_HasBothFlashesWired()
        {
            var performer = Object.FindFirstObjectByType<SkillPerformer>(FindObjectsInactive.Include);
            Assert.IsNotNull(performer, "씬에 SkillPerformer가 없다");

            Assert.IsNotNull(Ref(performer, "nameFlash"),
                "SkillPerformer.nameFlash가 비어 있다 - 오의 이름이 안 뜬다");
            Assert.IsNotNull(Ref(performer, "screenFlash"),
                "SkillPerformer.screenFlash가 비어 있다 - 화면 번쩍임이 안 난다");

            yield return null;
        }

        /**
         * @brief 빌더를 여러 번 돌려도 **오브젝트가 하나뿐이다** (4단계 §6).
         *
         * 빌더는 멱등이어야 하는데(`Replace`가 중복을 치운다) 그 성질은 코드를
         * 봐서는 확인되지 않는다 - 새 오브젝트를 `Replace` 없이 추가한 날
         * 조용히 둘이 되고, 화면에서는 겹쳐 보이거나 나중 것이 앞의 것을 가린다.
         *
         * 씬에 굳은 결과를 세는 것으로 그것을 잡는다.
         */
        [UnityTest]
        public IEnumerator SceneHasNoDuplicatedTrialObjects()
        {
            var unique = new[]
            {
                "TrialBanner", "TrialTransition", "TrialNotice", "TrialResult",
                "TrialHealth", "GuideQuestCard"
            };

            foreach (var name in unique)
            {
                int count = 0;
                foreach (var t in Object.FindObjectsByType<Transform>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == name) count++;

                Assert.AreEqual(1, count,
                    "'" + name + "'이(가) 씬에 " + count + "개다 - 빌더가 멱등이 아니다");
            }

            yield return null;
        }

        // ---------------------------------------------------------- 적 체력 바 (§2)

        /**
         * @brief 체력 바가 **지금 상대**를 따라간다.
         *
         * 세 가지를 본다: 싸울 때만 뜬다 · 깎으면 줄어든다 · 다음 적이 서면
         * 가득 찬 상태로 다시 시작한다. 셋째가 중요하다 - 참조가 첫 적에
         * 붙박이면 2체부터 바가 0에 멈춰 "이미 이겼다"로 읽힌다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialHealthBar_FollowsTheCurrentFoe()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();
            yield return null;

            var healthRoot = Root("healthRoot");
            Assert.IsTrue(Visible(healthRoot), "1체와 싸우는데 체력 줄이 안 보인다");
            Assert.AreEqual(1f, fight.TrialFoeHealthFraction, 0.05f, "갓 선 적이 이미 깎여 있다");

            // ---- 깎으면 줄어든다
            var foe = CurrentFoe();
            Assert.IsNotNull(foe, "1체가 없다");

            // 소프트캡을 되돌린 절반. 그냥 0.5를 넣으면 배율이 작을 때
            // 아무것도 안 깎여 "바가 안 움직인다"로 오진된다(KillCurrentFoe 주석)
            double undo = TrialDamageScale.IsActive && TrialDamageScale.Current > 0d
                ? 1d / TrialDamageScale.Current : 1d;
            foe.TakeDamage(foe.MaxHealth * BigDouble.FromDouble(0.5d * undo),
                           TrialDamageScale.Source.AutoAttack);
            yield return null;

            Assert.Less(fight.TrialFoeHealthFraction, 0.95f, "피해를 줬는데 바가 그대로다");

            // ---- 전환에는 숨는다
            KillCurrentFoe();
            yield return null;
            Assert.AreEqual(BossFight.TrialState.Transition1, fight.Trial);
            Assert.IsFalse(Visible(Root("healthRoot")),
                "전환 중에 체력 줄이 떠 있다 - 상대가 없어 0으로 보이면 이긴 것처럼 읽힌다");

            // ---- 2체가 서면 가득 찬 채로 다시 시작한다
            yield return WaitForTrial(BossFight.TrialState.FightingFoe2, 30f);
            yield return null;

            Assert.IsTrue(Visible(Root("healthRoot")), "2체와 싸우는데 체력 줄이 안 보인다");
            Assert.AreEqual(1f, fight.TrialFoeHealthFraction, 0.05f,
                "2체의 바가 가득 차 있지 않다 - 바가 1체에 붙박여 있다");
        }

        // ---------------------------------------------------------- 결과 (§4)

        /** 실패 결과가 **원인과 무료 재도전**을 말한다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator TrialResult_TellsTheReasonAndThatNothingWasLost()
        {
            yield return EnterTrialAtFoeOne(30);
            SuspendPlayerDamage();

            // 폐쇄를 **시계로** 낸다. 상태를 직접 밀어 넣으면 화면이 실제
            // 경로에서 나오는지 알 수 없다(Close_ 검사와 같은 규칙)
            float t = 0f;
            while (fight.Trial != BossFight.TrialState.Closed && t < 220f)
            {
                if (fight.Current != BossFight.Phase.Trial) break;
                t += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(BossFight.TrialState.Closed, fight.Trial, "폐쇄가 안 났다");

            var title = Field("resultTitle") as TMPro.TMP_Text;
            var detail = Field("resultDetail") as TMPro.TMP_Text;
            Assert.IsNotNull(title); Assert.IsNotNull(detail);

            Assert.AreEqual("귀문 실패", title.text, "실패 제목이 하나로 모이지 않았다");
            StringAssert.Contains("시간이 다 됐다", detail.text, "폐쇄의 원인이 안 적혔다");
            StringAssert.Contains("무료 재도전", detail.text, "무료 재도전이 안 적혔다");
        }

        // ---------------------------------------------------------- 가이드 카드 (§3)

        /**
         * @brief 귀문 입구가 **가이드 카드 하나**다.
         *
         * 셋을 본다: 카드가 문 이름을 적는가 · 카드를 누르면 진짜로 들어가는가 ·
         * 그동안 하단 도전 버튼이 잠겨 있는가. 셋째가 "중복 버튼 금지"의 검사다.
         */
        [UnityTest, Timeout(300000)]
        public IEnumerator GuideCard_IsTheOnlyTrialEntrance()
        {
            var card = Object.FindFirstObjectByType<Onikiri.UI.GuideQuestCard>(
                FindObjectsInactive.Include);
            Assert.IsNotNull(card, "씬에 GuideQuestCard가 없다");

            Assert.IsNotNull(Ref(card, "fight"),
                "가이드 카드에 BossFight가 안 물렸다 - 귀문 입구가 사라진다");
            Assert.IsNotNull(Ref(card, "cardButton"), "가이드 카드의 버튼이 안 물렸다");
            Assert.IsNotNull(Ref(card, "cardScreenButton"),
                "가이드 카드의 화면 열기가 안 물렸다 - 한 번의 탭에 퀘스트 화면이 함께 열린다");

            // ---- 귀문 대기 상태를 만든다
            StandAtPendingGate(30);
            yield return null;
            yield return null;

            Assert.IsTrue(fight.CanChallengeTrial, "귀문 대기가 안 만들어졌다");

            var detail = Ref(card, "detailLabel") as TMPro.TMP_Text;
            Assert.IsNotNull(detail);
            StringAssert.Contains("일문", detail.text, "카드가 문 이름을 안 적었다");
            StringAssert.Contains("도전", detail.text, "카드가 도전을 안 적었다");

            // ---- 하단 도전 버튼은 잠긴 채다 (중복 입구 금지)
            var bossHud = Object.FindFirstObjectByType<Onikiri.UI.BossHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(bossHud);
            Assert.IsFalse(Visible(BossHudRoot(bossHud, "challengeRoot")),
                "귀문 대기 중에 보스 도전 버튼이 떠 있다 - 입구가 둘이 됐다");

            // ---- 카드를 누르면 실제로 들어간다
            var button = Ref(card, "cardButton") as UnityEngine.UI.Button;
            button.onClick.Invoke();

            // 누른 그 프레임에 화력을 멈춘다 - 안 그러면 귀문이 열리자마자
            // 끝나 버려서 "안 열렸다"와 구분되지 않는다(EnterTrialAtFoeOne 주석)
            SuspendPlayerDamage();

            yield return WaitForTrial(BossFight.TrialState.FightingFoe1, 30f);

            Assert.AreEqual(BossFight.Phase.Trial, fight.Current,
                "가이드 카드를 눌렀는데 귀문이 안 열렸다");

            /**
             * @brief 그 한 번의 탭에 **퀘스트 화면이 함께 열리지 않았다.**
             *
             * 실기에서 이것이 났다. 카드 리스너가 먼저 돌아 귀문을 열면
             * `BossFight.Changed`가 같은 콜스택에서 억제를 풀었고, 뒤이어 도는
             * `HudScreenButton`이 퀘스트 화면을 열었다 - 귀문 띠와 퀘스트 목록이
             * 한 화면에 겹쳐 떴다.
             */
            var questScreen = Ref(Ref(card, "cardScreenButton"), "screen") as GameObject;
            Assert.IsNotNull(questScreen, "가이드 카드가 여는 퀘스트 화면이 안 물렸다");
            Assert.IsFalse(questScreen.activeInHierarchy,
                "귀문을 여는 탭 한 번에 퀘스트 화면까지 열렸다 - 입구가 둘로 갈렸다");
        }

        // ---------------------------------------------------------- 경지 표 (§5)

        /** 경지 표가 **지금 문의 상태**를 적는다 */
        [UnityTest, Timeout(300000)]
        public IEnumerator EvolutionPanel_SaysWhetherTheGateIsOpen()
        {
            var panel = Object.FindFirstObjectByType<Onikiri.UI.EvolutionPanel>(
                FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "씬에 EvolutionPanel이 없다");
            Assert.IsNotNull(Ref(panel, "fight"),
                "경지 표에 BossFight가 안 물렸다 - 문이 열려도 '돌파'인 채로 남는다");

            var cost = Ref(panel, "evolveCost") as TMPro.TMP_Text;
            Assert.IsNotNull(cost);

            // 전직 페이지는 **꺼진 채로 저장된다**(성장 패널의 셋째 탭). 켜지
            // 않으면 Start/OnEnable이 안 돌아 표가 한 글자도 안 그려지고,
            // 그 빈 문자열을 "안 적혔다"로 읽으면 없는 결함을 좇게 된다
            panel.gameObject.SetActive(true);
            yield return null;

            // ---- 아직 못 간 층: 요구 게이트를 적는다
            StandBeforeGateBoss(30);
            yield return null;
            yield return null;
            StringAssert.Contains("st", cost.text, "요구 게이트가 안 적혔다: '" + cost.text + "'");

            // ---- 문이 열렸다
            StandAtPendingGate(30);
            yield return null;
            yield return null;
            StringAssert.Contains("열림", cost.text,
                "문이 열렸는데 경지 표가 그대로다: '" + cost.text + "'");
        }
    }
}
