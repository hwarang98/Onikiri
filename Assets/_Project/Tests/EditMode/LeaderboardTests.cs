using System.IO;
using NUnit.Framework;
using Onikiri.Cloud;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 리더보드에서 **네트워크가 아닌 부분**을 검사한다 (54단계).
     *
     * Firestore 왕복은 여기서 증명되지 않는다 - 목을 세워봐야 "내가 짠 목이 내
     * 코드와 맞다"만 나오고, 실제 실패는 규칙·인덱스·Play 서비스에서 나온다.
     * 그 증명은 실기 logcat과 REST 조회의 몫이다(53단계에 세운 방식).
     *
     * 그래서 여기 있는 것은 **게임의 성질**뿐이다:
     *
     *   후퇴 거부   낮은 값이 기록을 덮으면 도달층의 단조성(52단계)이 서버에서만
     *               깨진다. 이 규칙은 Firestore를 다른 것으로 바꿔도 참이다
     *   이름 검증   남의 화면에 뜨는 유일한 내 문자열이다. 줄바꿈 하나가 남의
     *               랭킹표를 밀어낸다
     *   규칙 일치   클라의 상한과 firestore.rules의 상한이 갈리면, 로컬에서
     *               통과한 값이 서버에서 조용히 거부된다
     *   세이브      v19가 이름 한 칸만 늘리고 진행은 한 글자도 안 건드린다
     */
    public class LeaderboardTests
    {
        [TearDown]
        public void ClearProfile()
        {
            // 정적 상태라 테스트 사이에 샌다. 이름을 정한 채로 다음 테스트가
            // 시작하면 "안 정했을 때"의 검사가 조용히 통과한다
            PlayerProfile.Clear();
        }

        // ---------------------------------------------------------------- 제출 정책

        [Test]
        public void FirstSubmission_GoesThroughWithNoServerValue()
        {
            // 서버에 문서가 없으면 무조건 첫 제출이다. 여기서 막으면 아무도
            // 랭킹에 오르지 못한다
            Assert.IsTrue(LeaderboardPolicy.ShouldSubmit(1, 0, false));
        }

        [Test]
        public void HigherReach_IsSubmitted()
        {
            Assert.IsTrue(LeaderboardPolicy.ShouldSubmit(171, 170, true));
        }

        [Test]
        public void LowerReach_NeverOverwritesTheRecord()
        {
            // 재설치·후퇴 기기가 기록을 지우는 경로. 도달층은 로컬에서 절대
            // 안 내려가므로(52단계) 이 값이 오는 경로는 "다른 세이브"뿐이고,
            // 그것이 남의 기록을 덮을 이유는 없다
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(1, 170, true));
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(169, 170, true));
        }

        [Test]
        public void EqualReach_IsNotResubmitted()
        {
            // 같은 값을 다시 쓰면 updatedAt만 갱신되어, 동점 tie-break가
            // "먼저 도달한 사람이 위"인 이 랭킹에서 **자기 순위를 스스로 내린다**
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(170, 170, true));
        }

        [Test]
        public void RenamingReachesTheServerWithoutClimbing()
        {
            // 실기에서 잡은 결함: 개명이 도달층 상승에 묶여 있으면, 최전선에
            // 머무는 사람의 새 이름은 랭킹표에 영영 안 닿는다
            Assert.IsTrue(LeaderboardPolicy.ShouldSubmit(170, 170, true, true));
        }

        [Test]
        public void RenamingDoesNotPunchThroughTheRetreatGuard()
        {
            // 이름을 바꿨다고 낮은 도달층이 올라가서는 안 된다. 규칙도 막지만
            // 막히는 쓰기를 보내는 것은 요금만 쓴다
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(5, 170, true, true));
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(
                StageProgress.ReachSanityCap + 1, StageProgress.ReachSanityCap + 1, true, true));
        }

        [Test]
        public void SubmittableRange_MatchesTheSaveSanityCap()
        {
            Assert.IsFalse(LeaderboardPolicy.IsSubmittable(0), "0층은 존재하지 않는다");
            Assert.IsFalse(LeaderboardPolicy.IsSubmittable(-1));
            Assert.IsTrue(LeaderboardPolicy.IsSubmittable(1));
            Assert.IsTrue(LeaderboardPolicy.IsSubmittable(StageProgress.ReachSanityCap));
            Assert.IsFalse(LeaderboardPolicy.IsSubmittable(StageProgress.ReachSanityCap + 1),
                "새니티 캡 위의 값이 제출 가능하면 변조 세이브가 랭킹 1위를 산다");
        }

        [Test]
        public void OutOfRangeReach_IsNotSubmittedEvenWhenHigher()
        {
            // 범위 검사가 비교보다 **먼저**여야 한다. 높기만 하면 보낸다면
            // 변조 값(99만)이 정확히 그 조건을 만족한다
            Assert.IsFalse(LeaderboardPolicy.ShouldSubmit(
                StageProgress.ReachSanityCap + 1, 170, true));
        }

        [Test]
        public void DebounceWindow_IsNeitherZeroNorUnbearable()
        {
            // 정확한 값이 계약인 자리가 아니다. 0이면 층마다 쓰기가 나가고,
            // 너무 길면 앱을 껐다 켜야 랭킹이 갱신된 것처럼 보인다
            Assert.Greater(LeaderboardPolicy.DebounceSeconds, 0f);
            Assert.LessOrEqual(LeaderboardPolicy.DebounceSeconds, 60f);
        }

        // ---------------------------------------------------------------- 이름

        [Test]
        public void UnnamedPlayer_StillHasSomethingToShow()
        {
            Assert.IsFalse(PlayerProfile.HasChosenName);
            Assert.IsFalse(string.IsNullOrEmpty(PlayerProfile.Name),
                "이름 칸이 비면 랭킹 한 줄이 '불러오다 만 줄'로 읽힌다");
            Assert.AreEqual(string.Empty, PlayerProfile.Collect(),
                "안 정한 이름이 세이브에 굳으면 첫 진입 안내를 다시 띄울 근거가 사라진다");
        }

        [Test]
        public void ChosenName_IsWhatBothScreenAndServerSee()
        {
            Assert.IsTrue(PlayerProfile.SetName("랑"));
            Assert.IsTrue(PlayerProfile.HasChosenName);
            Assert.AreEqual("랑", PlayerProfile.Name);
            Assert.AreEqual("랑", PlayerProfile.Collect());
        }

        [Test]
        public void BlankName_IsRejected()
        {
            Assert.IsFalse(PlayerProfile.SetName(null));
            Assert.IsFalse(PlayerProfile.SetName(string.Empty));
            Assert.IsFalse(PlayerProfile.SetName("   "));
            Assert.IsFalse(PlayerProfile.HasChosenName);
        }

        [Test]
        public void NameIsTrimmedAndCapped()
        {
            Assert.AreEqual("무사", PlayerProfile.Sanitize("  무사  "));

            string tooLong = new string('가', PlayerProfile.MaxLength + 8);
            Assert.AreEqual(PlayerProfile.MaxLength, PlayerProfile.Sanitize(tooLong).Length,
                "상한을 넘는 이름이 랭킹표의 옆 칸을 민다");
        }

        [Test]
        public void ControlCharacters_NeverSurvive()
        {
            // 줄바꿈 하나가 **남의 화면에서** 한 줄을 두 줄로 만든다
            Assert.AreEqual("가나", PlayerProfile.Sanitize("가\n나"));
            Assert.AreEqual("가나", PlayerProfile.Sanitize("가\t나"));
            Assert.AreEqual("가나", PlayerProfile.Sanitize("\r\n가나\r\n"));
        }

        [Test]
        public void CuttingLongName_DoesNotLeaveATrailingSpace()
        {
            // 열두 번째 글자가 공백인 입력. 자른 뒤 다시 다듬지 않으면 이름이
            // "...다 "로 저장되고, 그 공백은 화면에서 안 보이는 채로 서버에 간다
            string raw = "가나다라마바사아자차카 타";
            string clean = PlayerProfile.Sanitize(raw);
            Assert.AreEqual(clean.Trim(), clean);
        }

        [Test]
        public void RestoreDoesNotAnnounceAChange()
        {
            // 복원은 변경이 아니다. 여기서 신호가 나가면 세이브를 불러오는
            // 것만으로 "이름이 바뀌었습니다"가 뜬다
            int calls = 0;
            System.Action handler = () => calls++;

            PlayerProfile.Changed += handler;
            try
            {
                PlayerProfile.Restore("랑");
                Assert.AreEqual(0, calls);

                PlayerProfile.SetName("다른이름");
                Assert.AreEqual(1, calls);
            }
            finally
            {
                PlayerProfile.Changed -= handler;
            }
        }

        // ---------------------------------------------------------------- 규칙 일치

        /**
         * @brief 클라의 상한과 보안 규칙의 상한이 같은 값인지.
         *
         * 두 곳에 적힌 숫자는 언젠가 갈린다. 갈렸을 때의 증상이 특히 나쁘다 -
         * 로컬에서는 통과한 제출이 서버에서 PERMISSION_DENIED로 떨어지고,
         * 그 실패는 조용하다(랭킹은 곁다리라 게임이 아무 말도 안 한다).
         *
         * 파일을 문자열로 읽어 확인한다. 규칙을 파싱할 방법이 EditMode에 없고,
         * 여기서 필요한 것은 "그 숫자가 그 파일에 있는가"뿐이다.
         */
        [Test]
        public void SecurityRules_UseTheSameLimitsAsTheClient()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "firestore.rules");

            Assert.IsTrue(File.Exists(path), "firestore.rules가 저장소 루트에 없다: " + path);

            string rules = File.ReadAllText(path);

            StringAssert.Contains(StageProgress.ReachSanityCap.ToString(), rules,
                "규칙의 도달층 상한이 ReachSanityCap과 다르다");
            StringAssert.Contains("size() <= " + PlayerProfile.MaxLength, rules,
                "규칙의 이름 길이 상한이 PlayerProfile.MaxLength와 다르다");

            // 4-A(전면 개방)로 되돌아가지 않았는지. 그 규칙은 스파이크
            // 기간에만 유효했고, 남아 있으면 누구나 아무 문서나 쓴다
            StringAssert.DoesNotContain("allow read, write: if request.time", rules,
                "테스트 모드 규칙(4-A)이 아직 살아 있다");
        }

        [Test]
        public void SecurityRules_RefuseToLetTheRecordGoDown()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "firestore.rules");
            string rules = File.ReadAllText(path);

            // 단조 검사가 규칙에서 빠지면, 클라 검사(LeaderboardPolicy)를
            // 지나지 않는 조작 클라 앞에서 후퇴 방어가 통째로 사라진다
            StringAssert.Contains("request.resource.data.maxStage >= resource.data.maxStage", rules,
                "규칙에 단조 검사가 없다 - 낮은 값이 기록을 덮을 수 있다");
        }

        /** 복합 정렬(도달층 내림차 + 시각 오름차)에 필요한 인덱스가 선언돼 있는지 */
        [Test]
        public void CompositeIndex_IsDeclaredForTheRankingQuery()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "firestore.indexes.json");

            Assert.IsTrue(File.Exists(path), "firestore.indexes.json이 없다");

            string json = File.ReadAllText(path);
            StringAssert.Contains("\"" + CloudScores.Collection + "\"", json);
            StringAssert.Contains(CloudScores.StageField, json);
            StringAssert.Contains(CloudScores.UpdatedField, json);
            StringAssert.Contains("DESCENDING", json,
                "도달층 내림차순이 없으면 랭킹이 꼴찌부터 나온다");
        }

        // ---------------------------------------------------------------- 문구

        /**
         * @brief 런타임에만 나오는 문구의 글자가 아틀라스에 있는가.
         *
         * 빌드 검사(VerifyGlyphCoverage)는 **씬에 적혀 있는** 라벨만 훑는다.
         * 이 화면의 문구 대부분은 코드가 상황에 따라 넣는 것이라 씬에 없고,
         * 그래서 그 검사에 안 잡힌다 - 실기에서 "잠시 □ 랭킹에 반영됩니다"로
         * 처음 드러났다(문구를 고치면서 UIStrings를 안 고쳤다).
         *
         * 플레이어 이름은 여기 없다. 그쪽은 동적 폰트가 맡는다(정적 아틀라스로
         * 덮을 수 없는 유일한 자리, PixelFontAssetBuilder.BuildNameFont).
         */
        [Test]
        public void RuntimeMessages_AreInTheBakedCharset()
        {
            string charsetPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Assets/_Project/Data/FontCharset.txt");

            Assert.IsTrue(File.Exists(charsetPath), "FontCharset.txt가 없다: " + charsetPath);
            string charset = File.ReadAllText(charsetPath);

            string[] messages =
            {
                "불러오는 중...",
                "불러올 수 없습니다 - 연결을 확인하고 다시 시도하세요",
                "아직 아무도 오르지 않았습니다",
                "이름을 한 글자 이상 입력하세요",
                "이름이 바뀌었습니다 - 잠시 뒤 랭킹에 반영됩니다",
                "내 순위  집계 중...",
                "내 순위  1,234위   (171층)",
                PlayerProfile.DefaultName,
            };

            foreach (var message in messages)
            {
                foreach (char c in message)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    Assert.IsTrue(charset.IndexOf(c) >= 0, string.Format(
                        "'{0}'이 아틀라스에 없다 (문구 \"{1}\") - "
                        + "UIStrings.txt에 적고 Onikiri/Art/Build Pixel Font Assets를 실행할 것",
                        c, message));
                }
            }
        }

        // ---------------------------------------------------------------- 세이브

        [Test]
        public void V18_MigratesToV19WithoutInventingAName()
        {
            var data = SaveData.NewGame();
            data.version = 18;
            data.stage = 40;
            data.maxStageReached = 170;
            data.playerName = null;

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(20, SaveData.CurrentVersion, "버전이 또 올랐으면 이 테스트도 함께 봐야 한다");

            Assert.AreEqual(string.Empty, data.playerName,
                "마이그레이션이 이름을 지어냈다 - 옛 플레이어 전원이 '이름을 고른 사람'이 된다");

            // 진행은 한 글자도 안 바뀐다. 리더보드는 도달층을 **읽기만** 한다
            Assert.AreEqual(40, data.stage);
            Assert.AreEqual(170, data.maxStageReached);
        }

        [Test]
        public void V19_MigrationIsIdempotent()
        {
            var data = SaveData.NewGame();
            data.version = 18;
            data.maxStageReached = 170;

            SaveData.Migrate(data);
            PlayerProfile.Restore(data.playerName);
            PlayerProfile.SetName("랑");
            data.playerName = PlayerProfile.Collect();

            Assert.IsTrue(SaveData.Migrate(data));
            Assert.AreEqual("랑", data.playerName,
                "두 번째 마이그레이션이 정한 이름을 지웠다");
        }

        [Test]
        public void NewGame_HasNoName()
        {
            var data = SaveData.NewGame();
            Assert.AreEqual(string.Empty, data.playerName ?? string.Empty);
        }
    }
}
