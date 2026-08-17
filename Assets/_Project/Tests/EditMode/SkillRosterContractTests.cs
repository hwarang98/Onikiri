using System;
using System.Collections.Generic;
using NUnit.Framework;
using Onikiri.Progression;

namespace Onikiri.Tests
{
    /**
     * @brief 15종 로스터 재설계(v2.2)의 **계약 선행 검사**. 구현 1단계의 전부다.
     *
     * ## 왜 코드보다 먼저 테스트가 서는가
     *
     * 이 재설계에서 가장 위험한 자리가 3단계의 거동 구조 변경이다. `SkillShape`
     * 하나를 `SkillArea`/`SkillSplit`/`SkillSpecial` 셋으로 쪼개면서 데미지 분배
     * 코드가 통째로 다시 써지는데, 그 변경이 **총량을 한 톨이라도 움직이면**
     * 45단계의 오의 몫 계약(49.0% / 한계 50%)이 조용히 깨진다. 조용히 깨지는
     * 이유는 밴드가 그 차이를 스테이지 수십 개 뒤에야 드러내기 때문이다.
     *
     * 그래서 **바꾸기 전에 지금 값을 굽는다.** 아래 골든 표는 리팩터 전의
     * `HitDamageShare` 출력을 IEEE754 비트 그대로 담고 있고, 리팩터가 끝난 뒤에도
     * 같은 비트가 나와야 한다. 근사 비교(허용 오차)로 두지 않은 이유는 이 검사가
     * 재는 것이 "비슷한가"가 아니라 **"안 건드렸는가"**이기 때문이다.
     *
     * ## 세 검사의 성격이 다르다
     *
     *     TheTwoPools_...        아직 없는 것을 요구한다  -> **레드**  (5단계에 그린)
     *     TheShapeRefactor_...   지금 있는 것을 잠근다     -> 그린 (계속 그린이어야 한다)
     *     EverySkillIcon_...     지금 있는 것을 잠근다     -> 그린 (4단계가 밟을 자리)
     *
     * 뒤의 둘을 레드로 심지 않는 것이 요점이다. 특성화 검사(characterization test)는
     * **처음부터 그린이어야 뜻이 있다** - 지금 값을 못 잡으면 나중 값과 비교할
     * 기준이 없다. 레드로 시작해야 하는 것은 아직 존재하지 않는 계약뿐이다.
     */
    public class SkillRosterContractTests
    {
        #region 로스터 - 아직 없는 계약 (레드)

        /**
         * @brief **뽑기 몫이 아홉이고 로스터가 열다섯인가.** 5단계까지 레드다.
         *
         * 설계 문서 v2.2 §2.1·§3.2가 요구하는 최종 형태다. 지금은 로스터가 여덟이고
         * 뽑기 몫이 둘(혈폭·혈조)이라 이 검사가 실패하고, **실패하는 것이 맞다** -
         * 이 줄이 그린이 되는 날이 곧 두 해금 풀이 실제로 선 날이다.
         *
         * 두 배열(StandardUnlockOrder 5종 / OniSecretUnlockOrder 4종)의 교집합이
         * 비었는지까지는 여기서 못 잰다. 그 배열이 아직 없어서 참조하면 컴파일이
         * 깨지고, 컴파일이 깨지면 **어셈블리의 나머지 검사 전부가 안 돈다** -
         * 레드 하나를 얻으려고 그린 수백 개를 잃는 거래다. 배열이 생기는 5단계에
         * 이 검사에 교집합 단언을 더한다.
         *
         * `Category`를 붙여 두는 이유는 CI가 "아직 구현 안 된 계약"과 "회귀"를
         * 갈라 볼 수 있게 하기 위해서다. 이 범주가 비는 날이 재설계가 끝난 날이다.
         */
        [Test]
        [Category("PendingRoster")]
        public void TheTwoPools_AreDisjointAndCoverTheNineGachaSkills()
        {
            int gachaGated = 0;
            for (int i = 0; i < SkillCatalog.Count; i++)
                if (SkillCatalog.Skills[i].GachaGated) gachaGated++;

            Assert.AreEqual(15, SkillCatalog.Count, string.Format(
                "로스터가 {0}종이다 - v2.2 §2.1이 요구하는 것은 검식 5 · 혈식 5 · "
                + "귀오의 5 = 15종이다", SkillCatalog.Count));

            Assert.AreEqual(9, gachaGated, string.Format(
                "뽑기 몫이 {0}종이다 - 표준 해금 풀 5종 + 귀오의 해금 풀 4종 = 9종이어야 "
                + "두 풀이 설 수 있다 (v2.2 §3.2)", gachaGated));
        }

