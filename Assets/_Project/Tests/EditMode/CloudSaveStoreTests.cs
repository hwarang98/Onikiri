using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 크로스 저장 2단계에서 **Firestore 없이 증명할 수 있는 것**.
     *
     * 서버 왕복 자체(트랜잭션·세션 인수·백업 원자성)는 Emulator 규칙 테스트가
     * 잰다(`tools/firestore-rules-tests`). 여기서 재는 것은 그 왕복의 양 끝이다:
     *
     *   문서 매핑   봉투가 **어떤 이름의 필드**로 나가고 돌아오는가.
     *               이름 하나가 갈리면 증상은 "저장이 조용히 안 된다"이고,
     *               원인은 서버 로그에도 안 남는다
     *
     *   규칙 대조   그 이름·상한·형식이 `firestore.rules`와 **같은가.**
     *               두 곳에 같은 목록이 두 벌 적혀 있고, 갈리면 실기에서만
     *               드러난다 - 55단계가 웹 클라이언트 ID를 이렇게 대조했다
     *
     *   세션 판단   인수·갱신·거절의 세 갈래. 서버가 최종 판정이지만, 거부될
     *               쓰기를 아예 안 보내는 것이 이 판단의 목적이다
     */
    public class CloudSaveStoreTests
    {
        private static string RepoRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string Rules
        {
            get { return File.ReadAllText(Path.Combine(RepoRoot, "firestore.rules")); }
        }

        // ---------------------------------------------------------------- 문서 매핑

        /**
         * ★ `updatedAt`은 클라가 만들지 않는다.
         *
         * 서버 타임스탬프여야 하고, 규칙이 `request.time`과 대조한다. 여기서
         * 값을 넣으면 그 순간 "언제 저장됐는가"가 조작 가능해진다.
         */
        [Test]
        public void TheClientNeverWritesTheServerTimestamp()
        {
            var fields = CloudSaveDocument.ToFields(Envelope());

            Assert.IsNotNull(fields);
            Assert.IsFalse(fields.ContainsKey(CloudSaveDocument.FieldUpdatedAt),
                           "updatedAt은 store가 FieldValue.ServerTimestamp로 붙인다");
            Assert.AreEqual(CloudSaveDocument.Fields.Length - 1, fields.Count,
                            "그 하나를 뺀 나머지는 전부 있어야 한다");
        }

        [Test]
        public void EveryDocumentFieldRoundTripsThroughFirestoreShapes()
        {
            var original = Envelope();
            var fields = CloudSaveDocument.ToFields(original);

            // Firestore는 정수를 전부 int64로 돌려준다. 그 모양 그대로 되돌린다
            var asServerSees = new Dictionary<string, object>
            {
                { CloudSaveDocument.FieldFormatVersion, (long)original.formatVersion },
                { CloudSaveDocument.FieldSaveVersion, (long)original.saveVersion },
                { CloudSaveDocument.FieldRevision, original.revision },
                { CloudSaveDocument.FieldBaseRevision, original.baseRevision },
                { CloudSaveDocument.FieldPayload, original.payload },
                { CloudSaveDocument.FieldPayloadSha, original.payloadSha256 },
                { CloudSaveDocument.FieldStateSha, original.stateSha256 },
                { CloudSaveDocument.FieldMutationId, original.lastMutationId },
                { CloudSaveDocument.FieldSessionId, original.sessionId },
                { CloudSaveDocument.FieldDeviceId, original.deviceId },
                { CloudSaveDocument.FieldSummary, ServerSummary(original.summary) }
            };

            var restored = CloudSaveDocument.FromFields(asServerSees, 638_000_000_000_000_000L);

            Assert.IsNotNull(restored);
            Assert.AreEqual(original.formatVersion, restored.formatVersion);
            Assert.AreEqual(original.saveVersion, restored.saveVersion);
            Assert.AreEqual(original.revision, restored.revision);
            Assert.AreEqual(original.baseRevision, restored.baseRevision);
            Assert.AreEqual(original.payload, restored.payload);
            Assert.AreEqual(original.payloadSha256, restored.payloadSha256);
            Assert.AreEqual(original.stateSha256, restored.stateSha256);
            Assert.AreEqual(original.lastMutationId, restored.lastMutationId);
            Assert.AreEqual(original.sessionId, restored.sessionId);
            Assert.AreEqual(original.deviceId, restored.deviceId);
            Assert.AreEqual(638_000_000_000_000_000L, restored.updatedAtUtcTicks);

            Assert.IsTrue(restored.summary.Matches(CloudSaveFingerprint.Deserialize(original.payload)));

            // 그리고 되돌린 봉투는 **그대로 쓸 수 있어야 한다**
            Assert.AreEqual(CloudSaveEnvelopeFault.None, restored.Validate());
        }

        [Test]
        public void AServerDocumentMissingAFieldIsNeverAnEnvelope()
        {
            var fields = CloudSaveDocument.ToFields(Envelope());
            fields.Remove(CloudSaveDocument.FieldStateSha);

            Assert.IsNull(CloudSaveDocument.FromFields(fields, 0L));
            Assert.IsNull(CloudSaveDocument.FromFields(null, 0L));
        }

        [Test]
        public void AServerSummaryMissingAFieldIsNeverAnEnvelope()
        {
            var fields = CloudSaveDocument.ToFields(Envelope());
            var summary = (Dictionary<string, object>)fields[CloudSaveDocument.FieldSummary];
            summary.Remove(CloudSaveDocument.SummaryGems);

            Assert.IsNull(CloudSaveDocument.FromFields(fields, 0L));
        }

        /** 범위를 넘는 정수는 0이 되고, 그 0은 Validate가 잡는다 */
        [Test]
        public void AnImpossibleIntegerNeverBecomesASilentValue()
        {
            var fields = CloudSaveDocument.ToFields(Envelope());
            fields[CloudSaveDocument.FieldSaveVersion] = long.MaxValue;

            var restored = CloudSaveDocument.FromFields(fields, 0L);

            Assert.IsNotNull(restored);
            Assert.AreEqual(0, restored.saveVersion);
            Assert.AreEqual(CloudSaveEnvelopeFault.SaveVersionOutOfRange, restored.Validate());
        }

        // ---------------------------------------------------------------- 규칙 대조

        /**
         * ★★ 코드의 필드 목록과 규칙의 허용 목록은 **같아야 한다.**
         *
         * 규칙이 `hasOnly`로 문을 잠그고 있으므로, 코드가 규칙에 없는 이름을
         * 쓰면 그 쓰기는 영원히 거부된다. 증상은 "저장이 안 된다" 하나뿐이고
         * 원인은 필드 이름 한 글자다 - 사람이 두 파일을 눈으로 맞추는 것은
         * 55단계에서 이미 실패한 방법이다(웹 클라이언트 ID).
         */
        [Test]
        public void TheRulesAllowExactlyTheFieldsWeWrite()
        {
            string rules = Rules;

            foreach (string field in CloudSaveDocument.Fields)
                StringAssert.Contains("'" + field + "'", rules, "규칙에 없는 필드: " + field);

            foreach (string field in CloudSaveDocument.SummaryFields)
                StringAssert.Contains("'" + field + "'", rules, "규칙에 없는 요약 필드: " + field);

            foreach (string field in new[] { CloudSaveSession.FieldSessionId, CloudSaveSession.FieldDeviceId,
                                             CloudSaveSession.FieldHeartbeatAt, CloudSaveSession.FieldReleased })
                StringAssert.Contains("'" + field + "'", rules, "규칙에 없는 세션 필드: " + field);
        }

        [Test]
        public void TheRulesGuardTheSameCollectionsWeWriteTo()
        {
            string rules = Rules;

            StringAssert.Contains("match /" + CloudSaveEnvelope.Collection + "/{uid}", rules);
            StringAssert.Contains("match /" + CloudSaveEnvelope.BackupCollection + "/{uid}", rules);
            StringAssert.Contains("match /" + CloudSaveEnvelope.SessionCollection + "/{uid}", rules);

            // 랭킹은 그대로 있어야 한다 - 54단계 계약을 이 스텝이 건드리지 않았다
            StringAssert.Contains("match /scores/{uid}", rules);
            StringAssert.Contains("allow read: if true;", rules);
        }

        /**
         * ★ 두 곳에 적힌 **숫자**가 같은가.
         *
         * 갈리면 클라가 통과시킨 값을 서버가 거부한다. 특히 세션 만료(180초)가
         * 갈리면 그 기기는 인수 가능하다고 판단한 순간에 거부당하고, 이유를
         * 모른 채 영원히 못 쓴다.
         */
        [Test]
        public void TheRulesAndTheCodeAgreeOnEveryLimit()
        {
            string rules = Rules;

            StringAssert.Contains("saveVersion <= " + SaveData.CurrentVersion, rules,
                                  "세이브 버전 상한이 갈렸다");
            StringAssert.Contains("payload.size() <= " + CloudSaveFingerprint.MaxPayloadBytes, rules,
                                  "payload 상한이 갈렸다");
            StringAssert.Contains("duration.value(" + (int)CloudSavePolicy.SessionExpirySeconds + ", 's')", rules,
                                  "세션 만료가 갈렸다");
            StringAssert.Contains("formatVersion == " + CloudSaveEnvelope.CurrentFormatVersion, rules,
                                  "봉투 형식이 갈렸다");
            StringAssert.Contains("revision == 1", rules, "첫 정본 규칙이 없다");
            StringAssert.Contains("maxStageReached <= " + StageProgress.ReachSanityCap, rules,
                                  "도달층 상한이 갈렸다");
            StringAssert.Contains("evolutionTier <= " + EvolutionCurve.MaxTier, rules,
                                  "승급 티어 상한이 갈렸다");
        }

        /** id 32자·지문 64자. 클라의 형식 검사와 규칙의 정규식이 같은 것을 말해야 한다 */
        [Test]
        public void TheRulesAndTheCodeAgreeOnIdAndHashShapes()
        {
            string rules = Rules;

            StringAssert.Contains("[0-9a-f]{" + CloudSaveIds.Length + "}", rules, "id 형식이 갈렸다");
            StringAssert.Contains("[0-9a-f]{" + CloudSaveFingerprint.HashLength + "}", rules,
                                  "지문 형식이 갈렸다");
        }

        /** 지우기는 어느 컬렉션에서도 열려 있지 않다 */
        [Test]
        public void NothingIsDeletableFromTheClient()
        {
            int denials = 0;
            foreach (string line in Rules.Split('\n'))
                if (line.Contains("allow delete: if false;")) denials++;

            // scores + playerSaves + playerSaveBackups + playerSaveSessions
            Assert.GreaterOrEqual(denials, 4, "지우기를 막지 않은 컬렉션이 있다");
        }

        // ---------------------------------------------------------------- 결과 계약

        /**
         * ★ **성공한 두 갈래만 pending을 지운다.**
         *
         * 오프라인·충돌·Busy·거부는 전부 "아직 안 갔다"이고, 그 상태의 정답은
         * 로컬 dirty 유지다. 여기가 틀리면 지하철에서 한 번 실패한 저장이
         * 영원히 안 올라가고(pending을 잃어), 아무도 그것을 모른다.
         */
        [Test]
        public void OnlyASyncedResultMayClearThePending()
        {
            foreach (CloudSaveStoreStatus status
                     in System.Enum.GetValues(typeof(CloudSaveStoreStatus)))
            {
                var result = new CloudSaveCommitResult { status = status };

                bool synced = status == CloudSaveStoreStatus.Committed
                              || status == CloudSaveStoreStatus.AlreadyApplied;

                Assert.AreEqual(synced, result.IsSynced, status + " 의 동기화 판정");
            }
        }

        // ---------------------------------------------------------------- 세션 판단

        [Test]
        public void AnEmptySeatIsTaken()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Claim,
                CloudSavePolicy.ClaimFor(false, null, false, 0f, Id(1)));
        }

        [Test]
        public void MyOwnSeatIsOnlyRenewed()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Renew,
                CloudSavePolicy.ClaimFor(true, Id(1), false, 5f, Id(1)));
        }

        /** ★ 살아 있는 다른 기기의 자리는 뺏지 않는다 */
        [Test]
        public void ALiveSeatIsNeverTaken()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Busy,
                CloudSavePolicy.ClaimFor(true, Id(2), false, 5f, Id(1)));

            Assert.AreEqual(CloudSaveSessionClaim.Busy,
                CloudSavePolicy.ClaimFor(true, Id(2), false,
                                         CloudSavePolicy.SessionExpirySeconds - 1f, Id(1)));
        }

        [Test]
        public void AReleasedSeatIsTakenImmediately()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Claim,
                CloudSavePolicy.ClaimFor(true, Id(2), true, 1f, Id(1)));
        }

        [Test]
        public void AnExpiredSeatIsTakenAtTheAgreedSecond()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Claim,
                CloudSavePolicy.ClaimFor(true, Id(2), false,
                                         CloudSavePolicy.SessionExpirySeconds, Id(1)));
        }

        /**
         * 기기 시계가 미래로 어긋난 경우다. **살아 있는 것으로 본다** -
         * 시계가 어긋난 쪽이 남의 세션을 뺏는 것보다 안전하다.
         */
        [Test]
        public void AFutureHeartbeatNeverLooksExpired()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Busy,
                CloudSavePolicy.ClaimFor(true, Id(2), false, -3600f, Id(1)));
        }

        /** 형식 아닌 내 id로는 아무 자리도 못 잡는다 - 규칙이 어차피 거부한다 */
        [Test]
        public void AMalformedSessionIdClaimsNothing()
        {
            Assert.AreEqual(CloudSaveSessionClaim.Busy,
                CloudSavePolicy.ClaimFor(false, null, false, 0f, "session-a"));
            Assert.AreEqual(CloudSaveSessionClaim.Busy,
                CloudSavePolicy.ClaimFor(true, Id(1), false, 0f, string.Empty));
        }

        /** 실행마다 하나. 형식은 기기 id와 같다 */
        [Test]
        public void TheRunHasExactlyOneSessionId()
        {
            string first = CloudSaveSession.CurrentId;

            Assert.IsTrue(CloudSaveIds.IsValid(first));
            Assert.AreEqual(first, CloudSaveSession.CurrentId, "실행 중에 바뀌면 안 된다");
        }

        // ---------------------------------------------------------------- 도구

        private static string Id(int seed)
        {
            return new string((char)('0' + seed), CloudSaveIds.Length);
        }

        private static CloudSaveEnvelope Envelope()
        {
            var save = SaveData.NewGame();
            save.maxStageReached = 171;
            save.characterLevel = 48;
            save.evolutionTier = 3;
            save.gems = 320L;
            save.gachaTotalPulls = 213;
            save.skillGachaTotalPulls = 88;
            save.stage = 82;

            return CloudSaveEnvelope.ForUpload(save, 5L, CloudSaveIds.New(),
                                               CloudSaveIds.New(), CloudSaveIds.New());
        }

        private static Dictionary<string, object> ServerSummary(CloudSaveSummary summary)
        {
            return new Dictionary<string, object>
            {
                { CloudSaveDocument.SummaryMaxStage, (long)summary.maxStageReached },
                { CloudSaveDocument.SummaryCharacterLevel, (long)summary.characterLevel },
                { CloudSaveDocument.SummaryEvolutionTier, (long)summary.evolutionTier },
                { CloudSaveDocument.SummaryGems, summary.gems },
                { CloudSaveDocument.SummaryGachaPulls, (long)summary.gachaTotalPulls },
                { CloudSaveDocument.SummarySkillGachaPulls, (long)summary.skillGachaTotalPulls }
            };
        }
    }
}
