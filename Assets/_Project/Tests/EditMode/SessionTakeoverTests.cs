using System;
using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;

namespace Onikiri.Tests
{
    /**
     * @brief 단일 활성 기기와 **명시적 인수** (63단계).
     *
     * ## 62단계까지 무엇이 열려 있었는가
     *
     * 세션은 첫 커밋 **직전**에 잡혔고, 못 잡으면 `Busy`로 커밋만 보류했다.
     * 그 기기는 **계속 놀았다.** 두 기기가 각자 진행을 쌓다가 나중에 하나가
     * 세션을 얻으면, 나머지 한 벌은 충돌 화면에서 버려진다. 재화 복제는 없지만
     * (전체 스냅샷이라 지불과 상품이 함께 빠진다) **진행이 사라진다.**
     *
     * 63단계는 갈라짐을 뒤에서 처리하는 대신 앞에서 만들지 않는다:
     * 새 기기가 **사람에게 묻고**, 승인되면 이전 기기가 **즉시 멈춘다.**
     *
     * ## 여기서 재는 것 / 못 재는 것
     *
     * 이 파일은 **판정과 배선**을 잰다. 서버가 실제로 무엇을 거부하는지는
     * Firestore Emulator가 재고(`tools/firestore-rules-tests`, 63단계 16개),
     * 화면이 실제로 뜨는지는 PlayMode가 잰다. 셋이 같은 계약의 세 면이다.
     */
    public class SessionTakeoverTests
    {
        private const string SessionA = "1111111111111111aaaaaaaaaaaaaaaa";
        private const string SessionB = "2222222222222222bbbbbbbbbbbbbbbb";
        private const string SessionC = "3333333333333333cccccccccccccccc";
        private const string DeviceA = "aaaaaaaaaaaaaaaa1111111111111111";
        private const string DeviceB = "bbbbbbbbbbbbbbbb2222222222222222";

        [SetUp]
        public void SetUp()
        {
            CloudSavePlayLock.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveTakeover.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            CloudSavePlayLock.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveSessionWatch.ResetForTests();
            CloudSaveTakeover.ResetForTests();
        }

        // ---------------------------------------------------------------- 자동 인수 없음

        /**
         * ★★ **① 다른 기기가 살아 있으면 자동으로 뺏지 않는다.**
         *
         * 62단계와 갈리는 첫 줄이다. 판정 이름 자체가 `AskTheHuman`이고,
         * 인수 커밋은 같은 문서에서 **거짓**이다 - 두 답이 짝이어야 "묻는다"가
         * 성립한다. 하나만 맞으면 묻고 나서 어차피 뺏거나, 묻지도 못한다.
         */
        [Test]
        public void ALiveSessionIsNeverTakenAutomatically()
        {
            var live = Live(SessionB, ageSeconds: 5f);

            Assert.AreEqual(CloudSaveTakeoverStep.AskTheHuman,
                CloudSaveTakeoverPolicy.StepFor(live, SessionA));

            Assert.IsFalse(CloudSaveTakeoverPolicy.MayCommitTakeover(live, SessionA),
                "앱 실행만으로 세션이 넘어가면 그것이 곧 '최신 기기 우선'이다");
        }

        /** ★ 빈 자리·놓아준 자리·만료된 자리는 **묻지 않는다** - 물을 상대가 없다 */
        [Test]
        public void AnEmptyOrReleasedOrExpiredSeatIsTakenWithoutAsking()
        {
            Assert.AreEqual(CloudSaveTakeoverStep.Claim,
                CloudSaveTakeoverPolicy.StepFor(default(CloudSaveSessionSnapshot), SessionA),
                "빈 자리");

            var released = Live(SessionB, ageSeconds: 2f);
            released.released = true;
            Assert.AreEqual(CloudSaveTakeoverStep.Claim,
                CloudSaveTakeoverPolicy.StepFor(released, SessionA), "놓아준 자리");

            Assert.AreEqual(CloudSaveTakeoverStep.Claim,
                CloudSaveTakeoverPolicy.StepFor(
                    Live(SessionB, CloudSavePolicy.SessionExpirySeconds + 1f), SessionA),
                "만료된 자리");
        }

        /**
         * ★★ **② 승인 전에는 기존 owner가 그대로다.**
         *
         * 요청은 남길 수 있다. 그것이 owner를 바꾸지 않는다는 것이 63단계의
         * 두 단계 구조 전체다 - 요청과 커밋이 한 쓰기면 "요청"이라는 말이
         * 거짓이 된다.
         */
        [Test]
        public void RequestingATakeoverDoesNotChangeTheOwner()
        {
            var live = Live(SessionB, ageSeconds: 5f);

            Assert.IsTrue(CloudSaveTakeoverPolicy.MayRequestTakeover(live, SessionA));
            Assert.IsFalse(CloudSaveTakeoverPolicy.MayCommitTakeover(live, SessionA));

            // 요청이 남은 뒤에도, 익기 전에는 여전히 못 뺏는다
            var requested = live;
            requested.takeoverSessionId = SessionA;
            requested.secondsSinceTakeoverRequest =
                CloudSaveTakeoverPolicy.TakeoverWaitSeconds - 1f;

            Assert.IsFalse(CloudSaveTakeoverPolicy.MayCommitTakeover(requested, SessionA),
                "대기가 익기 전에 넘어가면 온라인 A가 정리할 틈이 없다");
        }

