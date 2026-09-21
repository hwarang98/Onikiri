using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using Onikiri.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Onikiri.Tests.PlayMode
{
    /**
     * @brief 단일 활성 기기의 **두 화면**을 실제 씬에서 잰다 (63단계).
     *
     * 서버는 가짜다(`CloudSaveSession.UsePeekForTests` · `UseWriteForTests`) -
     * 이 검사가 필요로 하는 것은 네트워크가 아니라 **"다른 기기가 살아 있다"는
     * 한 장면**이고, 그래서 운영 프로젝트에 닿지 않는다.
     *
     * ## 이 파일이 재는 것
     *
     *   새 기기(B)   인수 확인 팝업이 뜬다 · 취소가 안전하다 · 연타해도 요청은 하나
     *   이전 기기(A) 감지 즉시 전투·재화가 멈춘다 · 종료 팝업이 한 번 · 타이틀로 ·
     *                Firebase 로그인은 그대로
     *
     * 씬 재로드는 두 팝업 모두 `SuppressReloadForTests`로 멈춘 채 잰다
     * (`CloudConflictPanel`과 같은 규칙).
     */
    public class CloudSessionPlayTests
    {
        const string MainScene = "Assets/_Project/Scenes/Main.unity";
        const string TestUid = "test-uid-63";

        static readonly string OtherSession = new string('7', 32);
        static readonly string OtherDevice = new string('8', 32);

        SaveSandbox sandbox;
        GameSession session;
        CloudTakeoverPanel takeoverPanel;
        CloudEvictedPanel evictedPanel;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            sandbox = new SaveSandbox();
            SaveSystem.Save(Local());

            CloudSaveCoordinator.ResetForTests();
            CloudSaveCoordinator.UseIdentityForTests(TestUid);
            CloudSaveSync.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveTakeover.ResetForTests();
            CloudSavePlayLock.ResetForTests();

            CloudTakeoverPanel.SuppressReloadForTests = true;
            CloudEvictedPanel.SuppressReloadForTests = true;
            CloudEvictedPanel.LeftToTitle = false;

            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Single);
            yield return null;

            session = Object.FindFirstObjectByType<GameSession>(FindObjectsInactive.Include);
            takeoverPanel = Object.FindFirstObjectByType<CloudTakeoverPanel>(FindObjectsInactive.Include);
            evictedPanel = Object.FindFirstObjectByType<CloudEvictedPanel>(FindObjectsInactive.Include);

            Assert.IsNotNull(session, "씬에 GameSession이 없다");
            Assert.IsNotNull(takeoverPanel,
                "씬에 CloudTakeoverPanel이 없다 - Build Hud Screens를 돌렸는가");
            Assert.IsNotNull(evictedPanel, "씬에 CloudEvictedPanel이 없다");
        }

        [TearDown]
        public void Restore()
        {
            // 시간·오디오 잔재를 끊는다. 이 클래스는 PlayLock이 timeScale을
            // 0으로 내리므로 **특히** 그렇다 - 그대로 두면 다음 클래스(귀문)의
            // 게임 시간이 안 흘러 장시간 검사 다섯이 무너진다(60단계 실측)
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (session != null) Object.DestroyImmediate(session);

            CloudSavePlayLock.ResetForTests();
            CloudSaveTakeover.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();

            CloudTakeoverPanel.SuppressReloadForTests = false;
            CloudEvictedPanel.SuppressReloadForTests = false;
            CloudEvictedPanel.LeftToTitle = false;

            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ---------------------------------------------------------------- 새 기기 (B)

        /** ★★ **⑮ 다른 기기가 살아 있으면 인수 확인 팝업이 뜬다** */
        [UnityTest]
        public IEnumerator TheNewDeviceIsAskedBeforeTakingOver()
        {
            yield return RunGateUntilAsked();

            Assert.AreEqual(CloudSaveTakeoverPhase.AwaitingConfirm, CloudSaveTakeover.Phase);
            Assert.IsTrue(takeoverPanel.IsShowing,
                "묻지 않고 지나가면 그것이 '최신 기기 우선'이다");
        }

        /**
         * ★★ **⑯ 취소하면 안전하게 돌아간다.**
         *
         * 세션 문서에 아무것도 안 썼으므로 되돌릴 것이 없다. 게임은 시작되지
         * 않고("취소하고 로컬로 논다"가 곧 62단계의 갈라짐이다), 화면에는
         * "다시 확인"이 남는다.
         */
        [UnityTest]
        public IEnumerator CancellingReturnsSafely()
        {
            var gate = RunGateUntilAsked();
            yield return gate;

            int writes = sessionWrites;

            takeoverPanel.OnCancel();
            yield return null;

            Assert.AreEqual(CloudSaveTakeoverPhase.Cancelled, CloudSaveTakeover.Phase);
            Assert.AreEqual(writes, sessionWrites, "취소가 세션 문서를 건드렸다");
            Assert.IsFalse(CloudSaveTakeover.MayStartGame, "취소하고 게임에 들어가면 안 된다");
            Assert.IsTrue(takeoverPanel.IsShowing, "다시 확인이 남아야 한다");

            Assert.IsFalse(CloudSavePlayLock.Locked, "게임 상태는 그대로다");
        }

        /** ★★ **⑰ 이어하기를 연타해도 요청은 한 번만 나간다** */
        [UnityTest]
        public IEnumerator ConfirmingTwiceSendsOneRequest()
        {
            var gate = RunGateUntilAsked();
            yield return gate;

            takeoverPanel.OnConfirm();
            takeoverPanel.OnConfirm();
            takeoverPanel.OnConfirm();

            // 국면이 Requesting을 지나 Waiting에 설 때까지 돌린다
            float deadline = Time.realtimeSinceStartup + 5f;
            while (CloudSaveTakeover.Phase == CloudSaveTakeoverPhase.AwaitingConfirm
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, requestCount,
                "두 번째 요청은 takeoverAt을 갱신해 강제 인수 시계를 처음부터 돌린다");
        }

        // ---------------------------------------------------------------- 이전 기기 (A)

        /**
         * ★★ **⑱ 감지 즉시 전투와 재화 변경이 멈춘다.**
         *
         * 팝업이 그려지기 전에 이미 멈춰 있어야 한다 - 그 사이 한두 프레임에
         * 눌린 10연은 올라가지 못하는 지불이 된다.
         */
        [UnityTest]
        public IEnumerator LosingTheSeatStopsCombatAndCurrencyAtOnce()
        {
            yield return WaitForBoot();

            var gems = GemWallet.Instance;
            var gold = PlayerWallet.Instance;
            var stage = Object.FindFirstObjectByType<StageProgress>(FindObjectsInactive.Include);

            Assert.IsNotNull(gems);
            Assert.IsNotNull(gold);
            Assert.IsNotNull(stage);

            gems.Add(500L);
            long gemsBefore = gems.Gems;
            int killsBefore = stage.KillsThisStage;

            LoseTheSeat();

            gems.Add(1000L);
            gold.Add(Onikiri.Core.BigDouble.FromDouble(1000d));
            stage.RegisterKill();
            stage.RegisterKill();

            Assert.IsTrue(CloudSavePlayLock.Locked);
            Assert.AreEqual(gemsBefore, gems.Gems, "회수 뒤에 보석이 늘었다");
            Assert.AreEqual(killsBefore, stage.KillsThisStage, "회수 뒤에 전투가 진행됐다");
            Assert.IsFalse(gems.TrySpend(1L), "회수 뒤에 지불이 성사됐다");
            Assert.AreEqual(0f, Time.timeScale, "화면이 계속 싸우면 사람에게 거짓을 보여 준다");

            yield return null;
        }

        /** ★★ **⑨ 회수 뒤에는 로컬 저장도 클라우드 후보 등록도 없다** */
        [UnityTest]
        public IEnumerator ARevokedDeviceNeitherSavesNorQueuesAnUpload()
        {
            yield return WaitForBoot();

            LoseTheSeat();

            Assert.IsFalse(session.Save(), "회수 뒤의 저장이 성공으로 보고됐다");

            // 회수 **전에** 등록된 후보는 그대로 있다(부팅이 한 번 저장한다).
            // 재는 것은 "그 뒤로 새 후보가 붙는가"와 "커밋이 나가는가"다
            var fresh = SaveData.NewGame();
            fresh.stage = 999;
            fresh.maxStageReached = 999;
            CloudSaveSync.NoteSaved(fresh);

            CloudSaveSync.SuppressCommitForTests = true;
            CloudSaveSync.RequestUrgent("검사");
            CloudSaveSync.Tick();

            Assert.AreEqual(0, CloudSaveSync.CommitAttemptsForTests,
                "회수된 기기가 서버로 커밋을 띄웠다");
            Assert.IsFalse(CloudSaveSync.IsUrgentForTests,
                "회수된 기기의 urgent 예약이 서 버렸다");
            Assert.IsFalse(CloudSaveSession.HoldsWrite, "회수됐는데 작성권이 남아 있다");

            yield return null;
        }

        /** ★★ **⑲ 종료 팝업은 한 번만 뜬다** - 폴링은 매 주기 같은 상실을 다시 본다 */
        [UnityTest]
        public IEnumerator TheEvictionNoticeAppearsExactlyOnce()
        {
            yield return WaitForBoot();

            int shown = 0;
            CloudSavePlayLock.Engaged += () => shown++;

            LoseTheSeat();
            LoseTheSeat();
            LoseTheSeat();

            yield return null;

            Assert.AreEqual(1, shown);
            Assert.IsTrue(evictedPanel.IsShowing, "종료 팝업이 안 떴다");
        }

        /**
         * ★★ **⑳㉑ 확인하면 타이틀로 가고, Firebase 로그인은 유지된다.**
         *
         * 회수하는 것은 권한이지 계정이 아니다. 강제 로그아웃하면 그 기기는
         * 익명 uid를 새로 받고, 그 순간 이 계정의 세이브에 영영 닿지 못한다.
         */
        [UnityTest]
        public IEnumerator ConfirmingLeavesToTitleWithTheLoginIntact()
        {
            yield return WaitForBoot();

            string uidBefore = CloudScores.Uid;

            LoseTheSeat();
            yield return null;

            evictedPanel.OnConfirm();
            yield return null;

            Assert.IsTrue(CloudEvictedPanel.LeftToTitle, "확인이 타이틀로 보내지 않았다");
            Assert.IsFalse(CloudSavePlayLock.Locked,
                "깃발을 안 내리면 재로드된 씬이 잠긴 채로 열린다");
            Assert.AreEqual(1f, Time.timeScale, "배속도 함께 돌아와야 한다");

            Assert.AreEqual(uidBefore, CloudScores.Uid,
                "인수는 로그인을 건드리지 않는다 - 강제 로그아웃하면 그 기기는 "
                + "익명 uid를 새로 받고 이 계정의 세이브에 영영 못 닿는다");
        }

        /**
         * ★★ **㉒ 서버 정본을 다시 조회한 뒤에만 게임에 들어간다.**
         *
         * 게이트가 승인을 기다리는 동안 `GameSession`은 아직 아무것도 얹지
         * 않았다 - 그것이 "재조회 전 게임 시작 금지"의 관측 가능한 형태다.
         */
        [UnityTest]
        public IEnumerator TheGameWaitsForTheGateBeforeApplyingASave()
        {
            var gate = RunGateUntilAsked();
            yield return gate;

            Assert.AreEqual(CloudSaveTakeoverPhase.AwaitingConfirm, CloudSaveTakeover.Phase);
            Assert.IsFalse(CloudSaveTakeover.MayStartGame,
                "승인 전에 게임이 시작되면 그 판정은 서버를 못 본 것이다");
        }

        // ---------------------------------------------------------------- 도구

        int sessionWrites;
        int requestCount;

        /**
         * 가짜 서버: **다른 기기가 살아 있다.** 게이트를 승인 대기까지 돌린다.
         */
        IEnumerator RunGateUntilAsked()
        {
            sessionWrites = 0;
            requestCount = 0;

            CloudSaveSession.UseSessionIdForTests(new string('1', 32));
            CloudSaveSession.UsePeekForTests(uid => new CloudSaveSessionSnapshot
            {
                exists = true,
                ownerSessionId = OtherSession,
                ownerDeviceId = OtherDevice,
                released = false,
                generation = 2L,
                takeoverSessionId = string.Empty,
                takeoverDeviceId = string.Empty,
                secondsSinceHeartbeat = 3f,
                secondsSinceTakeoverRequest = -1f
            });

            CloudSaveSession.UseWriteForTests(mode =>
            {
                sessionWrites++;
                if (mode == "RequestTakeover")
                {
                    requestCount++;
                    return CloudSaveSessionStatus.TakeoverRequested;
                }

                return CloudSaveSessionStatus.Busy;
            });

            session.StartCoroutine(
                CloudSaveTakeover.EnsureSession(TestUid, new string('a', 32)));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (CloudSaveTakeover.Phase != CloudSaveTakeoverPhase.AwaitingConfirm
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        /** 부팅이 끝나 게임이 돌고 있는 상태 (이전 기기 A의 자리) */
        IEnumerator WaitForBoot()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!session.IsLoaded && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsTrue(session.IsLoaded, "부팅이 안 끝났다");

            CloudSaveSession.UseSessionIdForTests(new string('1', 32));
            CloudSaveSession.UseGenerationForTests(2L);
        }

        /** 다른 기기가 인수한 세션 문서를 감시에 그대로 물린다 */
        void LoseTheSeat()
        {
            CloudSaveSessionWatch.Examine(new CloudSaveSessionSnapshot
            {
                exists = true,
                ownerSessionId = OtherSession,
                ownerDeviceId = OtherDevice,
                released = false,
                generation = 3L,
                takeoverSessionId = string.Empty,
                takeoverDeviceId = string.Empty,
                secondsSinceHeartbeat = 1f,
                secondsSinceTakeoverRequest = -1f
            });
        }

        static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            return data;
        }
    }
}
