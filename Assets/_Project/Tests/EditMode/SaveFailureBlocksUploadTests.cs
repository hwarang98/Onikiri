using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 로컬 저장 실패는 **클라우드 업로드를 막는다** (62.1.2).
     *
     * ## 무엇이 부러져 있었는가
     *
     * `GameSession.Save()`가 이랬다:
     *
     *     if (SaveSystem.Save(data)) CloudSaveCoordinator.NoteLocalSaveCommitted();
     *     CloudSaveSync.NoteSaved(data);          ← 실패해도 돌았다
     *
     * 저장이 실패해도 `NoteSaved`가 그 스냅샷을 업로드 후보로 등록했다. 그리고
     * `SaveRequested`가 `Action`이라 실패가 `Tick`으로 돌아오지 않아, urgent
     * 커밋이 **디스크에 없는 메모리 스냅샷**을 그대로 올릴 수 있었다.
     *
     * 더 나쁜 갈래가 하나 더 있다: 저장이 실패하면 `pendingData`에는 **실패 전의
     * 더 낡은 스냅샷**이 남아 있고, 그것이 대신 올라간다. 서버가 로컬보다 앞선
     * 것을 들고 있다는 거짓이 만들어지고, 다음 부팅이 그 거짓을 근거로 판정한다.
     *
     * ## 계약
     *
     * 저장이 실패하면 **그 커밋은 없다.** urgent 표시는 지우지 않는다 - 지불은
     * 여전히 서버에 없으므로, 다음 저장 성공이 최신 한 벌로 올려야 한다.
     */
    public class SaveFailureBlocksUploadTests
    {
        private const string Uid = "save-failure-uid";

        private float clock;

        [SetUp]
        public void SetUp()
        {
            clock = 0f;

            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            SaveSystem.FailNextSaveForTests = false;
            CloudSaveSidecar.FailNextSaveForTests = false;

            CloudSaveSync.UseClockForTests(() => clock);
            CloudSaveSync.SuppressCommitForTests = true;      // Firebase 없이 센다

            // 쓰기가 허용된 상태로 둔다 - 막히는 이유가 저장 실패여야 한다
            CloudSaveCoordinator.NoteSyncResult(CloudSaveState.Dirty);
#if UNITY_EDITOR
            CloudSaveSync.EditorNetworkAllowed = true;
#endif
        }

        [TearDown]
        public void TearDown()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            SaveSystem.FailNextSaveForTests = false;
            CloudSaveSidecar.FailNextSaveForTests = false;
        }

        // ---------------------------------------------------------------- urgent 커밋

        /**
         * ★★ **저장이 실패하면 urgent 커밋이 0회다.**
         */
        [Test]
        public void AFailedSaveStopsTheUrgentCommit()
        {
            CloudSaveSync.NoteSaved(Snapshot(10));            // 실패 전의 낡은 스냅샷
            CloudSaveSync.RequestUrgent("보석 소비");
            CloudSaveSync.SaveRequested = () => false;        // 저장이 실패한다

            Advance();
            CloudSaveSync.Tick();

            Assert.AreEqual(0, CloudSaveSync.CommitAttemptsForTests,
                "디스크에 없는 기록이 서버로 나갔다");
        }

        /**
         * ★★ **실패 전의 낡은 `pendingData`도 대신 올라가지 않는다.**
         *
         * 이쪽이 더 조용한 사고다 - 올라간 것이 "지금 상태"가 아니라 "옛 상태"라
         * 로그만 봐서는 정상 업로드와 구분되지 않는다.
         */
        [Test]
        public void AStalePendingSnapshotIsNotUploadedInstead()
        {
            SaveData stale = Snapshot(10);
            CloudSaveSync.NoteSaved(stale);
            CloudSaveSync.RequestUrgent("뽑기");

            // 저장은 실패하지만 낡은 후보는 그대로 남아 있다
            CloudSaveSync.SaveRequested = () => false;
            Assert.IsTrue(CloudSaveSync.HasPendingSnapshotForTests, "낡은 후보가 남아 있다");

            Advance();
            CloudSaveSync.Tick();

            Assert.AreEqual(0, CloudSaveSync.CommitAttemptsForTests,
                "낡은 스냅샷이 최신인 척 올라갔다");
        }

        /** ★★ 실패해도 **urgent 표시는 살아 있다** - 지불은 아직 서버에 없다 */
        [Test]
        public void TheUrgentRequestSurvivesAFailedSave()
        {
            CloudSaveSync.NoteSaved(Snapshot(10));
            CloudSaveSync.RequestUrgent("보석 소비");
            float marked = CloudSaveSync.UrgentSinceForTests;

            CloudSaveSync.SaveRequested = () => false;
            Advance();
            CloudSaveSync.Tick();

            Assert.AreEqual(marked, CloudSaveSync.UrgentSinceForTests,
                "urgent을 지우면 그 지불은 120초 디바운스까지 서버에 없다");
        }

        /**
         * ★★ **다음 저장이 성공하면 최신 한 벌이 정확히 한 번 올라간다.**
         *
         * 실패 구간에서는 커밋이 0이고, 성공 뒤에 1이다 - 그 사이 revision이
         * 오를 자리가 없다.
         */
        [Test]
        public void TheNextSuccessfulSaveCommitsTheLatestSnapshotExactlyOnce()
        {
            CloudSaveSync.NoteSaved(Snapshot(10));
            CloudSaveSync.RequestUrgent("보석 소비");

            // ① 실패 - 커밋 0
            CloudSaveSync.SaveRequested = () => false;
            Advance();
            CloudSaveSync.Tick();
            Assert.AreEqual(0, CloudSaveSync.CommitAttemptsForTests, "실패 구간에 커밋이 있었다");

            // ② 다음 저장이 성공하고 **최신** 스냅샷을 등록한다
            SaveData latest = Snapshot(42);
            CloudSaveSync.SaveRequested = () =>
            {
                CloudSaveSync.NoteSaved(latest);
                return true;
            };

            Advance(CloudSaveSyncPolicy.RetryDelaySeconds + 1f);   // 재시도 유예를 넘긴다
            CloudSaveSync.Tick();

            Assert.AreEqual(1, CloudSaveSync.CommitAttemptsForTests,
                "성공 뒤에는 정확히 한 번 올라간다");

            // ③ 그 프레임 뒤에 또 올리지 않는다
            Advance();
            CloudSaveSync.Tick();
            Assert.AreEqual(1, CloudSaveSync.CommitAttemptsForTests,
                "같은 지불로 revision이 두 번 오르면 백업이 한 칸 밀린다");
        }

        /** ★ 저장이 성공하면 막히지 않는다 - 방어가 과해지면 동기화가 멈춘다 */
        [Test]
        public void ASuccessfulSaveStillCommits()
        {
            CloudSaveSync.RequestUrgent("보석 소비");
            CloudSaveSync.SaveRequested = () =>
            {
                CloudSaveSync.NoteSaved(Snapshot(42));
                return true;
            };

            Advance();
            CloudSaveSync.Tick();

            Assert.AreEqual(1, CloudSaveSync.CommitAttemptsForTests);
        }

        // ---------------------------------------------------------------- 사슬 예약

        /**
         * ★★ **sidecar 저장이 실패하면 사슬 확정 예약이 살아남는다.**
         *
         * 예전에는 들어오자마자 예약을 풀었다. 그러면 sidecar 쓰기가 실패했을 때
         * 아무도 다시 시도하지 않고, 로컬은 서버 기록인데 사슬은 옛 자리에 남는다 -
         * 다음 커밋이 방금 채택한 그 기록을 두고 충돌을 낸다.
         */
        [Test]
        public void ASidecarWriteFailureKeepsTheChainReservation()
        {
            using (new SaveSandbox())
            {
                ArmChainViaBoot(9L);
                Assert.AreEqual(9L, CloudSaveCoordinator.PendingChainRevision);

                CloudSaveSidecar.FailNextSaveForTests = true;
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                Assert.AreEqual(9L, CloudSaveCoordinator.PendingChainRevision,
                    "sidecar가 디스크에 못 남았는데 예약을 풀면 아무도 다시 안 한다");
            }
        }

        /** ★★ 재시도가 성공하면 예약이 **한 번만** 풀린다 */
        [Test]
        public void ARetriedSidecarWriteClearsTheReservationOnce()
        {
            using (new SaveSandbox())
            {
                ArmChainViaBoot(9L);

                CloudSaveSidecar.FailNextSaveForTests = true;
                CloudSaveCoordinator.NoteLocalSaveCommitted();
                Assert.AreEqual(9L, CloudSaveCoordinator.PendingChainRevision);

                CloudSaveCoordinator.NoteLocalSaveCommitted();       // 재시도 성공
                Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision);
                Assert.AreEqual(9L, CloudSaveSidecar.Load().baseRevision);

                CloudSaveCoordinator.NoteLocalSaveCommitted();       // 또 불러도 무해
                Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision);
                Assert.AreEqual(9L, CloudSaveSidecar.Load().baseRevision);
            }
        }

        // ---------------------------------------------------------------- 계약 배선

        /**
         * ★★ `GameSession.Save()`가 **실패하면 `NoteSaved`까지 가지 않는다.**
         *
         * MonoBehaviour라 EditMode에서 직접 부를 수 없어 소스로 순서를 못 박는다 -
         * 이 순서가 뒤집히면 위의 모든 검사가 초록인 채로 계약이 깨진다.
         */
        [Test]
        public void TheSessionSaveReturnsEarlyWhenTheDiskWriteFails()
        {
            string source = Read("Assets/_Project/Scripts/Subsystems/GameSession.cs");

            int save = source.IndexOf("public bool Save()", StringComparison.Ordinal);
            Assert.Greater(save, -1, "Save()가 성공 여부를 돌려주지 않는다");

            string body = source.Substring(save);

            int guard = body.IndexOf("if (!SaveSystem.Save(data))", StringComparison.Ordinal);
            int noteSaved = body.IndexOf("CloudSaveSync.NoteSaved(", StringComparison.Ordinal);

            Assert.Greater(guard, -1, "저장 실패를 보는 갈래가 없다");
            Assert.Greater(noteSaved, -1, "NoteSaved 호출이 사라졌다");
            Assert.Less(guard, noteSaved,
                "실패 검사가 NoteSaved보다 뒤에 있다 - 그러면 디스크에 없는 스냅샷이 "
                + "업로드 후보가 된다");
        }

        /** ★ `SaveRequested`가 **성공 여부를 돌려주는** 모양이다 */
        [Test]
        public void TheSaveHookReportsSuccess()
        {
            StringAssert.Contains("Func<bool> SaveRequested",
                Read("Assets/_Project/Scripts/Cloud/CloudSaveSync.cs"),
                "Action이면 실패가 Tick으로 돌아오지 않는다");

            StringAssert.Contains("CloudSaveSync.SaveRequested = Save",
                Read("Assets/_Project/Scripts/Subsystems/GameSession.cs"));
        }

        /**
         * ★★ **pause 경로도 같은 계약을 지난다.**
         *
         * `OnApplicationPause(true)`는 `Save()` 바로 뒤에 `OnAppPaused()`를 부른다.
         * 그 저장이 실패했는데 결과를 안 넘기면, urgent 갈래에서 막은 것이
         * **pause 갈래로 그대로 나간다** - 낡은 `pendingData`가 올라간다.
         */
        [Test]
        public void ThePausePathCarriesTheSaveResult()
        {
            StringAssert.Contains("public static void OnAppPaused(bool localSaveOk",
                Read("Assets/_Project/Scripts/Cloud/CloudSaveSync.cs"),
                "pause 커밋이 저장 성공 여부를 모르면 낡은 스냅샷을 올린다");

            string session = Read("Assets/_Project/Scripts/Subsystems/GameSession.cs");

            StringAssert.Contains("bool saved = Save();", session);
            StringAssert.Contains("CloudSaveSync.OnAppPaused(saved)", session,
                "결과를 받아놓고 안 넘기면 인자가 있는 것만으로는 아무것도 막히지 않는다");
        }

        /** ★ 두 저장 seam이 출시 컴파일에 없다 */
        [Test]
        public void TheFailureSeamsAreCompiledOutOfReleaseBuilds()
        {
            foreach (string file in new[]
            {
                "Assets/_Project/Scripts/SaveGame/SaveSystem.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSidecar.cs",
                "Assets/_Project/Scripts/Cloud/CloudSaveSync.cs"
            })
            {
                string source = Read(file);

                foreach (string seam in new[] { "FailNextSaveForTests",
                                                 "SuppressCommitForTests",
                                                 "CommitAttemptsForTests" })
                {
                    int index = source.IndexOf(seam, StringComparison.Ordinal);

                    while (index >= 0)
                    {
                        string before = source.Substring(0, index);
                        Assert.Greater(Count(before, "#if UNITY_EDITOR"), Count(before, "#endif"),
                            file + " 의 " + seam + " 이 컴파일 가드 밖에 있다 (offset " + index + ")");

                        index = source.IndexOf(seam, index + 1, StringComparison.Ordinal);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 도구

        private void Advance(float seconds = 3f)
        {
            clock += seconds;
        }

        /**
         * 부팅이 클라우드를 채택한 상태를 만든다 - 사슬 **예약**만 서고, 확정은
         * `NoteLocalSaveCommitted`를 기다린다. 사슬이 없는 기기라 두 기록이
         * 같아야 채택 갈래(InSync)로 간다(다르면 충돌이 정답이다).
         */
        private static void ArmChainViaBoot(long revision)
        {
            SaveData same = Snapshot(12);
            CloudSaveEnvelope server = CloudSaveEnvelope.ForUpload(
                same, revision - 1L, CloudSaveIds.New(), CloudSaveIds.New(), CloudSaveIds.New());

            Assert.IsNotNull(server, "봉투를 못 만들면 이 검사의 전제가 없다");

            CloudSaveCoordinator.UseIdentityForTests(() => Uid);
            CloudSaveCoordinator.UseClockForTests(() => 0f);
            CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = server
            }));

            CloudSaveBootChoice choice = null;
            IEnumerator routine = CloudSaveCoordinator.ChooseBootSave(same, p => choice = p);
            while (routine.MoveNext()) { }

            Assert.IsNotNull(choice);
            Assert.AreEqual(CloudSaveDecision.InSync, choice.decision,
                "채택 갈래여야 사슬 예약이 선다");
            Assert.AreEqual(revision, CloudSaveCoordinator.PendingChainRevision,
                "예약이 서지 않으면 그 뒤의 검사가 아무것도 재지 않는다");
        }

        private static SaveData Snapshot(int stage)
        {
            var data = SaveData.NewGame();
            data.stage = stage;
            data.maxStageReached = stage;
            return data;
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), relative));
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