        /** ★★ 같은 요청을 **두 번** 남기지 않는다 - takeoverAt이 다시 감긴다 */
        [Test]
        public void TheSameRequestIsNotWrittenTwice()
        {
            var mine = Live(SessionB, ageSeconds: 5f);
            mine.takeoverSessionId = SessionA;
            mine.secondsSinceTakeoverRequest = 4f;

            Assert.IsFalse(CloudSaveTakeoverPolicy.MayRequestTakeover(mine, SessionA),
                "두 번째 요청이 시각을 갱신하면 강제 인수 시계가 처음부터 다시 간다");
        }

        // ------------------------------------------------ 실기가 잡은 것 (첫 부팅)

        /**
         * ★★ **잡은 적 없는 실행은 작성권을 들고 있지 않다.**
         *
         * `CloudSaveSessionStatus.Acquired`가 열거형의 0이라, 자동 속성의 기본값이
         * 그것이었다 - 부팅하자마자 `HoldsWrite == true`인 기기가 만들어졌고,
         * 감시가 첫 폴링을 돌아 **자기 자신에게 회수당했다.**
         */
        [Test]
        public void AFreshRunHoldsNoWriteBeforeAcquiring()
        {
            Assert.AreNotEqual(CloudSaveSessionStatus.Acquired, CloudSaveSession.LastStatus,
                "시작 상태가 '얻었다'면 아무것도 안 한 기기가 작성권을 든다");

            Assert.IsFalse(CloudSaveSession.HoldsWrite);

            // 상태만 억지로 맞춰도 세대가 없으면 여전히 아니다
            CloudSaveSession.UseGenerationForTests(0L);
            Assert.IsFalse(CloudSaveSession.HoldsWrite,
                "세대는 owner가 된 갈래에서만 선다 - 잡았다는 사실의 유일한 증거다");
        }

        /**
         * ★★ **잡은 적이 없으면 잃을 것도 없다.**
         *
         * 실기 첫 부팅이 이것으로 무너졌다: 서버에는 **이전 실행**의 세션 문서가
         * 남아 있고(강제 종료로 release를 못 했다), 아직 세션을 잡기 전의 첫
         * 폴링이 그것을 보고 "다른 기기가 인수했다(세대 0 -> 0)"로 읽었다.
         * 부팅 3초 만에 스스로를 멈췄다.
         */
        [Test]
        public void ADeviceThatNeverAcquiredCannotLoseTheSeat()
        {
            var someoneElse = Live(SessionB, ageSeconds: 2f);
            someoneElse.generation = 0L;      // 옛 형식 문서 = 세대 없음

            Assert.IsFalse(CloudSaveTakeoverPolicy.LostWrite(someoneElse, SessionA, 0L),
                "세대 0은 '아직 안 잡았다'다. 그것을 상실로 읽으면 부팅이 스스로를 멈춘다");

            // 잡은 뒤에는 같은 문서가 상실이다
            Assert.IsTrue(CloudSaveTakeoverPolicy.LostWrite(someoneElse, SessionA, 2L));
        }

        /**
         * ★★ **자기 폰의 이전 실행에는 묻지 않는다.**
         *
         * 앱을 강제 종료하면 세션을 놓지 못한다. 같은 폰에서 다시 켜면 세션 id는
         * 새것이라 owner와 다르고 heartbeat는 아직 180초 안이다 - 그래서 사람은
         * **자기 폰에게 "이 기기로 이어할까요"를 묻는 창**을 보게 됐다.
         *
         * 정책의 단위는 세션이 아니라 **기기**다.
         */
        [Test]
        public void TheSameDeviceIsNotAskedAboutItsOwnPreviousRun()
        {
            var previousRun = Live(SessionB, ageSeconds: 30f);
            previousRun.ownerDeviceId = DeviceA;              // 같은 폰, 다른 실행

            Assert.AreEqual(CloudSaveTakeoverStep.Claim,
                CloudSaveTakeoverPolicy.StepFor(previousRun, SessionA, DeviceA));
            Assert.IsTrue(CloudSaveTakeoverPolicy.MayCommitTakeover(previousRun, SessionA, DeviceA));

            // **다른 폰이면 여전히 묻는다.** 이 완화가 정책을 삼키면 안 된다
            Assert.AreEqual(CloudSaveTakeoverStep.AskTheHuman,
                CloudSaveTakeoverPolicy.StepFor(previousRun, SessionA, DeviceB));
            Assert.IsFalse(CloudSaveTakeoverPolicy.MayCommitTakeover(previousRun, SessionA, DeviceB));

            // ★★ **규칙에도 같은 갈래가 있어야 한다.**
            //
            // 클라이언트에만 넣었더니 서버가 그 쓰기를 거부했고, 그 기기는
            // 자기가 방금 놓친 자리를 되찾지 못한 채 작성권 없이 돌았다 -
            // 실기에서 그대로 물렸다. 이 파일의 다른 상수 대조와 같은 이유로,
            // 두 곳이 갈리는 것을 사람 눈으로 맞추지 않는다.
            StringAssert.Contains(
                "resource.data.deviceId == request.resource.data.deviceId", Rules(),
                "같은 기기 이어받기가 규칙에 없다 - 클라가 가능하다고 판단한 순간 "
                + "서버가 거부한다");
        }

