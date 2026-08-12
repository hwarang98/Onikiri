using System.Collections.Generic;
using Onikiri.Battle;
using Onikiri.Progression;
using UnityEditor;
using UnityEngine;

namespace Onikiri.EditorTools
{
    /**
     * @brief 성장 축과 재화의 아이콘. 축 id 하나에 심볼 하나.
     *
     * ## 왜 아이콘인가
     *
     * 14단계까지 성장 행은 글자뿐이었다. 여덟 줄이 전부 "OO 강화 Lv.N"이라
     * 목록을 훑을 때 눈이 **글자를 읽어야만** 어느 축인지 알 수 있었다.
     * 방치형에서 이 목록은 하루에 수십 번 보는 화면이고, 그때마다 읽게 만드는
     * 것은 비싸다. 심볼은 읽지 않고 알아본다.
     *
     * ## 색이 두 번째 축이다
     *
     * KURAI 팩은 아이콘마다 배경 타일 색이 다르고, 그것을 의미색으로 쓴다.
     * 검=빨강, 회오리=파랑, 눈=금색, 심장=초록. 심볼을 못 알아봐도 색으로
     * 계열(화력/생존)이 먼저 읽힌다.
     */
    public static class UiIcons
    {
        public const string Folder = "Assets/ThirdParty/UI/KuraiSkillIcons/Icons";

        /**
         * @brief 축 id -> 아이콘 파일.
         *
         * **증폭 축은 기본 축과 같은 심볼을 쓴다.** 다른 심볼을 주면 그것이 별개의
         * 스탯으로 읽히는데, 실제로는 같은 스탯을 다른 재화로 올리는 것이다.
         * 구분은 심볼이 아니라 아래 {@link AmplifierTint}가 맡는다 - 반 톤 눌러
         * "보조"라는 것을 밝기로 말한다.
         *
         * 팩에서 다른 색 변형을 골라 구분하는 방법도 있었는데, 그러면 색이
         * 의미를 잃는다. 초록 검을 공격력 증폭에 쓰면 초록(=생존)과 검(=화력)이
         * 한 아이콘 안에서 서로를 부정한다.
         */
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { UpgradeSystem.AttackPowerId,  "Icon084" },  // 은빛 검     (빨강)
            { UpgradeSystem.AttackSpeedId,  "Icon002" },  // 회오리      (파랑)
            { UpgradeSystem.CritRateId,     "Icon125" },  // 눈          (금색)
            { UpgradeSystem.CritDamageId,   "Icon144" },  // 화염 검     (주황)
            { UpgradeSystem.HealthId,       "Icon066" },  // 심장        (초록)
            { UpgradeSystem.HealthRegenId,  "Icon104" },  // 십자        (초록 밝게)

            // 획득 축은 상단 바의 골드와 **같은 금화**를 쓴다. 다른 심볼을 주면
            // "이 축이 무엇을 늘리는가"를 글자로 읽어야 알 수 있는데, 이 축만은
            // 늘리는 대상이 화면에 이미 아이콘으로 떠 있다
            { UpgradeSystem.GoldGainId,     GoldIcon },   // 금화 - 상단 바와 같은 심볼

            { CharacterLevel.AttackAmpId,   "Icon084" },  // 검 - 공격력과 같은 심볼
            { CharacterLevel.HealthAmpId,   "Icon066" },  // 심장 - 체력과 같은 심볼

            // 43단계의 심화 축. **증폭 축과 같은 판단**이다 - 초월 치명타는
            // 치명타 피해의 연장(전타 치명타 뒤의 순수 배수)이고 연격은
            // 타격 수의 연장이라, 각자 뿌리가 되는 축의 심볼을 이어받는다.
            // 새 심볼을 주면 별개의 스탯으로 읽히는데 실제로는 같은 힘의
            // 다음 층이다. 구분은 "심화" 머리글과 해금 게이트가 맡는다
            { UpgradeSystem.TranscendId,    "Icon144" },  // 화염 검 - 치명타 피해의 다음 층
            { UpgradeSystem.ComboId,        "Icon002" },  // 회오리 - 타격 수의 다음 층

            // 26단계의 발도 오의 셋. **여기서는 심볼을 전부 다르게 쓴다.**
            //
            // 증폭 축이 기본 축과 같은 심볼을 쓰는 것(위)과 반대 판단인데, 상황이
            // 다르다. 증폭은 같은 스탯을 다른 재화로 올리는 것이라 같은 심볼이
            // 맞지만, 세 오의는 서로 다른 동작이고 **화면에서 어느 것이 터졌는지
            // 알아봐야 한다.** 목록에서 고르는 것으로 끝나지 않고 전투 중에
            // 다시 읽히는 축은 이 셋뿐이다.
            //
            // 색도 대형 참격의 색(SkillCatalog.SlashRgba)과 계열을 맞춘다 -
            // 흰 삼연참 / 붉은 단발 / 금빛 오니.
            { SkillCatalog.ChainSlashId,    "Icon076" },  // 흰 삼연 참격  (붉은 타일)
            { SkillCatalog.FlashId,         "Icon140" },  // 붉은 단발 참격 (검은 타일)
            { SkillCatalog.OniCleaveId,     "Icon118" },  // 오니 뿔        (금빛)

