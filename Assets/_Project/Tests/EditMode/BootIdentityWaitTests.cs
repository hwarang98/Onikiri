using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 부팅 인증 대기 (62.1단계 P0). **실기가 물린 그 1초를 잰다.**
     *
     * ## 무엇이 부러져 있었는가
     *
     * 62단계 두 기기 실측(§5.B)이 그대로 찍었다:
     *
     *     14:01:13.023  부팅 선택: 서버 확인 없음 - 로컬 사용
     *     14:01:14.082  기존 로그인 재사용. uid = A9aMgx…      ← 1초 늦다
     *
     * `GameSession.Start()`가 `WillCheckServer`를 묻는 시점에 uid가 비어 있고,
     * uid가 없으면 그 값이 false다. 그래서 사이드카를 든 기존 사용자도 같은
     * 프레임에 로컬로 확정됐고, 다른 기기가 올린 최신 정본을 **부팅에서 못 봤다.**
     *
     * ## 이 검사가 잡는 것
     *
     * 대기는 값이 있을 때만 붙어야 한다 - 사이드카가 없는 신규·로컬 전용
     * 사용자에게는 한 프레임도 늦으면 안 된다(인트로의 "같은 프레임" 가정과
     * 귀문 PlayMode 검사 다섯이 그 경로에 걸려 있다).
     *
     * 실제 Firebase도 실제 시계도 쓰지 않는다. uid 도착·시간 경과·서버 조회를
     * seam으로 손에 쥐고, 코루틴을 직접 돌린다.
     */
    public class BootIdentityWaitTests
    {
        private const string Uid = "boot-await-uid";

        [SetUp]
        public void SetUp()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
        }

        // ---------------------------------------------------------------- 대기 판정

        /**
         * ★★ **사이드카가 있고 uid가 아직 없으면 기다린다.** P0의 핵심 한 줄.
         */
        [Test]
        public void AnExistingUserWaitsForIdentity()
        {
            Assert.IsTrue(CloudSavePolicy.ShouldAwaitIdentity(true, false, true));
        }

        /** ★ 사이드카가 없으면 **기다리지 않는다** - 신규·로컬 전용은 지연 0 */
        [Test]
        public void ANewUserNeverWaits()
        {
            Assert.IsFalse(CloudSavePolicy.ShouldAwaitIdentity(true, false, false));
        }

        /** uid가 이미 있으면 기다릴 것이 없다 - 곧바로 서버를 본다 */
        [Test]
        public void AReadyIdentityDoesNotWait()
        {
            Assert.IsFalse(CloudSavePolicy.ShouldAwaitIdentity(true, true, true));
        }

        /** 서버 확인이 꺼져 있으면 기다려 봐야 쓸 데가 없다 */
        [Test]
        public void ADisabledServerCheckNeverWaits()
        {
            Assert.IsFalse(CloudSavePolicy.ShouldAwaitIdentity(false, false, true));
            Assert.IsFalse(CloudSavePolicy.ShouldAwaitIdentity(false, true, true));
        }

        // ---------------------------------------------------------------- 에디터 게이트

        /**
         * ★★ **검사가 uid를 주지 않으면 에디터는 기다리지 않는다.**
         *
         * 62.1에서 실제로 물린 회귀다. 이 게이트가 없으면 전량 PlayMode가
         * `CloudConflictPlayTests`의 씬 재로드 뒤에서 매달린다 - 검사들은
         * `fetchOverride`로 가짜 서버를 쓰면서 로그인은 하지 않으므로 uid가 영영
         * 오지 않고, 부팅이 코루틴으로 빠지면 **첫 프레임에 `GameSession`을
         * 파괴하는 검사들**과 얽혀 59단계의 함정에 그대로 빠진다.
         *
         * bisect로 확정했다: 게이트 없이 매달림 → 넣고 44/44.
         */
        [Test]
        public void TheEditorNeverWaitsWithoutAnIdentitySeam()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(Missing()));

            // uid seam이 없다 = 에디터에 실제 로그인이 없는 상태
            Assert.IsFalse(CloudSaveCoordinator.WillAwaitIdentity,
                "에디터에서 오지 않을 uid를 기다리면 PlayMode가 매달린다");

            // seam을 주면 기다린다 - 실제로 도착할 uid가 있기 때문이다
            CloudSaveCoordinator.UseIdentityForTests(() => string.Empty);
            Assert.IsTrue(CloudSaveCoordinator.WillAwaitIdentity);
        }

        /**
         * ★ 가짜 서버도 uid seam도 없으면 부팅은 **동기 경로로 끝난다.**
         *
         * 실기의 신규·로컬 전용 사용자가 지나는 길이고, 인트로의 "같은 프레임"
         * 가정과 귀문 PlayMode 검사 다섯이 그 위에 서 있다.
         */
        [Test]
        public void APureLocalBootNeverEntersTheCoroutine()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveCoordinator.UseSidecarPresenceForTests(true);

            Assert.IsFalse(CloudSaveCoordinator.WillAwaitIdentity,
                "기다릴 로그인이 없다");
            Assert.IsFalse(CloudSaveCoordinator.WillCheckServer,
                "uid도 가짜 서버도 없으면 서버를 안 본다");
        }

        // ---------------------------------------------------------------- 지연 로그인

        /**
         * ★★ **늦게 온 uid를 기다렸다가 서버를 본다.**
         *
         * 실기의 1초를 프레임 셋으로 줄여 재현한다 - uid는 세 번째 MoveNext에서
         * 도착하고, 그 뒤에야 fetch가 나가야 한다.
         */
        [Test]
        public void ALateIdentityIsAwaitedAndTheServerIsRead()
        {
            int frames = 0;
            int fetches = 0;

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => frames >= 3 ? Uid : string.Empty);
            CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);
            CloudSaveCoordinator.UseFetchForTests(uid =>
            {
                fetches++;
                Assert.AreEqual(Uid, uid, "로그인이 끝난 뒤의 uid로 조회해야 한다");
                return Task.FromResult(Found(Server(), 7L));
            });

            CloudSaveBootChoice choice = Drive(Local(), ref frames);

            Assert.AreEqual(1, fetches, "서버를 정확히 한 번 본다");
            Assert.IsTrue(CloudSaveCoordinator.AwaitedIdentity, "기다린 부팅이어야 한다");
            Assert.GreaterOrEqual(frames, 3, "uid가 올 때까지 프레임을 썼어야 한다");
            Assert.AreNotEqual(CloudSaveState.LocalOnly, choice.state);
        }

        /**
         * ★★ **서버가 최신이면 서버 저장으로 부팅한다.** 기다린 값이 여기서 나온다.
         */
        [Test]
        public void AnAdvancedServerIsAdoptedAfterTheWait()
        {
            int frames = 0;
            var local = Local();

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => frames >= 2 ? Uid : string.Empty);
            CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);

            // 사이드카가 이 uid의 것이고 서버가 그보다 앞서 있다
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(Found(Server(), 6L)));

            CloudSaveBootChoice choice = DriveWithSidecar(local, Sidecar(local, 5L), ref frames);

            Assert.AreEqual(CloudSaveDecision.DownloadCloud, choice.decision);
            Assert.IsTrue(choice.fromCloud);
            Assert.AreEqual(171, choice.data.maxStageReached, "서버의 값이어야 한다");
        }

        // ---------------------------------------------------------------- 타임아웃

        /**
         * ★★ **인증이 안 오면 제한 시간 뒤 로컬로 들어간다.** Firebase는 게이트가 아니다.
         */
        [Test]
        public void AnIdentityThatNeverArrivesFallsBackToLocal()
        {
            int frames = 0;
            int fetches = 0;
            var local = Local();

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => string.Empty);   // 영영 안 온다
            CloudSaveCoordinator.UseClockForTests(() => frames * 1f);       // 프레임당 1초
            CloudSaveCoordinator.UseFetchForTests(uid => { fetches++; return Task.FromResult(Missing()); });

            CloudSaveBootChoice choice = Drive(local, ref frames);

            Assert.AreEqual(0, fetches, "uid가 없으면 서버를 부르지 않는다");
            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreSame(local, choice.data, "로컬 그대로 들어간다");
            Assert.AreEqual(CloudSaveStoreStatus.Offline, CloudSaveCoordinator.LastServerStatus);

            // 예산은 하나다 - 인증 대기가 그것을 다 쓰면 거기서 끝난다
            Assert.LessOrEqual(frames, Mathf(CloudSavePolicy.BootServerCheckSeconds) + 2,
                "제한 시간을 넘겨 계속 기다리면 안 된다");
        }

        /** ★ 서버 조회가 안 끝나도 제한 시간 뒤 로컬로 들어간다 */
        [Test]
        public void AServerFetchThatNeverCompletesFallsBackToLocal()
        {
            int frames = 0;
            var local = Local();
            var never = new TaskCompletionSource<CloudSaveFetchResult>();

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => Uid);            // uid는 곧바로 있다
            CloudSaveCoordinator.UseClockForTests(() => frames * 1f);
            CloudSaveCoordinator.UseFetchForTests(uid => never.Task);       // 영영 안 끝난다

            CloudSaveBootChoice choice = Drive(local, ref frames);

            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreSame(local, choice.data);
            Assert.AreEqual(CloudSaveStoreStatus.Offline, CloudSaveCoordinator.LastServerStatus);
        }

        /**
         * ★★ **오프라인에서도 반드시 로컬로 진입한다.**
         *
         * 부팅이 안 끝나는 것이 최악이다 - 방치형에서 첫 화면이 안 뜨면 그 실행은
         * 통째로 사라진다. 오프라인은 실패가 아니라 정상 경로다.
         */
        [Test]
        public void OfflineAlwaysEntersTheGame()
        {
            int frames = 0;
            var local = Local();

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => Uid);
            CloudSaveCoordinator.UseClockForTests(() => frames * 1f);
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(Offline()));

            CloudSaveBootChoice choice = Drive(local, ref frames);

            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreSame(local, choice.data);
            Assert.IsNotNull(choice.data, "부팅은 언제나 한 벌을 들고 끝난다");
        }

        // ---------------------------------------------------------------- 사슬 이동

        /**
         * ★★ **클라우드를 채택하면 사슬도 그 자리로 옮긴다.**
         *
         * 62.1 실기가 잡은 결함이다. 부팅이 서버 rev 124를 채택했는데 사이드카는
         * base 91에 남아, 2분 뒤 커밋이 **방금 채택한 그 기록을 두고** 충돌을 냈다:
         *
         *     [CloudSave] 부팅 선택: 클라우드 채택 (rev 124, 43층 · Lv.61)
         *     [CloudSave] 서버와 갈라졌습니다 (서버 rev 124). 기록 선택이 필요합니다.
         *
         * 최신을 받아 놓고 곧바로 다시 고르라고 묻는 것은 P0를 고친 의미를 없앤다.
         * 62.1 이전에는 실기 부팅이 클라우드를 채택하는 일이 아예 없어(그것이 P0-1)
         * 이 빠짐이 드러나지 않았다.
         */
        [Test]
        public void AdoptingTheCloudAlsoMovesTheChain()
        {
            int frames = 0;
            var local = Local();

            using (new SaveSandbox())
            {
                CloudSaveSidecar.Save(Sidecar(local, 5L));
                CloudSaveCoordinator.UseSidecarPresenceForTests(null);
                CloudSaveCoordinator.UseIdentityForTests(() => Uid);
                CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);
                CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Found(Server(), 9L)));

                CloudSaveBootChoice choice = Drive(local, ref frames);

                Assert.AreEqual(CloudSaveDecision.DownloadCloud, choice.decision);

                // ★ 62.1.1에서 계약이 **강해졌다**: 채택만으로는 아직 안 옮긴다.
                // 디스크에 안 들어간 기록을 사슬의 근거로 삼으면, 저장이 실패했을 때
                // 옛 로컬이 "서버에서 파생된 변경분"으로 읽힌다(§11.17).
                Assert.AreEqual(5L, CloudSaveSidecar.Load().baseRevision,
                    "저장 전에는 사슬이 움직이지 않는다");

                // 로컬 저장이 성공한 뒤에야 옮긴다
                Assert.IsTrue(SaveSystem.Save(Server()));
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                CloudSaveLocalState after = CloudSaveSidecar.Load();
                Assert.IsNotNull(after, "채택 뒤에도 사슬은 있어야 한다");
                Assert.AreEqual(9L, after.baseRevision,
                    "사슬이 서버 자리로 안 옮겨지면 다음 커밋이 곧바로 충돌을 낸다");
            }
        }

        /**
         * ★ 사슬이 **없던** 기기가 서버와 같은 기록을 보면 사슬을 새로 세운다.
         *
         * 재설치·복구 직후의 모양이다. 여기서 사슬을 안 세우면 첫 커밋이 base 0으로
         * 나가고, 트랜잭션이 서버 revision과 어긋나 곧바로 충돌이 된다.
         */
        [Test]
        public void AChainlessDeviceGetsAChainWhenItMatchesTheServer()
        {
            int frames = 0;
            var same = Local();

            using (new SaveSandbox())
            {
                // 사이드카 없음 + 서버가 **같은 기록**을 들고 있다 → InSync
                CloudSaveCoordinator.UseSidecarPresenceForTests(true);
                CloudSaveCoordinator.UseIdentityForTests(() => Uid);
                CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);
                CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Found(same, 4L)));

                CloudSaveBootChoice choice = Drive(same, ref frames);

                Assert.AreEqual(CloudSaveDecision.InSync, choice.decision);

                // 62.1.1: 저장 성공 전에는 사슬을 **세우지도** 않는다
                Assert.IsNull(CloudSaveSidecar.Load(),
                    "저장 전에 세운 사슬은 디스크에 없는 기록을 가리킨다");

                Assert.IsTrue(SaveSystem.Save(same));
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                CloudSaveLocalState after = CloudSaveSidecar.Load();
                Assert.IsNotNull(after, "사슬을 세우지 않으면 첫 커밋이 base 0으로 나간다");
                Assert.AreEqual(4L, after.baseRevision);
                Assert.AreEqual(Uid, after.ownerUid);
            }
        }

        // ---------------------------------------------------------------- uid 불일치

        /**
         * ★★ **기다린 사이드카가 남의 것이면 자동 적용하지 않는다.**
         *
         * 기다리는 동안 계정이 갈릴 수 있다(복구). 그때 그 사이드카는 이 uid의
         * 사슬이 아니고, 사슬 없이 서버 기록을 얹는 것은 **남의 진행을 내
         * 기기에 자동으로 씌우는 일**이다. 답은 사람이 고르는 것이다.
         */
        [Test]
        public void ASidecarFromAnotherAccountIsNeverAutoApplied()
        {
            int frames = 0;
            var local = Local();

            CloudSaveCoordinator.UseSidecarPresenceForTests(true);
            CloudSaveCoordinator.UseIdentityForTests(() => frames >= 2 ? "a-different-uid" : string.Empty);
            CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);
            CloudSaveCoordinator.UseFetchForTests(uid => Task.FromResult(Found(Server(), 9L)));

            // 사이드카는 **다른** uid의 것이다 (Drive가 OwnSidecar로 걸러 낸다)
            CloudSaveBootChoice choice = DriveWithSidecar(local, Sidecar(local, 8L), ref frames);

            Assert.AreNotEqual(CloudSaveDecision.DownloadCloud, choice.decision,
                "사슬이 없는데 서버를 자동으로 얹으면 안 된다");
            Assert.IsFalse(choice.fromCloud, "남의 기록이 조용히 적용됐다");
            Assert.AreEqual(CloudSaveDecision.Conflict, choice.decision,
                "근거가 없으면 사람이 고른다");
        }

        // ---------------------------------------------------------------- 한 번만

        /**
         * ★★ **어느 갈래로 끝나든 부팅 선택은 정확히 한 번 나온다.**
         *
         * 갈래가 셋(인증 타임아웃·서버 타임아웃·정상)으로 늘었다. `onDone`이 두 번
         * 불리면 `GameSession.BootWith`가 두 번 돌고, 그것은 곧 **방치 보상 두 번**이다 -
         * 59단계가 없앤 사고 그대로다.
         */
        [Test]
        public void EveryPathReportsExactlyOneChoice()
        {
            foreach (var lane in new[] { "지연 로그인", "인증 타임아웃", "서버 타임아웃", "오프라인" })
            {
                CloudSaveCoordinator.ResetForTests();

                int frames = 0;
                int reported = 0;
                var never = new TaskCompletionSource<CloudSaveFetchResult>();

                CloudSaveCoordinator.UseSidecarPresenceForTests(true);
                CloudSaveCoordinator.UseClockForTests(() => frames * 1f);

                switch (lane)
                {
                    case "지연 로그인":
                        CloudSaveCoordinator.UseIdentityForTests(() => frames >= 2 ? Uid : string.Empty);
                        CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Missing()));
                        break;
                    case "인증 타임아웃":
                        CloudSaveCoordinator.UseIdentityForTests(() => string.Empty);
                        CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Missing()));
                        break;
                    case "서버 타임아웃":
                        CloudSaveCoordinator.UseIdentityForTests(() => Uid);
                        CloudSaveCoordinator.UseFetchForTests(u => never.Task);
                        break;
                    default:
                        CloudSaveCoordinator.UseIdentityForTests(() => Uid);
                        CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Offline()));
                        break;
                }

                IEnumerator routine = CloudSaveCoordinator.ChooseBootSave(
                    Local(), _ => reported++);

                while (routine.MoveNext()) frames++;

                Assert.AreEqual(1, reported, lane + " 갈래가 부팅 선택을 " + reported + "번 냈다");
            }
        }

        /** ★ 방치 보상 지급 자리는 여전히 하나다 (59단계 계약, 소스에서 센다) */
        [Test]
        public void TheOfflineRewardStillHasExactlyOneGrantSite()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath),
                "Assets/_Project/Scripts/Subsystems/GameSession.cs"));

            int grants = 0;
            int index = source.IndexOf("GrantOfflineReward(", StringComparison.Ordinal);
            while (index >= 0)
            {
                grants++;
                index = source.IndexOf("GrantOfflineReward(", index + 1, StringComparison.Ordinal);
            }

            Assert.AreEqual(2, grants,
                "정의 1 + 호출 1이어야 한다 - 지급 자리가 늘면 보상이 두 번 나간다");
        }

        /** ★ 부팅이 두 조건을 **함께** 본다 - 하나만 보면 P0가 그대로 남는다 */
        [Test]
        public void TheSessionAsksBothBootQuestions()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath),
                "Assets/_Project/Scripts/Subsystems/GameSession.cs"));

            StringAssert.Contains("WillCheckServer", source);
            StringAssert.Contains("WillAwaitIdentity", source,
                "인증 대기 조건을 안 보면 지연 로그인 기기가 그대로 로컬로 확정된다");
        }

        // ---------------------------------------------------------------- 도구

        /** 코루틴을 손으로 돌린다. `yield return null`이 한 프레임이다 */
        private static CloudSaveBootChoice Drive(SaveData local, ref int frames)
        {
            CloudSaveBootChoice choice = null;
            IEnumerator routine = CloudSaveCoordinator.ChooseBootSave(local, picked => choice = picked);

            while (routine.MoveNext()) frames++;

            Assert.IsNotNull(choice, "부팅은 언제나 한 벌을 들고 끝나야 한다");
            return choice;
        }

        /**
         * @brief 사이드카를 디스크에 두고 돌린다.
         *
         * `OwnSidecar`가 uid로 거르므로, 사이드카의 주인과 로그인 uid가 갈리는
         * 갈래도 이 경로로 그대로 재현된다.
         */
        private static CloudSaveBootChoice DriveWithSidecar(SaveData local,
                                                            CloudSaveLocalState sidecar,
                                                            ref int frames)
        {
            using (new SaveSandbox())
            {
                CloudSaveSidecar.Save(sidecar);
                CloudSaveCoordinator.UseSidecarPresenceForTests(null);   // 진짜 디스크를 본다

                return Drive(local, ref frames);
            }
        }

        private static int Mathf(float seconds)
        {
            return (int)seconds;
        }

        private static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            return data;
        }

        private static SaveData Server()
        {
            var data = SaveData.NewGame();
            data.stage = 171;
            data.maxStageReached = 171;
            data.goldPerSecond = 1000d;
            return data;
        }

        private static CloudSaveLocalState Sidecar(SaveData local, long revision)
        {
            var state = CloudSaveLocalState.NewFor(Uid, CloudSaveIds.New());
            state.MarkSynced(revision, CloudSaveFingerprint.HashOf("payload"),
                             CloudSaveFingerprint.StateHashOf(local), 0L);
            return state;
        }

        private static CloudSaveFetchResult Found(SaveData data, long revision)
        {
            return new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = CloudSaveEnvelope.ForUpload(data, revision - 1L, CloudSaveIds.New(),
                                                       CloudSaveIds.New(), CloudSaveIds.New())
            };
        }

        private static CloudSaveFetchResult Missing()
        {
            return new CloudSaveFetchResult { status = CloudSaveStoreStatus.Missing };
        }

        private static CloudSaveFetchResult Offline()
        {
            return new CloudSaveFetchResult { status = CloudSaveStoreStatus.Offline };
        }
    }
}