        /**
         * ★★ **세션 게이트 전에 인증을 기다린다.**
         *
         * 게이트가 uid 없이 들어오면 곧바로 `Offline`로 빠져 **다른 기기가 켜져
         * 있어도 아무것도 묻지 않는다.** 62.1단계 P0-1과 정확히 같은 모양이고,
         * 실기 첫 부팅에서 그대로 재현됐다(로그인은 게이트보다 1초 늦게 끝난다).
         */
        [Test]
        public void TheGateWaitsForIdentityFirst()
        {
            string source = Read("Assets/_Project/Scripts/Subsystems/GameSession.cs");

            int await = source.IndexOf("CloudSaveCoordinator.AwaitIdentity()", StringComparison.Ordinal);
            int gate = source.IndexOf("yield return TakeoverGate();", StringComparison.Ordinal);

            Assert.Greater(await, -1, "게이트 전에 인증을 기다리지 않는다");
            Assert.Less(await, gate, "인증 대기가 게이트보다 뒤면 게이트는 uid를 못 본다");

            // 예산은 하나다 - 한 번 기다린 뒤에는 ChooseBootSave가 또 기다리지 않는다
            StringAssert.Contains("if (AwaitedIdentity) return false;",
                Read("Assets/_Project/Scripts/Cloud/CloudSaveCoordinator.cs"),
                "두 번 기다리면 오프라인 부팅이 12초가 된다");
        }

        /**
         * ★★ **차단 팝업은 인트로보다 위에 선다.**
         *
         * `IntroFlow.Awake`가 `transform.SetAsLastSibling()`으로 부팅 화면을
         * 맨 위에 놓는다("다른 빌더가 SafeArea 뒤에 무엇을 더 세우든"). 인수
         * 확인은 **그 인트로가 아직 떠 있는 동안** 뜨므로, 형제 순서를 잡지
         * 않으면 로그에는 떴는데 화면에는 없다 - 실기에서 그대로 물렸다.
         */
        [Test]
        public void TheBlockingPopupsRaiseThemselvesAboveTheIntro()
        {
            StringAssert.Contains("transform.SetAsLastSibling()",
                Read("Assets/_Project/Scripts/Widget/Intro/IntroFlow.cs"),
                "이 검사의 전제는 인트로가 스스로 맨 위로 간다는 것이다");

            // ★ 형제 순서만으로는 부족하다 - 인트로는 **UI Canvas 직속**이고
            // 이 팝업들은 SafeArea 안에 산다. 정렬을 덮어쓰는 Canvas가 있어야
            // 위로 올라간다(실기에서 두 번 물린 자리)
            string builder = Read("Assets/_Project/Editor/HudScreensBuilder.cs");

            StringAssert.Contains("sorting.overrideSorting = true;", builder);
            StringAssert.Contains("sorting.sortingOrder = BlockingPopupSortingOrder;", builder);
            StringAssert.Contains("AddComponent<GraphicRaycaster>()", builder,
                "정렬만 올리고 레이캐스터가 없으면 버튼이 안 눌린다");

            StringAssert.Contains("root.transform.SetParent(canvas.transform, false);",
                Read("Assets/_Project/Editor/IntroScreenBuilder.cs"),
                "인트로가 UI Canvas 직속이라는 전제가 깨졌다면 이 배선을 다시 봐야 한다");

            foreach (string panel in new[]
            {
                "Assets/_Project/Scripts/Widget/Popups/CloudTakeoverPanel.cs",
                "Assets/_Project/Scripts/Widget/Popups/CloudEvictedPanel.cs"
            })
            {
                string source = Read(panel);

                int show = source.IndexOf("if (visible) transform.SetAsLastSibling();",
                                          StringComparison.Ordinal);
                int body = source.IndexOf("if (body != null) body.SetActive(visible);",
                                          StringComparison.Ordinal);

                Assert.Greater(show, -1, panel + " 가 형제 순서를 잡지 않는다");
                Assert.Less(show, body, panel + " 는 켜기 전에 올라와야 한다");
            }
        }

        /**
         * ★★ **내 자리에 남의 요청이 오면 스스로 물러난다** (설계 §4.1).
         *
         * 구현하지 않아도 B는 결국 들어간다 - 요청이 20초 익으면 강제 인수가
         * 열리기 때문이다. 그래서 **동작하는 것처럼 보인다.** 잃는 것은 둘이다:
         * 사람이 매번 20초를 기다리고, **A의 마지막 몇 초가 서버에 못 올라간다.**
         * 실기가 그것을 그대로 보여 줬다(모든 인수가 20초 강제였다).
         */
        [Test]
        public void AnOwnerYieldsWhenSomeoneElseAsksForTheSeat()
        {
            var mine = Live(SessionA, ageSeconds: 1f);
            mine.takeoverSessionId = SessionB;

            Assert.IsTrue(CloudSaveTakeoverPolicy.ShouldYieldSeat(mine, SessionA));

            // 요청이 없으면 물러나지 않는다
            mine.takeoverSessionId = string.Empty;
            Assert.IsFalse(CloudSaveTakeoverPolicy.ShouldYieldSeat(mine, SessionA));

            // 내 요청에는 물러나지 않는다 (자기 자신에게 자리를 넘길 수 없다)
            mine.takeoverSessionId = SessionA;
            Assert.IsFalse(CloudSaveTakeoverPolicy.ShouldYieldSeat(mine, SessionA));

            // 이미 남의 자리면 양보가 아니라 상실이다
            var theirs = Live(SessionB, ageSeconds: 1f);
            theirs.takeoverSessionId = SessionC;
            Assert.IsFalse(CloudSaveTakeoverPolicy.ShouldYieldSeat(theirs, SessionA));
        }

