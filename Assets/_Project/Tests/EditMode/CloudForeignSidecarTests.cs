using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 다른 uid의 사이드카를 만난 부팅 (71단계) - **Recover의 부팅 입구.**
     *
     * ## 70단계 실기가 연 결함
     *
     * 재설치 -> Android 자동 백업이 세이브와 **옛 uid의 사이드카**를 되살림 ->
     * 새 익명 uid로 부팅. 그 뒤 30초마다 `세션 Acquire 실패: PERMISSION_DENIED`,
     * 업로드 0회, 부팅마다 인수 확인.
     *
     * 원인은 한 실행 안에서 기기 id가 **둘**이었던 것이다. 세션 게이트·heartbeat는
     * `CloudSaveSession.DeviceIdFor`(실행마다 하나, 메모리)를, 첫 커밋은
     * `NewDeviceId()`(시도마다 새것)를 썼다. 같은 세션 id에 다른 기기 id를 적는
     * 쓰기를 규칙 `ownerRenew`가 거부했다(Emulator가 같은 거부를 재현한다).
     * 사이드카는 끝내 디스크에 안 남아, 다음 부팅도 기기 id를 모르고 자기
     * 자신에게 인수를 물었다.
     *
     * ## 여기서 재는 것
     *
     *   - 입구는 **정확히 한 번** · 판정은 61단계 `PlanFor` 그대로 (새 갈래 없음)
     *   - 어느 갈래든 새 uid의 사이드카가 **디스크에** 남고, 그 기기 id가 게이트와 같다
     *   - 옛 사이드카·로컬 세이브는 지워지지 않고 `.prerecover`로 남는다
     *   - ⑤ 충돌 재료가 채워진다 · 옛 uid는 읽지 않는다
     *   - 사이드카를 못 남기면 서버로 아무것도 안 간다 (62.1.2 계약)
     */
    public class CloudForeignSidecarTests
    {
        const string OldUid = "old-anon-uid-70";
        const string NewUid = "new-anon-uid-71";

        SaveSandbox sandbox;
        List<string> fetchedUids;
        int frames;

        [SetUp]
        public void Isolate()
        {
            sandbox = new SaveSandbox();
            fetchedUids = new List<string>();
            frames = 0;

            ResetStatics();
        }

        [TearDown]
        public void Restore()
        {
            ResetStatics();

            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        private static void ResetStatics()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudSaveRecovery.ResetForTests();
            CloudSaveSession.ResetForTests();
            CloudSaveSidecar.FailNextSaveForTests = false;
        }

        // ---------------------------------------------------------------- 입구

        /**
         * ★★ **70단계 그 모양**: 옛 uid의 사이드카 + 새 uid + 새 uid의 서버에 없음.
         *
         * 입구 1회 · ③ 갈래 · 새 사이드카가 디스크에 · 기기 id = 게이트 ·
         * 옛 사이드카와 세이브가 `.prerecover`로 · 첫 업로드 urgent.
         */
        [Test]
        public void AForeignSidecarEntersRecoverOnceAndLeavesANewSidecarOnDisk()
        {
            SaveData local = Local(12);
            SeedDisk(local);
            string oldJson = File.ReadAllText(CloudSaveSidecar.Path);

            // 세션 게이트가 부팅 판정보다 **먼저** 이 값을 쓴다 (GameSession.TakeoverGate)
            string gateDevice = CloudSaveSession.DeviceIdFor(NewUid);

            CloudSaveBootChoice choice = Boot(local, Missing());

            Assert.AreEqual(1, CloudSaveRecovery.BootEntries, "입구는 정확히 한 번");
            Assert.AreEqual(CloudRecoveryPlan.KeepLocalAsFirstRevision, CloudSaveRecovery.LastPlan);
            Assert.AreEqual(CloudSaveDecision.UploadLocal, choice.decision,
                "부팅 판정은 그대로다 - 입구가 새 갈래를 만들지 않는다");
            Assert.AreEqual(CloudSaveState.Dirty, choice.state);
            Assert.AreSame(local, choice.data, "로컬 그대로 들어간다");

            CloudSaveLocalState disk = CloudSaveSidecar.Load();
            Assert.IsNotNull(disk, "★ 새 사이드카가 디스크에 없다 - 70단계 결함 그대로");
            Assert.AreEqual(NewUid, disk.ownerUid);
            Assert.AreEqual(0L, disk.baseRevision, "새 계정의 사슬은 0에서 출발한다");
            Assert.AreEqual(gateDevice, disk.deviceId,
                "★ 세션 게이트와 다른 기기 id면 첫 커밋이 규칙(ownerRenew)에 거부된다");

            Assert.AreEqual(oldJson, File.ReadAllText(CloudSaveSidecar.PreRecoverPath),
                "옛 uid의 사이드카는 지우지 않고 옆에 남긴다");
            Assert.IsTrue(File.Exists(CloudSaveRecovery.PreRecoverBackupPath), "① 로컬 세이브 보존");
            Assert.AreEqual(12, SaveSystem.Load().maxStageReached, "로컬 세이브는 그대로다");

            Assert.IsTrue(CloudSaveSync.IsUrgentForTests, "③ 새 계정의 첫 정본은 120초를 기다리지 않는다");
            CollectionAssert.AreEqual(new[] { NewUid }, fetchedUids, "⑥ 옛 uid는 읽지 않는다");
        }

        /**
         * ★★ **두 번째 부팅은 같은 기기 id를 쓰고 입구를 안 탄다.**
         *
         * 새 실행 = 세션 정적 상태가 비어 있다(`ResetForTests`가 메모리의 기기 id를
         * 지운다). 그래도 디스크가 기기 id를 들고 있어야 한다 - 그래야 같은 폰의
         * 이전 실행에게 인수를 묻지 않는다(규칙 takeoverCommit의 같은 기기 갈래).
         */
        [Test]
        public void TheNextBootReusesTheSameDeviceIdAndSkipsTheEntrance()
        {
            SaveData local = Local(12);
            SeedDisk(local);

            Boot(local, Missing());
            string firstDevice = CloudSaveSidecar.Load().deviceId;

            // ---- 프로세스가 새로 뜬다
            CloudSaveCoordinator.ResetForTests();
            CloudSaveRecovery.ResetForTests();
            CloudSaveSession.ResetForTests();

            Assert.AreEqual(firstDevice, CloudSaveSession.DeviceIdFor(NewUid),
                "다음 실행의 게이트가 디스크의 기기 id를 읽어야 한다");

            Boot(local, Missing());

            Assert.AreEqual(0, CloudSaveRecovery.BootEntries, "이제 이 uid의 사이드카다 - 입구 없음");
            Assert.AreEqual(firstDevice, CloudSaveSidecar.Load().deviceId);
        }

        /** ④ 서버와 같은 기록. 사슬은 **로컬 저장 뒤에** 그 사이드카 위에서 확정된다 (62.1.1) */
        [Test]
        public void TheSameRecordAdoptsTheChainOnlyAfterTheLocalSave()
        {
            SaveData local = Local(12);
            SeedDisk(local);

            CloudSaveBootChoice choice = Boot(local, Found(local, 4L));

            Assert.AreEqual(1, CloudSaveRecovery.BootEntries);
            Assert.AreEqual(CloudRecoveryPlan.AdoptServerQuietly, CloudSaveRecovery.LastPlan);
            Assert.AreEqual(CloudSaveDecision.InSync, choice.decision);

            CloudSaveLocalState before = CloudSaveSidecar.Load();
            Assert.AreEqual(0L, before.baseRevision, "저장 전에는 사슬이 움직이지 않는다 (62.1.1)");
            Assert.AreEqual(4L, CloudSaveCoordinator.PendingChainRevision);

            Assert.IsTrue(SaveSystem.Save(local));
            CloudSaveCoordinator.NoteLocalSaveCommitted();

            CloudSaveLocalState after = CloudSaveSidecar.Load();
            Assert.AreEqual(4L, after.baseRevision);
            Assert.AreEqual(before.deviceId, after.deviceId,
                "사슬 확정이 새 기기 id로 사이드카를 다시 만들면 70단계 결함이 돌아온다");
            Assert.IsFalse(CloudSaveSync.IsUrgentForTests, "같은 기록 - 올릴 것이 없다");
        }

        /** ⑤ 다르다. 충돌 화면의 재료가 채워지고 아무것도 자동으로 안 쓴다 */
        [Test]
        public void ADifferentServerFillsTheConflictMaterialAndWritesNothing()
        {
            SaveData local = Local(12);
            SeedDisk(local);

            CloudSaveBootChoice choice = Boot(local, Found(Local(171), 9L));

            Assert.AreEqual(1, CloudSaveRecovery.BootEntries);
            Assert.AreEqual(CloudRecoveryPlan.AskTheHuman, CloudSaveRecovery.LastPlan);
            Assert.AreEqual(CloudSaveDecision.Conflict, choice.decision);
            Assert.IsFalse(choice.fromCloud, "남의 기록이 조용히 적용되면 안 된다");
            Assert.IsNotNull(CloudSaveSync.ConflictServerEnvelope, "60단계 충돌 화면의 재료");
            Assert.AreEqual(9L, CloudSaveSync.ConflictServerEnvelope.revision);
            Assert.AreEqual(0L, CloudSaveCoordinator.PendingChainRevision, "충돌은 사슬을 옮기지 않는다");

            CloudSaveLocalState disk = CloudSaveSidecar.Load();
            Assert.AreEqual(NewUid, disk.ownerUid, "충돌 갈래도 기기 id는 남아야 한다");
            Assert.AreEqual(0L, disk.baseRevision);
            Assert.IsFalse(CloudSaveSync.IsUrgentForTests, "충돌 상태에서 업로드를 서두르지 않는다");
            Assert.AreEqual(12, SaveSystem.Load().maxStageReached);
        }

        /** 서버를 못 봤다. 아무것도 정하지 않지만 사이드카(기기 id)는 남긴다 */
        [Test]
        public void OfflineStillLeavesTheSidecarAndDecidesNothing()
        {
            SaveData local = Local(12);
            SeedDisk(local);

            CloudSaveBootChoice choice = Boot(local, Offline());

            Assert.AreEqual(1, CloudSaveRecovery.BootEntries);
            Assert.AreEqual(CloudRecoveryPlan.WaitForServer, CloudSaveRecovery.LastPlan);
            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreEqual(NewUid, CloudSaveSidecar.Load().ownerUid);
            Assert.IsFalse(CloudSaveSync.IsUrgentForTests);
        }

        /** 자기 사이드카면 입구는 없다 - 평소의 부팅이 1프레임도 달라지지 않는다 */
        [Test]
        public void AnOwnSidecarNeverEntersRecover()
        {
            SaveData local = Local(12);
            SaveSystem.Save(local);
            CloudSaveSidecar.Save(CloudSaveLocalState.NewFor(NewUid, CloudSaveIds.New()));

            Boot(local, Missing());

            Assert.AreEqual(0, CloudSaveRecovery.BootEntries);
            Assert.IsFalse(File.Exists(CloudSaveSidecar.PreRecoverPath));
            Assert.IsFalse(File.Exists(CloudSaveRecovery.PreRecoverBackupPath));
        }

        /** 인증이 안 오면(uid 없음) 누구 것인지 모르므로 입구를 타지 않는다 - 옛 파일 그대로 */
        [Test]
        public void WithoutAnIdentityTheForeignSidecarIsLeftAlone()
        {
            SaveData local = Local(12);
            SeedDisk(local);
            string oldJson = File.ReadAllText(CloudSaveSidecar.Path);

            CloudSaveCoordinator.UseIdentityForTests(() => string.Empty);
            CloudSaveCoordinator.UseClockForTests(() => frames * 1f);
            CloudSaveCoordinator.UseFetchForTests(u => Task.FromResult(Missing()));

            Drive(local);

            Assert.AreEqual(0, CloudSaveRecovery.BootEntries);
            Assert.AreEqual(oldJson, File.ReadAllText(CloudSaveSidecar.Path));
        }

        // ---------------------------------------------------------------- 저장 실패

        /**
         * ★★ **사이드카를 남기지 못하면 서버로 아무것도 안 간다** (62.1.2 계약 무회귀).
         *
         * 입구에서 실패 -> 옛 사이드카 그대로, urgent 없음. 그 뒤 커밋 준비도
         * 실패하면 null(=커밋하지 않는다). 다음 시도가 성공하면 **같은 기기 id**로 남는다.
         */
        [Test]
        public void ASidecarThatCannotBeSavedBlocksTheUpload()
        {
            SaveData local = Local(12);
            SeedDisk(local);
            string gateDevice = CloudSaveSession.DeviceIdFor(NewUid);

            CloudSaveSidecar.FailNextSaveForTests = true;
            Boot(local, Missing());

            Assert.AreEqual(1, CloudSaveRecovery.BootEntries);
            Assert.AreEqual(OldUid, CloudSaveSidecar.Load().ownerUid, "실패하면 디스크는 옛 그대로다");
            Assert.IsFalse(CloudSaveSync.IsUrgentForTests,
                "사이드카가 없는데 urgent를 걸면 곧바로 거부될 쓰기를 서두르게 된다");

            CloudSaveSidecar.FailNextSaveForTests = true;
            Assert.IsNull(CloudSaveSync.PrepareSidecar(NewUid),
                "★ 디스크에 없는 사이드카로 세션을 잡으면 70단계 결함이 그대로 돌아온다");

            CloudSaveLocalState retried = CloudSaveSync.PrepareSidecar(NewUid);
            Assert.IsNotNull(retried);
            Assert.AreEqual(gateDevice, retried.deviceId, "재시도도 이 실행의 기기 id다");
            Assert.AreEqual(gateDevice, CloudSaveSidecar.Load().deviceId);
        }

        /** 커밋은 사이드카를 디스크에 확보한 **뒤에** 세션을 잡는다 - 순서를 소스에서 잰다 */
        [Test]
        public void TheCommitSecuresTheSidecarBeforeTheSession()
        {
            string source = Read("Assets/_Project/Scripts/Cloud/CloudSaveSync.cs");
            int body = source.IndexOf("private static async Task CommitAsync", StringComparison.Ordinal);
            Assert.Greater(body, 0);

            int prepare = source.IndexOf("PrepareSidecar(uid)", body, StringComparison.Ordinal);
            int acquire = source.IndexOf("CloudSaveSession.AcquireAsync", body, StringComparison.Ordinal);

            Assert.Greater(prepare, 0);
            Assert.Less(prepare, acquire, "세션 쓰기가 사이드카 확보보다 앞서면 안 된다");
        }

        // ---------------------------------------------------------------- 기기 id 하나

        /** 커밋 준비가 만든 사이드카의 기기 id = 게이트 · heartbeat가 쓰는 값 */
        [Test]
        public void TheFirstCommitUsesTheGateDeviceId()
        {
            string gateDevice = CloudSaveSession.DeviceIdFor(NewUid);

            CloudSaveLocalState first = CloudSaveSync.PrepareSidecar(NewUid);
            CloudSaveLocalState second = CloudSaveSync.PrepareSidecar(NewUid);

            Assert.AreEqual(gateDevice, first.deviceId);
            Assert.AreEqual(gateDevice, second.deviceId, "재시도마다 새 id가 나오던 것이 70단계의 거부였다");
            Assert.AreEqual(gateDevice, CloudSaveSession.DeviceIdFor(NewUid));
        }

        /**
         * ★ **새 사이드카를 만드는 문은 하나다.** `NewDeviceId()`를 부르는 자리는
         * `CloudSaveSession.DeviceIdFor` 하나뿐이어야 한다 - 다섯 군데가 각자
         * 만들던 것이 결함의 뿌리였다. 주석이 아니라 **호출**을 센다.
         */
        [Test]
        public void OnlyTheSessionMintsDeviceIds()
        {
            var callers = new List<string>();
            string root = Path.GetDirectoryName(Application.dataPath);

            foreach (string file in Directory.GetFiles(
                         Path.Combine(root, "Assets/_Project/Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string code = Regex.Replace(line, @"//.*$", string.Empty);
                    if (code.TrimStart().StartsWith("*")) continue;
                    if (code.Contains("NewDeviceId()") && !code.Contains("public static string NewDeviceId"))
                        callers.Add(Path.GetFileName(file) + ": " + code.Trim());
                }
            }

            Assert.AreEqual(1, callers.Count, string.Join("\n", callers));
            StringAssert.StartsWith("CloudSaveSession.cs", callers[0]);
        }

        // ---------------------------------------------------------------- 도구

        private CloudSaveBootChoice Boot(SaveData local, CloudSaveFetchResult result)
        {
            CloudSaveCoordinator.UseIdentityForTests(() => NewUid);
            CloudSaveCoordinator.UseClockForTests(() => frames * 0.1f);
            CloudSaveCoordinator.UseFetchForTests(uid =>
            {
                fetchedUids.Add(uid);
                return Task.FromResult(result);
            });

            return Drive(local);
        }

        private CloudSaveBootChoice Drive(SaveData local)
        {
            int reported = 0;
            CloudSaveBootChoice choice = null;
            IEnumerator routine = CloudSaveCoordinator.ChooseBootSave(local, picked =>
            {
                reported++;
                choice = picked;
            });

            while (routine.MoveNext()) frames++;

            Assert.AreEqual(1, reported, "부팅 선택은 정확히 한 번 (방치 보상 1회의 전제)");
            return choice;
        }

        /** 70단계 실측 그대로: 로컬 세이브 + **옛 uid가 rev 7까지 동기화한** 사이드카 */
        private static void SeedDisk(SaveData local)
        {
            Assert.IsTrue(SaveSystem.Save(local));

            var old = CloudSaveLocalState.NewFor(OldUid, CloudSaveIds.New());
            Assert.IsTrue(old.MarkSynced(7L, CloudSaveFingerprint.HashOf("old-payload"),
                                         CloudSaveFingerprint.StateHashOf(local), 0L));
            Assert.IsTrue(CloudSaveSidecar.Save(old));
        }

        private static SaveData Local(int stage)
        {
            var data = SaveData.NewGame();
            data.stage = stage;
            data.maxStageReached = stage;
            return data;
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

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), relative));
        }
    }
}
