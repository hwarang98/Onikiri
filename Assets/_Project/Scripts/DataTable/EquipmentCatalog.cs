namespace Onikiri.Progression
{
    /** 장비 슬롯이 먹이는 스탯. 자가 갈리는 자리다 */
    public enum EquipmentStat
    {
        /** 공격력에 곱해진다. 효율은 골드당 %DPS (UpgradeEfficiency와 같은 자) */
        AttackPower,

        /** 최대 체력에 곱해진다. 효율은 골드당 %EHP (SurvivalEfficiency와 같은 자) */
        MaxHealth
    }

    /** 장비 슬롯 하나의 설계값 */
    public struct EquipmentSpec
    {
        /** 세이브와 UI가 이 문자열로 갈린다. 순서가 바뀌어도 등급이 섞이지 않는다 */
        public string Id;

        /** "무기" / "방어구". 슬롯의 이름이고 등급 이름과 다르다 */
        public string SlotName;

        public EquipmentStat Stat;

        /**
         * @brief 등급 이름 다섯. 화면에 뜨는 것은 이쪽이고 슬롯 이름이 아니다.
         *
         * 등급을 숫자로 적지 않는 이유는 "3등급 무기"가 아무것도 말해주지
         * 않기 때문이다. 이름이 바뀌면 도약이 형태로 읽힌다 - 강철칼에서
         * 요괴검이 되는 것이 x1.10이라는 숫자보다 먼저 보인다.
         *
         * 글자는 FontCharsetBuilder가 여기서 자동으로 걷어 간다. 오의 이름·
         * 퀘스트 제목과 같은 규칙이다 - 손으로 옮겨 적는 단계가 있는 한 그
         * 단계는 언젠가 빠지고, 화면에 ㅁㅁ이 뜬다.
         */
        public string[] GradeNames;

        /**
         * @brief 한 칸(%)당 골드. **해금 스테이지의 절대 골드다.**
         *
         * 슬롯마다 다르다. %DPS와 %EHP는 이 게임에서 골드 가격이 원래 여덟 배쯤
         * 다르고(아래 실측), 그 사실을 같은 값으로 덮으면 한 슬롯이 반드시
         * 죽는다 - 싼 쪽만 팔린다.
         *
         * 값은 해금 스테이지(11)에서 경쟁 축과 골드당 효율이 같아지도록 잡았다.
         * 시뮬레이션이 그 시점에 실제로 갖고 있는 레벨에서 잰 값이다:
         *
         *   무기   공격력 강화 Lv.44 = 4,400골드로 +12% DPS  ->  367골드/%
         *   방어구 체력 강화 Lv.30 =   513골드로 +10% EHP    ->   51골드/%
         *
         * 같은 자리에서 시작하면 구매 정책이 어느 한쪽으로 쏠리지 않고, 둘 중
         * 무엇을 먼저 올려도 손해가 아니다. SkillCurve.CostPerRate가 세 오의에
         * 하는 일과 같다.
         *
         * **레벨은 추정이 아니라 실측이고, 테스트가 다시 잰다** - 곡선을 손보면
         * 그 시점 레벨이 움직이고 그러면 이 값도 함께 움직여야 한다
         * (EquipmentTests.TemperCost_StartsAtParityWithTheCompetingAxis).
         * SkillSpec.UnlockStage와 같은 처리다.
         */
        public double GoldPerPercent;

        /**
         * @brief 단련 한 칸의 배수. 하한은 **+1%**다.
         *
         * 16단계가 스탯 포인트에서 그은 선이고(MinimumFeltGain), 그 아래는
         * 눌러도 숫자가 안 움직인다. 두 슬롯 다 여유를 두고 잡았다.
         */
        public double TemperStep;

        /**
         * @brief 등급 하나의 배수. **단련 한 칸의 대여섯 배여야 한다.**
         *
         * 그 비가 화면에서 "큰 도약"이 성립하는 근거이고, 두 재화(보석/골드)를
         * 가르는 유일한 수치적 이유다. 비슷해지면 보석이 "네 칸 더 살 권리"로만
         * 남고 도약이 사라진다.
         */
        public double GradeStep;

        /** 5등급 만점의 총 배수 */
        public double Ceiling
        {
            get { return EquipmentCurve.CeilingFor(GradeStep, TemperStep); }
        }

        /** 아이콘 (Kyrise 아이템 시트의 스프라이트 이름). 에디터 빌더만 쓴다 */
        public string IconSprite;

        /** 이 슬롯의 단련 첫 칸 비용 */
        public double TemperBaseCost
        {
            get { return EquipmentCurve.TemperBaseCost(GoldPerPercent, TemperStep); }
        }

        /** 등급 이름. 범위를 벗어나면 마지막 이름 */
        public string GradeName(int grade)
        {
            if (GradeNames == null || GradeNames.Length == 0) return SlotName;

            int index = grade - 1;
            if (index < 0) index = 0;
            if (index >= GradeNames.Length) index = GradeNames.Length - 1;
            return GradeNames[index];
        }
    }

    /**
     * @brief 장비 두 슬롯. 대장간에서 단련하고 등급을 올린다.
     *
     * ## 왜 둘인가
     *
     * **자가 둘뿐이기 때문이다.** 이 프로젝트에는 축을 재는 자가 셋 있고
     * (UpgradeEfficiency = %DPS, SurvivalEfficiency = %EHP, GoldGainEfficiency =
     * 회수 시간), 그중 장비가 올라탈 수 있는 것은 앞의 둘이다. 액세서리를
     * 넷째 슬롯으로 두려면 그 슬롯이 무엇을 올리는지 정해야 하는데, 세 자
     * 중 하나를 다시 쓰면 기존 슬롯과 같은 축이 되고 새 자를 만들면 26단계에
     * 스킬을 고를 때 피한 그 갈래가 넷째로 갈라진다.
     *
     * 액세서리·정련·드랍 재료는 다음 스텝이고, 그때 자를 먼저 정한다.
     *
     * ## 왜 ScriptableObject가 아닌가
     *
     * QuestCatalog·SkillCatalog와 같은 이유다. 이 값들은 아트가 아니라
     * **밸런스**이고, 밸런스는 시뮬레이션이 씬 없이 읽어야 한다. 에셋에 두면
     * StageSimulation이 EditMode 테스트에서 실제 값을 못 본다.
     */
    public static class EquipmentCatalog
    {
        public const string WeaponId = "equip_weapon";
        public const string ArmorId = "equip_armor";

        public static readonly EquipmentSpec[] Slots =
        {
            new EquipmentSpec {
                Id = WeaponId,
                SlotName = "무기",
                Stat = EquipmentStat.AttackPower,
                // **5등급이 "오니키리"였다. 44단계에 "명공검"으로 옮겼다.**
                //
                // 요도 시스템의 최종 목표가 오니키리(네 대요괴의 혼을 모두
                // 봉인한 칼)이고, 그 도감이 이 카드와 **같은 대장간 화면**에
                // 선다. 같은 이름의 다른 물건이 한 화면에 둘 있으면 그것은
                // 두 시스템이 아니라 버그로 읽힌다.
                //
                // 옮긴 쪽이 이쪽인 이유는 이 사다리가 **사람이 벼린 칼**의
                // 사다리이기 때문이다. 무쇠에서 시작해 요괴의 재료를 거쳐
                // 인간 대장장이가 닿을 수 있는 끝이 명공검이고, 그 위는
                // 대장간이 아니라 요괴가 연다 - 두 축의 관계가 이름으로도
                // 읽힌다. 값은 하나도 안 움직인다(이름만 바뀐다)
                GradeNames = new[] { "무쇠칼", "강철칼", "요괴검", "귀살도", "명공검" },
                GoldPerPercent = 367d,

                // **총 x1.30. 세 번 줄인 값이고, 그 과정이 이 스텝의 가장 큰 발견이다.**
                //
                // x2.0 -> x1.70 -> x1.30. 매번 st30 피날레 여유가 천장 1.70을
                // 넘었다(1.88 / 1.80). 원인은 계수가 아니라 **밴드에 남은 자리**다.
                //
                //   피날레 밴드      1.15 ~ 1.70   비 1.478
                //   32단계 前 실측   1.35 ~ 1.65   비 1.222  <- 이미 쓰고 있는 폭
                //   새 축에 남은 자리                x1.21
                //
                // 그리고 이 축이 만드는 폭은 상한보다 크다. **사는 동안과 다 산
                // 뒤가 다르기 때문이다:**
                //
                //   st20 (사는 중)  골드를 쓰고 보정을 맞으므로 여유가 내려간다
                //   st30 (다 산 뒤) 지불한 골드는 인플레이션에 씻겨 없어지고
                //                   배수만 남으므로 여유가 올라간다
                //
                // x1.70에서 그 폭이 1.36이었다 - 남은 자리 1.21보다 크다. 지수로도
                // 램프로도 못 메운다는 것을 둘 다 풀어서 확인했다(연립방정식의 해가
                // 음수 지수였다). **상한이 있는 곱연산 축은 반드시 이 모양이고,
                // 그래서 밴드가 축의 크기를 정한다.**
                //
                // x1.30이면 폭이 1.15라 자리에 들어가고 여유가 남는다. 그 남은
                // 자리가 다음 pillar(전직/펫)의 몫이다.
                TemperStep = 1.012d,    // +1.2% / 칸  (아홉 칸)
                GradeStep = 1.0405d,    // +4.05% / 등급 (단련의 3.4배, 네 번)
                IconSprite = UiSprites.WeaponSprite
            },

            new EquipmentSpec {
                Id = ArmorId,
                SlotName = "방어구",
                Stat = EquipmentStat.MaxHealth,
                GradeNames = new[] { "무명옷", "가죽갑옷", "사슬갑옷", "귀갑옷", "오니갑옷" },

                // **패리티(51)가 아니라 3이다.** 처음에 51로, 다음에 38로, 다음에
                // 8로 두고 돌렸는데 앞의 둘은 시뮬레이션이 방어구를 **한 칸도 사지
                // 않았고**, 8은 st24가 되어서야 한 칸 샀다.
                //
                // 이유는 이 게임에서 %EHP가 원래 싸기 때문이다. 체력 강화 한 칸이
                // +10%인데 방어구 한 칸은 +1.65%라, 방어구가 체력 강화의 6분의 1
                // 가격이 아니면 저울에서 이길 수가 없다. 51은 "해금 시점의 체력
                // 레벨이 30일 것"이라는 추정에서 나온 값이었고 실측은 11이었다 -
                // 생존 요구가 문턱(다음 보스에게 죽지 않을 만큼)이라 체력 레벨이
                // 화력만큼 자라지 않는다.
                //
                // 그래서 이 슬롯은 골드로는 싸고 **보석으로 늦다.** 등급 관문이
                // 속도를 정하고, 골드는 그 관문 사이를 곧바로 채운다. 골드 획득
                // 축이 "해금 즉시 한 번에 여는 스위치"인 것과 같은 성격이고
                // (GoldGainCurve.BaseCost 주석), 다른 점은 그 스위치가 다섯 번
                // 나뉘어 있고 매번 보석이 든다는 것이다.
                GoldPerPercent = 3d,

                // **총 x2.0. 무기(x1.30)보다 훨씬 크다.**
                //
                // 화면에서 두 줄은 같은 모양(5등급 10칸)인데 크기가 이렇게 갈리는
                // 것이 이상해 보이지만, 두 스탯이 서는 밴드가 다르다:
                //
                //   무기(%DPS)   보스 여유 밴드에 **천장이 있다.** 위 주석 참고
                //   방어구(%EHP) 생존 밴드는 **바닥만 있다**(>= 1.16). 두꺼워지는
                //                것 자체는 아무 밴드도 건드리지 않고, 아낀 골드가
                //                화력으로 가는 간접 효과만 보스 여유에 잡힌다
                //
                // 그래서 방어구 한 칸은 무기 한 칸보다 눈에 띄게 크고(+1.65% 대
                // +1.2%), 등급 도약은 두 배 이상 크다(+10% 대 +4.05%). 화면에서
                // "방어구가 더 시원하다"로 읽히는 것이 맞다 - 실제로 그렇다.
                TemperStep = 1.023d,    // +2.3% / 칸  (아홉 칸)
                GradeStep = 1.129d,     // +12.9% / 등급 (단련의 5.6배, 네 번)

                IconSprite = UiSprites.ArmorSprite
            }
        };

        public static int Count { get { return Slots.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].Id == id) return i;
            return -1;
        }

        public static EquipmentSpec Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? Slots[index] : default(EquipmentSpec);
        }
    }

    /**
     * @brief 아이템 시트의 스프라이트 이름. **런타임 어셈블리에 둔다.**
     *
     * UiIcons(에디터)에 두면 위 표가 에디터 전용 타입을 참조하게 되고, 그러면
     * 카탈로그가 빌드에서 빠진다. 이름 두 개뿐이라 여기 두는 비용이 작다.
     *
     * 이 둘은 팩이 원래 잘라둔 스프라이트가 아니다. Kyrise 시트는 손으로 대충
     * 잘려 있어서(한 렉트가 두세 칸을 덮는 것이 스물세 개) 칼도 방패도 온전한
     * 조각이 없다. `EquipmentIconSlicer`가 16px 격자에서 두 칸을 **덧붙여**
     * 자른다 - 기존 렉트를 지우지 않는 것이 중요하다. 보석(74)과 퀘스트
     * 책(23)이 같은 시트에 있고, 통째로 다시 자르면 그 둘의 이름이 사라진다.
     */
    public static class UiSprites
    {
        /** 흰 날 + 금 손잡이. 무기 슬롯 */
        public const string WeaponSprite = "kyrise_sword";

        /** 붉은 원형 방패. 방어구 슬롯 */
        public const string ArmorSprite = "kyrise_shield";
    }
}