        /**
         * ★★ **봉인은 새 진행을 막고 마지막 한 벌만 통과시킨다.**
         *
         * 잠금 하나로 전부 막으면 A가 넘기는 것은 몇 초 전의 기록이 되고,
         * 그 몇 초가 정확히 사람이 마지막으로 한 일이다.
         */
        [Test]
        public void TheSealBlocksNewProgressButLetsTheLastRecordThrough()
        {
            Assert.IsTrue(CloudSavePlayLock.BeginSeal("검사"));

            Assert.IsTrue(CloudSavePlayLock.Blocked, "새 진행은 즉시 막힌다");
            Assert.IsTrue(CloudSavePlayLock.AllowsFinalWrite, "마지막 저장·커밋은 지나간다");
            Assert.IsFalse(CloudSavePlayLock.BeginSeal("두 번째"), "봉인은 한 번뿐이다");

            CloudSavePlayLock.CompleteSeal();

            Assert.IsTrue(CloudSavePlayLock.Blocked);
            Assert.IsFalse(CloudSavePlayLock.AllowsFinalWrite,
                "정리가 끝난 뒤에도 쓸 수 있으면 회수가 아니다");
        }

        /**
         * ★★ **종료 팝업은 자리를 넘긴 뒤에 뜬다.**
         *
         * 앞에 오면 화면은 "종료합니다"인데 뒤에서 몇 초 더 서버와 주고받는다 -
         * 화면과 사실이 어긋나는 상태다.
         */
        [Test]
        public void TheEvictionNoticeWaitsUntilTheSeatIsHandedOver()
        {
            int shown = 0;
            CloudSavePlayLock.Engaged += () => shown++;

            CloudSavePlayLock.BeginSeal("검사");
            Assert.AreEqual(0, shown, "정리 전에 팝업이 떴다");

            CloudSavePlayLock.CompleteSeal();
            Assert.AreEqual(1, shown);

            CloudSavePlayLock.CompleteSeal();
            Assert.AreEqual(1, shown, "봉인이 끝난 뒤의 호출은 아무 일도 없다");
        }

        /** ★ 회수된 기기의 세 문은 **봉인 중에도** 잠겨 있다 */
        [Test]
        public void TheProgressDoorsStayShutEvenWhileSealing()
        {
            foreach (string door in new[]
            {
                "Assets/_Project/Scripts/Subsystems/PlayerWallet.cs",
                "Assets/_Project/Scripts/Subsystems/GemWallet.cs",
                "Assets/_Project/Scripts/Subsystems/StageProgress.cs"
            })
                StringAssert.Contains("CloudSavePlayLock.Locked", Read(door),
                    door + " 가 봉인 중에도 막혀야 한다 - AllowsFinalWrite를 보면 "
                    + "인계 정리 중에 전투 보상이 들어온다");

            // 저장·커밋만 봉인을 지난다
            StringAssert.Contains("CloudSavePlayLock.AllowsFinalWrite",
                Read("Assets/_Project/Scripts/Subsystems/GameSession.cs"));
            StringAssert.Contains("CloudSavePlayLock.AllowsFinalWrite",
                Read("Assets/_Project/Scripts/Cloud/CloudSaveSession.cs"));
        }

        /**
         * ★★ **`Busy` 한 번이 감시를 멈추면 안 된다.**
         *
         * 세션 호출이 `Busy`를 받으면 `HoldsWrite`는 false가 된다. 감시가 그
         * 값을 보고 멈추면 **자리를 잃은 사실을 영영 못 본다** - 그 기기는
         * 회수되지 않은 채 계속 놀고, 쌓은 진행은 올라가지 못한다. 실기에서
         * 오프라인 복귀 기기가 정확히 그랬다.
         */
        [Test]
        public void ABusyAnswerDoesNotSilenceTheWatch()
        {
            CloudSaveSession.UseSessionIdForTests(SessionA);
            CloudSaveSession.UseGenerationForTests(2L);

            Assert.IsTrue(CloudSaveSession.HasHeldSeat,
                "한 번 잡았으면 그 사실은 Busy로 지워지지 않는다");

            // 감시는 `HoldsWrite`가 아니라 `HasHeldSeat`를 본다
            string watch = Read("Assets/_Project/Scripts/Cloud/CloudSaveSessionWatch.cs");

            StringAssert.Contains("if (!CloudSaveSession.HasHeldSeat) return;", watch);
            StringAssert.DoesNotContain("if (!CloudSaveSession.HoldsWrite) return;", watch,
                "HoldsWrite로 게이트하면 Busy 한 번에 감시가 죽는다");

            // 그리고 그 상태에서 상실이 보이면 회수된다
            var taken = Live(SessionB, ageSeconds: 1f);
            taken.generation = 3L;

            CloudSaveSessionWatch.Examine(taken);
            Assert.IsTrue(CloudSavePlayLock.Locked);
        }

        /**
         * ★★ **타이틀로 나가면 그 실행은 끝난다.**
         *
         * 씬을 다시 열어도 정적 상태는 살아남는다. 세션 id와 세대를 그대로 두면
         * 새 실행이 첫 폴링에서 **또** 상실을 보고 스스로를 잠그고(종료 팝업과
         * 인수 확인이 겹친다), 그 뒤에는 인수에 성공해도 저장이 막힌 채로 돈다.
         * 실기 최종 검증에서 그대로 물렸다.
         */
        [Test]
        public void LeavingToTitleStartsAFreshRun()
        {
            CloudSaveSession.UseSessionIdForTests(SessionA);
            CloudSaveSession.UseGenerationForTests(12L);
            Assert.IsTrue(CloudSaveSession.HasHeldSeat);

            CloudSaveSession.BeginNewRun();

            Assert.IsFalse(CloudSaveSession.HasHeldSeat,
                "세대가 남으면 새 실행이 자기를 잃은 것으로 읽는다");
            Assert.AreNotEqual(SessionA, CloudSaveSession.CurrentId,
                "세션 id는 실행마다 하나다");

            // 그리고 그 상태에서는 감시가 조용하다
            var taken = Live(SessionB, ageSeconds: 1f);
            taken.generation = 13L;

            CloudSaveSessionWatch.Examine(taken);
            Assert.IsFalse(CloudSavePlayLock.Locked);
        }

