using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 계정 복구의 세이브 통합 (61단계 S5-2) - 설계 §8.2의 여섯 순서.
     *
     * 서버는 가짜다(fetch override). 여기서 재는 것은 네트워크가 아니라
     * **순서와 갈래**다:
     *
     *   ① 복구 후보 보존 (메모리 + .prerecover 백업)
     *   ② 기존 uid의 playerSaves만 읽는다 - 버려진 uid는 안 읽는다
     *   ③ 서버에 없음   -> 로컬이 새 계정의 첫 정본 (revision 1 준비)
     *   ④ 지문이 같음   -> 조용히 서버 head 채택
     *   ⑤ 다름          -> 충돌 화면 재료만 채우고 아무것도 안 쓴다
     *   ⑥ 버려진 익명 문서를 지우는 코드가 존재하지 않는다
     *
     * 그리고 이 스텝의 금지 하나: 랭킹의 max 병합이 세이브로 **확대되지 않는다** -
     * 복구가 세이브의 어떤 필드도 바꾸지 않는 것을 갈래마다 잰다.
     */
    public class CloudSaveRecoveryTests
    {
        const string OldUid = "old-anon-uid-61";
        const string NewUid = "recovered-uid-61";

        SaveSandbox sandbox;
        List<string> fetchedUids;

        [SetUp]
        public void Isolate()
        {
            sandbox = new SaveSandbox();
            fetchedUids = new List<string>();

            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudSaveRecovery.ResetForTests();
        }

        [TearDown]
        public void Restore()
        {
            CloudSaveCoordinator.ResetForTests();
            CloudSaveSync.ResetForTests();
            CloudSaveRecovery.ResetForTests();

            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ---------------------------------------------------------------- 판정표

        /** 설계 §8.2의 갈래표. 순수 함수라 시나리오 전부를 한 자리에서 잰다 */
        [Test]
        public void ThePlanTableMatchesTheDesign()
        {
            string a = CloudSaveFingerprint.HashOf("state-a");
            string b = CloudSaveFingerprint.HashOf("state-b");

            Assert.AreEqual(CloudRecoveryPlan.KeepLocalAsFirstRevision,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Missing, a, string.Empty),
                "③ 기존 계정에 세이브가 없으면 로컬이 첫 정본이다");

            Assert.AreEqual(CloudRecoveryPlan.AdoptServerQuietly,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Found, a, a),
                "④ 같은 기록은 조용히 채택한다");

            Assert.AreEqual(CloudRecoveryPlan.AskTheHuman,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Found, a, b),
                "⑤ 다르면 사람이 고른다 - 자동 병합 없음");

            Assert.AreEqual(CloudRecoveryPlan.WaitForServer,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Offline, a, string.Empty),
                "못 본 것과 없는 것을 섞지 않는다");
            Assert.AreEqual(CloudRecoveryPlan.WaitForServer,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Failed, a, string.Empty));

            Assert.AreEqual(CloudRecoveryPlan.Blocked,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Invalid, a, string.Empty),
                "성립하지 않는 서버 문서는 적용도 덮어쓰기도 없다");
        }

        /** 지문 계산 실패(빈 값)를 "같은 기록"으로 읽지 않는다 - 부팅 판정과 같은 규칙 */
        [Test]
        public void AMissingFingerprintNeverPassesAsTheSameRecord()
        {
            Assert.AreEqual(CloudRecoveryPlan.AskTheHuman,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Found,
                                                string.Empty, string.Empty));
        }

        /** ⑥ 어느 갈래도 버려진 익명 문서를 지울 수 없다 - 규칙 4-B의 클라이언트 쪽 거울 */
        [Test]
        public void NoPlanMayDeleteTheAbandonedSave()
        {
            foreach (CloudRecoveryPlan plan in Enum.GetValues(typeof(CloudRecoveryPlan)))
                Assert.IsFalse(CloudSaveRecoveryPolicy.MayDeleteAbandonedSave(plan), plan.ToString());

            // 코드에도 삭제 경로가 없어야 한다. 주석이 아니라 **호출**을 잡는다
            string source = File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Scripts/Cloud/CloudSaveRecovery.cs"));
            StringAssert.DoesNotContain(".DeleteAsync(", source);
            StringAssert.DoesNotContain(".SetAsync(", source,
                "복구는 서버 정본을 직접 쓰지 않는다 - 쓰기는 언제나 58단계 트랜잭션이다");
        }

        // ---------------------------------------------------------------- ① 후보 보존

        /** ① 복구 후보가 메모리와 백업 파일 양쪽에 남는다 */
        [Test]
        public void PreparingTheCandidateKeepsMemoryAndABackupFile()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            SaveSystem.Save(data);

            SaveData candidate = CloudSaveRecovery.PrepareCandidate();

            Assert.IsNotNull(candidate);
            Assert.AreEqual(12, candidate.maxStageReached, "메모리의 후보");

            Assert.IsTrue(File.Exists(CloudSaveRecovery.PreRecoverBackupPath),
                "복구 도중 앱이 죽어도 이 파일이 되돌릴 길이다");
            var backup = JsonUtility.FromJson<SaveData>(
                File.ReadAllText(CloudSaveRecovery.PreRecoverBackupPath));
            Assert.AreEqual(12, backup.maxStageReached, "백업 파일의 후보");
        }

        /** 로컬 세이브가 아예 없어도 후보는 null이 아니다 - 새 게임 한 벌이다 */
        [Test]
        public void PreparingWithoutALocalSaveStillYieldsACandidate()
        {
            Assert.IsFalse(SaveSystem.Exists);
            Assert.IsNotNull(CloudSaveRecovery.PrepareCandidate());
        }

        // ---------------------------------------------------------------- ③ 서버에 없음

        /** ③ 새 uid의 sidecar가 revision 0으로 서고, Dirty + urgent로 첫 업로드를 준비한다 */
        [Test]
        public void AMissingServerSaveAdoptsTheLocalAsTheFirstRevision()
        {
            var candidate = Candidate(12);
            UseFetch(uid => Result(CloudSaveStoreStatus.Missing, null));

            var plan = Run(candidate);

            Assert.AreEqual(CloudRecoveryPlan.KeepLocalAsFirstRevision, plan);
            Assert.AreEqual(plan, CloudSaveRecovery.LastPlan);

            var sidecar = CloudSaveSidecar.Load();
            Assert.IsNotNull(sidecar, "새 계정의 sidecar가 서야 한다");
            Assert.AreEqual(NewUid, sidecar.ownerUid);
            Assert.AreEqual(0L, sidecar.baseRevision, "revision 1은 다음 커밋이 만든다");

            Assert.AreEqual(CloudSaveState.Dirty, CloudSaveCoordinator.State);
            Assert.IsTrue(CloudSaveSync.HasPendingForTests, "올릴 후보가 물려 있다");
            Assert.IsTrue(CloudSaveSync.IsUrgentForTests, "계정 교체는 120초를 기다리지 않는다");
        }

        // ---------------------------------------------------------------- ④ 같은 기록

        /** ④ 지문이 같으면 조용히 서버 head를 채택한다 - 묻지도 쓰지도 않는다 */
        [Test]
        public void AnIdenticalSaveAdoptsTheServerHeadQuietly()
        {
            var candidate = Candidate(30);
            var envelope = Envelope(Candidate(30), 6L);

            UseFetch(uid => Result(CloudSaveStoreStatus.Found, envelope));

            var plan = Run(candidate);

            Assert.AreEqual(CloudRecoveryPlan.AdoptServerQuietly, plan);

            var sidecar = CloudSaveSidecar.Load();
            Assert.IsNotNull(sidecar);
            Assert.AreEqual(NewUid, sidecar.ownerUid);
            Assert.AreEqual(envelope.revision, sidecar.baseRevision, "서버 head를 채택했다");

            Assert.AreEqual(CloudSaveState.InSync, CloudSaveCoordinator.State);
            Assert.IsFalse(CloudSaveSync.HasPendingForTests,
                "같은 기록이 dirty로 남으면 내용 그대로인 revision 하나가 더 올라간다");
        }

        // ---------------------------------------------------------------- ⑤ 다른 기록

        /** ⑤ 다르면 충돌 화면 재료만 채운다. 파일도 서버도 아무것도 안 쓴다 */
        [Test]
        public void ADivergedSaveAsksTheHumanAndWritesNothing()
        {
            var candidate = Candidate(12);
            SaveSystem.Save(candidate);
            byte[] before = File.ReadAllBytes(SaveSystem.Path);

            var envelope = Envelope(Candidate(171), 6L);
            UseFetch(uid => Result(CloudSaveStoreStatus.Found, envelope));

            var plan = Run(candidate);

            Assert.AreEqual(CloudRecoveryPlan.AskTheHuman, plan);
            Assert.AreEqual(CloudSaveState.Conflict, CloudSaveCoordinator.State);
            Assert.AreSame(envelope, CloudSaveSync.ConflictServerEnvelope,
                "60단계 충돌 화면이 그대로 이 재료로 열린다 - 새 화면은 없다");

            Assert.IsNull(CloudSaveSidecar.Load(), "선택 전에는 sidecar도 안 세운다");
            Assert.AreEqual(Convert.ToBase64String(before),
                Convert.ToBase64String(File.ReadAllBytes(SaveSystem.Path)),
                "로컬 세이브는 바이트 그대로다 - 덮는 것은 사람이 고른 뒤다");
        }

        /**
         * ★ 랭킹 max 병합과 세이브 선택의 분리 (S5-3, 설계 §8.2 꼬리).
         *
         * 랭킹이 30층(병합된 최고 기록)이어도 세이브 후보의 진행은 12층
         * 그대로다 - 복구가 세이브의 어떤 필드도 만지지 않는다. 두 값이 잠시
         * 다른 것은 버그가 아니라 "랭킹 = 최고 기록 / 세이브 = 현재 진행"이고,
         * 화면은 그것을 "최고 N층"(랭킹 줄)으로 적는다.
         */
        [Test]
        public void TheRankingMergeNeverLeaksIntoTheChosenSave()
        {
            // 랭킹 쪽 병합: 로컬 12 / 버린 문서 30 / 복구 문서 20 -> 30
            Assert.AreEqual(30, AccountLinkPolicy.MergedStage(12, 30, 20));

            // 세이브 쪽: 같은 상황에서 후보는 12 그대로다
            var candidate = Candidate(12);
            UseFetch(uid => Result(CloudSaveStoreStatus.Missing, null));
            Run(candidate);

            Assert.AreEqual(12, candidate.maxStageReached,
                "세이브는 한 벌 통째 선택이다 - 랭킹의 max가 스며들면 그것이 필드별 병합의 시작이다");
        }

        // ---------------------------------------------------------------- 경계 (S5-5)

        /** 서버를 못 봤으면 아무것도 정하지 않는다 - sidecar도 상태 확정도 없다 */
        [Test]
        public void AnUnreachableServerDecidesNothing()
        {
            UseFetch(uid => Result(CloudSaveStoreStatus.Offline, null));

            var plan = Run(Candidate(12));

            Assert.AreEqual(CloudRecoveryPlan.WaitForServer, plan);
            Assert.AreEqual(CloudSaveState.LocalOnly, CloudSaveCoordinator.State);
            Assert.IsNull(CloudSaveSidecar.Load(), "모르는 상태에서 사슬을 세우지 않는다");
        }

        /** ② 읽는 것은 복구된 uid 하나뿐이다. 버려진 uid의 세이브는 읽지도 않는다 */
        [Test]
        public void OnlyTheRecoveredUidIsEverFetched()
        {
            UseFetch(uid => Result(CloudSaveStoreStatus.Missing, null));
            Run(Candidate(12));

            Assert.AreEqual(1, fetchedUids.Count);
            Assert.AreEqual(NewUid, fetchedUids[0]);
        }

        /**
         * S5-1 Link - uid가 유지되면 sidecar 사슬이 **그대로 이어진다.**
         *
         * 연동(Link)은 uid를 바꾸지 않으므로 복구 코드가 개입할 자리가 없다 -
         * `playerSaves/{uid}`도 sidecar `ownerUid`도 그대로이고, 로컬이 dirty면
         * 다음 커밋이 평소처럼 올라간다. 이 테스트는 그 전제(같은 uid = 같은
         * 사슬)가 흔들리지 않는 것을 잰다.
         */
        [Test]
        public void LinkingKeepsTheSidecarChainIntact()
        {
            var sidecar = CloudSaveLocalState.NewFor(OldUid, CloudSaveIds.New());
            sidecar.MarkSynced(41L, CloudSaveFingerprint.HashOf("p"),
                               CloudSaveFingerprint.HashOf("s"), 0L);
            Assert.IsTrue(CloudSaveSidecar.Save(sidecar));

            var loaded = CloudSaveSidecar.Load();
            Assert.IsTrue(CloudSavePolicy.SidecarAppliesTo(loaded, OldUid),
                "Link 뒤에도 uid가 같으므로 사슬이 이어진다");
            Assert.AreEqual(41L, loaded.baseRevision);
        }

        /** 옛 uid의 sidecar는 새 uid에게 없는 것이다 (59단계 규칙의 재확인) */
        [Test]
        public void TheOldSidecarDoesNotApplyToTheRecoveredUid()
        {
            var old = CloudSaveLocalState.NewFor(OldUid, CloudSaveIds.New());
            old.MarkSynced(41L, CloudSaveFingerprint.HashOf("p"),
                           CloudSaveFingerprint.HashOf("s"), 0L);
            Assert.IsTrue(CloudSaveSidecar.Save(old));

            Assert.IsFalse(CloudSavePolicy.SidecarAppliesTo(CloudSaveSidecar.Load(), NewUid),
                "남의 계정 sidecar로 판정하면 엉뚱한 baseRevision으로 출발하는 쓰기가 나간다");
        }

        /** uid가 비어 있으면(있을 수 없는 호출) 아무것도 하지 않는다 */
        [Test]
        public void AnEmptyUidDoesNothing()
        {
            UseFetch(uid => { throw new InvalidOperationException("불려서는 안 된다"); });

            var plan = CloudSaveRecovery.RunAsync(string.Empty, Candidate(1))
                .GetAwaiter().GetResult();

            Assert.AreEqual(CloudRecoveryPlan.WaitForServer, plan);
            Assert.AreEqual(0, fetchedUids.Count);
        }

        // ---------------------------------------------------------------- 도구

        static SaveData Candidate(int stage)
        {
            var data = SaveData.NewGame();
            data.stage = stage;
            data.maxStageReached = stage;
            data.characterLevel = 7;
            data.gems = 320L;
            return data;
        }

        static CloudSaveEnvelope Envelope(SaveData data, long baseRevision)
        {
            var envelope = CloudSaveEnvelope.ForUpload(
                data, baseRevision, CloudSaveIds.New(), CloudSaveIds.New(), CloudSaveIds.New());
            Assert.IsNotNull(envelope, "테스트용 봉투를 만들지 못했다");
            return envelope;
        }

        void UseFetch(Func<string, CloudSaveFetchResult> fetch)
        {
            CloudSaveRecovery.UseFetchForTests(uid =>
            {
                fetchedUids.Add(uid);
                return Task.FromResult(fetch(uid));
            });
        }

        static CloudSaveFetchResult Result(CloudSaveStoreStatus status, CloudSaveEnvelope envelope)
        {
            return new CloudSaveFetchResult { status = status, envelope = envelope };
        }

        static CloudRecoveryPlan Run(SaveData candidate)
        {
            // fetch가 완료된 Task라 await가 동기로 이어진다 - EditMode에서 안전하다
            return CloudSaveRecovery.RunAsync(NewUid, candidate).GetAwaiter().GetResult();
        }
    }
}
