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
         * @brief 이전 티어에서 이 티어로 오르는 보석 값.
         *
         * 장비 등급(GradeGems)과 같은 이유로 곡선이 아니라 손으로 적은 표다 -
         * 보석 수입은 퀘스트에서 오고 지수로 자라지 않는다.
         *
         * 크기의 근거 (과금 지향 - 요도 시스템 설계의 gem sink 지속):
         *
         *   1티어 60     f2p가 장비에 쓰고 남는 몫(st40 시점 실측 약 75).
         *                무과금도 첫 진화는 본다 - 스프라이트가 바뀌는 첫
         *                도약이 사다리 전체의 광고판이기 때문이다
         *   2~6티어      일일 보석(하루 55) 기준 사흘~한 달 간격의 마일스톤.
         *                전체 사다리 3,010개 = 약 두 달의 일일 퀘스트,
         *                또는 과금. 여기가 장비(1,060) 다음의 gem sink다
         */
        public int GemCost;

        /**
         * @brief 골드 몫. **기대 스테이지의 절대 골드다** (도약형).
         *
         * 골드도 받는 이유는 장비 등급업과 같다 - 보석만 받으면 이 버튼이 골드
         * 경제 밖에 서고, "전직에 골드를 쓰는 것이 이득인가"라는 질문 자체가
         * 사라진다.
         *
         * 크기는 기대 스테이지(EvolutionCurve.ExpectedStageOfTier)의 **보스
         * 격파 보상 한 번 분량**(잡몹 골드의 약 12배)이다. 처음에 "2분치
         * 파밍"으로 잡았다가 물렸다 - 골드는 처치마다 다른 축이 곧바로
         * 소비하므로 지갑은 그렇게 쌓이지 않고, 실제 스파이크는 보스 보상
         * 하나다. 그보다 크게 잡으면 티어가 몇 스테이지씩 늦는 동안 그 골드가
         * 정규 축에서 빠져나가 후반 여유가 통째로 주저앉는다(초안 실측에서
         * st50 여유 0.88). 상수가 어느 단위인지가 함수의 전부라는 것을 장비
         * 단련 비용에서도 물렸다(EquipmentCurve.TemperBaseCost 주석). 값이
         * 실측과 어긋나면 EvolutionTests.ExpectedCurve_TracksTheSimulation이
         * 잡는다.
         */
        public double GoldCost;

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
                GemCost = 60, GoldCost = 2e10d,
                SpriteFolder = "Samurai_3", Aura = false
            },
            new EvolutionTierSpec {
                Name = "검객", AttackStep = 1.10d, HealthStep = 1.10d,
                GemCost = 150, GoldCost = 6e10d,
                SpriteFolder = "Samurai_5", Aura = false
            },
            new EvolutionTierSpec {
                Name = "검귀", AttackStep = 1.12d, HealthStep = 1.10d,
                GemCost = 300, GoldCost = 1.8e11d,
                SpriteFolder = "Samurai_6", Aura = false
            },
            new EvolutionTierSpec {
                Name = "귀장", AttackStep = 1.12d, HealthStep = 1.10d,
                GemCost = 500, GoldCost = 5.5e11d,
                SpriteFolder = "Samurai_2", Aura = false
            },
            new EvolutionTierSpec {
                Name = "데몬사무라이", AttackStep = 1.15d, HealthStep = 1.10d,
                GemCost = 800, GoldCost = 1.6e12d,
                SpriteFolder = "Demon_Samurai", Aura = false
            },
            new EvolutionTierSpec {
                Name = "진 데몬사무라이", AttackStep = 1.20d, HealthStep = 1.10d,
                GemCost = 1200, GoldCost = 5e12d,
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