        /** ★ 종료 팝업이 타이틀로 나가기 전에 **그 둘을** 부른다 */
        [Test]
        public void TheEvictedPanelResetsTheRunBeforeReloading()
        {
            string source = Read("Assets/_Project/Scripts/Widget/Popups/CloudEvictedPanel.cs");

            int fresh = source.IndexOf("CloudSaveSession.BeginNewRun();", StringComparison.Ordinal);
            int release = source.IndexOf("CloudSavePlayLock.Release();", StringComparison.Ordinal);
            int load = source.IndexOf("SceneManager.LoadScene", StringComparison.Ordinal);

            Assert.Greater(fresh, -1, "새 실행을 시작하지 않는다");
            Assert.Less(fresh, release);
            Assert.Less(release, load, "깃발을 안 내리면 재로드된 씬이 잠긴 채 열린다");
        }

        // ---------------------------------------------------------------- 세대

        /** ★★ **③ 인수 성공마다 세대가 정확히 1 오른다** */
        [Test]
        public void EachTakeoverAdvancesTheGenerationByExactlyOne()
        {
            Assert.AreEqual(2L, CloudSaveTakeoverPolicy.NextGeneration(1L));
            Assert.AreEqual(3L, CloudSaveTakeoverPolicy.NextGeneration(2L));
            Assert.AreEqual(124L, CloudSaveTakeoverPolicy.NextGeneration(123L));

            // 세대를 모르는 문서(옛 형식·손상)는 1에서 출발한다. 0은
            // "없음"과 구분되지 않으므로 세대 값으로 쓰지 않는다
            Assert.AreEqual(CloudSaveTakeoverPolicy.FirstGeneration,
                CloudSaveTakeoverPolicy.NextGeneration(0L));
            Assert.AreEqual(CloudSaveTakeoverPolicy.FirstGeneration,
                CloudSaveTakeoverPolicy.NextGeneration(-5L));
        }

        /**
         * ★★ **⑩ 동시에 둘이 인수하면 하나만 성공한다.**
         *
         * 두 기기가 같은 세대 5를 보고 둘 다 6을 쓴다. 규칙이
         * `generation == resource.generation + 1`을 요구하므로, 먼저 든 쪽이
         * 6으로 올리는 순간 나중 쪽의 전제가 무너진다 - **트랜잭션 하나가 진다.**
         * 그 거부를 Emulator가 실제로 재고(63단계 "동시 인수는 하나만 성공한다"),
         * 여기서는 두 클라이언트가 같은 값을 계산한다는 것을 못 박는다.
         */
        [Test]
        public void TwoRacingDevicesComputeTheSameNextGeneration()
        {
            var seen = Live(SessionC, ageSeconds: 5f);
            seen.generation = 5L;
            seen.released = true;

            long fromA = CloudSaveTakeoverPolicy.NextGeneration(seen.generation);
            long fromB = CloudSaveTakeoverPolicy.NextGeneration(seen.generation);

            Assert.AreEqual(fromA, fromB,
                "둘이 다른 값을 쓰면 규칙이 둘 다 통과시킬 수 있다");
            Assert.AreEqual(6L, fromA);

            StringAssert.Contains("request.resource.data.generation == resource.data.generation + 1",
                Rules(), "경쟁의 최종 판정은 규칙이다");
        }

        // ---------------------------------------------------------------- 이전 기기

        /**
         * ★★ **⑤⑥⑦ 이전 기기는 새 세션을 건드리지 못한다.**
         *
         * heartbeat·release·commit 셋 다 같은 한 줄에서 막힌다: 그 기기의
         * `sessionId`가 더 이상 owner가 아니다. 세 갈래를 따로 막는 대신 하나로
         * 모으는 것이 이 계약이 무너지지 않는 이유다 - 갈래가 셋이면 언젠가
         * 넷째가 생기고, 그것은 아무도 안 막는다.
         */
        [Test]
        public void TheOldDeviceCanNoLongerRenewOrReleaseOrCommit()
        {
            var afterTakeover = Live(SessionB, ageSeconds: 1f);
            afterTakeover.ownerDeviceId = DeviceB;
            afterTakeover.generation = 3L;

            // A가 보는 판정: 내 세션이 아니다 (heartbeat·release가 여기서 막힌다)
            Assert.AreNotEqual(CloudSaveTakeoverStep.Renew,
                CloudSaveTakeoverPolicy.StepFor(afterTakeover, SessionA));

            // 그리고 A는 작성권을 잃은 것으로 판정된다 (commit이 여기서 막힌다)
            Assert.IsTrue(CloudSaveTakeoverPolicy.LostWrite(afterTakeover, SessionA, 2L));

            string rules = Rules();
            StringAssert.Contains("resource.data.sessionId == request.resource.data.sessionId",
                rules, "규칙의 ownerRenew가 sessionId 일치를 요구해야 한다");
            StringAssert.Contains("writerHoldsSession(uid)", rules,
                "커밋은 지금 owner 세션만 통과한다");
        }

