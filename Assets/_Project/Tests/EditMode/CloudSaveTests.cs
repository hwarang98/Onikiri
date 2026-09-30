using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Core;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 크로스 저장 1단계 - **Firebase 없이 증명할 수 있는 전부.**
     *
     * 이 스텝의 코드에는 Firestore가 한 줄도 없다. 그래서 여기서 검사하는 것이
     * 곧 이 단계의 산출물 전체이고, 2단계(저장소·규칙)가 들어와도 이 파일은
     * 그대로 남아야 한다 - 네트워크가 바뀌어도 아래 규칙들은 참이다.
     *
     * 재는 것이 다섯이다:
     *
     *   왕복      세이브 한 벌이 payload를 지나 **한 글자도 안 잃고** 돌아오는가.
     *             이것이 깨지면 크로스 저장은 진행을 조용히 갉아먹는 기능이 된다
     *
     *   지문      같은 상태는 같은 지문인가. 그리고 **시계는 상태가 아닌가** -
     *             자동 저장마다 바뀌는 값을 변경으로 세면 가만히 있어도 충돌이 뜬다
     *
     *   봉투      내려받은 문서가 **스스로 일관된가.** 보안 규칙은 우리가 쓴
     *             문서만 지킨다 - 콘솔에서 손댄 문서·옛 앱이 남긴 문서·전송 중
     *             잘린 문서는 규칙을 지나온 적이 없다
     *
     *   판정표    설계 6.1의 줄들. 특히 **막을 이유가 다른 무엇보다 먼저**인가
     *
     *   불변식    sidecar가 **반쯤 맞는 상태로 살아남지 않는가.** 모르는 것보다
     *             거짓을 아는 것이 위험하다
     */
    public class CloudSaveTests
    {
        /**
         * 실사용 파일과의 접촉을 끊는다 (61단계 S5-0). 60단계까지의 백업/복원은
         * TearDown이 못 돌면 안 붙는 반창고였다 - 이제 경로째 격리하고, Dispose가
         * 실사용 파일이 바이트 그대로인지까지 검사한다.
         */
        private SaveSandbox sandbox;

        [SetUp]
        public void IsolateSaves()
        {
            sandbox = new SaveSandbox();
        }

        [TearDown]
        public void VerifyIsolation()
        {
            if (sandbox != null) sandbox.Dispose();
            sandbox = null;
        }

        // ---------------------------------------------------------------- 왕복

        /**
         * ★ 이 스텝의 바닥. payload는 세이브 **전체**여야 한다.
         *
         * 필드 하나가 빠져도 그날부터 크로스 저장은 "기기를 바꾸면 그 값만
         * 사라지는" 기능이 된다. 리플렉션으로 도는 이유는 다음 스텝이 필드를
         * 늘렸을 때 이 테스트가 **자동으로** 그것까지 재기 때문이다.
         */
        [Test]
        public void TheWholeSaveSurvivesThePayloadRoundTrip()
        {
            var original = BusySave();

            string payload = CloudSaveFingerprint.Serialize(original);
            var restored = CloudSaveFingerprint.Deserialize(payload);

            Assert.IsNotNull(restored);
            AssertSameFields(original, restored);
        }

        /** 정규화는 **사본에서** 한다. 원본의 종료 시각이 사라지면 방치 보상이 무너진다 */
        [Test]
        public void NormalizingDoesNotTouchTheOriginal()
        {
            var original = BusySave();
            long quit = original.lastQuitUtcTicks;

            CloudSaveFingerprint.StateHashOf(original);

            Assert.AreEqual(quit, original.lastQuitUtcTicks);
            Assert.AreNotEqual(0d, original.goldPerSecond);
        }

        // ---------------------------------------------------------------- 지문

        [Test]
        public void TheSameStateAlwaysHashesTheSame()
        {
            string a = CloudSaveFingerprint.StateHashOf(BusySave());
            string b = CloudSaveFingerprint.StateHashOf(BusySave());

            Assert.IsNotNull(a);
            Assert.AreEqual(a, b);
            Assert.AreEqual(CloudSaveFingerprint.HashLength, a.Length, "SHA-256 소문자 16진 64자");
        }

        /**
         * ★ 시계는 진행이 아니다.
         *
         * 30초 자동 저장마다 `lastQuitUtcTicks`가 바뀐다. 그것을 변경으로 세면
         * 두 기기가 번갈아 켜져 있기만 해도 충돌 화면이 뜨고, 사람은 아무것도
         * 안 했는데 기록을 고르라는 요구를 받는다.
         */
        [Test]
        public void TheClockChangesThePayloadButNotTheState()
        {
            var before = BusySave();
            var after = BusySave();

            after.lastQuitUtcTicks += TimeSpan.TicksPerHour;
            after.goldPerSecond *= 2d;
            after.expPerSecond *= 3d;

            Assert.AreNotEqual(CloudSaveFingerprint.HashOf(CloudSaveFingerprint.Serialize(before)),
                               CloudSaveFingerprint.HashOf(CloudSaveFingerprint.Serialize(after)),
                               "payload 지문은 달라야 한다 - 실제로 다른 바이트다");

            Assert.AreEqual(CloudSaveFingerprint.StateHashOf(before),
                            CloudSaveFingerprint.StateHashOf(after),
                            "상태 지문은 같아야 한다 - 진행은 한 걸음도 안 갔다");
        }

        /**
         * 그 반대편. **골드·경험치는 빼지 않는다.**
         *
         * 시간성 셋과 함께 빼고 싶어지는 자리인데(둘 다 매 순간 변한다), 빼면
         * 두 기기가 다른 재화를 들고도 같은 지문을 갖는다 - 그 상태에서 InSync는
         * 한쪽의 파밍을 통째로 버린다는 뜻이다.
         */
        [Test]
        public void ProgressAlwaysChangesTheState()
        {
            AssertStateChanges(save => save.gold += BigDouble.FromDouble(1d), "골드");
            AssertStateChanges(save => save.exp += BigDouble.FromDouble(1d), "경험치");
            AssertStateChanges(save => save.gems -= 225L, "보석");
            AssertStateChanges(save => save.killsThisStage += 1, "이번 스테이지 처치");
            AssertStateChanges(save => save.evolutionTier += 1, "승급 티어");
            AssertStateChanges(save => save.gachaPity += 1, "요도 천장");
            AssertStateChanges(save => save.skillGachaAwakenPity += 1, "하드 천장");
            AssertStateChanges(save => save.playerName = "다른이름", "이름");
        }

        [Test]
        public void ABrokenPayloadNeverBecomesASave()
        {
            Assert.IsNull(CloudSaveFingerprint.Deserialize(null));
            Assert.IsNull(CloudSaveFingerprint.Deserialize(string.Empty));
            Assert.IsNull(CloudSaveFingerprint.Deserialize("이것은 JSON이 아니다"));
        }

        /**
         * 상한은 **우리 것**이다. Firestore의 1MiB에 기대지 않는다.
         *
         * 기대는 순간 상한이 남의 것이 되고, 배열 하나가 잘못 자라 한도를 밟는
         * 날 그 계정은 클라우드 저장이 통째로 죽는다 - 원인은 세이브가 아니라
         * Firestore 에러로 보인다.
         */
        [Test]
        public void ThePayloadLimitIsOurs()
        {
            Assert.AreEqual(200 * 1024, CloudSaveFingerprint.MaxPayloadBytes);

            string payload = CloudSaveFingerprint.Serialize(BusySave());
            Assert.IsTrue(CloudSaveFingerprint.IsWithinLimit(payload));
            Assert.Less(CloudSaveFingerprint.ByteCount(payload), 32 * 1024,
                        "현재 세이브는 상한의 한참 아래여야 한다");
        }

        /** id와 지문은 **형식이 하나다.** 검사가 하나여야 빠뜨린 자리가 안 생긴다 */
        [Test]
        public void IdsAndHashesHaveExactlyOneShape()
        {
            string id = CloudSaveIds.New();
            Assert.AreEqual(CloudSaveIds.Length, id.Length);
            Assert.IsTrue(CloudSaveIds.IsValid(id));

            Assert.IsFalse(CloudSaveIds.IsValid(null));
            Assert.IsFalse(CloudSaveIds.IsValid(string.Empty));
            Assert.IsFalse(CloudSaveIds.IsValid("editor-session"));
            Assert.IsFalse(CloudSaveIds.IsValid(id.Substring(1)), "31자");
            Assert.IsFalse(CloudSaveIds.IsValid(id.ToUpperInvariant()), "대문자는 우리 규격이 아니다");

            string hash = CloudSaveFingerprint.HashOf("무엇이든");
            Assert.IsTrue(CloudSaveFingerprint.IsHash(hash));
            Assert.IsFalse(CloudSaveFingerprint.IsHash(hash.Substring(1)));
            Assert.IsFalse(CloudSaveFingerprint.IsHash("payload-sha"));
            Assert.IsFalse(CloudSaveFingerprint.IsHash(string.Empty));
        }

        // ---------------------------------------------------------------- 봉투 (만들기)

        [Test]
        public void TheEnvelopeAdvancesExactlyOneRevision()
        {
            var save = BusySave();
            var envelope = Upload(save, 7L);

            Assert.IsNotNull(envelope);
            Assert.AreEqual(8L, envelope.revision);
            Assert.AreEqual(7L, envelope.baseRevision);
            Assert.AreEqual(SaveData.CurrentVersion, envelope.saveVersion);
            Assert.AreEqual(CloudSaveEnvelope.CurrentFormatVersion, envelope.formatVersion);

            // 서버 타임스탬프 자리는 **업로드가 채우지 않는다**
            Assert.AreEqual(0L, envelope.updatedAtUtcTicks);

            Assert.AreEqual(CloudSaveEnvelopeFault.None, envelope.Validate());
            Assert.AreEqual(CloudSaveFingerprint.StateHashOf(save), envelope.stateSha256);
        }

        [Test]
        public void TheFirstEnvelopeIsRevisionOne()
        {
            var envelope = Upload(BusySave(), 0L);

            Assert.AreEqual(CloudSaveEnvelope.FirstRevision, envelope.revision);
            Assert.AreEqual(0L, envelope.baseRevision);
            Assert.IsTrue(envelope.IsUsable());
        }

        /** 요약은 화면용 사본이다. 값이 어긋나면 충돌 화면이 거짓을 그린다 */
        [Test]
        public void TheSummaryMirrorsTheSixShownValues()
        {
            var save = BusySave();
            var summary = Upload(save, 0L).summary;

            Assert.AreEqual(save.maxStageReached, summary.maxStageReached);
            Assert.AreEqual(save.characterLevel, summary.characterLevel);
            Assert.AreEqual(save.evolutionTier, summary.evolutionTier);
            Assert.AreEqual(save.gems, summary.gems);
            Assert.AreEqual(save.gachaTotalPulls, summary.gachaTotalPulls);
            Assert.AreEqual(save.skillGachaTotalPulls, summary.skillGachaTotalPulls);
            Assert.IsTrue(summary.Matches(save));
        }

        /** 상한을 넘는 봉투는 **만들어지지도 않는다** - 거부될 쓰기를 들고 있지 않는다 */
        [Test]
        public void AnOversizedSaveNeverBecomesAnEnvelope()
        {
            var save = BusySave();
            save.playerName = new string('가', 200 * 1024);

            Assert.IsNull(Upload(save, 0L));
        }

        /**
         * ★ 잘못된 id를 들고 봉투를 만들지 않는다.
         *
         * 임의 문자열을 허용하면 `sessionId = "editor-session"` 같은 값이 서버에
         * 올라가고, 2단계의 세션 일치 규칙이 그것을 진짜 세션으로 받아들인다.
         */
        [Test]
        public void AnEnvelopeNeverCarriesAMalformedId()
        {
            var save = BusySave();
            string ok = CloudSaveIds.New();

            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, 0L, string.Empty, ok, ok), "빈 세션");
            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, 0L, ok, string.Empty, ok), "빈 기기");
            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, 0L, ok, ok, string.Empty), "빈 쓰기 id");
            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, 0L, null, ok, ok));
            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, 0L, "editor-session", ok, ok), "형식 밖");
        }

        /** revision이 감기는 자리를 열어 두지 않는다 */
        [Test]
        public void TheRevisionCounterNeverOverflows()
        {
            var save = BusySave();
            string id = CloudSaveIds.New();

            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, long.MaxValue, id, id, id));
            Assert.IsNull(CloudSaveEnvelope.ForUpload(save, -1L, id, id, id));

            var high = CloudSaveEnvelope.ForUpload(save, long.MaxValue - 1L, id, id, id);
            Assert.IsNotNull(high);
            Assert.AreEqual(long.MaxValue, high.revision);
        }

        // ---------------------------------------------------------------- 봉투 (내려받기)

        [Test]
        public void ATamperedPayloadIsNeverAccepted()
        {
            var envelope = Upload(BusySave(), 3L);

            string tampered = envelope.payload.Replace("\"gems\":320", "\"gems\":999999");
            Assert.AreNotEqual(envelope.payload, tampered, "payload에 보석 잔액이 그대로 들어 있어야 한다");

            // 여전히 **읽히는** JSON이라는 것이 요점이다. 손상 검사가 파싱에만
            // 기대면 이런 조작은 그대로 통과한다 - 지문이 그것을 잡는다
            envelope.payload = tampered;
            Assert.IsNotNull(CloudSaveFingerprint.Deserialize(envelope.payload));
            Assert.AreEqual(CloudSaveEnvelopeFault.PayloadHashMismatch, envelope.Validate());
        }

        /**
         * ★ payload 해시만 맞추고 **상태 지문을 다른 값으로** 적은 문서.
         *
         * 이것을 안 잡으면 같은 기록이 영원히 충돌로 읽히거나, 더 나쁘게는
         * 다른 기록이 InSync로 읽혀 한쪽이 조용히 버려진다.
         */
        [Test]
        public void AForgedStateFingerprintIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.stateSha256 = CloudSaveFingerprint.HashOf("다른 상태");

            Assert.AreEqual(CloudSaveEnvelopeFault.StateHashMismatch, envelope.Validate());
        }

        /** 봉투가 말한 버전과 안의 버전이 다르면 둘 중 하나는 거짓이다 */
        [Test]
        public void ASaveVersionThatDisagreesWithThePayloadIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.saveVersion = SaveData.CurrentVersion - 1;

            Assert.AreEqual(CloudSaveEnvelopeFault.SaveVersionMismatch, envelope.Validate());
        }

        [Test]
        public void ASaveVersionOutsideTheKnownRangeIsRejected()
        {
            var zero = Upload(BusySave(), 3L);
            zero.saveVersion = 0;
            Assert.AreEqual(CloudSaveEnvelopeFault.SaveVersionOutOfRange, zero.Validate());

            var future = Upload(BusySave(), 3L);
            future.saveVersion = SaveData.CurrentVersion + 1;
            Assert.AreEqual(CloudSaveEnvelopeFault.SaveVersionOutOfRange, future.Validate());
        }

        /** 요약은 화면이 읽는 값이다. 위조되면 사람이 거짓을 보고 브랜치를 고른다 */
        [Test]
        public void AForgedSummaryIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.summary.gems += 1L;

            Assert.AreEqual(CloudSaveEnvelopeFault.SummaryMismatch, envelope.Validate());
        }

        [Test]
        public void AMissingSummaryIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.summary = null;

            Assert.AreEqual(CloudSaveEnvelopeFault.SummaryMismatch, envelope.Validate());
        }

        /**
         * ★ **읽히는 것과 세이브인 것은 다르다.**
         *
         * "{}"는 JsonUtility가 군말 없이 객체 하나로 만들어 준다. 해시까지 맞춰
         * 올려 두면 검사가 파싱에만 기댈 때 그대로 통과하고, 적용하는 순간 진행이
         * 통째로 사라진다.
         */
        [Test]
        public void AnEmptyJsonPayloadIsNeverASave()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.payload = "{}";
            envelope.payloadSha256 = CloudSaveFingerprint.HashOf(envelope.payload);

            Assert.AreEqual(CloudSaveEnvelopeFault.EmptySave, envelope.Validate());
        }

        [Test]
        public void AnEmptyPayloadIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.payload = string.Empty;
            envelope.payloadSha256 = CloudSaveFingerprint.HashOf(envelope.payload);

            Assert.AreEqual(CloudSaveEnvelopeFault.PayloadEmpty, envelope.Validate());
        }

        [Test]
        public void AnUnreadablePayloadIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.payload = "{\"version\":21, 이건 JSON이 아니다";
            envelope.payloadSha256 = CloudSaveFingerprint.HashOf(envelope.payload);

            Assert.AreEqual(CloudSaveEnvelopeFault.PayloadUnreadable, envelope.Validate());
        }

        [Test]
        public void ARevisionBelowOneIsRejected()
        {
            var zero = Upload(BusySave(), 3L);
            zero.revision = 0L;
            zero.baseRevision = -1L;
            Assert.AreEqual(CloudSaveEnvelopeFault.RevisionOutOfRange, zero.Validate());

            var negative = Upload(BusySave(), 3L);
            negative.revision = -5L;
            negative.baseRevision = -6L;
            Assert.AreEqual(CloudSaveEnvelopeFault.RevisionOutOfRange, negative.Validate());
        }

        /** 사슬이 문서 안에 남아 있어야 "무엇에서 이어졌는가"를 말할 수 있다 */
        [Test]
        public void ABrokenRevisionChainIsRejected()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.baseRevision = envelope.revision - 2L;

            Assert.AreEqual(CloudSaveEnvelopeFault.RevisionChainBroken, envelope.Validate());
        }

        /** 형식은 **정확히 일치**해야 한다. 옛 형식도 우리가 아는 모양이 아니다 */
        [Test]
        public void AnEnvelopeFormatThatIsNotOursIsRejected()
        {
            var older = Upload(BusySave(), 3L);
            older.formatVersion = CloudSaveEnvelope.CurrentFormatVersion - 1;
            Assert.AreEqual(CloudSaveEnvelopeFault.FormatVersionMismatch, older.Validate());

            var newer = Upload(BusySave(), 3L);
            newer.formatVersion = CloudSaveEnvelope.CurrentFormatVersion + 1;
            Assert.AreEqual(CloudSaveEnvelopeFault.FormatVersionMismatch, newer.Validate());
        }

        [Test]
        public void AnOversizedPayloadIsRejectedOnDownload()
        {
            var envelope = Upload(BusySave(), 3L);
            envelope.payload = new string('x', CloudSaveFingerprint.MaxPayloadBytes + 1);
            envelope.payloadSha256 = CloudSaveFingerprint.HashOf(envelope.payload);

            Assert.AreEqual(CloudSaveEnvelopeFault.PayloadTooLarge, envelope.Validate());
        }

        /**
         * ★ id 셋이 규격 밖이면 **우리 코드가 쓴 문서가 아니다.**
         *
         * 그대로 두면 응답 유실 복구가 서버의 id와 영원히 안 맞고(mutation),
         * 2단계의 세션 일치 규칙이 임의 문자열을 진짜 세션으로 받아들인다.
         */
        [Test]
        public void AnEnvelopeWithMalformedIdsIsRejectedOnDownload()
        {
            var mutation = Upload(BusySave(), 3L);
            mutation.lastMutationId = "mutation-a";
            Assert.AreEqual(CloudSaveEnvelopeFault.MutationIdMalformed, mutation.Validate());

            var session = Upload(BusySave(), 3L);
            session.sessionId = string.Empty;
            Assert.AreEqual(CloudSaveEnvelopeFault.SessionIdMalformed, session.Validate());

            var device = Upload(BusySave(), 3L);
            device.deviceId = "device-1";
            Assert.AreEqual(CloudSaveEnvelopeFault.DeviceIdMalformed, device.Validate());

            foreach (var envelope in new[] { mutation, session, device })
                Assert.AreEqual(CloudSaveBlock.InvalidEnvelope,
                                CloudSavePolicy.BlockFor(envelope.Validate()));
        }

        /**
         * ★ **만든 것은 언제나 스스로 검사를 지난다.**
         *
         * 내려받는 쪽에만 검사가 있으면 잘못된 것을 만들어 놓고 서버가 거부하기를
         * 기다리는 코드가 남는다.
         */
        [Test]
        public void EveryEnvelopeWeBuildValidatesClean()
        {
            long[] bases = { 0L, 1L, 41L, long.MaxValue - 1L };

            foreach (long baseRevision in bases)
            {
                var envelope = Upload(BusySave(), baseRevision);

                Assert.IsNotNull(envelope, "base " + baseRevision);
                Assert.AreEqual(CloudSaveEnvelopeFault.None, envelope.Validate(), "base " + baseRevision);
            }
        }

        /**
         * ★ 세이브가 성립하지 않으면 **봉투가 되지 않는다.**
         *
         * 이 값들은 위 검사(id·revision·상한)를 전부 지나서 payload가 된 뒤에야
         * 드러난다 - 그래서 만드는 자리가 자기 결과를 한 번 검사해야 걸린다.
         */
        [Test]
        public void AnInvalidSaveNeverBecomesAnEnvelope()
        {
            AssertNotUploadable(save => save.version = 0, "버전 0");
            AssertNotUploadable(save => save.stage = 0, "스테이지 0");
            AssertNotUploadable(save => save.characterLevel = 0, "레벨 0");
            AssertNotUploadable(save => save.maxStageReached = 0, "최전선 0");
            AssertNotUploadable(save => save.version = SaveData.CurrentVersion + 1, "미래 버전");
        }

        // ---------------------------------------------------------------- 판정표

        /** 오프라인은 실패가 아니라 정상 경로다. 게임은 멈추지 않는다 */
        [Test]
        public void WithoutTheServerWeJustPlayLocally()
        {
            var facts = Facts();
            facts.serverChecked = false;

            AssertDecision(CloudSaveDecision.LocalOnly, facts);
        }

        [Test]
        public void AnEmptyServerTakesTheLocalSaveAsRevisionOne()
        {
            var facts = Facts();
            facts.hasCloud = false;
            facts.hasSidecar = false;
            facts.baseRevision = 0L;

            AssertDecision(CloudSaveDecision.UploadLocal, facts);
        }

        /** 아직 한 번도 안 올린 기기는 sidecar가 있어도 첫 정본을 만든다 */
        [Test]
        public void ADeviceThatNeverSyncedMayStillCreateTheFirstRevision()
        {
            var facts = Facts();
            facts.hasCloud = false;
            facts.hasSidecar = true;
            facts.baseRevision = 0L;

            AssertDecision(CloudSaveDecision.UploadLocal, facts);
        }

        /**
         * ★ **동기화 이력이 있는데 서버 문서가 없다.**
         *
         * 여기서 신규 업로드를 내면 사슬이 1로 리셋되어, 다른 기기가 들고 있는
         * base 41이 영원히 맞을 곳을 잃는다. 진짜 이유는 대개 로그인이 갈렸거나
         * 서버 사고이지 "새로 시작해도 된다"가 아니다.
         */
        [Test]
        public void ASyncedDeviceNeverRecreatesAVanishedDocument()
        {
            var facts = Facts();
            facts.hasCloud = false;
            facts.hasSidecar = true;
            facts.baseRevision = 41L;

            AssertBlocked(CloudSaveBlock.MissingServerAfterSync, facts);
        }

        [Test]
        public void OnlyAnUnsyncedDeviceMayCreateTheFirstRevision()
        {
            Assert.IsTrue(CloudSavePolicy.CanCreateFirstRevision(false, 0L));
            Assert.IsTrue(CloudSavePolicy.CanCreateFirstRevision(false, 41L), "sidecar가 없으면 아는 것이 없다");
            Assert.IsTrue(CloudSavePolicy.CanCreateFirstRevision(true, 0L));
            Assert.IsFalse(CloudSavePolicy.CanCreateFirstRevision(true, 1L));
            Assert.IsFalse(CloudSavePolicy.CanCreateFirstRevision(true, 41L));
        }

        [Test]
        public void AFreshInstallWithNothingAnywhereJustStarts()
        {
            var facts = Facts();
            facts.hasCloud = false;
            facts.hasLocal = false;
            facts.hasSidecar = false;
            facts.baseRevision = 0L;

            AssertDecision(CloudSaveDecision.LocalOnly, facts);
        }

        /** 재설치·데이터 삭제. 클라우드가 유일한 기록이라 자동 복구가 맞다 */
        [Test]
        public void WithoutALocalSaveTheCloudIsTheOnlyRecord()
        {
            var facts = Facts();
            facts.hasLocal = false;

            AssertDecision(CloudSaveDecision.DownloadCloud, facts);
        }

        [Test]
        public void TheSameRecordOnBothSidesWritesNothing()
        {
            var facts = Facts();
            facts.cloudStateSha = facts.localStateSha;

            AssertDecision(CloudSaveDecision.InSync, facts);
        }

        /** 다른 기기가 앞서 갔고 이쪽은 논 적이 없다 */
        [Test]
        public void AnUntouchedDeviceFollowsTheServer()
        {
            var facts = Facts();
            facts.lastSyncedStateSha = facts.localStateSha;  // 로컬은 마지막 동기화 그대로

            AssertDecision(CloudSaveDecision.DownloadCloud, facts);
        }

        /** 이쪽만 놀았다. 서버는 우리가 두고 온 그 자리다 */
        [Test]
        public void APlayedDeviceOnAnUnchangedServerUploads()
        {
            var facts = Facts();
            SetServerRevision(ref facts, facts.baseRevision);

            AssertDecision(CloudSaveDecision.UploadLocal, facts);
        }

        /**
         * ★ 이 설계의 중심. **둘 다 움직였으면 자동으로 고르지 않는다.**
         *
         * 여기서 한쪽을 고르는 코드를 넣는 순간(예: 도달층이 높은 쪽) 그것이
         * 곧 필드별 병합의 첫 줄이 된다.
         */
        [Test]
        public void TwoBranchesAreNeverMergedAutomatically()
        {
            AssertDecision(CloudSaveDecision.Conflict, Facts());
        }

        /** 아무것도 모르는 기기(첫 실행·복구 직후)는 무조건 사람에게 묻는다 */
        [Test]
        public void WithoutASidecarWeCannotTellWhichCameFirst()
        {
            var facts = Facts();
            facts.hasSidecar = false;
            facts.baseRevision = 0L;
            facts.lastSyncedStateSha = string.Empty;

            AssertDecision(CloudSaveDecision.Conflict, facts);
        }

        /** 같은 revision인데 내용이 다르다 = 사슬이 설명 못 하는 상태 */
        [Test]
        public void ContentThatMovedWithoutARevisionIsAConflict()
        {
            var facts = Facts();
            SetServerRevision(ref facts, facts.baseRevision);
            facts.lastSyncedStateSha = facts.localStateSha;

            AssertDecision(CloudSaveDecision.Conflict, facts);
        }

        // ---------------------------------------------------------------- 차단

        [Test]
        public void AFutureSaveIsNeverAppliedAndNeverOverwritten()
        {
            var facts = Facts();
            facts.cloudSaveVersion = SaveData.CurrentVersion + 1;

            AssertBlocked(CloudSaveBlock.FutureSaveVersion, facts);
        }

        [Test]
        public void AFutureEnvelopeFormatIsAlsoBlocked()
        {
            var facts = Facts();
            facts.cloudFormatVersion = CloudSaveEnvelope.CurrentFormatVersion + 1;

            AssertBlocked(CloudSaveBlock.FutureFormatVersion, facts);
        }

        [Test]
        public void ACorruptPayloadIsNeverApplied()
        {
            var facts = Facts();
            facts.cloudEnvelopeFault = CloudSaveEnvelopeFault.PayloadHashMismatch;

            AssertBlocked(CloudSaveBlock.CorruptPayload, facts);
        }

        [Test]
        public void AnOversizedDocumentIsBlocked()
        {
            var facts = Facts();
            facts.cloudEnvelopeFault = CloudSaveEnvelopeFault.PayloadTooLarge;

            AssertBlocked(CloudSaveBlock.PayloadTooLarge, facts);
        }

        /** revision 0·음수는 정본이 아니다 */
        [Test]
        public void AnEnvelopeWithoutARevisionIsBlocked()
        {
            var zero = Facts();
            zero.cloudRevision = 0L;
            zero.cloudBaseRevision = -1L;
            AssertBlocked(CloudSaveBlock.InvalidEnvelope, zero);

            var negative = Facts();
            negative.cloudRevision = -3L;
            negative.cloudBaseRevision = -4L;
            AssertBlocked(CloudSaveBlock.InvalidEnvelope, negative);
        }

        [Test]
        public void ABrokenChainIsBlockedEvenWhenThePayloadIsFine()
        {
            var facts = Facts();
            facts.cloudBaseRevision = facts.cloudRevision - 2L;

            AssertBlocked(CloudSaveBlock.InvalidEnvelope, facts);
        }

        /** 손상과 갈라 둔 이유는 고치는 사람이 다르기 때문이다 */
        [Test]
        public void EveryEnvelopeFaultMapsToABlock()
        {
            foreach (CloudSaveEnvelopeFault fault
                     in Enum.GetValues(typeof(CloudSaveEnvelopeFault)))
            {
                CloudSaveBlock block = CloudSavePolicy.BlockFor(fault);

                if (fault == CloudSaveEnvelopeFault.None)
                {
                    Assert.AreEqual(CloudSaveBlock.None, block);
                    continue;
                }

                Assert.AreNotEqual(CloudSaveBlock.None, block, fault + " 에 이유가 없다");
            }

            Assert.AreEqual(CloudSaveBlock.InvalidEnvelope,
                            CloudSavePolicy.BlockFor(CloudSaveEnvelopeFault.RevisionChainBroken));
            Assert.AreEqual(CloudSaveBlock.CorruptPayload,
                            CloudSavePolicy.BlockFor(CloudSaveEnvelopeFault.SummaryMismatch));
            Assert.AreEqual(CloudSaveBlock.CorruptPayload,
                            CloudSavePolicy.BlockFor(CloudSaveEnvelopeFault.EmptySave));
        }

        /** 서버가 뒤로 갔다. 그 위에 새 사슬을 얹으면 사고가 굳는다 */
        [Test]
        public void AServerThatWentBackwardsIsNeverWrittenTo()
        {
            var facts = Facts();
            facts.baseRevision = 9L;
            SetServerRevision(ref facts, 4L);

            AssertBlocked(CloudSaveBlock.ServerRollback, facts);
        }

        /**
         * ★ 순서가 곧 우선순위다. **미래 버전이 다른 무엇보다 먼저다.**
         *
         * 미래 버전 세이브를 "로컬이 변했으니 업로드"로 덮으면, 다른 기기의
         * 최신 진행이 옛 앱에 의해 지워진다. 봉투가 동시에 망가져 있어도
         * 사람에게 할 말은 여전히 "앱을 업데이트하세요"다.
         */
        [Test]
        public void FutureVersionsWinOverEveryOtherReason()
        {
            var save = Facts();
            save.cloudSaveVersion = SaveData.CurrentVersion + 1;
            save.cloudEnvelopeFault = CloudSaveEnvelopeFault.PayloadHashMismatch;
            save.cloudRevision = 0L;
            save.cloudBaseRevision = -1L;
            SetLocalDirtyOnUnchangedServer(ref save);
            AssertBlocked(CloudSaveBlock.FutureSaveVersion, save);

            var format = Facts();
            format.cloudFormatVersion = CloudSaveEnvelope.CurrentFormatVersion + 1;
            format.cloudSaveVersion = SaveData.CurrentVersion + 1;
            AssertBlocked(CloudSaveBlock.FutureFormatVersion, format);
        }

        // ---------------------------------------------------------------- 재시도

        /**
         * ★ 응답 유실 복구. 서버는 커밋했는데 앱이 그것을 못 들은 경우다.
         */
        [Test]
        public void TheSameMutationIdMeansTheCommitAlreadyLanded()
        {
            string id = CloudSaveIds.New();

            Assert.IsTrue(CloudSavePolicy.WasCommitAlreadyApplied(id, id));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(id, CloudSaveIds.New()));
        }

        /** 빈 값끼리는 절대 같다고 하지 않는다 - 첫 업로드가 영원히 안 나간다 */
        [Test]
        public void EmptyOrMalformedMutationIdsAreNeverConsideredApplied()
        {
            string id = CloudSaveIds.New();

            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(null, null));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(string.Empty, string.Empty));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(id, string.Empty));
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied(string.Empty, id));

            // 형식이 아닌 값이 양쪽에 같이 적혀 있어도 "성공했다"로 읽지 않는다
            Assert.IsFalse(CloudSavePolicy.WasCommitAlreadyApplied("mutation-a", "mutation-a"));
        }

        /** 지문을 못 구한 것과 같은 기록인 것은 다른 말이다 */
        [Test]
        public void MissingFingerprintsAreNeverEqual()
        {
            Assert.IsFalse(CloudSavePolicy.SameState(null, null));
            Assert.IsFalse(CloudSavePolicy.SameState(string.Empty, string.Empty));
            Assert.IsTrue(CloudSavePolicy.SameState("ab12", "AB12"));
        }

        // ---------------------------------------------------------------- sidecar

        [Test]
        public void TheSidecarSurvivesTheDisk()
        {
            var state = NewSidecar();
            string payloadSha = CloudSaveFingerprint.HashOf("payload");
            string stateSha = CloudSaveFingerprint.HashOf("state");

            Assert.IsTrue(state.MarkSynced(12L, payloadSha, stateSha, 638_000_000_000_000_000L));

            Assert.IsTrue(CloudSaveSidecar.Save(state), "원자 교체까지 끝났으면 true");
            var loaded = CloudSaveSidecar.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(state.ownerUid, loaded.ownerUid);
            Assert.AreEqual(state.deviceId, loaded.deviceId);
            Assert.AreEqual(12L, loaded.baseRevision);
            Assert.AreEqual(payloadSha, loaded.lastSyncedPayloadSha256);
            Assert.AreEqual(stateSha, loaded.lastSyncedStateSha256);
            Assert.AreEqual(638_000_000_000_000_000L, loaded.lastKnownServerUpdatedAtUtcTicks);
            Assert.IsFalse(loaded.HasPending);
        }

        /** 없는 것은 없다고 말한다. 이 파일이 없어도 게임은 돈다 */
        [Test]
        public void AnAbsentSidecarIsSimplyAbsent()
        {
            CloudSaveSidecar.Delete();
            Assert.IsNull(CloudSaveSidecar.Load());
        }

        /** 모르는 형식의 revision을 믿으면 사슬 중간에서 출발하는 쓰기가 나간다 */
        [Test]
        public void AFutureSidecarIsTreatedAsAbsent()
        {
            WriteRawSidecar("{\"formatVersion\":99,\"ownerUid\":\"uid-1\",\"baseRevision\":41}");

            Assert.IsNull(CloudSaveSidecar.Load());
        }

        [Test]
        public void AnUnreadableSidecarIsTreatedAsAbsent()
        {
            WriteRawSidecar("이것은 JSON이 아니다");

            Assert.IsNull(CloudSaveSidecar.Load());
        }

        /**
         * ★ **반쯤 맞는 sidecar가 없는 것보다 나쁘다.**
         *
         * JsonUtility는 잘린 JSON에서도 객체를 만들어 준다(없는 필드는 기본값).
         * 그것을 믿으면 "revision 41까지 동기화했다"고만 적힌 껍데기가 다음
         * 부팅에서 로컬을 안 변한 것으로 읽어 조용히 덮거나 내려받는다.
         */
        [Test]
        public void APartialSidecarIsTreatedAsAbsent()
        {
            WriteRawSidecar("{\"formatVersion\":1,\"baseRevision\":41}");
            Assert.IsNull(CloudSaveSidecar.Load(), "소유 uid도 기기 id도 없다");

            WriteRawSidecar("{\"formatVersion\":1,\"ownerUid\":\"uid-1\",\"deviceId\":\""
                            + CloudSaveIds.New() + "\",\"baseRevision\":41}");
            Assert.IsNull(CloudSaveSidecar.Load(), "동기화했다는데 지문이 없다");

            WriteRawSidecar("{\"formatVersion\":1,\"ownerUid\":\"uid-1\",\"deviceId\":\"device-1\"}");
            Assert.IsNull(CloudSaveSidecar.Load(), "기기 id가 형식 밖이다");

            WriteRawSidecar("{\"formatVersion\":1,\"ownerUid\":\"uid-1\",\"deviceId\":\""
                            + CloudSaveIds.New() + "\",\"baseRevision\":-1}");
            Assert.IsNull(CloudSaveSidecar.Load(), "revision이 음수다");
        }

        /** 짝이 하나만 남으면 "무엇을 보내는 중이었는가"를 말할 수 없다 */
        [Test]
        public void AHalfWrittenPendingPairIsTreatedAsAbsent()
        {
            string device = CloudSaveIds.New();

            WriteRawSidecar("{\"formatVersion\":1,\"ownerUid\":\"uid-1\",\"deviceId\":\"" + device
                            + "\",\"baseRevision\":0,\"pendingMutationId\":\"" + CloudSaveIds.New() + "\"}");
            Assert.IsNull(CloudSaveSidecar.Load(), "쓰기 id만 있다");

            WriteRawSidecar("{\"formatVersion\":1,\"ownerUid\":\"uid-1\",\"deviceId\":\"" + device
                            + "\",\"baseRevision\":0,\"pendingPayloadSha256\":\""
                            + CloudSaveFingerprint.HashOf("payload") + "\"}");
            Assert.IsNull(CloudSaveSidecar.Load(), "지문만 있다");
        }

        /** 불변식을 어긴 것은 **디스크에 남기지도 않는다** - 그리고 false를 낸다 */
        [Test]
        public void AMalformedSidecarIsNeverWritten()
        {
            Assert.IsFalse(CloudSaveSidecar.Save(null));

            var noOwner = NewSidecar();
            noOwner.ownerUid = string.Empty;
            Assert.IsFalse(CloudSaveSidecar.Save(noOwner));

            // 부분 손상: revision은 있는데 그때의 지문이 없다
            var halfSynced = NewSidecar();
            halfSynced.baseRevision = 41L;
            Assert.IsFalse(CloudSaveSidecar.Save(halfSynced));

            // 짝이 하나만 남은 pending
            var halfPending = NewSidecar();
            halfPending.pendingMutationId = CloudSaveIds.New();
            Assert.IsFalse(CloudSaveSidecar.Save(halfPending));

            Assert.IsFalse(CloudSaveSidecar.Exists);
        }

        /**
         * ★★ **pending이 디스크에 남기 전에는 서버로 나가지 않는다.**
         *
         * 응답 유실 복구가 그 파일 하나에 걸려 있다 - 서버는 커밋했는데 그
         * 커밋의 mutation id가 로컬 어디에도 없으면, 다음 실행이 같은 쓰기를
         * 다시 올려 revision을 하나 더 올리고 직전 백업을 한 칸 밀어낸다.
         *
         * 저장 실패를 진짜로 만든다 - 파일이 놓일 자리를 폴더로 막으면
         * 원자 교체가 IO 예외로 끝난다. 2단계는 이 false를 보고 멈춰야 한다.
         */
        [Test]
        public void AFailedSidecarWriteStopsTheServerWrite()
        {
            var state = NewSidecar();
            Assert.IsTrue(state.MarkPending(CloudSaveIds.New(),
                                            CloudSaveFingerprint.HashOf("payload")));

            Directory.CreateDirectory(CloudSaveSidecar.Path);
            try
            {
                bool persisted = CloudSaveSidecar.Save(state);

                Assert.IsFalse(persisted, "원자 교체가 실패했으면 false여야 한다");
                Assert.IsFalse(CloudSavePolicy.MayStartServerWrite(state, persisted),
                               "저장 실패 상태에서 클라우드 write는 금지다 - 로컬 dirty로 남는다");
            }
            finally
            {
                Directory.Delete(CloudSaveSidecar.Path, true);
                if (File.Exists(CloudSaveSidecar.Path + ".tmp"))
                    File.Delete(CloudSaveSidecar.Path + ".tmp");
            }
        }

        /** 그 계약의 나머지 절반: 저장이 됐고 보낼 것이 있을 때만 나간다 */
        [Test]
        public void TheServerWriteWaitsForAPersistedPending()
        {
            var state = NewSidecar();

            Assert.IsFalse(CloudSavePolicy.MayStartServerWrite(state, true),
                           "보낼 것을 가리키는 id가 없다");
            Assert.IsFalse(CloudSavePolicy.MayStartServerWrite(null, true));

            Assert.IsTrue(state.MarkPending(CloudSaveIds.New(),
                                            CloudSaveFingerprint.HashOf("payload")));

            Assert.IsTrue(CloudSaveSidecar.Save(state));
            Assert.IsTrue(CloudSavePolicy.MayStartServerWrite(state, true));
            Assert.IsFalse(CloudSavePolicy.MayStartServerWrite(state, false),
                           "디스크에 안 남았으면 pending이 있어도 안 나간다");

            // 불변식을 어긴 상태로 손상되면(부분 쓰기 등) 그것도 멈춘다
            state.baseRevision = 41L;
            Assert.IsFalse(CloudSavePolicy.MayStartServerWrite(state, true));
        }

        /** 보내기 **직전에** 적고, 성공을 확인한 **뒤에** 지운다 */
        [Test]
        public void PendingIsClearedOnlyBySuccess()
        {
            var state = NewSidecar();
            string mutation = CloudSaveIds.New();
            string payloadSha = CloudSaveFingerprint.HashOf("payload");

            Assert.IsTrue(state.MarkPending(mutation, payloadSha));
            Assert.IsTrue(state.HasPending);
            Assert.AreEqual(mutation, state.pendingMutationId);
            Assert.IsTrue(state.IsWellFormed());

            Assert.IsTrue(state.MarkSynced(1L, payloadSha, CloudSaveFingerprint.HashOf("state"), 0L));
            Assert.IsFalse(state.HasPending);
            Assert.AreEqual(1L, state.baseRevision);
        }

        /**
         * 형식이 아닌 mutation id를 적어 두면 응답 유실 복구가 서버의 id와
         * 영원히 안 맞는다 - "복구 장치가 있는데 작동하지 않는" 모양이다.
         */
        [Test]
        public void MarkPendingRefusesWhatItCannotUseLater()
        {
            var state = NewSidecar();
            string mutation = CloudSaveIds.New();
            string payloadSha = CloudSaveFingerprint.HashOf("payload");

            Assert.IsFalse(state.MarkPending("mutation-a", payloadSha));
            Assert.IsFalse(state.MarkPending(mutation, "payload-sha"));
            Assert.IsFalse(state.MarkPending(string.Empty, payloadSha));
            Assert.IsFalse(state.MarkPending(mutation, string.Empty));

            Assert.IsFalse(state.HasPending, "거부된 값이 절반이라도 남으면 안 된다");
            Assert.IsTrue(state.IsWellFormed());
        }

        [Test]
        public void MarkSyncedRefusesImpossibleRevisionsAndHashes()
        {
            var state = NewSidecar();
            string payloadSha = CloudSaveFingerprint.HashOf("payload");
            string stateSha = CloudSaveFingerprint.HashOf("state");

            Assert.IsFalse(state.MarkSynced(0L, payloadSha, stateSha, 0L), "정본은 1부터다");
            Assert.IsFalse(state.MarkSynced(-1L, payloadSha, stateSha, 0L));
            Assert.IsFalse(state.MarkSynced(1L, "payload-sha", stateSha, 0L));
            Assert.IsFalse(state.MarkSynced(1L, payloadSha, "state-sha", 0L));
            Assert.IsFalse(state.MarkSynced(1L, payloadSha, stateSha, -1L));

            Assert.AreEqual(0L, state.baseRevision, "거부된 값이 남으면 안 된다");
            Assert.IsTrue(state.IsWellFormed());
        }

        /** 만들 때부터 형식을 본다 - 잘못된 객체는 존재하지 않는 편이 낫다 */
        [Test]
        public void ASidecarIsNeverCreatedWithAMalformedIdentity()
        {
            Assert.IsNull(CloudSaveLocalState.NewFor(string.Empty, CloudSaveIds.New()));
            Assert.IsNull(CloudSaveLocalState.NewFor(null, CloudSaveIds.New()));
            Assert.IsNull(CloudSaveLocalState.NewFor("uid-1", "device-1"));
            Assert.IsNull(CloudSaveLocalState.NewFor("uid-1", string.Empty));

            Assert.IsNotNull(CloudSaveLocalState.NewFor("uid-1", CloudSaveIds.New()));
        }

        /** 복구로 uid가 바뀌면 그 sidecar는 **다른 계정의 사슬**이다 */
        [Test]
        public void ASidecarBelongsToExactlyOneAccount()
        {
            var state = CloudSaveLocalState.NewFor("uid-1", CloudSaveIds.New());

            Assert.IsTrue(CloudSavePolicy.SidecarAppliesTo(state, "uid-1"));
            Assert.IsFalse(CloudSavePolicy.SidecarAppliesTo(state, "uid-2"));
            Assert.IsFalse(CloudSavePolicy.SidecarAppliesTo(state, string.Empty));
            Assert.IsFalse(CloudSavePolicy.SidecarAppliesTo(null, "uid-1"));
        }

        /** 불변식을 어긴 sidecar도 "이 계정 것"이라 말하지 않는다 */
        [Test]
        public void AMalformedSidecarBelongsToNobody()
        {
            var state = CloudSaveLocalState.NewFor("uid-1", CloudSaveIds.New());
            state.baseRevision = 41L;   // 지문 없이 revision만 적힌 상태

            Assert.IsFalse(state.IsWellFormed());
            Assert.IsFalse(CloudSavePolicy.SidecarAppliesTo(state, "uid-1"));
        }

        // ---------------------------------------------------------------- 시간

        /**
         * 세션 만료는 저장 주기보다 길어야 한다. 짧으면 정상적으로 놀고 있는
         * 기기가 저장과 저장 사이에 스스로 만료된다.
         */
        [Test]
        public void ASessionOutlivesItsOwnSaveInterval()
        {
            Assert.Greater(CloudSavePolicy.SessionExpirySeconds, CloudSavePolicy.DebounceSeconds);

            Assert.IsFalse(CloudSavePolicy.IsSessionExpired(
                CloudSavePolicy.SessionExpirySeconds - 1f));
            Assert.IsTrue(CloudSavePolicy.IsSessionExpired(
                CloudSavePolicy.SessionExpirySeconds));
        }

        /** 부팅 게이트는 유한해야 한다. Firebase는 무한 로딩의 이유가 될 수 없다 */
        [Test]
        public void TheBootGateIsFinite()
        {
            Assert.Greater(CloudSavePolicy.BootServerCheckSeconds, 0f);
            Assert.Less(CloudSavePolicy.BootServerCheckSeconds, 15f);
        }

        // ---------------------------------------------------------------- 부재

        /**
         * **보조 검사다.** 이름에 merge/union이 들어간 API가 없다는 것만 말한다.
         *
         * 이 검사가 필드 병합을 막는다고 읽으면 안 된다 - 어떤 함수 안에서
         * 손으로 필드를 섞는 코드는 이름을 아무렇게나 지을 수 있고, 리플렉션은
         * 그것을 보지 못한다. 실제로 막는 것은 둘이다:
         *
         *   판정      갈라지면 Conflict가 나온다. 합칠 자리가 애초에 없다
         *             (TwoBranchesAreNeverMergedAutomatically)
         *   형식      서버에 놓이는 것은 필드가 아니라 payload 문자열 한 덩어리다.
         *             반만 가져올 방법이 없다 (TheWholeSaveSurvivesThePayloadRoundTrip)
         *
         * 이름 검사가 잡는 것은 **의도**다 - 누군가 병합 API를 세우려 할 때
         * 가장 먼저 부딪히는 벽이고, 그 순간 이 주석을 읽게 된다.
         *
         * 랭킹의 max 병합(AccountLinkPolicy.MergedStage)은 남아 있어야 한다.
         * 그것은 세이브가 아니라 순위표의 값 하나이고, 지불과 묶여 있지 않다 -
         * **경계가 어디인지**를 이 테스트가 함께 적어 둔다.
         */
        [Test]
        public void NoMethodNameAdvertisesABranchMerge()
        {
            Type[] types =
            {
                typeof(CloudSavePolicy), typeof(CloudSaveFingerprint), typeof(CloudSaveEnvelope),
                typeof(CloudSaveSummary), typeof(CloudSaveLocalState), typeof(CloudSaveSidecar),
                typeof(CloudSaveIds), typeof(CloudSaveFacts), typeof(CloudSaveVerdict)
            };

            string[] banned = { "merge", "union", "combine", "reconcile" };

            foreach (Type type in types)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                                       | BindingFlags.Static | BindingFlags.Instance
                                                       | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    string name = method.Name.ToLowerInvariant();
                    foreach (string word in banned)
                        Assert.IsFalse(name.Contains(word),
                            type.Name + "." + method.Name + " - 브랜치를 합치는 이름은 쓰지 않는다");
                }
            }

            Assert.IsNotNull(typeof(AccountLinkPolicy).GetMethod("MergedStage"),
                "랭킹 도달층의 max 병합은 세이브와 다른 물건이라 그대로 남는다");
        }

        /** 세이브 형식은 이 스텝에서 **한 칸도 안 움직인다** */
        [Test]
        public void CrossSaveDoesNotTouchTheSaveFormat()
        {
            Assert.AreEqual(21, SaveData.CurrentVersion,
                "동기화 메타는 sidecar에 산다 - 세이브 버전을 올릴 이유가 없다");
        }

        // ---------------------------------------------------------------- 도구

        /**
         * @brief 판정 기본값: **서버가 앞섰고 로컬도 놀았다** = 충돌.
         *
         * 각 테스트가 한 필드만 바꿔 자기 줄을 만든다. 기본값을 가장 위험한
         * 상태로 둔 것은 의도다 - 실수로 아무것도 안 바꾼 테스트가 통과해서는 안 된다.
         */
        private static CloudSaveFacts Facts()
        {
            return new CloudSaveFacts
            {
                serverChecked = true,
                hasLocal = true,
                localStateSha = "local-state",

                hasCloud = true,
                cloudFormatVersion = CloudSaveEnvelope.CurrentFormatVersion,
                cloudSaveVersion = SaveData.CurrentVersion,
                cloudRevision = 6L,
                cloudBaseRevision = 5L,
                cloudStateSha = "cloud-state",
                cloudEnvelopeFault = CloudSaveEnvelopeFault.None,

                hasSidecar = true,
                baseRevision = 5L,
                lastSyncedStateSha = "synced-state"
            };
        }

        /** 서버 revision을 옮길 때 **사슬도 함께 옮긴다** - 안 그러면 InvalidEnvelope에 걸린다 */
        private static void SetServerRevision(ref CloudSaveFacts facts, long revision)
        {
            facts.cloudRevision = revision;
            facts.cloudBaseRevision = revision - 1L;
        }

        private static void SetLocalDirtyOnUnchangedServer(ref CloudSaveFacts facts)
        {
            facts.lastSyncedStateSha = "synced-state";
            facts.baseRevision = facts.cloudRevision;
        }

        private static void AssertDecision(CloudSaveDecision expected, CloudSaveFacts facts)
        {
            CloudSaveVerdict verdict = CloudSavePolicy.Decide(facts);

            Assert.AreEqual(expected, verdict.decision, "판정: " + verdict);
            Assert.AreEqual(CloudSaveBlock.None, verdict.block);
        }

        private static void AssertBlocked(CloudSaveBlock expected, CloudSaveFacts facts)
        {
            CloudSaveVerdict verdict = CloudSavePolicy.Decide(facts);

            Assert.AreEqual(CloudSaveDecision.Blocked, verdict.decision, "판정: " + verdict);
            Assert.AreEqual(expected, verdict.block);
        }

        private static void AssertStateChanges(Action<SaveData> change, string what)
        {
            var before = BusySave();
            var after = BusySave();
            change(after);

            Assert.AreNotEqual(CloudSaveFingerprint.StateHashOf(before),
                               CloudSaveFingerprint.StateHashOf(after),
                               what + "은(는) 진행이다 - 상태 지문이 달라져야 한다");
        }

        /** 실제 생성 함수로 만든 id 셋을 쓴다 - 테스트가 형식을 우회하지 않는다 */
        private static CloudSaveEnvelope Upload(SaveData data, long baseRevision)
        {
            return CloudSaveEnvelope.ForUpload(data, baseRevision,
                                               CloudSaveSidecar.NewSessionId(),
                                               CloudSaveSidecar.NewDeviceId(),
                                               CloudSaveSidecar.NewMutationId());
        }

        private static void AssertNotUploadable(Action<SaveData> damage, string what)
        {
            var save = BusySave();
            damage(save);

            Assert.IsNull(Upload(save, 0L), what + " 인 세이브는 봉투가 되면 안 된다");
        }

        private static CloudSaveLocalState NewSidecar()
        {
            return CloudSaveLocalState.NewFor("uid-1", CloudSaveSidecar.NewDeviceId());
        }

        private static void WriteRawSidecar(string json)
        {
            File.WriteAllText(CloudSaveSidecar.Path, json);
        }

        /**
         * @brief 모든 칸이 기본값과 다른 세이브.
         *
         * id들이 실제 카탈로그와 맞지 않아도 된다. 여기서 재는 것은 **형식의
         * 왕복**이지 카탈로그의 유효성이 아니고, 진짜 id를 쓰면 카탈로그가
         * 바뀔 때 이 테스트가 엉뚱한 이유로 깨진다.
         */
        private static SaveData BusySave()
        {
            var data = SaveData.NewGame();

            data.version = SaveData.CurrentVersion;
            data.gold = BigDouble.FromDouble(1.234e12);
            data.lifetimeGold = BigDouble.FromDouble(9.87e15);

            data.upgradeIds = new[] { "track_a", "track_b" };
            data.upgradeLevels = new[] { 665, 42 };

            data.stage = 82;
            data.killsThisStage = 7;
            data.bossKillCount = 311;
            data.maxStageReached = 171;

            data.characterLevel = 48;
            data.exp = BigDouble.FromDouble(4.2e6);
            data.attackPoints = 30;
            data.healthPoints = 12;

            data.lastQuitUtcTicks = 638_000_000_000_000_000L;
            data.goldPerSecond = 1234.5d;
            data.expPerSecond = 67.25d;

            data.skillIds = new[] { "skill_a", "skill_b" };
            data.skillLevels = new[] { 11, 7 };
            data.skillEquipped = new[] { "skill_a", string.Empty, "skill_b", string.Empty };
            data.skillAutoCast = true;

            data.gems = 320L;
            data.questIds = new[] { "quest_a", "quest_b" };
            data.questClaims = new[] { 1, 3 };

            data.questTodayMobKills = 40d;
            data.questTodayBossKills = 2d;
            data.questTodaySkillCasts = 88d;
            data.questTodayUpgrades = 15d;
            data.questTodayGold = BigDouble.FromDouble(5.5e8);

            data.questTotalMobKills = 91_000d;
            data.questTotalBossKills = 311d;
            data.questTotalSkillCasts = 40_000d;
            data.questTotalUpgrades = 2_100d;
            data.questTotalGold = BigDouble.FromDouble(7.7e14);

            data.lastDailyResetUtcTicks = 638_100_000_000_000_000L;

            data.equipmentIds = new[] { "slot_a", "slot_b" };
            data.equipmentGrades = new[] { 5, 3 };
            data.equipmentLevels = new[] { 12, 4 };

            data.evolutionTier = 3;

            data.petIds = new[] { "pet_a", "pet_b" };
            data.petUnlocked = new[] { 1, 0 };
            data.petLevels = new[] { 9, 1 };
            data.activePetId = string.Empty;

            data.yodoIds = new[] { "yodo_a", "yodo_b" };
            data.yodoSouls = new[] { 12L, 5L };
            data.yodoTiers = new[] { 2, 1 };
            data.yodoDiscovered = new[] { 1, 1 };
            data.yodoRarities = new[] { 4, 3 };
            data.yodoShards = 77L;

            data.gachaPity = 17;
            data.gachaTotalPulls = 213;
            data.gachaFreePullDayTicks = 638_090_000_000_000_000L;

            data.legendaryYodoIds = new[] { "legend_a" };
            data.legendaryYodoCopies = new[] { 2 };

            data.gachaSkillIds = new[] { "skill_c" };
            data.skillXp = 4_200L;
            data.skillGachaPity = 9;
            data.skillGachaTotalPulls = 88;
            data.skillGachaFreePullDayTicks = 638_095_000_000_000_000L;

            data.skillGachaAwakenPity = 41;
            data.skillGachaIntroClaimed = true;
            data.skillGachaIntroEquipDone = true;

            data.playerName = "랑무사";

            return data;
        }

        /** 리플렉션으로 도는 이유는 다음 스텝이 늘린 필드까지 자동으로 재기 위해서다 */
        private static void AssertSameFields(SaveData expected, SaveData actual)
        {
            FieldInfo[] fields = typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.Greater(fields.Length, 40, "세이브의 public 필드를 실제로 훑었는가");

            foreach (FieldInfo field in fields)
            {
                object a = field.GetValue(expected);
                object b = field.GetValue(actual);

                var arrayA = a as Array;
                if (arrayA != null)
                {
                    var arrayB = b as Array;
                    Assert.IsNotNull(arrayB, field.Name + " 가 배열이 아니게 돌아왔다");
                    Assert.AreEqual(arrayA.Length, arrayB.Length, field.Name + " 길이");

                    for (int i = 0; i < arrayA.Length; i++)
                        Assert.AreEqual(arrayA.GetValue(i), arrayB.GetValue(i),
                                        field.Name + "[" + i + "]");
                    continue;
                }

                Assert.AreEqual(a, b, field.Name);
            }
        }
    }
}
