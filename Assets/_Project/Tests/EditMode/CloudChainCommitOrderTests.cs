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
     * @brief 클라우드 채택의 **저장 확정 순서** (62.1.1 P1).
     *
     * ## 무엇이 부러져 있었는가
     *
     * `CloudSaveCoordinator.Finish`가 판정 직후 사이드카를 서버 revision으로
     * 옮겼다. 그런데 고른 클라우드 한 벌이 **디스크에 들어가는 것은 그 뒤**다
     * (`GameSession.BootWith` → `Save()`). 그 사이에 앱이 죽거나 저장이
     * 실패하면 이런 상태가 남는다:
     *
     *     로컬 세이브 파일 = 옛 기록
     *     sidecar base     = 최신 서버 revision
     *
     * 다음 동기화는 그 옛 기록을 **서버에서 파생된 변경분**으로 읽는다
     * (`localChanged` 판정이 `lastSyncedStateSha`를 기준으로 하기 때문이다).
     * 그러면 다른 기기의 최신 진행 위에 옛 기록이 올라간다 - 이 설계 전체가
     * 막으려는 사고가 정확히 그것이다.
     *
     * ## 계약
     *
     *     클라우드 선택 → 메모리 적용 → 방치 보상 1회
     *     → **로컬 저장 성공** → sidecar 서버 사슬 확정 → 동기화 허용
     *
     * 저장 실패를 만들 수 있어야 잴 수 있으므로 `SaveSystem.FailNextSaveForTests`
     * seam을 쓴다(실제 파일 권한을 부수면 그 뒤 검사가 전부 오염된다).
     */
    public class CloudChainCommitOrderTests
    {
        private const string Uid = "chain-order-uid";

        [SetUp]
        public void SetUp()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            SaveSystem.FailNextSaveForTests = false;
        }

        [TearDown]
        public void TearDown()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            SaveSystem.FailNextSaveForTests = false;
        }

        // ---------------------------------------------------------------- 순서

        /**
         * ★★ **채택 직후에는 사슬이 아직 안 움직인다.** P1의 핵심 한 줄.
         */
        [Test]
        public void AdoptingTheCloudDoesNotMoveTheChainYet()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                CloudSaveSidecar.Save(Sidecar(local, 5L));

                Adopt(local, 9L);

                Assert.AreEqual(5L, CloudSaveSidecar.Load().baseRevision,
                    "저장 전에 사슬이 움직이면, 저장이 실패했을 때 옛 로컬이 "
                    + "서버에서 파생된 것으로 읽힌다");
                Assert.AreEqual(9L, CloudSaveCoordinator.PendingChainRevision,
                    "확정 대기 중이어야 한다");
            }
        }

        /** ★★ **로컬 저장이 성공한 뒤에야** 사슬이 서버 값으로 간다 */
        [Test]
        public void TheChainMovesOnlyAfterTheLocalSaveSucceeds()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                CloudSaveSidecar.Save(Sidecar(local, 5L));

                CloudSaveEnvelope server = Envelope(Server(), 9L);
                Adopt(local, 9L, server);

                Assert.IsTrue(SaveSystem.Save(Server()), "저장이 성공해야 한다");
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                CloudSaveLocalState after = CloudSaveSidecar.Load();
                Assert.AreEqual(9L, after.baseRevision);
                Assert.AreEqual(server.stateSha256, after.lastSyncedStateSha256,
                    "지문도 서버 값이어야 다음 판정이 옳다");
                Assert.AreEqual(server.payloadSha256, after.lastSyncedPayloadSha256);
                Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision,
                    "확정 뒤에는 대기가 남지 않는다");
            }
        }

        /** ★★ **저장이 실패하면 사슬은 그대로 있는다** */
        [Test]
        public void AFailedSaveLeavesTheChainWhereItWas()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                CloudSaveSidecar.Save(Sidecar(local, 5L));
                SaveSystem.Save(local);                      // 디스크의 정본 = 옛 기록

                Adopt(local, 9L);

                SaveSystem.FailNextSaveForTests = true;
                Assert.IsFalse(SaveSystem.Save(Server()), "seam이 실패를 만들어야 한다");
                // 저장이 실패했으므로 GameSession은 NoteLocalSaveCommitted를 부르지 않는다

                Assert.AreEqual(5L, CloudSaveSidecar.Load().baseRevision,
                    "디스크에 안 들어간 기록을 사슬의 근거로 삼으면 안 된다");
                Assert.AreEqual(9L, CloudSaveCoordinator.PendingChainRevision,
                    "예약은 살아 있어야 다음 저장이 마저 한다");
            }
        }

        /**
         * ★★ 첫 저장이 실패해도 **다음 저장 성공에서 정확히 한 번** 옮긴다.
         */
        [Test]
        public void ARetriedSaveCompletesThePendingChainExactlyOnce()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                CloudSaveSidecar.Save(Sidecar(local, 5L));

                Adopt(local, 9L);

                // ① 실패
                SaveSystem.FailNextSaveForTests = true;
                Assert.IsFalse(SaveSystem.Save(Server()));
                Assert.AreEqual(5L, CloudSaveSidecar.Load().baseRevision);

                // ② 다음 자동 저장이 성공
                Assert.IsTrue(SaveSystem.Save(Server()));
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                Assert.AreEqual(9L, CloudSaveSidecar.Load().baseRevision);
                Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision);

                // ③ 또 불러도 아무 일도 없다 - 확정은 한 번뿐이다
                CloudSaveCoordinator.NoteLocalSaveCommitted();
                Assert.AreEqual(9L, CloudSaveSidecar.Load().baseRevision);
                Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision);
            }
        }

        /**
         * ★★ 저장 성공 전에 앱이 죽으면 **옛 로컬과 옛 사슬이 맞물린다.**
         *
         * 다음 부팅이 보는 것이 그 둘이고, 둘이 짝이 맞으면 판정은 "로컬이
         * 변한 적 없다"가 된다 - 그래야 서버가 앞섰을 때 조용히 받아올 수 있다.
         */
        [Test]
        public void AKilledAppLeavesTheOldSaveAndOldChainMatching()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                SaveSystem.Save(local);

                CloudSaveLocalState before = Sidecar(local, 5L);
                CloudSaveSidecar.Save(before);

                Adopt(local, 9L);

                SaveSystem.FailNextSaveForTests = true;
                SaveSystem.Save(Server());

                // ---- 앱이 죽었다. 다음 실행이 디스크에서 읽는 것은 이 둘이다
                CloudSaveCoordinator.ResetForTests();

                SaveData onDisk = SaveSystem.Load();
                CloudSaveLocalState chain = CloudSaveSidecar.Load();

                Assert.AreEqual(5L, chain.baseRevision, "옛 사슬 그대로");
                Assert.AreEqual(local.maxStageReached, onDisk.maxStageReached, "옛 로컬 그대로");
                Assert.AreEqual(CloudSaveFingerprint.StateHashOf(onDisk),
                                chain.lastSyncedStateSha256,
                    "둘이 짝이 맞아야 '로컬은 변한 적 없다'로 읽힌다");
            }
        }

        // ---------------------------------------------------------------- 사슬 없는 기기

        /** ★★ 사슬이 없던 기기는 **저장 성공 전에 사이드카를 만들지 않는다** */
        [Test]
        public void AChainlessDeviceCreatesNoSidecarBeforeTheSaveSucceeds()
        {
            using (new SaveSandbox())
            {
                Assert.IsNull(CloudSaveSidecar.Load(), "시작할 때는 사슬이 없다");

                // 사슬이 없으면 두 기록이 **같아야** 채택 갈래로 간다(InSync).
                // 다르면 어느 쪽에서 이어졌는지 말할 근거가 없어 충돌이 정답이다
                var same = Local();
                Adopt(same, 4L, Envelope(same, 4L));

                Assert.IsNull(CloudSaveSidecar.Load(),
                    "저장 전에 사슬을 세우면 그 사슬이 가리키는 기록이 디스크에 없다");
                Assert.AreEqual(4L, CloudSaveCoordinator.PendingChainRevision);
            }
        }

        /** ★ 저장이 성공하면 그때 **서버 revision으로** 사슬을 세운다 */
        [Test]
        public void AChainlessDeviceGetsItsChainAfterTheSaveSucceeds()
        {
            using (new SaveSandbox())
            {
                var same = Local();
                CloudSaveEnvelope server = Envelope(same, 4L);
                Adopt(same, 4L, server);

                Assert.IsTrue(SaveSystem.Save(same));
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                CloudSaveLocalState chain = CloudSaveSidecar.Load();
                Assert.IsNotNull(chain, "사슬을 안 세우면 첫 커밋이 base 0으로 나간다");
                Assert.AreEqual(4L, chain.baseRevision);
                Assert.AreEqual(Uid, chain.ownerUid);
                Assert.AreEqual(server.stateSha256, chain.lastSyncedStateSha256);
            }
        }

        // ---------------------------------------------------------------- 뒤따르는 결과

        /**
         * ★★ 채택 → 저장 → 확정 뒤의 다음 동기화는 **가짜 충돌을 내지 않는다.**
         *
         * 62.1이 실기에서 잡은 증상이 이것이었다(설계 §11.11). 방치 보상으로
         * 로컬이 서버와 달라지지만, 사슬이 서버 자리에 있으면 그것은 **정상적인
         * 로컬 dirty**이지 갈라짐이 아니다.
         */
        [Test]
        public void TheNextSyncAfterAdoptionUploadsInsteadOfConflicting()
        {
            using (new SaveSandbox())
            {
                var local = Local();
                CloudSaveSidecar.Save(Sidecar(local, 5L));

                CloudSaveEnvelope server = Envelope(Server(), 9L);
                Adopt(local, 9L, server);

                // 방치 보상이 얹힌 뒤의 로컬 - 서버와 다르다
                SaveData afterReward = Server();
                afterReward.gold = afterReward.gold + 12345d;

                Assert.IsTrue(SaveSystem.Save(afterReward));
                CloudSaveCoordinator.NoteLocalSaveCommitted();

                CloudSaveLocalState chain = CloudSaveSidecar.Load();

                var verdict = CloudSavePolicy.Decide(new CloudSaveFacts
                {
                    serverChecked = true,
                    hasLocal = true,
                    hasCloud = true,
                    hasSidecar = true,
                    localStateSha = CloudSaveFingerprint.StateHashOf(afterReward),
                    lastSyncedStateSha = chain.lastSyncedStateSha256,
                    baseRevision = chain.baseRevision,
                    cloudRevision = server.revision,
                    cloudBaseRevision = server.baseRevision,
                    cloudStateSha = server.stateSha256,
                    cloudFormatVersion = server.formatVersion,
                    cloudSaveVersion = server.saveVersion,
                    cloudEnvelopeFault = CloudSaveEnvelopeFault.None
                });

                Assert.AreEqual(CloudSaveDecision.UploadLocal, verdict.decision,
                    "방치 보상 변경분은 정상 dirty다 - 여기서 Conflict가 나면 "
                    + "최신을 받아 놓고 곧바로 다시 고르라고 묻는 것이 된다");
            }
        }

        /** ★ seam은 **한 번만** 실패시킨다 - 켠 채로 잊으면 그 실행이 통째로 사라진다 */
        [Test]
        public void TheFailureSeamDisarmsItself()
        {
            using (new SaveSandbox())
            {
                SaveSystem.FailNextSaveForTests = true;

                Assert.IsFalse(SaveSystem.Save(Local()));
                Assert.IsFalse(SaveSystem.FailNextSaveForTests, "스스로 꺼져야 한다");
                Assert.IsTrue(SaveSystem.Save(Local()), "다음 저장은 정상이다");
            }
        }

        /** ★ 저장 seam이 출시 컴파일에 없다 */
        [Test]
        public void TheFailureSeamIsCompiledOutOfReleaseBuilds()
        {
            string file = "Assets/_Project/Scripts/SaveGame/SaveSystem.cs";
            string source = File.ReadAllText(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), file));

            int index = source.IndexOf("FailNextSaveForTests", StringComparison.Ordinal);
            Assert.Greater(index, -1);

            while (index >= 0)
            {
                string before = source.Substring(0, index);
                Assert.Greater(Count(before, "#if UNITY_EDITOR"), Count(before, "#endif"),
                    file + " 의 저장 실패 seam이 컴파일 가드 밖에 있다 (offset " + index + ")");

                index = source.IndexOf("FailNextSaveForTests", index + 1, StringComparison.Ordinal);
            }
        }

        // ---------------------------------------------------------------- 도구

        /** 부팅이 클라우드를 채택한 상태를 만든다 (코루틴을 손으로 돌린다) */
        private static void Adopt(SaveData local, long revision, CloudSaveEnvelope envelope = null)
        {
            CloudSaveEnvelope server = envelope ?? Envelope(Server(), revision);

            CloudSaveCoordinator.UseIdentityForTests(() => Uid);
            CloudSaveCoordinator.UseClockForTests(() => 0f);
            CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = server
            }));

            CloudSaveBootChoice choice = null;
            IEnumerator routine = CloudSaveCoordinator.ChooseBootSave(local, p => choice = p);
            while (routine.MoveNext()) { }

            Assert.IsNotNull(choice);
            Assert.IsTrue(choice.decision == CloudSaveDecision.DownloadCloud
                          || choice.decision == CloudSaveDecision.InSync,
                "이 검사는 서버 사슬을 채택하는 갈래를 전제한다 (실제 = "
                + choice.decision + ")");
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

        private static CloudSaveEnvelope Envelope(SaveData data, long revision)
        {
            return CloudSaveEnvelope.ForUpload(data, revision - 1L, CloudSaveIds.New(),
                                               CloudSaveIds.New(), CloudSaveIds.New());
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