        /** ★★ **⑬ 오프라인 A가 돌아오면 쓰기 전에 상실을 알아차린다** */
        [Test]
        public void AReturningOfflineDeviceNoticesTheLossBeforeWriting()
        {
            CloudSaveSession.UseSessionIdForTests(SessionA);
            CloudSaveSession.UseGenerationForTests(2L);

            var taken = Live(SessionB, ageSeconds: 1f);
            taken.generation = 3L;

            Assert.IsFalse(CloudSavePlayLock.Locked, "시작할 때는 놀고 있다");

            CloudSaveSessionWatch.Examine(taken);

            Assert.IsTrue(CloudSavePlayLock.Locked,
                "상실을 보고도 안 멈추면 그 뒤의 진행은 전부 버려질 것이다");
        }

        /**
         * ★★ **문서를 못 읽은 것은 상실이 아니다.**
         *
         * 오프라인은 실패가 아니라 정상 경로다. 읽지 못한 것을 상실로 치면
         * 지하철에서 게임이 멈춘다 - 방치형 게임에서 그것은 어떤 정합성보다 나쁘다.
         */
        [Test]
        public void AnUnreadableDocumentIsNotTreatedAsALoss()
        {
            CloudSaveSession.UseSessionIdForTests(SessionA);
            CloudSaveSession.UseGenerationForTests(2L);

            CloudSaveSessionWatch.Examine(default(CloudSaveSessionSnapshot));

            Assert.IsFalse(CloudSavePlayLock.Locked, "오프라인에서 게임이 멈추면 안 된다");
            Assert.IsFalse(CloudSaveTakeoverPolicy.LostWrite(
                default(CloudSaveSessionSnapshot), SessionA, 2L));
        }

        /** ★ 내 세션이 그대로면 상실이 아니다 - 과방어가 곧 멈춘 게임이다 */
        [Test]
        public void HoldingTheSameSeatIsNotALoss()
        {
            var mine = Live(SessionA, ageSeconds: 1f);
            mine.generation = 7L;

            Assert.IsFalse(CloudSaveTakeoverPolicy.LostWrite(mine, SessionA, 7L));
        }

        // ---------------------------------------------------------------- 회수된 권한

        /**
         * ★★ **④⑱ 회수 = 여섯 가지 권한이 한꺼번에 닫힌다.**
         *
         * 전투 진행 · 재화 변경 · 뽑기와 구매 · 스테이지 진행 · 로컬 진행 저장 ·
         * 클라우드 작성권. 여기서는 **소스로** 확인한다 - 각 문에 깃발이 실제로
         * 물려 있는지가 계약이고, MonoBehaviour 여섯을 EditMode에서 세울 수는 없다.
         */
        [Test]
        public void EveryRevokedDoorChecksTheFlag()
        {
            var doors = new[]
            {
                "Assets/_Project/Scripts/Subsystems/PlayerWallet.cs",
                "Assets/_Project/Scripts/Subsystems/GemWallet.cs",
                "Assets/_Project/Scripts/Subsystems/GachaSystem.cs",
                "Assets/_Project/Scripts/Subsystems/StageProgress.cs",
                "Assets/_Project/Scripts/Subsystems/GameSession.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSync.cs"
            };

            foreach (string door in doors)
                StringAssert.Contains("CloudSavePlayLock.Locked", Read(door),
                    door + " 가 회수 깃발을 안 본다 - 그 문으로 진행이 새어 나간다");
        }

        /** ★★ 회수되면 **작성권 자체가 거짓**이 된다 - 그 위의 모든 판단이 따라온다 */
        [Test]
        public void ARevokedDeviceDoesNotHoldWrite()
        {
            CloudSavePlayLock.Engage("검사");

            Assert.IsFalse(CloudSaveSession.HoldsWrite,
                "마지막 세션 결과가 무엇이든, 회수된 기기는 작성권이 없다");
        }

        /**
         * ★★ **깃발이 팝업보다 먼저다.**
         *
         * 감지와 그리기 사이의 한두 프레임에 10연 버튼이 눌리면 그 뽑기는
         * 이미 남의 기기가 정본을 쥔 계정에서 일어난다. 이벤트를 받는 쪽에서
         * 이미 잠겨 있어야 한다.
         */
        [Test]
        public void TheFlagIsUpBeforeTheEventFires()
        {
            bool lockedWhenNotified = false;
            int notifications = 0;

            CloudSavePlayLock.Engaged += () =>
            {
                notifications++;
                lockedWhenNotified = CloudSavePlayLock.Locked;
            };

            CloudSavePlayLock.Engage("검사");
            CloudSavePlayLock.Engage("두 번째");   // ⑲ 폴링이 매 주기 다시 본다

            Assert.IsTrue(lockedWhenNotified, "이벤트가 울릴 때 이미 잠겨 있어야 한다");
            Assert.AreEqual(1, notifications, "종료 팝업은 한 번만 뜬다");
        }

        // ---------------------------------------------------------------- 취소

        /**
         * ★★ **⑪ 취소하면 세션 문서도 게임 상태도 그대로다.**
         *
         * 취소는 요청을 보내기 **전**에만 받는다. 그래서 되돌릴 것이 없다 -
         * 되돌리는 코드가 있으면 그 코드가 언젠가 남의 세션을 되돌린다.
         */
        [Test]
        public void CancellingWritesNothing()
        {
            Assert.AreEqual(CloudSaveTakeoverPhase.Idle, CloudSaveTakeover.Phase);

            CloudSaveTakeover.Cancel();

            Assert.IsFalse(CloudSaveTakeover.CancelledForTests,
                "승인을 기다리는 국면이 아니면 입력을 받지 않는다");
            Assert.IsFalse(CloudSavePlayLock.Locked, "게임 상태도 그대로다");
        }