            // 49단계의 신규 오의 다섯. **전부 붉은 타일**이다 - 팩의 타일 색이
            // 계열을 말하고(위 주석), 이 다섯은 혈(血) 한 계열이라 색이 같아야
            // 목록에서 "같은 무리"로 읽힌다. 심볼은 다섯이 서로 다르다 -
            // 전투 중에 어느 것이 터졌는지 알아봐야 하는 축이라는 점은 셋일 때와
            // 같고, 여덟이 되면서 오히려 더 중요해졌다
            { SkillCatalog.BloodWaveId,     "Icon058" },  // 사방으로 퍼지는 방사
            { SkillCatalog.BloodFallId,     "Icon056" },  // 떨어지는 핏줄기
            { SkillCatalog.BloodWheelId,    "Icon062" },  // 회전하는 톱니 고리
            { SkillCatalog.BloodBurstId,    "Icon083" },  // 터져 오르는 폭발 기둥
            { SkillCatalog.BloodWhipId,     "Icon082" },  // 휘어 감기는 갈고리
        };

        public const string GoldIcon = "Icon114";   // 금화
        public const string ExpIcon = "Icon175";    // 별

        /**
         * @brief 31단계에 쓴 아이템 팩. **다른 팩이라 로더가 따로 있다.**
         *
         * 스킬 아이콘 팩(KuraiSkillIcons)은 파일 하나에 아이콘 하나지만 이쪽은
         * 시트 한 장에 133개가 잘려 있다. 새 팩을 들이지 않고 이미 프로젝트에
         * 있는 것에서 고른 이유는, 필요한 것이 둘뿐이고 **팩 하나가 통째로
         * 늘어나는 비용이 아이콘 두 개보다 크기** 때문이다.
         */
        public const string ItemSheet = "Assets/ThirdParty/UI/KyriseItemIcons/spritesheet_16x16.png";

        /** 파란 다이아. 보석 재화 */
        public const string GemSprite = "spritesheet_16x16_74";

        /** 펼친 책. 퀘스트 탭 */
        public const string QuestSprite = "spritesheet_16x16_23";

        /**
         * @brief 원본 채도를 반 톤 누르는 틴트.
         *
         * 팩 아이콘은 밝고 채도가 높다. 우리 행은 어두운 자주색이라, 원본 그대로
         * 여덟 개를 나란히 놓으면 화면이 아이콘 색으로 파편화된다 - 배경보다
         * 아이콘이 먼저 눈에 들어오면 목록이 아니라 색 표가 된다.
         *
         * 스탯 구분은 유지될 만큼만 누른다. 완전히 죽이면 아이콘을 쓰는 이유가
         * 사라진다.
         */
        public static readonly Color Tint = new Color(0.82f, 0.80f, 0.86f, 1f);

        /** 증폭 축. 기본 축과 같은 심볼이라 밝기로 가른다 */
        public static readonly Color AmplifierTint = new Color(0.58f, 0.56f, 0.64f, 1f);

        /**
         * @brief 아이콘 한 변의 캔버스 크기.
         *
         * 16px 아트의 **정수배**여야 한다. 96 = 16 x 6. 정수가 아니면 픽셀
         * 격자가 화면 픽셀 사이에 놓여 흐려진다 - 보스 확대 배율, 9-슬라이스
         * 배율과 같은 규칙이다.
         *
         * 행 높이 144 안에서 96은 위아래 24씩 남는 크기다. 글자(55pt 두 줄)를
         * 밀어내지 않으면서 눈에 먼저 들어온다.
         */
        public const float Size = 96f;

        /** 이 축의 아이콘. 표에 없으면 null이고 VerifyWiring이 잡는다 */
        public static Sprite For(string trackId)
        {
            string file;
            if (string.IsNullOrEmpty(trackId) || !Map.TryGetValue(trackId, out file)) return null;
            return Load(file);
        }

        public static Sprite Load(string file)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + file + ".png");
            if (sprite == null)
                Debug.LogWarning("[Onikiri] Icon missing: " + Folder + "/" + file + ".png");
            return sprite;
        }

        /**
         * @brief 아이템 시트에서 이름으로 하나 꺼낸다.
         *
         * `LoadAssetAtPath<Sprite>`로는 시트의 **첫 서브 스프라이트**만 나온다.
         * 전부 읽어 이름으로 찾아야 한다 - 팩 참격 시트를 자를 때와 같은 자리다
         * (SkillPanelBuilder.SliceSlashSheet).
         */
        public static Sprite LoadItem(string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ItemSheet))
            {
                var sprite = asset as Sprite;
                if (sprite != null && sprite.name == spriteName) return sprite;
            }

            Debug.LogWarning("[Onikiri] Item icon missing: " + spriteName + " in " + ItemSheet);
            return null;
        }

        /** 표에 등록된 축 id 전부. VerifyWiring이 빠진 축을 찾을 때 쓴다 */
        public static bool HasIcon(string trackId)
        {
            return !string.IsNullOrEmpty(trackId) && Map.ContainsKey(trackId);
        }
    }
}
