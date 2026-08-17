namespace Onikiri.Progression
{
    /** 동료(펫) 하나의 설계값 */
    public struct PetSpec
    {
        /** 세이브가 쓰는 불변 id. 표시 이름과 분리한다 - 이름은 바뀔 수 있다 */
        public string Id;

        /**
         * @brief 화면에 뜨는 이름. 폰트 아틀라스가 여기서 자동 수집한다.
         *
         * 전직 티어 이름과 같은 규칙이다(EvolutionTierSpec.Name) - 손으로 옮겨
         * 적는 단계가 있는 한 그 단계는 언젠가 빠지고, 화면에 ㅁ이 뜬다.
         */
        public string Name;

        /** 역할 한 줄. 펫 화면의 설명이 쓴다 (근접/원거리/강타) */
        public string Role;

        /**
         * @brief 해금 보석 값. **보석만이다. 골드가 섞이지 않는다.**
         *
         * 전직(보석 + 골드)과 다른 판단이다. 이 스텝의 재화 계약이 "해금 =
         * 보석 / 레벨 = 골드"이고, 화면에서 두 재화가 한 버튼에 섞이면 그
         * 계약이 첫 화면에서 깨진다. 골드의 질문("펫에 골드를 쓰는 것이
         * 이득인가")은 레벨 쪽이 온전히 갖는다.
         *
         * 크기의 근거 (장비 1,060 · 전직 3,010 사이의 gem sink):
         *
         *   청랑 80    f2p가 st31 언저리에서 낼 수 있는 값. 업적+반복 하한이
         *              st30까지 약 535, 장비 3등급(240)을 빼도 남는다 - 무과금도
         *              첫 동료는 데리고 싸운다. 첫 펫이 사다리의 광고판이다
         *   명궁 200   일일 보석(하루 55) 나흘치. 컬렉션의 두 번째 칸
         *   묵웅 400   컬렉션 완성. 셋 합쳐 680 - 향후 가챠가 이 자리에 온다
         */
        public int UnlockGems;

        /**
         * @brief 공격 간격 (초). 스타일의 절반이다 (나머지 절반은 몫의 크기).
         *
         * 한 타의 크기 = 이 동료의 보너스 x 플레이어 기대 DPS x 이 간격.
         * 간격이 길수록 한 방이 크다 - 청랑은 잦은 잔타, 묵웅은 느린 강타.
         */
        public double AttackIntervalSeconds;

        /**
         * @brief Lv.1의 DPS 보너스. **셋의 합이 예약 몫을 나눠 갖는다.**
         *
         * 다중 출전으로 바뀌면서(전원 함께 싸운다) 예약된 가속 천장 x1.25는
         * 한 마리가 아니라 **합산**의 몫이 됐다. 그래서 곡선이 동료마다 다르다:
         *
         *   청랑 7.0% -> 30%   첫 동료. 무과금의 기본 세트가 이 몫이고,
         *                      혼자서 명목 x1.30 - 예약의 바닥 근처에 선다
         *   명궁 2.8% -> 12%   컬렉션 확장 = 가속기. 여기부터는 해금 자체가
         *                      DPS를 산다 (이전의 "둘째부터 수집"과 달라진 점)
         *   묵웅 1.9% -> 8%   합산 상한 50% = 명목 x1.5, 33단계 예약 그대로
         *
         * 셋 다 Lv.20에서 자기 상한에 닿는다(성장 x1.08 공유) - 리듬은 같고
         * 크기만 다르다.
         */
        public double FirstBonus;

        /** 이 동료의 보너스 상한. 셋의 합 = PetCurve.TotalBonusCeiling(0.5) */
        public double BonusCeiling;

        /**
         * @brief 원거리인가. 궁수만 true다.
         *
         * 전투 연출이 읽는다 - 원거리는 제자리에서 화살(ARROW -> ARROW HIT)을
         * 날리고, 근접은 대상 앞까지 붙어서 때린다.
         */
        public bool Ranged;

        /**
         * @brief 스프라이트 팩 폴더 이름 (Assets/ThirdParty/Characters/ 아래).
         *
         * 에디터 빌더(PetContentBuilder)만 읽는다. 런타임은 빌더가 씬에 구워둔
         * 프레임 배열을 쓴다 - 전직(EvolutionTierSpec.SpriteFolder)과 같은 규칙.
         */
        public string SpriteFolder;
    }

    /**
     * @brief 동료(펫) 셋. 늑대 청랑, 궁수 명궁, 팬더 묵웅.
     *
     * ## 왜 ScriptableObject가 아닌가
     *
     * QuestCatalog·SkillCatalog·EquipmentCatalog·EvolutionCatalog와 같은 이유다.
     * 이 값들은 아트가 아니라 **밸런스**이고, 밸런스는 시뮬레이션이 씬 없이
     * 읽어야 한다.
     *
     * ## 보유 동료 전원이 함께 출전한다
     *
     * 처음에는 액티브 슬롯 하나였다가 뒤집었다 - "동료"답게 팀으로 싸운다.
     * 그래서 예약 몫 x1.25는 **합산**의 몫이고, 상한의 분배가 위 FirstBonus /
     * BonusCeiling 표다. 슬롯 제한과 추가 동료 판매는 과금 훅으로 다음에
     * 온다 - 그날 이 분배를 다시 잰다.
     */
    public static class PetCatalog
    {
        public const string WolfId = "pet.wolf";
        public const string ArcherId = "pet.archer";
        public const string PandaId = "pet.panda";

        public static readonly PetSpec[] Pets =
        {
            new PetSpec {
                Id = WolfId, Name = "청랑", Role = "근접 · 빠른 물기",
                UnlockGems = 80, AttackIntervalSeconds = 0.8d, Ranged = false,
                FirstBonus = 0.07d, BonusCeiling = 0.30d,
                SpriteFolder = "Wolf_Samurai"
            },
            new PetSpec {
                Id = ArcherId, Name = "명궁", Role = "원거리 · 화살",
                UnlockGems = 200, AttackIntervalSeconds = 1.6d, Ranged = true,
                FirstBonus = 0.028d, BonusCeiling = 0.12d,
                SpriteFolder = "Samurai_Archer"
            },
            new PetSpec {
                Id = PandaId, Name = "묵웅", Role = "강타 · 느리고 큰 한 방",
                UnlockGems = 400, AttackIntervalSeconds = 2.4d, Ranged = false,
                FirstBonus = 0.019d, BonusCeiling = 0.08d,
                SpriteFolder = "Samurai_Panda"
            }
        };

        /** 전 동료 보너스 상한의 합. 예약 몫의 명목이 이 값에서 나온다 */
        public static double TotalBonusCeiling
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < Pets.Length; i++) total += Pets[i].BonusCeiling;
                return total;
            }
        }

        public static int Count { get { return Pets.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Pets.Length; i++)
                if (Pets[i].Id == id) return i;
            return -1;
        }

        /** 해금 보석 총합. 보고와 테스트가 쓴다 */
        public static int TotalUnlockGems
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Pets.Length; i++) total += Pets[i].UnlockGems;
                return total;
            }
        }

        /**
         * @brief 가장 싼 펫의 해금 보석 값.
         *
         * 시뮬레이션의 구매 정책이 쓴다 - 셋의 DPS 곡선이 같으므로 곡선 추종
         * 플레이어는 가장 싼 문으로 들어간다. f2p 바닥 검사도 이 값 기준이다.
         */
        public static int CheapestUnlockGems
        {
            get
            {
                int cheapest = int.MaxValue;
                for (int i = 0; i < Pets.Length; i++)
                    if (Pets[i].UnlockGems < cheapest) cheapest = Pets[i].UnlockGems;
                return cheapest == int.MaxValue ? 0 : cheapest;
            }
        }
    }
}
