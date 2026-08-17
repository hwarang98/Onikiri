namespace Onikiri.Progression
{
    /** 전직(진화) 티어 하나의 설계값 */
    public struct EvolutionTierSpec
    {
        /**
         * @brief 화면에 뜨는 이름. 폰트 아틀라스가 여기서 자동 수집한다.
         *
         * 등급 이름과 같은 규칙이다(EquipmentSpec.GradeNames) - 손으로 옮겨
         * 적는 단계가 있는 한 그 단계는 언젠가 빠지고, 화면에 ㅁ이 뜬다.
         */
        public string Name;

        /**
         * @brief 이 티어로 오를 때 공격력에 곱해지는 증분.
         *
         * 누적이 아니라 **한 칸의 도약**이다. 누적 배수는
         * EvolutionCurve.AttackMultiplierAt이 곱해서 낸다.
         */
        public double AttackStep;

        /** 최대 체력 증분. 생존 밴드는 바닥만 있으므로(32단계) 화력보다 후하다 */
        public double HealthStep;

        /**
         * @brief 값을 적는 칸은 **없다.** 승급은 귀문 돌파로 무료로 온다.
         *
         * 3단계 전까지 여기에 `GemCost`(60~1,200, 합 3,010)와 `GoldCost`가
         * 있었다. 승인 A-1이 승급 비용을 0으로 정하면서 두 칸을 지웠다 -
         * 0을 적어 두는 쪽을 안 고른 이유는 그것이 "언젠가 다시 채울 칸"으로
         * 읽히기 때문이다. gem sink는 이제 장비(1,060) · 동료(680) ·
         * 요도·오의 뽑기(무한)가 담당한다.
         *
         * 값이 다시 필요해지면 그때 이 주석이 무엇이 있었는지 말해 준다.
         */

        /**
         * @brief 스프라이트 팩 폴더 이름 (Assets/ThirdParty/Characters/ 아래).
         *
         * 에디터 빌더(EvolutionContentBuilder)만 읽는다. 런타임은 빌더가 씬에
         * 구워둔 프레임 배열을 쓴다.
         */
        public string SpriteFolder;

        /**
         * @brief FLAMING SWORD 시트를 쓰는가. 데몬사무라이 팩에만 있다.
         *
         * 최종 티어의 Aura가 이것이다 - 별도 이펙트를 얹는 것이 아니라
         * 원화가가 검에 맞춰 그린 불꽃 시트로 갈아탄다. 참격 아크를 시트
         * 안의 그림으로 줄인 22단계와 같은 판단이다.
         */
        public bool Aura;
    }

    /**
     * @brief 전직(사무라이 진화) 티어 여섯. 로닌에서 진 데몬사무라이까지.
     *
     * ## 왜 ScriptableObject가 아닌가
     *
     * QuestCatalog·SkillCatalog·EquipmentCatalog와 같은 이유다. 이 값들은
     * 아트가 아니라 **밸런스**이고, 밸런스는 시뮬레이션이 씬 없이 읽어야 한다.
     *
     * ## 티어 0은 표에 없다
     *
     * 로닌(기본)은 진화가 아니라 시작 상태다. 배수 1.0이고 세이브 마이그레이션
     * (v9 -> v10)이 기존 플레이어를 여기에 세운다 - 장비 1등급 Lv.1이 배수
     * 1배인 것과 같은 설계다.
     *
     * ## 스프라이트 배정 (아트 팩 다섯 중 넷을 골랐다)
     *
     *   0 로닌          FULL_Samurai      (기존 플레이어 아트 그대로)
     *   1 무사          Samurai_3         (푸른 삿갓의 방랑 무사)
     *   2 검객          Samurai_5         (청록 도복)
     *   3 검귀          Samurai_6         (어두운 복장 + 푸르게 빛나는 검)
     *   4 귀장          Samurai_2         (금장식 투구의 붉은 갑주 - 인간형 최상위)
     *   5 데몬사무라이   Demon_Samurai     (뿔 달린 붉은 오니)
     *   6 진 데몬사무라이 Demon_Samurai    (FLAMING SWORD 시트 = Aura)
     *
     * Samurai_4(보라 머리)는 뺐다. 어두운 실루엣이 검귀(Samurai_6)와 겹쳐
     * 한 칸의 도약이 화면에서 읽히지 않는다 - 진화의 존재 이유가 "눈에 보이는
     * 도약"인데 두 티어가 비슷해 보이면 그 사이 보석 값이 설명되지 않는다.
     *
     * 순서는 "맨몸 -> 갑주 -> 오니"다. 로닌이 갑주를 얻어 인간의 정점(귀장)에
     * 오르고, 그 다음 칸에서 사냥하던 오니 자신이 된다 - 검귀(鬼)라는 이름이
     * 그 예고다.
     */
    public static class EvolutionCatalog
    {
        /** 티어 0의 이름. 표 밖이지만 화면에는 뜬다 */
        public const string BaseName = "로닌";

        /** 티어 0의 스프라이트 팩. 기존 플레이어 아트 */
        public const string BaseSpriteFolder = "FULL_Samurai";

        public static readonly EvolutionTierSpec[] Tiers =
        {
            // 화력 증분이 뒤로 갈수록 커진다(1.10 -> 1.20). 도약형 재화의 값도
            // 함께 커지므로(60 -> 1200) "비싼 칸이 더 크게 뛴다"가 성립한다 -
            // 등급이 클수록 GradeStep이 같아 보석당 이득이 줄던 장비와 다른
            // 점이고, 마일스톤 축이라 그래야 한다.
            //
            // 체력은 여섯 칸 모두 +10%다. 생존 밴드는 바닥만 있어서
            // (SurvivalMargin >= 1.16) 위로는 얼마를 곱해도 아무 밴드도 건드리지
            // 않는다 - 방어구가 무기보다 큰 것과 같은 산수다.
            new EvolutionTierSpec {
                Name = "무사", AttackStep = 1.10d, HealthStep = 1.10d,
                SpriteFolder = "Samurai_3", Aura = false
            },
            new EvolutionTierSpec {
                Name = "검객", AttackStep = 1.10d, HealthStep = 1.10d,
                SpriteFolder = "Samurai_5", Aura = false
            },
            new EvolutionTierSpec {
                Name = "검귀", AttackStep = 1.12d, HealthStep = 1.10d,
                SpriteFolder = "Samurai_6", Aura = false
            },
            new EvolutionTierSpec {
                Name = "귀장", AttackStep = 1.12d, HealthStep = 1.10d,
                SpriteFolder = "Samurai_2", Aura = false
            },
            new EvolutionTierSpec {
                Name = "데몬사무라이", AttackStep = 1.15d, HealthStep = 1.10d,
                SpriteFolder = "Demon_Samurai", Aura = false
            },
            new EvolutionTierSpec {
                Name = "진 데몬사무라이", AttackStep = 1.20d, HealthStep = 1.10d,
                SpriteFolder = "Demon_Samurai", Aura = true
            }
        };

        public static int Count { get { return Tiers.Length; } }

        /** 이 티어의 이름. 0이면 로닌, 범위 밖이면 마지막 이름 */
        public static string NameOf(int tier)
        {
            if (tier <= 0) return BaseName;
            int index = tier - 1;
            if (index >= Tiers.Length) index = Tiers.Length - 1;
            return Tiers[index].Name;
        }
    }
}