        #endregion

        #region 거동 리팩터 안전선 - 지금 값을 잠근다 (그린)

        /**
         * @brief 기존 여덟의 데미지 분배가 **비트 하나까지 그대로인가.**
         *
         * 3단계가 `SkillShape` 하나를 세 필드로 쪼갤 때 이 검사가 그린으로 남아야
         * 한다. 아래 표는 리팩터 **전**에 구운 값이고, 그래서 이 검사는 리팩터가
         * 무엇을 하든 상관하지 않는다 - 결과만 본다.
         *
         * ## 왜 허용 오차가 아니라 비트인가
         *
         * `HitDamageShare`의 마지막 타격은 나눗셈의 나머지를 받는다
         * (`total - share * (hits - 1)`). 그 뺄셈이 앞선 타격들과 **1~2 ULP 다른**
         * 값을 내는 것이 정상이고, 그 미세한 차이가 곧 "합이 정확히 원본"을
         * 성립시키는 장치다. 허용 오차로 재면 그 장치가 사라져도 검사가 통과한다 -
         * 즉 이 검사가 지키려는 바로 그것을 못 지킨다.
         *
         * ## 인덱스가 아니라 id로 찾는 이유
         *
         * 4단계가 카탈로그에 일곱 행을 더한다. 신규 오의가 표 중간에 들어가면
         * 인덱스가 밀리는데, 그때 이 표가 엉뚱한 오의를 검사하면 **에러가 아니라
         * 조용한 통과**로 나타난다. 세이브가 id를 쓰는 이유와 같은 자리다.
         */
        [Test]
        public void TheShapeRefactor_IsBitIdenticalForTheExistingEight()
        {
            for (int row = 0; row < GoldenIds.Length; row++)
            {
                string id = GoldenIds[row];
                int index = SkillCatalog.IndexOf(id);

                Assert.GreaterOrEqual(index, 0,
                    "'" + id + "'가 카탈로그에서 사라졌다 - 기존 id는 변경 금지다 (v2.2 §2.1)");

                int hits = SkillCatalog.HitsPerCast(index);
                Assert.AreEqual(GoldenHitsPerCast[row], hits, string.Format(
                    "'{0}'의 타격 수가 {1}에서 {2}로 바뀌었다 - 거동 리팩터가 총량 분배를 "
                    + "건드렸다", id, GoldenHitsPerCast[row], hits));
            }

            // 골든 표는 (스킬 -> 타격 -> 탐침) 순서로 평탄화돼 있다
            int cursor = 0;
            for (int row = 0; row < GoldenIds.Length; row++)
            {
                int index = SkillCatalog.IndexOf(GoldenIds[row]);

                for (int hit = 0; hit < GoldenHitsPerCast[row]; hit++)
                {
                    for (int p = 0; p < Probes.Length; p++, cursor++)
                    {
                        double actual = SkillCatalog.HitDamageShare(index, hit, Probes[p]);
                        long bits = BitConverter.DoubleToInt64Bits(actual);

                        Assert.AreEqual(GoldenShareBits[cursor], bits, string.Format(
                            "'{0}' {1}번째 타격, 총 배율 x{2}: {3:R} 이 나왔는데 리팩터 전에는 "
                            + "{4:R} 였다. 총 데미지 불변이 깨졌다",
                            GoldenIds[row], hit, Probes[p], actual,
                            BitConverter.Int64BitsToDouble(GoldenShareBits[cursor])));
                    }
                }
            }

            Assert.AreEqual(GoldenShareBits.Length, cursor,
                "골든 표의 길이와 실제로 훑은 칸 수가 다르다 - 표가 깨졌다");
        }

