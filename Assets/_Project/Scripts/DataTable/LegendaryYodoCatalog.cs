namespace Onikiri.Progression
{
    /**
     * @brief 전설 妖刀 한 자루. **벤 적 없는 칼이다.**
     *
     * ## 보스 혼 넷과 무엇이 다른가 - 이 구조체가 갈린 이유
     *
     * YodoSpec은 **혼과 칼이 일대일**이라 하나였다("등롱의 혼을 봉인하면
     * 등롱도가 된다"). 전설 요도에는 그 일대일이 없다 - 봉인한 혼이 없기
     * 때문이다. 이것들은 요괴를 벤 증거가 아니라 **어딘가에 있던 물건**이고,
     * 그래서 뽑기에서 나온다.
     *
     * 그 차이가 게임의 두 축을 만든다:
     *
     *   보스 혼 넷   완성의 **세로축**. 오니키리로 끝나고, 뽑기로 못 판다
     *   전설 요도    수집의 **가로축**. 끝이 없고, 도감에서만 만난다
     *
     * 세로축을 뽑기가 건드리지 않는 것이 44단계부터의 기둥이다(YodoCurve
     * 머리 주석 - "첫 봉인은 영원히 보스의 혼이 필요하다"). 전설 요도가
     * **도감 세트 보너스에 안 들어가는 것**이 그 기둥을 값으로 적은
     * 자리다: 다섯 자루를 모아도 오니키리는 완성되지 않는다. 오니키리는
     * 넷이다.
     *
     * ## 티어가 없다 - 사본이 있다
     *
     * 벼릴 재료(혼·파편)가 안 들어오므로 티어가 있을 자리가 없다. 대신
     * **중복이 돌파가 된다** - 같은 칼을 또 뽑으면 그 칼이 한 칸 깊어진다.
     * 뽑기에서만 나오는 물건의 성장은 뽑기에서만 나와야 하고, 그것이
     * 이 축이 상점의 재고로 성립하는 이유다.
     */
    public struct LegendaryYodoSpec
    {
        /** 세이브가 이 문자열로 갈린다. YodoSpec.Id와 같은 규칙이다 */
        public string Id;

        /** 화면에 뜨는 이름. "백면도" */
        public string BladeName;

        /**
         * @brief 도감의 한 줄 설명. **잠긴 미리보기가 이것을 적는다.**
         *
         * 보스 요도는 "○○ 처치 시 해금"이라고 적을 수 있었다 - 조건이
         * 행동이기 때문이다. 전설 요도의 조건은 확률이라 적을 말이 다르고,
         * 그래서 조건 대신 **무엇을 하는 칼인가**를 적는다.
         */
        public string Flavor;

        /**
         * @brief 이 칼이 강화하는 오의의 id. **비면 전 오의다.**
         *
         * 45단계의 YodoSpec.AffinitySkillId와 같은 규칙이고 같은 곡선
         * 모양을 쓴다(LegendaryYodoCurve). 넷 대 셋의 불일치를 흑야가
         * 풀었듯, 여기서도 하나는 전 오의를 얕게 민다.
         */
        public string AffinitySkillId;

        /** 아이콘 (Kyrise 아이템 시트) */
        public string IconSprite;

        /** 아이콘 틴트. 흰색이면 아트 그대로 */
        public UnityEngine.Color IconTint;
    }

    /**
     * @brief 가챠 전용 전설 妖刀 표. **이번 스텝은 두 자루다.**
     *
     * ## 왜 둘인가 - 하나는 뽑기가 아니고 넷은 다른 스텝이다
     *
     * 하나면 "★5가 나왔다"와 "그 칼이 나왔다"가 같은 사건이라 수집이
     * 아니다 - 두 번째 ★5부터는 전부 중복이고, 그러면 이 축은 돌파
     * 하나짜리 막대가 된다.
     *
     * 셋 이상은 아트와 상성 배정이 함께 늘어난다. 상성은 오의 셋에
     * 붙는데 보스 혼 넷이 이미 그 셋을 나눠 물고 있어(45단계), 전설이
     * 셋이면 어느 오의에도 세 겹이 겹친다 - 빌드를 만들려고 넣은 축이
     * "무조건 다 모으는 것이 답"으로 접힌다.
     *
     * 둘이면 하나는 전담(연참)이고 하나는 전 오의다. 그 둘이 45단계가
     * 만든 두 방향(몰아주기 / 고르기)에 각각 얹히므로, 어느 쪽을
     * 뽑았는지가 빌드의 답을 바꾼다.
     *
     * ## 아트는 0장이다
     *
     * 44단계의 요도 넷이 Kyrise 시트의 **같은 칼 네 색**으로 선 것과 같은
     * 자리에서, 남아 있는 두 색을 쓴다(EquipmentIconSlicer의 아랫줄 -
     * 칼 두 벌 x 다섯 색). 새 시트도 새 셀도 없다.
     */
    public static class LegendaryYodoCatalog
    {
        public const string WhiteMaskId = "yodo_whitemask";
        public const string ThousandHandId = "yodo_thousandhand";

        public static readonly LegendaryYodoSpec[] Blades =
        {
            new LegendaryYodoSpec {
                Id = WhiteMaskId,
                BladeName = "백면도",

                // 가면에는 얼굴이 없다. 그래서 어느 오의의 것도 아니고
                // 전부의 것이다 - 흑야도(YodoCatalog)가 같은 자리에 있고,
                // 곡선도 같은 이유로 얕다(LegendaryYodoCurve.BroadStep)
                AffinitySkillId = null,
                Flavor = "얼굴 없는 칼 · 모든 오의를 민다",

                IconSprite = LegendaryYodoSprites.WhiteMaskSprite,

                // 아랫줄의 은색 날. 창백한 청백으로 눌러 "가면"의 색을 만든다.
                // 곱연산은 없는 채널을 못 만들지만 줄이는 것은 된다(35단계)
                IconTint = new UnityEngine.Color(0.86f, 0.92f, 1.00f, 1f)
            },
            new LegendaryYodoSpec {
                Id = ThousandHandId,
                BladeName = "천수도",

                // 천 개의 손 = 다타. 연참은 셋 중 유일한 다타이고,
                // 적안도가 이미 물고 있다 - 겹치는 것이 의도다. 몰아주기
                // 빌드(45단계 상성)에 전설이 한 겹 더 얹히는 자리가 하나는
                // 있어야 "이 칼을 뽑아서 그 빌드를 한다"가 성립한다
                AffinitySkillId = SkillCatalog.ChainSlashId,
                Flavor = "천 개의 손 · 연참이 깊어진다",

                IconSprite = LegendaryYodoSprites.ThousandHandSprite,

                // 짙은 자주. 넷 중 어느 요도와도 안 겹치는 색이라
                // 도감에서 "다른 계열"로 읽힌다
                IconTint = new UnityEngine.Color(0.78f, 0.40f, 0.72f, 1f)
            }
        };

        public static int Count { get { return Blades.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Blades.Length; i++)
                if (Blades[i].Id == id) return i;
            return -1;
        }

        public static LegendaryYodoSpec Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? Blades[index] : default(LegendaryYodoSpec);
        }
    }

    /**
     * @brief 전설 요도 아이콘의 스프라이트 이름. YodoSprites와 같은 자리다.
     */
    public static class LegendaryYodoSprites
    {
        public const string WhiteMaskSprite = "kyrise_yodo_whitemask";
        public const string ThousandHandSprite = "kyrise_yodo_thousandhand";
    }
}
