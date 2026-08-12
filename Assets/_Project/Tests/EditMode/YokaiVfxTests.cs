using NUnit.Framework;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.Tests
{
    /**
     * @brief 요괴 팩에서 뜯어낸 이펙트 라이브러리.
     *
     * ## 무엇을 걸 수 있는가
     *
     * 이 스텝의 결과물은 그림이라 "예쁜가"는 테스트가 못 본다. 대신 **그림이
     * 아닌 부분**은 전부 걸 수 있다: 조각이 다 뜯혔는가, 프레임이 들어 있는가,
     * 배율이 픽셀 격자 규칙을 지키는가, 길이가 보스 공격 주기 안에 들어가는가.
     *
     * 마지막 것이 이 파일의 존재 이유에 가깝다. 참격이 주기보다 길면 이전
     * 참격이 사라지기 전에 다음 것이 뜨고, 화면에는 붉은 것이 계속 떠 있게 되어
     * **"지금 친다"는 신호가 아니라 배경**이 된다.
     */
    public class YokaiVfxTests
    {
        /**
         * 경로와 조각 이름을 여기 다시 적는다. 테스트 어셈블리는 에디터 스크립트
         * (`Assembly-CSharp-Editor`)를 참조할 수 없어서 `YokaiVfxBaker`의 상수를
         * 가져다 쓸 수가 없다 - asmdef는 미리 정의된 어셈블리를 참조하지 못한다.
         *
         * 사본이지만 나쁜 사본은 아니다. 빌더가 이름을 바꾸면 여기서 걸리는 것이
         * 맞고, `BossRosterTests`도 로스터 경로를 같은 이유로 다시 적는다.
         */
        const string LibraryPath = "Assets/_Project/Data/VfxLibrary_Yokai.asset";

        static readonly string[] ExpectedIds = { "crescent", "whip", "wave" };

        static VfxLibrary Load()
        {
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            Assert.IsNotNull(library, "참격 라이브러리가 없다: " + LibraryPath
                             + " (Onikiri/Art/Harvest Yokai VFX)");
            return library;
        }

        /** 굽기 표에 적힌 조각이 전부 라이브러리에 들어왔는가 */
        [Test]
        public void EveryHarvestedClip_IsInTheLibrary()
        {
            var library = Load();

            foreach (var id in ExpectedIds)
            {
                var clip = library.Find(id);
                Assert.IsNotNull(clip, "라이브러리에 '" + id + "' 조각이 없다");
                Assert.Greater(clip.frames != null ? clip.frames.Length : 0, 0,
                    "'" + id + "' 조각에 프레임이 없다 - 시트를 자르는 데 실패했다");
            }

            Assert.AreEqual(ExpectedIds.Length, library.Count,
                "굽기 표와 라이브러리의 조각 수가 다르다");
        }

        /**
         * @brief 배율은 정수만. 11단계 픽셀 격자 규칙이다.
         *
         * Pixel Perfect 카메라가 아트 픽셀 하나를 화면 픽셀 N개로 늘리는데,
         * 소수 배율이 곱해지면 어떤 픽셀은 7개 어떤 픽셀은 8개로 그려진다.
         */
        [Test]
        public void EveryClip_UsesAnIntegerScale()
        {
            var library = Load();

            for (int i = 0; i < library.Count; i++)
            {
                var clip = library.At(i);
                Assert.IsNotNull(clip);
                Assert.Greater(clip.scale, 0f, clip.id + ": 배율이 0 이하다");
                Assert.AreEqual(Mathf.Round(clip.scale), clip.scale,
                    clip.id + ": 배율이 정수가 아니다 - 픽셀 격자가 일그러진다");
            }
        }

        /**
         * @brief 참격 한 번이 보스의 공격 주기 안에서 끝나는가.
         *
         * 넘으면 이전 참격이 살아 있는 채로 다음 것이 뜬다. 풀이 늘어나는 것도
         * 문제지만 그보다 화면이 문제다 - 붉은 것이 끊기지 않으면 그것은 예고가
         * 아니라 배경이다.
         */
        [Test]
        public void EveryClip_FitsInsideTheBossAttackInterval()
        {
            var library = Load();
            float interval = (float)BossCurve.AttackIntervalSeconds;

            for (int i = 0; i < library.Count; i++)
            {
                var clip = library.At(i);
                Assert.Greater(clip.frameRate, 0f, clip.id + ": 재생 속도가 0이다");
                Assert.Less(clip.Seconds, interval, string.Format(
                    "{0}: 참격 {1:F2}초가 공격 주기 {2:F2}초보다 길다",
                    clip.id, clip.Seconds, interval));
            }
        }

        // ------------------------------------------------ 이 참격은 누구의 것인가

        const string BossFolder = "Assets/_Project/Data/Bosses";

        /**
         * 요괴(Inimig 9)의 시트를 구워 쓰는 보스. 파일명과 화면 이름이 서로
         * 엇갈려 있어서(BossContentBuilder 45~48줄) 파일명으로 짚는다 -
         * `Boss_RedEyeYokai`의 화면 이름이 "다크 사무라이"다.
         */
        const string YokaiBossAsset = "Boss_RedEyeYokai";

        static bool IsYokaiId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var known in ExpectedIds) if (known == id) return true;
            return false;
        }

        /**
         * @brief 요괴의 서명이 **다른 보스로 새지 않는다.**
         *
         * 한 번 `BossFight`의 공용 기본값으로 두었다가 되돌렸다. 그 순간 다섯
         * 보스가 전부 같은 참격을 뿜었다 - 어느 BossConfig도 그 칸을 채우지
         * 않았으니 전부 기본값을 상속했고, 등롱도 처형인도 붉은눈도 요괴의
         * 초승달을 뿌렸다. 참격은 **한 요괴의 서명**이고, 넷이 같은 것을 뿜으면
         * 넷을 구분하던 신호가 사라진다.
         *
         * ## 지금은 요괴 보스도 안 쓴다 (0개가 정상)
         *
         * 이 검사가 한때 "정확히 하나"를 걸었다. 그때는 요괴 보스의 공격 시트가
         * Tag_0(내려베기)이라 이펙트가 그려져 있지 않았고, 뜯어낸 초승달을 몸
         * 앞에 따로 얹어 보완했기 때문이다.
         *
         * 지금은 공격 시트 자체가 오의 블록에서 잘려 나와 **초승달이 프레임에
         * 함께 그려져 있다**(YokaiSheetBaker). 얹을 이유가 사라져 그 보스도
         * 칸을 비웠다.
         *
         * 그래서 거는 것은 개수가 아니라 **귀속**이다 - 쓰는 보스가 없어도 되고,
         * 있다면 요괴 보스여야 한다. 다른 보스에 붙는 순간만 걸린다.
         */
        [Test]
        public void TheYokaiSignature_NeverLeaksToAnotherBoss()
        {
            var owners = new System.Collections.Generic.List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:BossConfig", new[] { BossFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<BossConfig>(path);
                if (config == null) continue;

                if (IsYokaiId(config.attackVfxId)) owners.Add(config.name);
            }

            foreach (var owner in owners)
            {
                Assert.AreEqual(YokaiBossAsset, owner,
                    "요괴 참격이 요괴 아닌 보스에 붙어 있다: " + owner);
            }

            Assert.LessOrEqual(owners.Count, 1, string.Format(
                "요괴 참격을 쓰는 보스가 {0}개다 [{1}]",
                owners.Count, string.Join(", ", owners.ToArray())));
        }

        /**
         * @brief 요괴 보스의 공격 시트에 **초승달이 그려져 있다.**
         *
         * 오버레이를 뗀 근거가 이것이다. 시트가 다시 Tag_0(이펙트 없는 내려베기)로
         * 돌아가면 공격이 회색 몸과 칼날뿐이 되는데, 오버레이는 이미 꺼져 있으니
         * 화면에서 조용히 밋밋해진다.
         *
         * 붉은 픽셀(r-g >= 40)이 프레임의 몇 할을 차지하는지로 잡는다. 이펙트가
         * 있는 프레임은 붉은 것이 압도적이고, 몸만 있는 프레임은 칼날뿐이라
         * 한 줌이다 - 실측으로 초승달 프레임은 100%, 대기는 20% 안쪽이다.
         */
        [Test]
        public void TheYokaiBossAttackSheet_HasTheCrescentDrawnIn()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(
                "Assets/_Project/Data/Generated/Enemy_Boss_RedEyeYokai.asset");
            Assert.IsNotNull(definition, "요괴 보스 정의가 없다 - Build Combat Content 를 실행하세요");

            var frames = definition.attackFrames;
            Assert.IsNotNull(frames);
            Assert.Greater(frames.Length, 0, "공격 프레임이 비었다");

            // 프레임 하나라도 '거의 전부 붉은' 것이 있으면 이펙트가 그려진 것이다
            float widest = 0f;
            foreach (var frame in frames)
            {
                if (frame == null) continue;
                float ratio = frame.rect.width / Mathf.Max(1f, frame.rect.height);
                if (ratio > widest) widest = ratio;
            }

            // 초승달은 몸보다 훨씬 옆으로 길다. 몸만 있는 클립은 셀이 정사각에 가깝다
            Assert.Greater(widest, 1.2f, string.Format(
                "공격 프레임이 전부 몸 비율이다(가장 넓은 것 {0:F2}) - " +
                "이펙트 없는 클립으로 되돌아간 것 아닌가", widest));
        }

        /**
         * @brief 공용 기본값 자리에 어느 요괴의 서명도 놓이지 않았는가.
         *
         * 위 검사는 애셋만 본다. 기본값은 코드에 있으므로 여기서 따로 건다 -
         * 애셋을 전부 비워두고 기본값에 요괴 이름을 적으면 위 검사는 통과하면서
         * 화면은 다시 다섯이 같아진다. 실제로 그렇게 새어 나갔다.
         *
         * 비어 있는 것이 정답이고, 언젠가 공용 중립 참격이 생기면 그 이름이
         * 들어와도 된다. 들어오면 안 되는 것은 **요괴의 것**뿐이다.
         */
        [Test]
        public void TheSharedDefault_IsNotAYokaiSignature()
        {
            var go = new GameObject("BossFightUnderTest");
            try
            {
                var fight = go.AddComponent<Onikiri.Battle.BossFight>();
                var serialized = new SerializedObject(fight);
                string fallback = serialized.FindProperty("defaultAttackVfxId").stringValue;

                Assert.IsFalse(IsYokaiId(fallback), string.Format(
                    "공용 기본 참격이 요괴의 서명 '{0}'이다 - " +
                    "이 칸을 채우면 그것을 안 적은 보스가 전부 같은 것을 뿜는다",
                    fallback));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /**
         * @brief 뜯어낸 그림에 요괴의 몸이 섞여 들어오지 않았는가.
         *
         * 색으로 갈랐으므로(YokaiVfxBaker의 팔레트 표) 몸이 남으면 프레임의
         * 크기부터 달라진다 - 몸은 참격보다 작고 아래쪽에 붙어 있어서, 섞이면
         * 잘린 셀이 세로로 훌쩍 커진다. 여기서는 그보다 직접적인 것을 건다:
         * **가로가 세로보다 크다.** 셋 다 옆으로 휘두르거나 옆으로 퍼지는
         * 그림이라 참격만 남으면 반드시 납작하고, 몸이 붙으면 세워진다.
         */
        [Test]
        public void HarvestedFrames_AreWiderThanTall()
        {
            var library = Load();

            for (int i = 0; i < library.Count; i++)
            {
                var clip = library.At(i);
                if (clip.frames == null || clip.frames.Length == 0) continue;

                var first = clip.frames[0];
                Assert.IsNotNull(first, clip.id + ": 첫 프레임이 비었다");

                // 파도(wave)는 위로 솟는 그림이라 예외다. 나머지 둘만 건다
                if (clip.id == "wave") continue;

                Assert.Greater(first.rect.width, first.rect.height, string.Format(
                    "{0}: 셀이 {1}x{2}로 세워져 있다 - 요괴의 몸이 함께 잘려 " +
                    "들어온 것 아닌가", clip.id, first.rect.width, first.rect.height));
            }
        }
    }
}