        /**
         * @brief 분배된 타격의 **합이 정확히 원본 배율인가.** 위 검사의 짝이다.
         *
         * 골든 표가 "값이 안 바뀌었다"를 재는 동안 이쪽은 "그 값들이 옳다"를 잰다.
         * 둘 다 있어야 하는 이유는 골든이 **틀린 값도 그대로 잠글 수 있기** 때문이다.
         *
         * 비교가 비트 단위인 것은 `HitDamageShare` 머리 주석이 약속한 것이 정확히
         * 그것이기 때문이다 - "나눗셈을 한 번만 하고 마지막에서 빼면 합이 **정확히**
         * 원래 배율이다".
         */
        [Test]
        public void EveryHitSplit_SumsBackToTheOriginalMultiplier()
        {
            for (int row = 0; row < GoldenIds.Length; row++)
            {
                int index = SkillCatalog.IndexOf(GoldenIds[row]);
                int hits = SkillCatalog.HitsPerCast(index);

                foreach (double total in Probes)
                {
                    double sum = 0d;
                    for (int hit = 0; hit < hits; hit++)
                        sum += SkillCatalog.HitDamageShare(index, hit, total);

                    Assert.AreEqual(BitConverter.DoubleToInt64Bits(total),
                                    BitConverter.DoubleToInt64Bits(sum), string.Format(
                        "'{0}'의 {1}타 합이 {2:R}인데 원본은 {3:R}이다 - 마지막 타격이 "
                        + "나머지를 안 받고 있다", GoldenIds[row], hits, sum, total));
                }
            }
        }

        #endregion

        #region 아이콘 - 4단계가 밟을 자리 (그린)

        /**
         * @brief 아이콘이 오의마다 유일한가. **4단계에 일곱이 들어올 자리다.**
         *
         * 지금은 여덟이 전부 다른 파일을 써서 그린이다. 이 검사를 미리 심는 이유는
         * 4단계에 반입할 일곱 개가 **리포에 남는 아이콘이 0개인 상태**에서 오기
         * 때문이다(설계 문서 §10.2 - 열여섯 개 중 여덟은 오의, 여덟은 강화 축이
         * 이미 쓴다). 모자란 자리를 기존 파일로 메우고 싶어지는 유혹이 실재하고,
         * 그렇게 메우면 목록에서 두 오의가 같은 그림으로 읽힌다.
         *
         * VfxId 유일성 검사(SkillShapeTests.EverySkillEffect_BelongsToExactlyOneSkill)와
         * 같은 자리, 같은 이유다. 다른 점은 **빈 값을 봐주지 않는다**는 것 -
         * 아이콘이 없는 오의는 목록에서 빈 칸으로 뜨므로 그것도 결함이다.
         */
        [Test]
        public void EverySkillIcon_BelongsToExactlyOneSkill()
        {
            var seen = new Dictionary<string, string>();

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];

                Assert.IsFalse(string.IsNullOrEmpty(spec.IconFile),
                    "'" + spec.DisplayName + "'에 아이콘이 없다 - 목록에서 빈 칸으로 뜬다");

                string owner;
                if (seen.TryGetValue(spec.IconFile, out owner))
                {
                    Assert.Fail(string.Format(
                        "'{0}'과 '{1}'이 같은 아이콘 '{2}'를 쓴다 - 목록에서 두 오의가 "
                        + "같은 그림으로 읽힌다", owner, spec.DisplayName, spec.IconFile));
                }

