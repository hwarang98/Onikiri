using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 옛 버전이 올린 클라우드 세이브를 **손상으로 읽지 않는다** (71.1단계).
     *
     * ## 실기가 잡은 것
     *
     * 재설치한 폰에서 구글 로그인 → 복구 → `서버 정본을 쓸 수 없습니다: StateHashMismatch`
     * → Blocked. 랭킹은 44층인데 세이브는 새 게임이었다. 서버 문서는 **멀쩡했다**:
     * v21 · rev 1243 · payload 해시 일치. 검증이 payload를 **v23 클래스로 읽고 다시
     * 직렬화**해 지문을 냈고, v22·v23의 새 필드가 기본값으로 붙어(+37자) 올린 쪽의
     * 지문과 영영 안 맞았다. 66단계(v22)부터 있던 결함 - 업데이트를 받은 모든 사용자의
     * 옛 클라우드 세이브가 같은 이유로 막힌다.
     *
     * ## 고정값
     *
     * `Fixtures/cloud_payload_v21_rev1243.json`은 운영 문서의 payload **원문 그대로**다
     * (읽기 전용으로 가져옴, 2026-10-06). 그 문서에 적힌 두 지문을 여기 못 박는다 -
     * 우리 코드가 아니라 **실제로 v21 클라이언트가 계산해 서버에 남긴 값**이라,
     * 지문 정의가 다시 어긋나면 이 검사가 그것을 말한다.
     */
    public class CloudLegacyStateHashTests
    {
        const string StoredPayloadSha = "5fdc23627ee9938e4cf21406ac1c566a5e64b9bc53d2a0e265aacf54063bd10d";
        const string StoredStateSha = "80d2fdb2b1abcd102457e448b6de95b7154827fbdc92629b7b327eb37c1f1fd6";

        string payload;

        [SetUp]
        public void LoadFixture()
        {
            payload = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Tests/EditMode/Fixtures/cloud_payload_v21_rev1243.json"));

            Assert.AreEqual(StoredPayloadSha, CloudSaveFingerprint.HashOf(payload),
                "고정값이 서버 원문과 다르다 - 줄바꿈·인코딩이 바뀌었는가");
        }

        [TearDown]
        public void Restore()
        {
            CloudSaveSession.ResetForTests();
        }

        // ---------------------------------------------------------------- 지문

        /** ★★ v21 클라이언트가 서버에 적은 지문을 v23 앱이 **그대로** 다시 낸다 */
        [Test]
        public void TheV21StateHashIsReproducedFromThePayloadText()
        {
            Assert.AreEqual(StoredStateSha, CloudSaveFingerprint.StateHashOfPayload(payload));
        }

        /** ★★ 그래서 운영 v21 봉투가 검증을 **통과한다** (예전: StateHashMismatch → Blocked) */
        [Test]
        public void TheV21EnvelopeValidates()
        {
            Assert.AreEqual(CloudSaveEnvelopeFault.None, V21Envelope().Validate());
        }

        /**
         * 예전 정의(다시 직렬화)는 정확히 그 값을 **못 낸다** - 결함의 기록이다.
         * 이 검사가 실패하면 SaveData가 v21과 같은 모양으로 돌아갔다는 뜻이다.
         */
        [Test]
        public void ReserializingThroughTheCurrentClassWasTheBug()
        {
            SaveData parsed = CloudSaveFingerprint.Deserialize(payload);
            string reserialized = CloudSaveFingerprint.Serialize(CloudSaveFingerprint.Normalize(parsed));

            Assert.AreNotEqual(StoredStateSha, CloudSaveFingerprint.HashOf(reserialized));
            Assert.Greater(CloudSaveFingerprint.Serialize(parsed).Length, payload.Length,
                "새 필드가 붙어 길어진다 (실측 2570 -> 2607)");
        }

        /**
         * ★ **지금 버전**에서는 새 정의가 예전 정의와 한 글자도 다르지 않다.
         *
         * 이미 서버에 있는 v23 문서들의 지문이 이 비교로 무사하다. 시간성 세 값이
         * 크고 작은 여러 모양(0 · 정수 · 지수 표기가 나올 만한 큰 수 · 소수)을 지난다.
         */
        [Test]
        public void TheTextDefinitionEqualsTheOldOneForTheCurrentVersion()
        {
            SaveData migrated = CloudSaveFingerprint.Deserialize(payload);
            Assert.IsTrue(SaveData.Migrate(migrated));

            var cases = new[]
            {
                SaveData.NewGame(),
                migrated,
                With(SaveData.NewGame(), 0L, 0d, 0d),
                With(SaveData.NewGame(), 639246441762970490L, 132738943269.37721d, 5096.0718327456725d),
                With(SaveData.NewGame(), 1L, 1e30d, 1e-7d),
                With(SaveData.NewGame(), 42L, 3d, 0.5d)
            };

            foreach (SaveData data in cases)
            {
                string old = CloudSaveFingerprint.HashOf(
                    CloudSaveFingerprint.Serialize(CloudSaveFingerprint.Normalize(data)));

                Assert.AreEqual(old, CloudSaveFingerprint.StateHashOf(data),
                    "v23에서 지문 정의가 바뀌면 서버의 v23 문서가 전부 손상으로 읽힌다");
            }
        }

        /** 시간성 세 값만 무시한다. 진행(골드·단계)이 바뀌면 지문이 바뀐다 */
        [Test]
        public void OnlyTheVolatileFieldsAreIgnored()
        {
            string later = payload.Replace("\"lastQuitUtcTicks\":639246441762970490",
                                           "\"lastQuitUtcTicks\":639246441999999999")
                                  .Replace("\"goldPerSecond\":132738943269.37721", "\"goldPerSecond\":7.0");
            Assert.AreEqual(StoredStateSha, CloudSaveFingerprint.StateHashOfPayload(later));

            string progressed = payload.Replace("\"stage\":43", "\"stage\":44");
            Assert.AreNotEqual(StoredStateSha, CloudSaveFingerprint.StateHashOfPayload(progressed));
        }

        /** 모르는 모양은 **실패로** 읽는다 - 같은 상태로 읽지 않는다 */
        [Test]
        public void AnUnexpectedShapeFailsClosed()
        {
            string twice = payload.Replace("\"stage\":43", "\"stage\":43,\"goldPerSecond\":1.0");
            Assert.IsNull(CloudSaveFingerprint.StateHashOfPayload(twice), "같은 키가 둘");

            string notANumber = payload.Replace("\"goldPerSecond\":132738943269.37721", "\"goldPerSecond\":\"x\"");
            Assert.IsNull(CloudSaveFingerprint.StateHashOfPayload(notANumber), "숫자가 아님");

            Assert.IsNull(CloudSaveFingerprint.StateHashOfPayload(string.Empty));
            Assert.IsFalse(CloudSavePolicy.SameState(null, null), "빈 지문끼리는 같지 않다");
        }

        /** 그 필드가 생기기 전 버전(키 자체가 없음)은 정상이다 */
        [Test]
        public void AMissingVolatileFieldIsAnOlderVersionNotAFault()
        {
            string withoutExp = payload.Replace(",\"expPerSecond\":5096.0718327456725", string.Empty);
            Assert.IsNotNull(CloudSaveFingerprint.StateHashOfPayload(withoutExp));
        }

        // ---------------------------------------------------------------- 판정

        /**
         * ★ 막히지 않고 **사람에게 묻는다.** 새 게임 로컬 + 사이드카 없음 + v21 서버
         * = 실기에서 Blocked였던 바로 그 부팅. 이제 충돌 화면의 재료가 채워진다.
         */
        [Test]
        public void TheRecoveryBootNowAsksInsteadOfBlocking()
        {
            CloudSaveEnvelope server = V21Envelope();
            var fetch = new CloudSaveFetchResult { status = CloudSaveStoreStatus.Found, envelope = server };

            CloudSaveBootChoice choice = CloudSaveCoordinator.ChooseFrom(SaveData.NewGame(), null, fetch, true);

            Assert.AreEqual(CloudSaveDecision.Conflict, choice.decision);
            Assert.AreEqual(CloudRecoveryPlan.AskTheHuman,
                CloudSaveRecoveryPolicy.PlanFor(CloudSaveStoreStatus.Found,
                    CloudSaveFingerprint.StateHashOf(SaveData.NewGame()), server.stateSha256));
        }

        /** 클라우드를 고르면 v21이 v23으로 올라와 43층이 그대로 들어온다 */
        [Test]
        public void ChoosingTheV21CloudMigratesItWhole()
        {
            CloudSaveEnvelope server = V21Envelope();
            var fetch = new CloudSaveFetchResult { status = CloudSaveStoreStatus.Found, envelope = server };

            // 사슬이 rev 1242에 있고 로컬이 그 뒤로 안 변했다 → 서버만 움직였다 → 내려받기
            SaveData local = CloudSaveFingerprint.Deserialize(payload.Replace("\"stage\":43", "\"stage\":42"));
            var sidecar = CloudSaveLocalState.NewFor("op-uid", CloudSaveIds.New());
            Assert.IsTrue(sidecar.MarkSynced(1242L, CloudSaveFingerprint.HashOf("prev"),
                CloudSaveFingerprint.StateHashOfPayload(CloudSaveFingerprint.Serialize(local)), 0L));

            CloudSaveBootChoice choice = CloudSaveCoordinator.ChooseFrom(local, sidecar, fetch, true);

            Assert.AreEqual(CloudSaveDecision.DownloadCloud, choice.decision);
            Assert.AreEqual(SaveData.CurrentVersion, choice.data.version, "적용 전에 v23으로 올린다");
            Assert.AreEqual(43, choice.data.maxStageReached);
            Assert.AreEqual(8717L, choice.data.gems);
        }

        // ---------------------------------------------------------------- 세션 자리 (71.1)

        /**
         * ★ 자리는 **uid마다** 따로다. 복구로 uid가 바뀌면 옛 자리로 새 uid의 문서를
         * 판정하지 않는다 - 실기에서 "세대 1 -> 20"으로 스스로를 회수했다.
         */
        [Test]
        public void ASeatBelongsToTheUidThatTookIt()
        {
            CloudSaveSession.UseWriteForTests(mode => CloudSaveSessionStatus.Acquired);

            CloudSaveSessionStatus status = CloudSaveSession
                .AcquireAsync("abandoned-anon-uid", CloudSaveIds.New()).GetAwaiter().GetResult();

            Assert.AreEqual(CloudSaveSessionStatus.Acquired, status);
            Assert.IsTrue(CloudSaveSession.HoldsSeatFor("abandoned-anon-uid"));
            Assert.IsFalse(CloudSaveSession.HoldsSeatFor("recovered-uid"),
                "옛 uid의 세대로 새 uid의 세션 문서를 판정하면 아무도 안 뺏었는데 회수된다");
            Assert.IsTrue(CloudSaveSession.HasHeldSeat, "Busy 계약(63단계)은 그대로 - 잡은 사실은 남는다");

            CloudSaveSession.BeginNewRun();
            Assert.IsFalse(CloudSaveSession.HoldsSeatFor("abandoned-anon-uid"));
        }

        /** 감시가 그 판정을 실제로 지난다 - 순서를 소스에서 잰다 */
        [Test]
        public void TheWatchOnlyJudgesItsOwnUidsSeat()
        {
            string watch = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Scripts/Cloud/CloudSaveSessionWatch.cs"));

            int uidRead = watch.IndexOf("string uid = CloudScores.Uid;", StringComparison.Ordinal);
            int gate = watch.IndexOf("if (!CloudSaveSession.HoldsSeatFor(uid)) return;", StringComparison.Ordinal);
            int poll = watch.IndexOf("PollAsync(uid", StringComparison.Ordinal);

            Assert.Greater(uidRead, 0);
            Assert.Greater(gate, uidRead);
            Assert.Less(gate, poll, "폴링이 자리 확인보다 앞서면 남의 문서로 회수된다");
        }

        // ---------------------------------------------------------------- 도구

        CloudSaveEnvelope V21Envelope()
        {
            return new CloudSaveEnvelope
            {
                formatVersion = 1,
                saveVersion = 21,
                revision = 1243L,
                baseRevision = 1242L,
                payload = payload,
                payloadSha256 = StoredPayloadSha,
                stateSha256 = StoredStateSha,
                lastMutationId = CloudSaveIds.New(),
                sessionId = CloudSaveIds.New(),
                deviceId = "a440699e6d6148c4800f98cdd8c4b54a",
                updatedAtUtcTicks = 0L,
                summary = new CloudSaveSummary
                {
                    maxStageReached = 43,
                    characterLevel = 65,
                    evolutionTier = 5,
                    gems = 8717L,
                    gachaTotalPulls = 13,
                    skillGachaTotalPulls = 11
                }
            };
        }

        static SaveData With(SaveData data, long quit, double gold, double exp)
        {
            data.lastQuitUtcTicks = quit;
            data.goldPerSecond = gold;
            data.expPerSecond = exp;
            data.stage = 7;
            data.maxStageReached = 7;
            return data;
        }
    }
}
