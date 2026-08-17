namespace Onikiri.Progression
{
    /**
     * @brief 대요괴의 혼 하나와, 그 혼을 봉인해 벼려낸 요도(妖刀) 하나.
     *
     * 혼과 요도를 한 구조체에 담는 이유는 **일대일이기 때문**이다. 혼 종류가
     * 곧 요도 종류이고, 그 사이에 조합이나 선택이 없다 - "등롱의 혼을 봉인하면
     * 등롱도가 된다"가 전부다. 둘을 나눠 표 두 벌로 만들면 그 일대일을 코드가
     * 스스로 지킬 수 없고, 언젠가 혼만 있고 칼이 없는 종류가 생긴다.
     *
     * 조합(혼 둘을 섞어 다른 칼)은 이 스텝의 범위 밖이다. 그날이 오면 이
     * 구조체가 갈라지고, 갈라지는 것이 그 스텝의 첫 일이 된다.
     */
    public struct YodoSpec
    {
        /**
         * @brief 세이브와 애셋이 이 문자열로 갈린다.
         *
         * 표의 순서가 바뀌어도 봉인 상태가 섞이지 않는다 - 강화 축·오의·장비·
         * 동료가 전부 같은 규칙이고, 이유도 같다(SaveData 머리 주석).
         *
         * **BossConfig.soulId가 이 값을 가리킨다.** 보스 애셋이 "내가 어떤 혼을
         * 남기는가"를 스스로 말하는 것이 요점이다 - 스테이지 번호로 매핑하면
         * 42단계의 세계 순환(BossRoster.RegionForStage)에서 곧바로 어긋난다.
         */
        public string Id;

        /** 화면에 뜨는 혼 이름. "외눈 등롱의 혼" */
        public string SoulName;

        /** 화면에 뜨는 요도 이름. "등롱도" */
        public string BladeName;

        /**
         * @brief 이 혼을 남기는 대요괴의 이름. **도감의 잠긴 미리보기가 적는다.**
         *
         * BossConfig.displayName의 사본이다. 사본을 두는 이유는 카탈로그가
         * 순수 C#이어야 하기 때문이고(밸런스는 시뮬레이션이 씬 없이 읽는다),
         * 갈리는 것은 테스트가 막는다
         * (YodoTests.Catalog_MatchesTheBossAssets).
         */
        public string BossName;

        /**
         * @brief 이 혼이 드랍될 확률 (0~1).
         *
         * ## 지금은 넷 다 1이다. 그리고 그것이 설계다
         *
         * 대요괴는 **한 바퀴(40스테이지)에 한 번만** 만난다. 여기에 확률을
         * 걸면 빗나간 한 번이 40스테이지를 통째로 지운다 - 그 크기의 손실은
         * 긴장이 아니라 사고로 읽힌다.
         *
         * 확률은 **싸게 반복할 수 있는 출처**의 것이다. 그 출처(보석·현금 뽑기)가
         * 다음 스텝의 일이고, 이 필드는 그때 1이 아닌 값을 받는다. 지금 자리를
         * 만들어 두는 이유는 런타임 경로(YodoSystem.ReportBossDefeated)가 확률을
         * 이미 굴리고 있어야 그 스텝이 표만 고치면 되기 때문이다.
         *
         * 1이 아닌 값이 들어오면 시뮬레이션은 기댓값으로 세고 실제는 굴린다 -
         * 둘이 갈리는 순간이고, YodoTests가 그 사실을 못 박는다.
         */
        public double DropChance;

        /** 아이콘 (Kyrise 아이템 시트의 스프라이트 이름). 에디터 빌더만 쓴다 */
        public string IconSprite;

        // ------------------------------------------------------------ 45단계: 상성

        /**
         * @brief 이 혼이 강화하는 오의의 id (SkillCatalog). **비면 전 오의다.**
         *
         * ## 왜 혼마다 다른 오의인가 - 이 축을 스탯 막대에서 빌드로 바꾸는 자리
         *
         * 44단계의 요도는 자루가 넷이지만 하는 일이 하나였다(공격력 x배). 넷을
         * 구분하는 것이 이름과 아이콘뿐이라, "어느 자루에 파편을 몰까"라는
         * 질문의 답이 언제나 "아무거나"였다 - 선택지가 넷인데 선택이 없었다.
         *
         * 상성이 그 질문에 답을 만든다. 파편은 지갑 하나를 넷이 나눠 쓰므로
         * (YodoCurve.ShardCostAtTier) 한 자루를 밀면 나머지가 늦는데, 이제 그
         * 결정이 **어느 오의가 세지는가**를 정한다. 새 재화도 새 레벨도 없이
         * 빌드가 생기는 이유가 이것이다 - 세기는 그 요도의 티어에 자동으로
         * 연동된다(YodoAffinityCurve).
         *
         * ## 넷 대 셋의 불일치 - 남는 하나는 전 오의를 소폭 민다
         *
         * 혼은 넷이고 오의는 셋이다. 두 혼이 같은 오의를 물게 하는 안이 가장
         * 쉬웠지만 택하지 않았다 - 그러면 그 오의만 두 배로 자라고, 나머지
         * 둘을 고를 이유가 사라진다. 빌드 다양성을 만들려고 넣은 축이 오히려
         * 정답을 하나로 줄인다.
         *
         * 평타(발도 기본타)에 붙이는 안도 버렸다. **티어 배수와 구분되지
         * 않기 때문이다** - 요도 티어는 이미 공격력에 곱해지고(UpgradeSystem.
         * Apply), 평타 상성은 같은 자리에 같은 방식으로 한 번 더 곱하는 것이라
         * 화면에서 두 값이 한 값으로 읽힌다. 죽은 버튼이 아니라 **보이지 않는
         * 버튼**이다.
         *
         * 남은 답이 "전 오의를 소폭"이고, 그것이 흑야다. 넷 중 **유일하게
         * 자신도 검을 쓰는 요괴**(다크 사무라이)라 발도 전체가 그의 것이라는
         * 설정이 값과 같은 말을 한다. 곡선도 갈린다 - 셋에 걸리므로 한 자루
         * 몫보다 훨씬 얕다(YodoAffinityCurve.BroadSealStep).
         */
        public string AffinitySkillId;

        /**
         * @brief 같은 혼의 **가족**에 드는 두 번째 오의 (49단계). 비면 없다.
         *
         * ## 왜 한 혼이 둘을 무는가
         *
         * 45단계는 "두 혼이 같은 오의를 물면 안 된다"고 적었다 - 그 오의만
         * 두 배로 자라고 나머지 혼을 고를 이유가 사라지기 때문이다. 이쪽은
         * **반대 방향**이라 그 함정에 안 걸린다.
         *
         * 49단계에 장착 슬롯이 생기면서 질문이 하나 늘었다: "네 자리에 무엇을
         * 끼우는가." 한 혼이 오의 둘을 밀면 그 혼을 민 플레이어에게는 **두
         * 자리의 답이 함께 정해진다** - 몰아주기가 슬롯 구성의 모양까지 바꾼다.
         * 고르게 미는 플레이어(f2p 기대 곡선)에게는 신규 다섯이 여전히 동률이라
         * 아무것도 안 바뀐다.
         *
         * 가족은 **거동으로** 묶는다. 등롱이 귀참을 문 이유가 "사방을 비추는
         * 눈 = 화면 전체"였으므로, 같은 Screen인 혈파동이 같은 이유로 든다.
         * 설정이 값과 같은 말을 하는 것이 45단계의 규칙이고 그대로 따른다.
         *
         * 흑야는 비어 있다 - 전 오의를 미는 혼에는 가족이 따로 없다.
         */
        public string AffinityFamilyId;

        /**
         * @brief 소환됐을 때 화면에 뜨는 이름. "등롱의 영체"
         *
         * 혼 이름과 따로 두는 이유는 둘이 다른 물건이기 때문이다 - 혼은
         * 대장간의 재료이고 영체는 전장에 서는 것이다. "등롱의 혼이 나타났다"고
         * 적으면 재료가 걸어 나온 것으로 읽힌다.
         */
        public string SpiritName;

        /**
         * @brief 아이콘 틴트. 흰색이면 아트 그대로.
         *
         * 시트의 칼 네 자루는 은/금/적/은(청 손잡이)이라 셋은 그대로 쓰면
         * 되는데, **흑야도만 검은 날이 없다.** 곱연산 틴트로 만든다 - 은색
         * 날(거의 흰색)에 짙은 보라를 곱하면 검푸른 날이 되고, 그것이 이
         * 프로젝트가 배경 재틴트에서 확인한 성질이다(35단계: 곱연산은 원본에
         * 없는 채널을 못 만들지만, **줄이는 것은 언제나 된다**).
         *
         * 셋을 흰색으로 두는 이유는 아트의 색이 이미 그 요괴의 색이기
         * 때문이다. 통일감을 위해 넷 다 물들이면 금색 등롱도가 죽는다.
         */
        public UnityEngine.Color IconTint;
    }

    /**
     * @brief 요괴 봉인 검(妖刀) 표. 네 대요괴의 혼과 네 자루의 요도.
     *
     * ## 왜 넷인가 - 지역이 넷이기 때문이다
     *
     * 임의로 고른 수가 아니라 BossRoster가 정한 수다. 각 지역의 피날레가
     * 그 지역의 대요괴이고, 42단계의 세계 순환으로 그 넷이 40스테이지마다
     * 한 바퀴 돈다. 다섯째 혼을 만들려면 다섯째 지역이 먼저 있어야 한다.
     *
     * **챕터 보스(정예)는 혼을 남기지 않는다.** 정예는 지역마다 같은 놈이고
     * (Region_*.asset이 넷 다 Boss_Elite를 문다), 이름도 "정예"다 - 고유한
     * 요괴가 아니라 등급이다. 고유하지 않은 것의 혼을 봉인하면 도감의 한 줄이
     * "그 지역의 무엇"을 가리키지 못한다. 대신 정예는 **파편**을 남긴다
     * (YodoCurve.ShardsPerElite) - 합성의 재료는 고유할 필요가 없다.
     *
     * ## 왜 ScriptableObject가 아닌가
     *
     * EquipmentCatalog·PetCatalog와 같은 이유다. 이 값들은 아트가 아니라
     * **밸런스**이고, 밸런스는 StageSimulation이 씬 없이 읽어야 한다.
     *
     * ## 순서가 곧 순환 순서다
     *
     * 표의 i번째가 지역 (i+1)의 피날레다. 한 바퀴 안에서 혼이 등롱 -> 처형인
     * -> 적안 -> 흑야 순으로 들어오고, 그 순서가 도감의 줄 순서이자
     * YodoCurve.ExpectedSoulsAtStage의 위상이다. 표를 재배열하면 그 셋이
     * 함께 움직인다 - 그래서 재배열하지 않는다.
     */
    public static class YodoCatalog
    {
        public const string LanternId = "yodo_lantern";
        public const string ExecutionerId = "yodo_executioner";
        public const string RedEyeId = "yodo_redeye";
        public const string DarkSamuraiId = "yodo_darksamurai";

        public static readonly YodoSpec[] Blades =
        {
            new YodoSpec {
                Id = LanternId,
                SoulName = "등롱의 혼",
                BladeName = "등롱도",
                BossName = "외눈 등롱",
                DropChance = 1d,

                // 등롱은 **하나의 큰 눈으로 사방을 비추는** 요괴다. 귀참도
                // 화면 전체를 한 번에 벤다(SkillShape.Screen) - 같은 모양의
                // 사건이라 상성이 설정에서 나온다
                AffinitySkillId = SkillCatalog.OniCleaveId,

                // 49단계: 같은 Screen 거동인 혈파동이 같은 이유로 이 혼의 가족이다
                AffinityFamilyId = SkillCatalog.BloodWaveId,
                SpiritName = "등롱의 영체",

                IconSprite = YodoSprites.LanternSprite,
                IconTint = UnityEngine.Color.white
            },
            new YodoSpec {
                Id = ExecutionerId,
                SoulName = "처형인의 혼",
                BladeName = "참수도",
                BossName = "처형인",
                DropChance = 1d,

                // 처형은 **한 번에 끝내는 일**이다. 일섬은 단발 관통이고
                // (SkillShape.Pierce) 셋 중 유일하게 돌진이 붙는다
                AffinitySkillId = SkillCatalog.FlashId,

                // 49단계: 낙혈도 전방 관통(Pierce)이다 - 한 줄로 끝내는 쪽
                AffinityFamilyId = SkillCatalog.BloodFallId,
                SpiritName = "처형인의 영체",

                IconSprite = YodoSprites.ExecutionerSprite,
                IconTint = UnityEngine.Color.white
            },
            new YodoSpec {
                Id = RedEyeId,
                SoulName = "적안의 혼",
                BladeName = "적안도",

                // 애셋 파일명과 화면 이름이 교차한다 - 지역 3 피날레는
                // Boss_DarkSamurai.asset인데 화면에는 "붉은눈 요괴"로 뜬다
                // (35단계, 사용자가 화면을 보고 확정했고 GUID 때문에 파일명은
                // 유지). 여기 적는 것은 **화면 이름**이다 - 도감이 "○○ 처치 시
                // 해금"이라고 말할 때 플레이어가 대조하는 것이 화면이기 때문이다
                BossName = "붉은눈 요괴",
                DropChance = 1d,

                // 붉은 눈은 광분이다. 연참은 셋 중 유일한 다타
                // (SkillShape.MultiHit, 세 번) - 몰아치는 쪽에 붙는다
                AffinitySkillId = SkillCatalog.ChainSlashId,

                // 49단계: 혈륜은 다섯 번 도는 다타다 - 광분의 가족
                AffinityFamilyId = SkillCatalog.BloodWheelId,
                SpiritName = "적안의 영체",

                IconSprite = YodoSprites.RedEyeSprite,
                IconTint = UnityEngine.Color.white
            },
            new YodoSpec {
                Id = DarkSamuraiId,
                SoulName = "흑야의 혼",
                BladeName = "흑야도",
                BossName = "다크 사무라이",
                DropChance = 1d,

                // **비어 있다 = 전 오의.** 넷 중 유일하게 자신도 검을 쓰는
                // 요괴라 발도 전체가 그의 것이다. 넷 대 셋의 불일치를 여기서
                // 푸는 이유는 YodoSpec.AffinitySkillId 주석에 있다
                AffinitySkillId = null,

                // 전 오의를 미는 혼에는 가족이 따로 없다. 신규 다섯도 구조상
                // 자동으로 받는다(FactorForSkill이 빈 대상을 전부로 읽는다)
                AffinityFamilyId = null,
                SpiritName = "흑야의 영체",

                IconSprite = YodoSprites.DarkSamuraiSprite,

                // 은색 날에 짙은 보라를 곱해 검푸른 날을 만든다. 위 IconTint 주석 참고
                IconTint = new UnityEngine.Color(0.42f, 0.36f, 0.62f, 1f)
            }
        };

        public static int Count { get { return Blades.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Blades.Length; i++)
                if (Blades[i].Id == id) return i;
            return -1;
        }

        public static YodoSpec Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? Blades[index] : default(YodoSpec);
        }

        /**
         * @brief 넷을 다 봉인했을 때 완성되는 칼의 이름.
         *
         * 게임의 이름이다. 이것이 도감의 마지막 줄이고, 이 스텝이 향하는
         * 곳이다 - "요괴를 벤 칼이 그 요괴를 삼킨다"의 끝.
         *
         * **장비 5등급 이름과 겹치지 않게 32단계 쪽을 옮겼다**
         * (EquipmentCatalog: 오니키리 -> 명공검). 대장간 한 화면에 같은
         * 이름의 다른 물건이 둘 서 있으면 그것은 두 시스템이 아니라 버그로
         * 읽힌다. 옮긴 쪽이 장비인 이유는 그쪽 사다리가 **사람이 벼린 칼**의
         * 사다리이기 때문이다 - 인간 대장장이의 끝이 명공검이고, 그 위는
         * 대장간이 아니라 요괴가 연다.
         */
        public const string OnikiriName = "오니키리";
    }

    /**
     * @brief 요도 아이콘의 스프라이트 이름. **런타임 어셈블리에 둔다.**
     *
     * UiSprites(EquipmentCatalog.cs)와 같은 자리, 같은 이유다 - 에디터 타입을
     * 참조하면 카탈로그가 빌드에서 빠진다.
     *
     * 넷 다 Kyrise 아이템 시트에 이미 잘려 있는 조각이다. 요도 전용 아트를
     * 굽지 않은 이유는 값싼 콘텐츠 원칙이고(35단계 지역 4가 같은 판단),
     * 아이콘이 서로 다르기만 하면 도감의 네 줄은 네 자루로 읽힌다.
     */
    public static class YodoSprites
    {
        public const string LanternSprite = "kyrise_yodo_lantern";
        public const string ExecutionerSprite = "kyrise_yodo_executioner";
        public const string RedEyeSprite = "kyrise_yodo_redeye";
        public const string DarkSamuraiSprite = "kyrise_yodo_darksamurai";

        /** 혼 카운터와 도감 머리글이 쓰는 혼 아이콘 */
        public const string SoulSprite = "kyrise_soul";

        /** 파편 카운터가 쓰는 아이콘 */
        public const string ShardSprite = "kyrise_shard";
    }
}