        /**
         * ★★ **취소하면 게임이 시작되지 않는다.**
         *
         * "취소하고 로컬로 논다"는 갈래를 두지 않은 것이 63단계의 판단이다 -
         * 그것이 정확히 62단계의 갈라짐이기 때문이다. 로컬로 논 진행은 서버에
         * 못 올라가고, 다음 부팅이 충돌 화면에서 그것을 버리라고 묻는다.
         */
        [Test]
        public void TheGameDoesNotStartAfterACancelOrTimeout()
        {
            string session = Read("Assets/_Project/Scripts/Cloud/CloudSaveTakeover.cs");

            // 게임을 시작해도 되는 국면은 셋뿐이다
            StringAssert.Contains("Phase == CloudSaveTakeoverPhase.Idle", session);
            StringAssert.Contains("Phase == CloudSaveTakeoverPhase.Acquired", session);
            StringAssert.Contains("Phase == CloudSaveTakeoverPhase.Offline", session);

            StringAssert.DoesNotContain("Phase == CloudSaveTakeoverPhase.Cancelled", session);
            StringAssert.DoesNotContain("Phase == CloudSaveTakeoverPhase.TimedOut", session);
        }

        // ---------------------------------------------------------------- 부팅 순서

        /**
         * ★★ **⑧⑨ 작성권이 서버 정본 재조회보다 먼저다.**
         *
         * 순서가 뒤집히면 B는 **자기 로컬**로 판정을 마친 뒤 세션을 얻는다.
         * 그 판정은 A가 방금 올린 최신을 못 본 것이고, 그 상태에서 올라가는
         * 것이 곧 "오래된 로컬의 자동 업로드"다.
         */
        [Test]
        public void TheSessionGateRunsBeforeTheServerRefetch()
        {
            string source = Read("Assets/_Project/Scripts/Subsystems/GameSession.cs");

            int gate = source.IndexOf("yield return TakeoverGate();", StringComparison.Ordinal);
            int guard = source.IndexOf("CloudSaveTakeover.MayStartGame", StringComparison.Ordinal);
            int fetch = source.IndexOf("CloudSaveCoordinator.ChooseBootSave", StringComparison.Ordinal);

            Assert.Greater(gate, -1, "부팅에 세션 게이트가 없다");
            Assert.Greater(guard, -1, "게이트 결과를 보지 않는다");
            Assert.Less(gate, guard, "게이트를 지나기 전에 결과를 볼 수 없다");
            Assert.Less(guard, fetch,
                "정본 재조회가 게이트보다 앞서면 B는 옛 로컬로 판정한다");
        }

        /** ★ 부팅 판정 자체는 62단계 그대로다 - 인수가 판정을 바꾸지 않는다 */
        [Test]
        public void TakeoverDoesNotChangeTheBootVerdict()
        {
            string policy = Read("Assets/_Project/Scripts/Cloud/CloudSavePolicy.cs");

            StringAssert.DoesNotContain("generation", policy,
                "부팅 판정에 세대가 끼면 '세대가 올랐으니 클라우드'라는 자동 선택이 생긴다");
        }

        // ---------------------------------------------------------------- 로그인·로컬 보존

        /**
         * ★★ **⑫ Firebase 로그인은 회수 대상이 아니다.**
         *
         * 회수하는 것은 권한이지 계정이 아니다. 강제 로그아웃하면 그 기기는
         * 익명 uid를 새로 받고(56단계), 그 순간 **이 계정의 세이브에 영영 닿지
         * 못한다** - 되돌리려면 61단계 계정 복구를 타야 한다.
         */
        [Test]
        public void NothingInTheTakeoverPathSignsTheUserOut()
        {
            var files = new[]
            {
                "Assets/_Project/Scripts/Cloud/CloudSaveTakeover.cs",
                "Assets/_Project/Scripts/Cloud/CloudSavePlayLock.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSessionWatch.cs",
                "Assets/_Project/Scripts/Widget/Popups/CloudTakeoverPanel.cs",
                "Assets/_Project/Scripts/Widget/Popups/CloudEvictedPanel.cs"
            };

            foreach (string file in files)
            {
                string source = Read(file);
                StringAssert.DoesNotContain("SignOut", source, file);
                StringAssert.DoesNotContain("DeleteAsync", source, file);
            }
        }

        /**
         * ★★ **⑭ 이전 기기의 로컬 기록을 지우지 않는다.**
         *
         * 회수는 저장을 **멈추는** 것이지 지우는 것이 아니다. 그 파일이
         * 복구 자료다 - 다음 부팅의 판정이 그것을 사람에게 보여 준다(60단계
         * 충돌 화면). 조용히 덮지도, 지우지도 않는다.
         */
        [Test]
        public void TheOldDeviceKeepsItsLocalSave()
        {
            var files = new[]
            {
                "Assets/_Project/Scripts/Cloud/CloudSaveTakeover.cs",
                "Assets/_Project/Scripts/Cloud/CloudSavePlayLock.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSessionWatch.cs",
                "Assets/_Project/Scripts/Widget/Popups/CloudEvictedPanel.cs"
            };

            foreach (string file in files)
            {
                string source = Read(file);
                StringAssert.DoesNotContain("SaveSystem.Delete", source, file);
                StringAssert.DoesNotContain("CloudSaveSidecar.Delete", source, file);
            }
        }