                seen[spec.IconFile] = spec.DisplayName;
            }
        }

        #endregion

        #region 계열 - 2단계

        /**
         * @brief 모든 오의가 **자기 계열을 명시하는가.** 표 하나가 셋을 다 채운다.
         *
         * `SkillSpec.Family`는 기본값이 SwordForm(0)이라 **필드를 빠뜨린 행이
         * 조용히 검식이 된다.** 열거형의 첫 칸이 기본값인 것은 C#의 사정이지
         * 설계의 뜻이 아니고, 그래서 빠뜨린 것과 의도한 것을 값만으로는 못 가른다.
         *
         * 그 함정을 여기서 막는다 - 세 계열이 전부 하나 이상을 갖고 있으면 적어도
         * "표 전체가 기본값으로 남은" 상태는 아니다. 계열별 정확한 수는 로스터가
         * 열다섯이 되는 4단계에 잰다(아래 EveryFamily_HoldsExactlyFiveSkills).
         */
        [Test]
        public void EverySkill_DeclaresAFamilyThatExists()
        {
            int familyCount = Enum.GetValues(typeof(SkillFamily)).Length;

            Assert.AreEqual(familyCount, SkillCatalog.FamilyNames.Length,
                "계열 수와 주 표기 수가 다르다 - 탭 하나가 이름 없이 선다");
            Assert.AreEqual(familyCount, SkillCatalog.FamilySubtitles.Length,
                "계열 수와 부제 수가 다르다");

            for (int i = 0; i < SkillCatalog.Count; i++)
            {
                var spec = SkillCatalog.Skills[i];
                Assert.IsTrue(Enum.IsDefined(typeof(SkillFamily), spec.Family),
                    "'" + spec.DisplayName + "'의 계열이 정의되지 않은 값이다");
            }

            foreach (SkillFamily family in Enum.GetValues(typeof(SkillFamily)))
            {
                Assert.Greater(SkillCatalog.CountOf(family), 0, string.Format(
                    "'{0}' 계열이 비었다 - 탭을 눌러도 빈 목록이 뜬다. 표에서 Family를 "
                    + "빠뜨린 행이 있는지 보라 (기본값이 SwordForm이라 조용히 쏠린다)",
                    SkillCatalog.NameOf(family)));
            }
        }

        /**
         * @brief 계열 표기가 서로 갈리는가. 탭 셋이 같은 낱말을 쓰면 자가 사라진다.
         *
         * 부제(일반·상급·각성)가 뽑기 희귀도의 낱말과 **"일반" 하나만 겹치는** 것도
         * 여기서 함께 잰다. 그 하나는 자리로 가른다 - 뽑기 결과 화면은 등급의 낱말만,
         * 스킬 목록은 계열의 낱말만 쓴다. 두 낱말이 더 겹치기 시작하면 그 규칙으로도
         * 못 가르므로, 이 검사가 그 경계를 지킨다.
         */
        [Test]
        public void TheFamilyLabels_AreDistinct()
        {
            var seen = new HashSet<string>();

            foreach (var name in SkillCatalog.FamilyNames)
            {
                Assert.IsFalse(string.IsNullOrEmpty(name), "계열 주 표기가 비었다");
                Assert.IsTrue(seen.Add(name), "계열 주 표기 '" + name + "'이 두 번 쓰인다");
            }

            seen.Clear();
            foreach (var subtitle in SkillCatalog.FamilySubtitles)
            {
                Assert.IsFalse(string.IsNullOrEmpty(subtitle), "계열 부제가 비었다");
                Assert.IsTrue(seen.Add(subtitle), "계열 부제 '" + subtitle + "'이 두 번 쓰인다");
            }

            // 희귀도 낱말과 겹치는 것은 "일반" 하나여야 한다
            var grades = new HashSet<string> { "일반", "고급", "희귀", "영웅", "전설" };
            int overlap = 0;
            foreach (var subtitle in SkillCatalog.FamilySubtitles)
                if (grades.Contains(subtitle)) overlap++;

            Assert.AreEqual(1, overlap, string.Format(
                "계열 부제가 희귀도 낱말과 {0}개 겹친다 - 하나(일반)까지는 자리로 가르지만 "
                + "둘부터는 화면에서 두 축이 같은 것으로 읽힌다", overlap));
        }

        /**
         * @brief 계열 표기의 모든 글자가 **아틀라스에 구워져 있는가.**
         *
         * 이 프로젝트의 폰트는 완성형 11,172자가 아니라 실제로 쓰는 것만 굽는다
         * (FontCharsetBuilder). 그래서 새 낱말을 화면에 올리는 것은 문자열을 적는
         * 일이 아니라 **아틀라스를 다시 굽는 일**이다.
         *
         * `식`이 정확히 그 자리다. 2026-08-14 시점 FontCharset.txt에 없었고, 오의
         * 이름 어디에도 없어서 아무도 대신 데려오지 못한다 - "검식/혈식"이 화면에서
         * "검□/혈□"이 된다.
         *
         * 그래서 이 검사는 2단계에서 **먼저 레드로 뜨고**, 하베스트에 계열 표를
         * 물린 뒤 `Rebuild Font Charset` -> `Build Pixel Font Assets`를 돌리면
         * 그린이 된다. 순서를 강제하는 것이 목적이다.
         */
        [Test]
        public void TheFamilyLabels_AreBakedIntoTheFontAtlas()
        {
            string path = UnityEngine.Application.dataPath + "/_Project/Data/FontCharset.txt";
            Assert.IsTrue(System.IO.File.Exists(path), "문자셋 파일이 없다: " + path);

            string charset = System.IO.File.ReadAllText(path)
                                       .Replace("\r", string.Empty)
                                       .Replace("\n", string.Empty);

            var missing = new SortedSet<char>();
            foreach (var label in Labels())
                foreach (char c in label)
                    if (charset.IndexOf(c) < 0) missing.Add(c);

            Assert.IsEmpty(missing, string.Format(
                "계열 표기의 글자 [{0}]가 아틀라스에 없다 - 탭이 □로 뜬다. "
                + "FontCharsetBuilder에 계열 표를 물린 뒤 'Rebuild Font Charset' -> "
                + "'Build Pixel Font Assets'를 돌려라",
                string.Join(",", new List<char>(missing).ConvertAll(c => c.ToString()).ToArray())));
        }

        private static IEnumerable<string> Labels()
        {
            foreach (var name in SkillCatalog.FamilyNames) yield return name;
            foreach (var subtitle in SkillCatalog.FamilySubtitles) yield return subtitle;
        }

        /**
         * @brief 계열마다 정확히 다섯인가. **4단계까지 레드다.**
         *
         * 설계 문서 v2.2 §2.1의 최종 형태 - 검식 5 · 혈식 5 · 귀오의 5. 지금은
         * 검식 2 · 혈식 5 · 귀오의 1이라 실패하고, 실패하는 것이 맞다.
         *
         * 이 균형이 뜻을 갖는 이유는 탭 하나가 한 화면에 들어가야 하기 때문이다.
         * 한 계열이 여덟이 되면 그 탭만 스크롤이 생기고, 그러면 세 탭이 같은
         * 종류의 화면이 아니게 된다.
         */
        [Test]
        [Category("PendingRoster")]
        public void EveryFamily_HoldsExactlyFiveSkills()
        {
            foreach (SkillFamily family in Enum.GetValues(typeof(SkillFamily)))
            {
                Assert.AreEqual(5, SkillCatalog.CountOf(family), string.Format(
                    "'{0}' 계열이 {1}종이다 - v2.2 §2.1은 계열마다 다섯을 요구한다",
                    SkillCatalog.NameOf(family), SkillCatalog.CountOf(family)));
            }
        }

        #endregion

        #region 골든 표 (리팩터 전에 구운 값)

        /**
         * @brief 검사할 오의. **표 순서가 아니라 id 목록이다** - 위 검사 주석 참고.
         *
         * 4단계가 일곱을 더해도 이 목록은 안 늘어난다. 신규 오의의 분배는
         * "리팩터 전"이라는 것이 없으므로 골든이 아니라 총량 계약 검사
         * (검진 4틱 합 x1.26 · 귀신난무 5타 합 x2.16)가 맡는다.
         */
        private static readonly string[] GoldenIds =
        {
            SkillCatalog.ChainSlashId,   // 연참   MultiHit 3
            SkillCatalog.FlashId,        // 일섬   Pierce
            SkillCatalog.OniCleaveId,    // 귀참   Screen
            SkillCatalog.BloodWaveId,    // 혈파동 Screen
            SkillCatalog.BloodFallId,    // 낙혈   Pierce
            SkillCatalog.BloodWheelId,   // 혈륜   MultiHit 5
            SkillCatalog.BloodBurstId,   // 혈폭   MultiHit 1
            SkillCatalog.BloodWhipId     // 혈조   Pierce
        };

        private static readonly int[] GoldenHitsPerCast = { 3, 1, 1, 1, 1, 5, 1, 1 };

        /**
         * @brief 탐침 배율. **나눗셈이 떨어지는 값과 안 떨어지는 값을 섞는다.**
         *
         * 1.0을 셋으로 나누면 순환소수가 되고 그때 나머지 규칙이 실제로 일한다.
         * 0.54·1.26·2.16·3.52는 실제 오의 배율이라 현장의 값을 밟고,
         * 100/3은 이미 순환하는 값을 다시 나눠 오차가 겹치는 자리를 밟는다.
         */
        private static readonly double[] Probes = { 1.0d, 0.54d, 1.26d, 2.16d, 3.52d, 100.0d / 3.0d };

        /**
         * @brief `HitDamageShare`의 출력을 IEEE754 비트로 담은 표.
         *
         * 배치는 (스킬 -> 타격 -> 탐침)이고, 한 줄이 한 타격의 여섯 탐침이다.
         * 마지막 타격 줄만 앞선 줄과 1~2 ULP 다른데, 그것이 나머지 규칙의 지문이다.
         */
        private static readonly long[] GoldenShareBits =
        {
            4599676419421066581L, 4595653203753948939L, 4601237667291888353L, 4604660403008689931L, 4607963042735428294L, 4622444617537217423L,   // 연참 [0]
            4599676419421066581L, 4595653203753948939L, 4601237667291888353L, 4604660403008689931L, 4607963042735428294L, 4622444617537217423L,   // 연참 [1]
            4599676419421066582L, 4595653203753948938L, 4601237667291888354L, 4604660403008689930L, 4607963042735428294L, 4622444617537217422L,   // 연참 [2] 나머지
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L,   // 일섬 [0]
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L,   // 귀참 [0]
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L,   // 혈파동 [0]
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L,   // 낙혈 [0]
            4596373779694328218L, 4592446640819261146L, 4598211248342295380L, 4601453840074002138L, 4604516287820614074L, 4619192017806338731L,   // 혈륜 [0]
            4596373779694328218L, 4592446640819261146L, 4598211248342295380L, 4601453840074002138L, 4604516287820614074L, 4619192017806338731L,   // 혈륜 [1]
            4596373779694328218L, 4592446640819261146L, 4598211248342295380L, 4601453840074002138L, 4604516287820614074L, 4619192017806338731L,   // 혈륜 [2]
            4596373779694328218L, 4592446640819261146L, 4598211248342295380L, 4601453840074002138L, 4604516287820614074L, 4619192017806338731L,   // 혈륜 [3]
            4596373779694328216L, 4592446640819261144L, 4598211248342295380L, 4601453840074002136L, 4604516287820614076L, 4619192017806338732L,   // 혈륜 [4] 나머지
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L,   // 혈폭 [0]
            4607182418800017408L, 4603039107142836552L, 4608353354703133737L, 4612046306397577544L, 4615108754144189481L, 4629888066921343659L    // 혈조 [0]
        };

        #endregion
    }
}
