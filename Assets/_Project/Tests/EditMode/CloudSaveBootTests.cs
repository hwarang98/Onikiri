using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 부팅 선택(59단계) - **설계 §6.1의 각 줄이 실제로 어디로 가는가.**
     *
     * `CloudSaveCoordinator.ChooseFrom`은 순수 함수다(파일도 네트워크도 없다).
     * 그래서 판정표의 모든 줄을 Firebase 없이 한 번씩 지날 수 있고, 그것이
     * 이 파일의 전부다.
     *
     * 재는 것이 셋이다:
     *
     *   갈래      각 상황이 어느 상태로 끝나는가. 특히 **충돌에서 아무것도 쓰지
     *             않는가**(4단계 전까지 자동 해결은 없다)
     *
     *   한 벌     고른 것이 정확히 무엇인가. 클라우드를 채택하면 **클라우드의
     *             값**이 나와야 하고, 그 밖의 모든 갈래는 로컬 그대로여야 한다
     *
     *   순서      방치 보상을 지급하는 자리가 **하나뿐인가.** 소스에서 센다 -
     *             이 스텝이 막으려던 것이 정확히 "두 번째 지급 자리"다
     */
    public class CloudSaveBootTests
    {
        // ---------------------------------------------------------------- 갈래

        /** 오프라인·미로그인·게스트. 전부 **정상 경로**다 */
        [Test]
        public void WithoutTheServerWeEnterWithTheLocalSave()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, null, default(CloudSaveFetchResult), false);

            Assert.AreEqual(CloudSaveState.LocalOnly, choice.state);
            Assert.AreSame(local, choice.data);
            Assert.IsFalse(choice.fromCloud);
        }

        [Test]
        public void AnEmptyServerLeavesUsWithSomethingToUpload()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, null, Missing(), true);

            Assert.AreEqual(CloudSaveDecision.UploadLocal, choice.decision);
            Assert.AreEqual(CloudSaveState.Dirty, choice.state);
            Assert.AreSame(local, choice.data);
        }

        [Test]
        public void TheSameRecordOnBothSidesEntersInSync()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), Cloud(local, 5L), true);

            Assert.AreEqual(CloudSaveState.InSync, choice.state);
            Assert.AreSame(local, choice.data);
            Assert.IsFalse(choice.fromCloud);
        }

        /**
         * ★ 다른 기기가 앞서 갔고 이쪽은 논 적이 없다. **클라우드가 정본이다.**
         *
         * 이 갈래가 이 스텝의 이유다 - 여기서 고른 한 벌이 곧 방치 보상의
         * 기준이 되고, 잘못 고르면 보상이 두 번 나가거나 남의 진행이 사라진다.
         */
        [Test]
        public void AnAdvancedServerIsAdoptedWholesale()
        {
            var local = Local();
            var cloud = Cloud(Server(), 6L);

            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), cloud, true);

            Assert.AreEqual(CloudSaveDecision.DownloadCloud, choice.decision);
            Assert.AreEqual(CloudSaveState.InSync, choice.state);
            Assert.IsTrue(choice.fromCloud);

            Assert.AreNotSame(local, choice.data);
            Assert.AreEqual(171, choice.data.maxStageReached, "클라우드의 값이어야 한다");
            Assert.AreEqual(1000d, choice.data.goldPerSecond, 1e-9d,
                            "방치 보상의 기준도 클라우드 것이어야 한다");
        }

        [Test]
        public void APlayedDeviceOnAnUnchangedServerStaysDirty()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(Server(), 5L), Cloud(Server(), 5L), true);

            Assert.AreEqual(CloudSaveDecision.UploadLocal, choice.decision);
            Assert.AreEqual(CloudSaveState.Dirty, choice.state);
            Assert.AreSame(local, choice.data);
        }

        /**
         * ★★ **둘 다 움직였으면 로컬로 들어가고 아무것도 쓰지 않는다.**
         *
         * 4단계의 충돌 화면이 서기 전까지 이 상태의 정답은 "고르지 않는 것"이다.
         * 여기서 한쪽을 자동으로 고르면 그것이 곧 필드별 병합의 첫 줄이 된다.
         */
        [Test]
        public void TwoBranchesEnterLocallyAndWriteNothing()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(Server(), 5L), Cloud(Server(), 6L), true);

            Assert.AreEqual(CloudSaveDecision.Conflict, choice.decision);
            Assert.AreEqual(CloudSaveState.Conflict, choice.state);
            Assert.AreSame(local, choice.data, "충돌에서 클라우드를 적용하지 않는다");
            Assert.IsFalse(choice.fromCloud);
        }

        /** sidecar가 없으면 아무것도 모른다 - 그 답도 충돌이다 */
        [Test]
        public void WithoutASidecarADifferentServerIsAConflict()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, null, Cloud(Server(), 6L), true);

            Assert.AreEqual(CloudSaveState.Conflict, choice.state);
            Assert.AreSame(local, choice.data);
        }

        // ---------------------------------------------------------------- 차단

        [Test]
        public void AFutureCloudSaveBlocksBothDirections()
        {
            var cloud = Cloud(Server(), 6L);
            cloud.envelope.saveVersion = SaveData.CurrentVersion + 1;

            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), cloud, true);

            Assert.AreEqual(CloudSaveState.Blocked, choice.state);
            Assert.AreEqual(CloudSaveBlock.FutureSaveVersion, choice.block);
            Assert.AreSame(local, choice.data, "차단은 로컬로 들어간다 - 게임은 멈추지 않는다");
        }

        [Test]
        public void AServerThatWentBackwardsIsBlocked()
        {
            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 9L), Cloud(Server(), 4L), true);

            Assert.AreEqual(CloudSaveState.Blocked, choice.state);
            Assert.AreEqual(CloudSaveBlock.ServerRollback, choice.block);
        }

        /** 서버 문서가 손상됐다. 봉투가 없으니 정책의 fault 사상을 그대로 쓴다 */
        [Test]
        public void AnUnusableServerDocumentIsBlocked()
        {
            var fetch = new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Invalid,
                fault = CloudSaveEnvelopeFault.PayloadHashMismatch
            };

            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), fetch, true);

            Assert.AreEqual(CloudSaveState.Blocked, choice.state);
            Assert.AreEqual(CloudSaveBlock.CorruptPayload, choice.block);
            Assert.AreSame(local, choice.data);
        }

        /** 내려받기로 정해졌는데 payload가 안 읽히면 **적용하지 않는다** */
        [Test]
        public void ACloudPayloadThatCannotBeReadIsBlocked()
        {
            var cloud = Cloud(Server(), 6L);
            cloud.envelope.payload = "이건 JSON이 아니다";

            var local = Local();
            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), cloud, true);

            Assert.AreEqual(CloudSaveState.Blocked, choice.state);
            Assert.AreEqual(CloudSaveBlock.CorruptPayload, choice.block);
            Assert.AreSame(local, choice.data);
        }

        /**
         * ★ **시계로 정하지 않는다.**
         *
         * 로컬이 더 최근에 저장됐어도(기기 시각) 서버 revision이 앞서면
         * 클라우드가 정본이다. 기기 시각은 사용자가 바꿀 수 있다.
         */
        [Test]
        public void TheNewerClockNeverWinsOverTheRevisionChain()
        {
            var local = Local();
            local.lastQuitUtcTicks = System.DateTime.UtcNow.Ticks;

            var older = Server();
            older.lastQuitUtcTicks = System.DateTime.UtcNow.AddDays(-7d).Ticks;

            var choice = CloudSaveCoordinator.ChooseFrom(local, Sidecar(local, 5L), Cloud(older, 6L), true);

            Assert.AreEqual(CloudSaveDecision.DownloadCloud, choice.decision);
            Assert.AreEqual(171, choice.data.maxStageReached);
        }

        /** 부팅이 어긋나도 게임은 들어간다 */
        [Test]
        public void TheFallbackIsAlwaysTheLocalSave()
        {
            var local = Local();

            Assert.AreSame(local, CloudSaveCoordinator.LocalFallback(local).data);
            Assert.IsNotNull(CloudSaveCoordinator.LocalFallback(null).data, "세이브가 없어도 새 게임으로 들어간다");
        }

        // ---------------------------------------------------------------- 순서

        /**
         * ★★ **방치 보상을 지급하는 자리는 하나뿐이다.**
         *
         * 59단계 전에는 `Apply` 끝에서 지급했다. 그 자리에 두면 이 함수를 부르는
         * 모든 경로가 지급 경로가 되고(프리셋 검사·클라우드 덮어쓰기), 그것이
         * 이중 지급의 문이다. 소스에서 세는 이유는 그 문이 다시 열리는 것을
         * 코드 리뷰가 아니라 테스트가 잡아야 하기 때문이다.
         */
        [Test]
        public void OnlyOnePlaceGrantsTheOfflineReward()
        {
            string source = File.ReadAllText(Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Scripts/Subsystems/GameSession.cs"));

            int mentions = 0;
            int index = source.IndexOf("GrantOfflineReward(");
            while (index >= 0)
            {
                mentions++;
                index = source.IndexOf("GrantOfflineReward(", index + 1);
            }

            // 정의 하나 + ApplyBoot 안의 호출 하나
            Assert.AreEqual(2, mentions,
                "방치 보상 지급 자리가 하나가 아니다 - 이중 지급의 문이 다시 열렸다");

            StringAssert.Contains("ApplyBoot(choice.data", source, "부팅이 고른 한 벌만 적용해야 한다");
            StringAssert.Contains("if (bootApplied)", source, "부팅 적용은 두 번 돌 수 없어야 한다");
        }

        // ---------------------------------------------------------------- 도구

        private static SaveData Local()
        {
            var data = SaveData.NewGame();
            data.stage = 12;
            data.maxStageReached = 12;
            data.characterLevel = 7;
            data.goldPerSecond = 100d;
            data.lastQuitUtcTicks = 638_000_000_000_000_000L;
            return data;
        }

        /** 다른 기기가 훨씬 앞서 간 상태 */
        private static SaveData Server()
        {
            var data = SaveData.NewGame();
            data.stage = 171;
            data.maxStageReached = 171;
            data.characterLevel = 48;
            data.goldPerSecond = 1000d;
            data.lastQuitUtcTicks = 638_100_000_000_000_000L;
            return data;
        }

        private static CloudSaveFetchResult Missing()
        {
            return new CloudSaveFetchResult { status = CloudSaveStoreStatus.Missing };
        }

        private static CloudSaveFetchResult Cloud(SaveData data, long revision)
        {
            var envelope = CloudSaveEnvelope.ForUpload(data, revision - 1L, CloudSaveIds.New(),
                                                       CloudSaveIds.New(), CloudSaveIds.New());
            Assert.IsNotNull(envelope, "테스트용 봉투를 만들지 못했다");

            return new CloudSaveFetchResult
            {
                status = CloudSaveStoreStatus.Found,
                envelope = envelope
            };
        }

        private static CloudSaveLocalState Sidecar(SaveData syncedWith, long baseRevision)
        {
            var sidecar = CloudSaveLocalState.NewFor("uid-1", CloudSaveIds.New());

            sidecar.MarkSynced(baseRevision,
                               CloudSaveFingerprint.HashOf(CloudSaveFingerprint.Serialize(syncedWith)),
                               CloudSaveFingerprint.StateHashOf(syncedWith), 0L);

            return sidecar;
        }
    }
}