        /**
         * ★★ **회수 뒤에는 저장도 업로드도 없다** (⑨의 다른 얼굴).
         *
         * 62.1.2가 "디스크에 없는 것을 올리지 않는다"를 닫았고, 여기는
         * "올릴 수 없는 것을 디스크에 굳히지 않는다"를 닫는다. 굳히면 다음
         * 부팅이 그것을 두고 사람에게 고르라고 묻는다.
         */
        [Test]
        public void ARevokedDeviceNeitherSavesNorUploads()
        {
            string session = Read("Assets/_Project/Scripts/Subsystems/GameSession.cs");
            string sync = Read("Assets/_Project/Scripts/Cloud/CloudSaveSync.cs");

            int save = session.IndexOf("public bool Save()", StringComparison.Ordinal);
            int locked = session.IndexOf("CloudSavePlayLock.AllowsFinalWrite", save,
                                         StringComparison.Ordinal);
            int writes = session.IndexOf("SaveSystem.Save(data)", save, StringComparison.Ordinal);

            Assert.Greater(locked, -1, "Save가 회수 깃발을 안 본다");
            Assert.Less(locked, writes, "깃발 검사가 디스크 쓰기보다 뒤에 있다");

            foreach (string entry in new[] { "public static void Tick()",
                                              "public static void NoteSaved(",
                                              "public static void RequestUrgent(" })
            {
                int at = sync.IndexOf(entry, StringComparison.Ordinal);
                Assert.Greater(at, -1, entry + " 를 못 찾았다");

                // `Tick`·`RequestUrgent`는 `Locked`(봉인 중에도 막힘),
                // `NoteSaved`는 `AllowsFinalWrite`(마지막 한 벌은 지나간다).
                // 여기서 재는 것은 **깃발을 함수 앞머리에서 본다**는 것이다
                int guard = sync.IndexOf("CloudSavePlayLock.", at, StringComparison.Ordinal);
                Assert.Greater(guard, -1, entry + " 가 회수 깃발을 안 본다");
                Assert.Less(guard - at, 400, entry + " 의 깃발 검사가 함수 앞머리에 없다");
            }
        }

        // ---------------------------------------------------------------- 상수 대조

        /**
         * ★★ 대기 시간과 만료가 **규칙과 같은 값**이어야 한다.
         *
         * 갈리면 클라가 가능하다고 판단한 순간 서버가 거부하고, 그 기기는
         * 이유를 모른 채 못 들어간다. 규칙↔코드 상수 대조는 58단계부터의
         * 이 프로젝트 규칙이다(`CloudSaveStoreTests`가 같은 일을 한다).
         */
        [Test]
        public void TheWaitAndExpiryMatchTheRules()
        {
            string rules = Rules();

            StringAssert.Contains(
                "duration.value(" + (int)CloudSaveTakeoverPolicy.TakeoverWaitSeconds + ", 's')",
                rules, "강제 인수 대기(초)가 규칙과 갈렸다");

            StringAssert.Contains(
                "duration.value(" + (int)CloudSavePolicy.SessionExpirySeconds + ", 's')",
                rules, "세션 만료(초)가 규칙과 갈렸다");
        }

        /** ★ 세션 문서의 여덟 필드가 규칙과 코드에서 같은 이름이다 */
        [Test]
        public void TheSessionFieldNamesMatchTheRules()
        {
            string rules = Rules();

            foreach (string field in new[]
            {
                CloudSaveSession.FieldSessionId, CloudSaveSession.FieldDeviceId,
                CloudSaveSession.FieldHeartbeatAt, CloudSaveSession.FieldReleased,
                CloudSaveSession.FieldGeneration, CloudSaveSession.FieldTakeoverSessionId,
                CloudSaveSession.FieldTakeoverDeviceId, CloudSaveSession.FieldTakeoverAt
            })
                StringAssert.Contains("'" + field + "'", rules,
                    field + " 가 규칙의 필드 목록에 없다 - 그 쓰기는 조용히 거부된다");
        }

        // ---------------------------------------------------------------- seam

        /** ★ 63단계 seam도 출시 컴파일에 없다 */
        [Test]
        public void TheSessionSeamsAreCompiledOutOfReleaseBuilds()
        {
            var files = new[]
            {
                "Assets/_Project/Scripts/Cloud/CloudSavePlayLock.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSession.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSessionWatch.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveTakeover.cs"
            };

            foreach (string file in files)
            {
                string source = Read(file);
                int index = source.IndexOf("ForTests", StringComparison.Ordinal);

                while (index >= 0)
                {
                    string before = source.Substring(0, index);
                    Assert.Greater(Count(before, "#if UNITY_EDITOR"), Count(before, "#endif"),
                        file + " 의 seam이 컴파일 가드 밖에 있다 (offset " + index + ")");

                    index = source.IndexOf("ForTests", index + 1, StringComparison.Ordinal);
                }
            }
        }

        // ---------------------------------------------------------------- 도구

        private static CloudSaveSessionSnapshot Live(string owner, float ageSeconds)
        {
            return new CloudSaveSessionSnapshot
            {
                exists = true,
                ownerSessionId = owner,
                ownerDeviceId = DeviceA,
                released = false,
                generation = CloudSaveTakeoverPolicy.FirstGeneration,
                takeoverSessionId = string.Empty,
                takeoverDeviceId = string.Empty,
                secondsSinceHeartbeat = ageSeconds,
                secondsSinceTakeoverRequest = -1f
            };
        }

        private static string Rules()
        {
            return Read("firestore.rules");
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(UnityEngine.Application.dataPath), relative));
        }

        private static int Count(string text, string token)
        {
            int count = 0, index = text.IndexOf(token, StringComparison.Ordinal);

            while (index >= 0)
            {
                count++;
                index = text.IndexOf(token, index + 1, StringComparison.Ordinal);
            }

            return count;
        }
    }
}
