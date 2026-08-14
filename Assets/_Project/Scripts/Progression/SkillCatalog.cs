using System;

namespace Onikiri.Progression
{
    /**
     * @brief 오의가 데미지를 **어떻게 뿌리는가**.
     *
     * 27단계에 생겼다. 26단계에는 셋 다 단일 대상 한 방이었고, 색만 다른 같은
     * 아크가 떴다 - 화면에서 "무엇이 나갔는지"가 구분되지 않았고 그것이 뽕맛이
     * 없다는 소감의 원인이었다.
     *
     * **총 데미지는 바뀌지 않는다.** 배율·쿨다운·상한은 26단계 값 그대로이고,
     * 이 enum이 정하는 것은 그 데미지를 시간(다타)과 공간(관통·광역)에 어떻게
     * 펴는가뿐이다. 그래서 밸런스 가드가 성립한다 - **보스는 언제나 단일
     * 대상**이므로 어느 거동이든 보스에게 들어가는 총량이 같다.
     */
    public enum SkillShape
    {
        /**
         * @brief 한 대상을 여러 번. 총 배율을 HitCount로 나눈다.
         *
         * 나눗셈이라 합이 원래 배율과 정확히 같아야 한다. 마지막 타격이
         * **나머지를 받는다**(SkillCatalog.HitDamageShare) - 배율을 셋으로
         * 나눠 셋을 더하면 부동소수점에서 원래 값과 미세하게 어긋나고,
         * 그 어긋남이 "총 데미지 불변"을 검사할 수 없게 만든다.
         */
        MultiHit,

        /** 전방 일렬 관통. 경로의 모든 대상이 **각자 총 배율**을 받는다 */
        Pierce,

        /** 화면 전체. 살아 있는 모든 대상이 각자 총 배율을 받는다 */
        Screen
    }

    /**
     * @brief 발도 오의 하나의 설계값.
     *
     * 곡선은 셋이 공유하고(SkillCurve), 여기 있는 것은 그 곡선의 **시작점과
     * 리듬**뿐이다. 스킬 사이의 차이는 전부 `BaseMultiplier / CooldownSeconds`
     * 하나에서 나온다 - 그것이 이 축이 DPS에 기여하는 전부이기 때문이다.
     */
    public struct SkillSpec
    {
        /** 세이브와 UI가 이 문자열로 갈린다. 순서가 바뀌어도 레벨이 섞이지 않는다 */
        public string Id;

        public string DisplayName;

        /** 레벨 1의 배율. 공격력에 곱해진다 */
        public double BaseMultiplier;

        /**
         * @brief 시전 간격 (초). **성장 축이 아니다 - 끝까지 고정이다.**
         *
         * 쿨다운을 레벨로 줄이면 9단계 공격속도와 같은 벽에 부딪힌다. 자세한
         * 이유는 SkillCurve 참고.
         */
        public double CooldownSeconds;

        /**
         * @brief 이 캐릭터 레벨부터 목록에 열린다. **0이면 스테이지 게이트다.**
         *
         * 49단계에 두 번째 게이트가 생겼다. 신규 오의 다섯은 레벨이 아니라
         * **최전선 스테이지**로 열린다(UnlockStage). 이유는 이 스텝 전체의
         * 기둥이고 44단계 요도가 st41을 고른 것과 같은 이유다 - 코리더(st1~30)와
         * 가속 구간(st31~50)의 밸런스를 **비트 단위로** 지키려면 새 힘이 그
         * 구간 밖에서만 들어와야 하는데, 레벨 게이트는 그것을 보증하지 못한다.
         * 레벨은 스테이지와 느슨하게만 묶여 있어서(경험치·재선택·방치) "Lv.55는
         * 언제나 st51 이후"라고 말할 수 없다.
         *
         * 기존 셋은 레벨 게이트 그대로다. 그 셋의 해금 순간이 곧 26단계에
         * 정해진 리듬이고, 여기서 게이트 종류를 바꾸면 코리더가 움직인다.
         */
        public int UnlockLevel;

        /** 레벨이 아니라 최전선 스테이지로 열리는가 */
        public bool StageGated { get { return UnlockLevel <= 0; } }

        /**
         * @brief 진행이 아니라 **뽑기**로 열리는가 (50단계).
         *
         * 49단계는 가챠 몫 둘(혈폭·혈조)을 다른 신규 셋과 같은 문(st51)에
         * 걸어 두고 "다음 스텝의 뽑기가 그 자리"라고 적었다. 50단계가 그
         * 자리를 채우면서 게이트의 **출처**가 바뀐다 - 최전선이 아니라
         * 보유(SkillGachaSystem)가 연다.
         *
         * `StageGated`를 끄지 않고 그 위에 얹는 이유는 자리 수 때문이다.
         * `SkillCurve.SlotsFor`는 "레벨 게이트가 아닌 오의는 자리를 안 연다"로
         * 적혀 있고(StageGated), 가챠 몫도 자리를 열면 안 된다 - 뽑을 때마다
         * 자리가 늘면 슬롯 예산이 아무것도 안 막게 되고 49단계가 밴드에
         * 못 박아 둔 한 칸이 통째로 풀린다.
         *
         * 그래서 두 플래그의 관계가 이렇게 갈린다:
         *
         *     StageGated   자리를 여는가          (아니오 - 둘 다)
         *     GachaGated   무엇이 이 오의를 여는가 (최전선 / 보유)
         *
         * UnlockStage는 그대로 남는다. 열리는 자리가 아니라 **골드 비용의
         * 기준점**이 되는데(SkillSpec.UnlockStage 주석), 뽑기로 열리는 오의의
         * "열리는 스테이지"는 확률이라 정해지지 않으므로 **가장 이른 획득
         * 가능 시점**인 상점 해금 칸을 쓴다.
         */
        public bool GachaGated;

        /**
         * @brief 위 레벨에 실제로 닿는 스테이지. **실측값이고 비용의 기준점이다.**
         *
         * 해금은 레벨로 걸리는데 비용은 골드 규모에 맞춰야 한다. 골드는 스테이지마다
         * x1.72로 자라므로(StageCurve.GoldGrowth), 어느 스테이지에서 열리는지를
         * 모르면 "비싸다/싸다"를 말할 수 없다.
         *
         * 이 값이 틀리면 비용이 통째로 어긋난다 - 한 스테이지만 틀려도 1.72배다.
         * 그래서 선언으로 두지 않고 `Skills_UnlockWhereTheCostAssumes` 가
         * 시뮬레이션 실측과 대조한다. 골드 축의 StagesToCeiling과 같은 처리다.
         */
        public int UnlockStage;

        /** 아이콘 파일명 (KURAI 팩). 에디터 빌더만 쓴다 */
        public string IconFile;

        /**
         * @brief 이 오의가 뿌리는 이펙트 클립의 이름 (VfxLibrary). 비면 팩 참격이다.
         *
         * 49단계에 생겼다. 기존 셋은 팩 참격(Slashes)과 클립 자체의 궤적을 쓰므로
         * 비어 있고, 신규 다섯은 48단계 하베스트(혈조·혈파)와 Pozac 팩에서 구운
         * 조각을 가리킨다.
         *
         * **이 값이 오의마다 유일해야 한다.** 색이 유일한 구분자였던 27단계와
         * 달리, 여덟 오의는 혈(血) 한 계열을 나눠 쓰므로 색만으로는 못 가른다 -
         * 그림이 두 번째 구분자이고, 그림까지 같으면 두 오의가 화면에서 한
         * 사건으로 읽힌다. SkillShapeTests가 유일성을 못 박는다.
         */
        public string VfxId;

        // ------------------------------------------------------------ 27단계: 거동

        /** 데미지를 어떻게 뿌리는가. 총량은 바꾸지 않는다 */
        public SkillShape Shape;

        /**
         * @brief MultiHit의 타격 수. 나머지 거동은 1이다.
         *
         * 클립에 실제로 그려진 참격 프레임 수와 같아야 한다 - 23단계가 확인한
         * 대로 원화가가 그린 궤적이 정답이고, 그 프레임보다 많이 때리면 그림 없는
         * 타격이 생긴다. 연참은 ATTACK 1/2/3을 이어 붙여 f4·f10·f16 세 곳이다.
         */
        public int HitCount;

        /**
         * @brief 무게 등급 0~2. 히트스톱·셰이크·숫자 크기의 단계를 정한다.
         *
         * 배율 순서(연참 < 일섬 < 귀참)와 같아야 한다. 다르면 화면에서 가장
         * 무겁게 느껴지는 오의가 실제로 가장 센 오의가 아니게 되고, 그것은
         * 숫자보다 강한 거짓말이다.
         */
        public int Weight;

        /**
         * @brief 이 오의의 색. 참격 아크 · 이름 플래시 · 데미지 숫자가 **함께** 쓴다.
         *
         * 셋이 같은 색이어야 한 사건으로 읽힌다. 각자 색을 갖고 있으면 화면이
         * 오의 하나에 세 가지 색을 쓰게 되고, 그러면 색이 아무것도 가리키지 않는다.
         *
         * ## 흰 -> 적 -> 금인데 값이 그 이름과 조금 다른 이유
         *
         * 무기 등급 톤(23단계에 `Slash_White.png`을 남겨둔 이유)이 설계이지만,
         * **화면에 이미 있는 색을 피해야 한다.** 27단계에 27단계 색을 그대로 잡았다가
         * 실측으로 물렸다:
         *
         * ```
         * 연참 #FFF4E4  vs  평타 데미지 #FFF4D6   거의 같다  -> 구분 불가
         * 일섬 #FF6A4A  vs  처치 데미지 #FF8A7A   가깝다
         * 귀참 #FFD34D  vs  치명타 데미지 #FFD34D  완전히 같다 -> 치명타율 60% 구간에서
         *                                          화면의 큰 숫자 대부분이 이 색이다
         * ```
         *
         * 그래서 같은 계열 안에서 밀었다. 흰은 **차가운 쪽**으로(창백한 청백),
         * 적은 **더 선명한 쪽**으로, 금은 **더 깊은 쪽**으로(호박빛). 등급 순서는
         * 그대로 읽히고, 기존 세 색과는 나란히 놓아도 갈린다.
         *
         * 한 칸만 미는 것으로는 부족했다 - 흰을 #E8F4FF로 잡았을 때도 평타와의
         * 거리가 0.18이라 테스트가 잡았다. 흰과 크림색은 RGB에서 원래 가까워서,
         * 청색을 실제로 섞어야(#A8D8FF) 갈린다. `SkillShapeTests`가 여섯 쌍의
         * 거리를 전부 재고, 최소 거리 0.25가 그 기준이다.
         *
         * ## 남는 것 하나 - 일섬과 타격 불꽃
         *
         * 일섬 #FF4A3A는 평타 불꽃 #FF453A와 거리 0.02다. 거의 같다. 그래도 두는
         * 이유는 두 신호가 다른 층위에 있기 때문이다 - 불꽃은 12px짜리로 "여기
         * 맞았다"를 찍고, 일섬은 3.0u 아크와 x2 숫자와 이름으로 나온다. 크기가
         * 스물다섯 배 다르면 색이 같아도 같은 것으로 읽히지 않는다.
         *
         * 데미지 숫자 팔레트만 검사에 넣은 것도 그래서다. 숫자는 불꽃과 같은
         * 크기·같은 자리에 뜨므로 색이 유일한 구분자이지만, 아크는 아니다.
         *
         * 값은 Color가 아니라 32비트 RGBA다. 이 어셈블리는 UnityEngine을 참조하지만
         * 그래도 밸런스 표에 색 구조체를 섞지 않는 편이 낫다 - 이 배열은
         * 시뮬레이션과 테스트가 함께 읽는 곳이다.
         */
        public uint SlashRgba;

        /** 초당 환산 기여. 이 축이 DPS에 하는 일의 전부다 */
        public double BaseRate { get { return CooldownSeconds > 0d ? BaseMultiplier / CooldownSeconds : 0d; } }

        /**
         * @brief 첫 구매 비용. 두 가지에 비례한다.
         *
         *   초당 기여    셋의 골드당 효율을 같게 만든다 (SkillCurve.CostPerRate)
         *   해금 시점의 골드 규모  셋이 각자 열리는 자리에서 같은 무게를 갖게 한다
         *
         * 두 번째가 빠져 있었을 때 무슨 일이 났는지 적어둔다. 세 스킬의 비용을
         * 42/60/78로 두고 돌렸더니, **일섬과 귀참이 해금된 스테이지에서 곧바로
         * 상한까지 팔렸다.** st15의 초당 수입은 st8의 서른여섯 배라 60골드짜리
         * 첫 칸이 사실상 공짜였기 때문이다. 화면에서는 "새 스킬이 열렸는데 열자마자
         * MASTER"로 나타난다.
         *
         * 절대 골드 액수는 게임 어디에서도 고정된 뜻이 없다. 뜻을 갖는 것은
         * **그 시점의 수입 대비 몇 초치인가**뿐이고, 수입은 스테이지마다 x1.72로
         * 자란다. 그래서 해금 스테이지의 골드 배수를 곱한다.
         */
        public double BaseCost
        {
            get
            {
                return BaseRate * SkillCurve.CostPerRate
                       * StageCurve.GoldMultiplier(UnlockStage).ToDouble();
            }
        }
    }

    /**
     * @brief 발도 오의 여덟. 자동 시전이고 골드로 레벨을 올린다.
     *        **그중 장착한 것만 나간다**(49단계).
     *
     * ## 49단계 - 셋에서 여덟으로, 그리고 슬롯
     *
     * 26단계에 셋이었던 이유("셋이면 한 화면에 들어가고 리듬의 세 자리가
     * 채워진다")는 여전히 맞다. 그래서 넷째를 그냥 더하지 않았다 - **장착
     * 슬롯**을 만들고, 화면에서 도는 것은 여전히 서너 개로 묶었다.
     *
     * 슬롯이 있어야 하는 진짜 이유는 화면이 아니라 밸런스다. 신규 오의를 전부
     * 상시 발동으로 더하면 DPS가 오의 수에 **선형으로** 불어난다. 다음 스텝의
     * 스킬 뽑기가 풀을 계속 키울 예정이므로, 그것은 뽑을 때마다 밴드 천장을
     * 미는 구조 - 47단계가 "+28%를 두어 번 더 쌓으면 밴드가 무의미"라고 적어둔
     * 그 부채다.
     *
     * 슬롯이면 **밴드가 보는 것이 슬롯 예산 하나**다. 풀이 여덟이 되든 스물이
     * 되든 장착 수가 그대로면 DPS가 안 자란다. 이 스텝이 지는 빚은 슬롯이
     * 셋에서 넷이 되는 **한 칸뿐이고, 그 한 칸은 여기서 끝난다.**
     *
     * ## 신규 다섯의 초당 기여가 전부 같은 이유 - 두 마리 토끼
     *
     * 다섯 다 `배율/쿨 = 0.180`이다(연참과 같은 값). 우연이 아니라 두 가지를
     * 동시에 얻으려고 맞춘 값이다.
     *
     * **하나. 밴드가 닫힌 식으로 적힌다.** 4번째 슬롯에 무엇을 끼우든 상한
     * 기여 합이 같으므로, 기대 곡선이 "플레이어가 무엇을 골랐는가"를 몰라도
     * 된다. 고르는 것이 밴드를 움직이면 밴드는 최댓값을 가정할 수밖에 없고,
     * 그러면 나머지 선택지는 전부 함정이 된다.
     *
     * **둘. 뽑기가 파워를 못 판다.** 다섯이 동률이면 "더 센 오의"라는 상품이
     * 존재하지 않는다. 뽑기가 파는 것은 거동과 상성 집이지 숫자가 아니고,
     * 그것이 f2p 바닥을 구조로 지킨다 - 45단계가 상성을 "새 축이 아니라 이미
     * 있는 티어의 두 번째 읽는 법"으로 만든 것과 같은 수법이다.
     *
     * 그럼 무엇이 갈리는가: **쿨다운 리듬(6~19초)·거동·상성 집**이다. 배율이
     * 쿨다운에 비례해 따라오므로 긴 쿨은 한 방이 크고 짧은 쿨은 자주 터진다.
     *
     * ## 상성 집 - 세 혼이 각자 가족을 얻는다
     *
     * 45단계는 혼 셋이 오의 셋을 하나씩 물었다. 49단계는 그 셋을 **가족**으로
     * 넓힌다 - 거동이 같은 신규 오의가 같은 혼에 든다:
     *
     *     등롱(Screen)    귀참 · 혈파동
     *     처형인(Pierce)  일섬 · 낙혈
     *     적안(MultiHit)  연참 · 혈륜
     *     흑야·백면       전 오의 (구조상 신규도 자동으로 받는다)
     *
     * 한 혼에 오의 둘을 묶는 것은 45단계가 **거부한** 반대 방향과 다르다.
     * 저쪽이 거부한 것은 "두 혼이 같은 오의를 문다"이고(그러면 그 오의만
     * 두 배로 자라 나머지를 고를 이유가 사라진다), 이쪽은 한 혼을 밀면
     * **슬롯 두 자리가 함께 값을 갖는다** - 몰아주기가 빌드의 모양을 정한다.
     *
     * 가챠 몫으로 예약한 둘(혈폭·혈조)에는 전담 혼이 없다. 결함이 아니라
     * 값이다: 몰아주기 빌드에서는 언제나 진행 해금 오의가 4번 슬롯의 답이고,
     * **뽑기로 얻는 것이 진행으로 얻는 것보다 세지 않다.**
     *
     * ## 왜 전부 공격형인가
     *
     * 버프나 생존기를 하나 섞는 안도 있었지만 택하지 않았다. 이유는 **자**다 -
     * 공격형은 전부 초당 환산 기여 하나로 잴 수 있어서 여섯 축과 같은 저울에
     * 올라가지만(UpgradeEfficiency), 버프는 %DPS로, 생존기는 %EHP로 재야 한다.
     * 11단계에 생존 축이 SurvivalEfficiency로 갈라지고 20단계에 골드 축이
     * GoldGainEfficiency로 또 갈라졌다. 자가 넷째로 갈라지는 값은 스킬 셋 중
     * 하나를 다르게 만드는 것보다 크다.
     *
     * 생존기는 전직이나 장비에서 다시 볼 자리다.
     *
     * ## 자동 시전
     *
     * 쿨다운이 돌면 알아서 나간다. 방치형에서 "눌러야 나가는 오의"는 화면을
     * 보고 있는 사람에게만 주는 보상이라, 자리를 비우는 플레이어의 DPS가
     * 시뮬레이션과 갈린다. 수동 탭 보너스는 그 위에 얹는 별개 층이고 이번
     * 범위 밖이다.
     *
     * ## 해금이 Lv.10 / 15 / 20 인 이유
     *
     * 하단 탭이 12단계부터 "스킬 Lv.10"으로 서 있었고 그것이 약속이다. 나머지
     * 둘을 같이 열지 않는 이유는 두 가지다 - 셋을 한꺼번에 열면 그 순간 DPS가
     * 한 번에 뛰어 보스 여유 밴드에 계단이 생기고, 열린 뒤에는 새로 열릴 것이
     * 30레벨(전직)까지 없다.
     *
     * 5레벨 간격은 실측으로 대략 3~4스테이지다. 전직(Lv.30)과 겹치지 않는다.
     */
    public static class SkillCatalog
    {
        public const string ChainSlashId = "skill_chain";
        public const string FlashId = "skill_flash";
        public const string OniCleaveId = "skill_oni";

        // ------------------------------------------------------------ 49단계

        /** 진행 해금 셋 */
        public const string BloodWaveId = "skill_bloodwave";
        public const string BloodFallId = "skill_bloodfall";
        public const string BloodWheelId = "skill_bloodwheel";

        /**
         * @brief 뽑기가 여는 둘 (50단계). 전담 혼이 없다 - 위 머리 주석 참고.
         *
         * 49단계에는 "가챠 몫으로 예약"이었고 게이트는 st51이었다. 50단계가
         * 그 예약을 실제 게이트로 바꾼다(GachaGated) - 진행으로는 영원히
         * 안 열리는 것이 이 둘의 정의다.
         */
        public const string BloodBurstId = "skill_bloodburst";
        public const string BloodWhipId = "skill_bloodwhip";

        /**
         * @brief 신규 다섯이 공유하는 초당 환산 기여.
         *
         * 연참과 같은 값이다. 왜 다섯이 동률인지는 머리 주석의 "두 마리 토끼"에
         * 있고, 이 상수를 두는 이유는 **표에서 눈으로 확인할 수 없기 때문**이다 -
         * 표에는 배율과 쿨다운만 적히고 그 비는 계산해야 나온다. 테스트가 이
         * 값과 대조한다(SkillAxisTests.NewSkills_ShareOneRate).
         */
        public const double ExpansionRate = 0.180d;

        /**
         * @brief 진행 해금 셋이 열리는 최전선 (49b).
         *
         * ## 왜 코리더 안인가 - 49단계는 셋 다 st51이었다
         *
         * 49단계는 다섯을 전부 4번 자리와 같은 문(st51)에 걸었다. 재기준이 한
         * 번으로 끝나는 대신 **코리더 서른 스테이지가 오의 셋 그대로**였고,
         * "새 오의를 배운다"는 비트가 게임의 절반이 지나서야 왔다.
         *
         * 자리를 램프로 바꾸면서(SkillCurve.SlotsFor) 그 제약이 사라졌다 -
         * 코리더의 자리는 언제나 차 있으므로 새 오의는 **바꿔 끼우는 것**이고,
         * 다섯이 동률이라 바꾼 결과가 같다. 파워가 아니라 **선택**만 온다.
         *
         * ## 자리를 고른 방식 - 기본 셋 사이에 끼운다
         *
         *     st8  연참(기본)   st12 혈파동   st15 일섬(기본)
         *     st18 낙혈         st21 귀참(기본)  st27 혈륜
         *
         * 기본 오의의 해금(8/15/21)은 **자리가 하나 늘어나는 순간**이고 신규의
         * 해금은 **고를 것이 하나 늘어나는 순간**이다. 둘을 번갈아 두면 서로
         * 다른 종류의 사건이 3~6스테이지마다 온다. 같은 스테이지에 겹치지
         * 않게 한 칸씩 띄웠다 - 겹치면 둘 중 하나는 안 읽힌다.
         *
         * 가챠 몫 둘(혈폭·혈조)은 여기 없다. 진행으로 안 열리는 것이 그 둘의
         * 정의이고, 다음 스텝의 뽑기가 그 자리다.
         */
        public const int BloodWaveStage = 12;
        public const int BloodFallStage = 18;
        public const int BloodWheelStage = 27;

        /**
         * 배율과 쿨다운의 크기를 고른 근거.
         *
         *   연참 x0.54 / 3초  = 0.180   자주 터지는 잔 오의
         *   일섬 x1.50 / 6초  = 0.250   중간
         *   귀참 x3.52 /11초  = 0.320   가장 무겁다
         *
         * ## 밸런스 리듬 재조정 - 배율과 쿨다운을 같은 비로 눌렀다
         *
         * 처음 값은 연참 1.26/7초 · 일섬 3.25/13초 · 귀참 7.04/22초였다.
         * "쿨다운 20초 안팎" 규칙으로 잡았는데 실전에서는 **전투가 답답했다** -
         * 슬롯 두엇의 초반 구성에서 오의가 4~5초에 한 번 나왔다.
         *
         * 그래서 각 오의의 배율과 쿨다운을 **같은 비율로** 절반 안팎으로
         * 눌렀다(연참 3/7, 일섬 6/13, 귀참 1/2). 비를 지키면 초당 기여
         * (BaseRate = 배율/쿨)가 소수 그대로 보존되므로:
         *
         *   - 스킬 DPS 총량이 안 움직인다 (해금 몫 4~5%, 상한 합 2.40 그대로)
         *   - 밴드·시뮬레이션·보스 게이트가 이 축에서 아무것도 못 느낀다
         *   - 골드 효율(BaseCost = BaseRate x 150 x 골드 배수)도 저절로 그대로다
         *
         * 바뀐 것은 리듬뿐이다: 초반 구성(연참, +일섬)에서 2~3초에 한 번,
         * 한 방은 가벼워지고 자주 터진다. 역할 순서(빠름 -> 무거움)와 상대
         * 강약은 그대로다 - 귀참은 여전히 11초에 한 번 오는 결정타다.
         *
         * 합이 0.75다. 해금 시점에는 공격속도가 이미 상한(3.88)이라 스킬 하나가
         * 열릴 때 DPS의 4~5%를 맡고, 그것이 **한 칸의 체감이 0.5%를 넘는 최소
         * 크기**다(SkillCurve.CeilingRatio 주석의 산수).
         *
         * 상한(x3.2)에서 합이 2.40이 되어 공격속도 상한 3.88 아래에 머문다.
         *
         * 쿨다운의 하한은 이펙트다 - 클립이 쿨다운의 25% 안에 끝나야 한다는
         * 검사(SkillVfxTests)가 있어서, 0.36초짜리 whip을 쓰는 혈조도 1.5초
         * 밑으로는 못 내려간다. 2.5초는 그 위에 여유를 둔 값이다.
         */
        public static readonly SkillSpec[] Skills =
        {
            new SkillSpec {
                Id = ChainSlashId, DisplayName = "연참",
                BaseMultiplier = 0.54d, CooldownSeconds = 3d,
                UnlockLevel = 10, UnlockStage = 8,
                IconFile = "Icon076",           // 붉은 타일 + 흰 삼연 참격
                SlashRgba = 0xA8D8FFFFu,        // 창백한 청백 (평타 #FFF4D6 과 거리 0.39)
                Shape = SkillShape.MultiHit, HitCount = 3, Weight = 0
            },
            new SkillSpec {
                Id = FlashId, DisplayName = "일섬",
                BaseMultiplier = 1.50d, CooldownSeconds = 6d,
                UnlockLevel = 15, UnlockStage = 15,
                IconFile = "Icon140",           // 어두운 타일 + 붉은 단발 참격
                SlashRgba = 0xFF4A3AFFu,        // 선명한 적 (처치 #FF8A7A 과 거리 0.36)
                Shape = SkillShape.Pierce, HitCount = 1, Weight = 1
            },
            new SkillSpec {
                Id = OniCleaveId, DisplayName = "귀참",
                BaseMultiplier = 3.52d, CooldownSeconds = 11d,
                UnlockLevel = 20, UnlockStage = 21,
                IconFile = "Icon118",           // 오니 뿔
                SlashRgba = 0xFF9500FFu,        // 깊은 호박빛 금 (치명타 #FFD34D 과 거리 0.39)
                Shape = SkillShape.Screen, HitCount = 1, Weight = 2
            },

            // ---------------------------------------------------------- 49단계
            //
            // 다섯 다 `배율/쿨 = 0.180`이다. 표에서는 안 보이므로 옆에 적어 둔다.
            // (리듬 재조정에서 다섯 모두 배율·쿨을 같은 비 1/2로 눌렀다 -
            //  비가 같으므로 0.180 동률이 소수 그대로 남는다)
            //
            //   혈파동  1.44/8.0 = 0.180   지면 파동 (Screen)     등롱 가족
            //   낙혈    0.99/5.5 = 0.180   전방 관통 (Pierce)     처형인 가족
            //   혈륜    0.81/4.5 = 0.180   회전 다타 (MultiHit)   적안 가족
            //   혈폭    1.71/9.5 = 0.180   단발 버스트            가챠 몫
            //   혈조    0.45/2.5 = 0.180   원거리 단타            가챠 몫
            //
            // 쿨다운이 2.5·4.5·5.5·8·9.5초라 기존 셋(3·6·11)의 사이를 메운다.
            // 해금은 앞의 셋이 **코리더 안**(st12/18/27, 49b)이고 뒤의 둘은
            // **뽑기**다(50단계, GachaGated). 어느 쪽도 레벨 게이트가 아니라
            // 자리를 열지 않는다 - 코리더와 가속 구간을 비트 단위로 지키는
            // 것이 그 두 스텝의 공통된 기둥이다.

            new SkillSpec {
                Id = BloodWaveId, DisplayName = "혈파동",
                BaseMultiplier = 1.44d, CooldownSeconds = 8d,
                UnlockLevel = 0, UnlockStage = BloodWaveStage,
                IconFile = "Icon058",           // 붉은 타일 + 사방으로 퍼지는 방사
                SlashRgba = 0xE8446EFFu,        // 선명한 로즈
                Shape = SkillShape.Screen, HitCount = 1, Weight = 1,
                VfxId = SkillVfx.WaveRing
            },
            new SkillSpec {
                Id = BloodFallId, DisplayName = "낙혈",
                BaseMultiplier = 0.99d, CooldownSeconds = 5.5d,
                UnlockLevel = 0, UnlockStage = BloodFallStage,
                IconFile = "Icon056",           // 위에서 떨어지는 핏줄기
                SlashRgba = 0xB02060FFu,        // 짙은 자적
                Shape = SkillShape.Pierce, HitCount = 1, Weight = 1,
                VfxId = SkillVfx.Wave
            },
            new SkillSpec {
                Id = BloodWheelId, DisplayName = "혈륜",
                BaseMultiplier = 0.81d, CooldownSeconds = 4.5d,
                UnlockLevel = 0, UnlockStage = BloodWheelStage,
                IconFile = "Icon062",           // 회전하는 톱니 고리
                SlashRgba = 0xC8304CFFu,        // 하베스트의 중심색
                Shape = SkillShape.MultiHit, HitCount = 5, Weight = 0,
                VfxId = SkillVfx.Vortex
            },
            // 아래 둘은 **뽑기가 연다**(50단계). UnlockStage가 st41인 것은
            // 열리는 자리가 아니라 골드 비용의 기준점이고, 뽑기로만 열리는
            // 오의의 "가장 이른 획득 가능 시점"이 곧 상점이 열리는 칸이다
            // (SkillSpec.GachaGated 주석).
            new SkillSpec {
                Id = BloodBurstId, DisplayName = "혈폭",
                BaseMultiplier = 1.71d, CooldownSeconds = 9.5d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon083",           // 터져 오르는 폭발 기둥
                SlashRgba = 0xD81E7AFFu,        // 자홍
                Shape = SkillShape.MultiHit, HitCount = 1, Weight = 2,
                VfxId = SkillVfx.Burst
            },
            new SkillSpec {
                Id = BloodWhipId, DisplayName = "혈조",
                BaseMultiplier = 0.45d, CooldownSeconds = 2.5d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon082",           // 휘어 감기는 갈고리
                SlashRgba = 0x96285EFFu,        // 어두운 자적
                Shape = SkillShape.Pierce, HitCount = 1, Weight = 0,
                VfxId = SkillVfx.Whip
            }
        };

        /**
         * @brief 신규 오의가 가리키는 이펙트 클립의 이름.
         *
         * **런타임 어셈블리에 둔다.** YodoSprites·UiSprites와 같은 자리, 같은
         * 이유다 - 에디터 타입(PozacVfxBaker·YokaiVfxBaker)을 참조하면 카탈로그가
         * 빌드에서 빠진다. 굽는 쪽의 상수와 갈리면 테스트가 잡는다.
         */
        public static class SkillVfx
        {
            /** 48단계 하베스트 (Inimig 9 colo 2의 오의 블록) */
            public const string Wave = "wave";
            public const string Whip = "whip";

            /** 49단계 Pozac 팩에서 구운 것 */
            public const string WaveRing = "pozac_wave_ring";
            public const string Burst = "pozac_burst";
            public const string Vortex = "pozac_vortex";
        }

        /**
         * @brief MultiHit에서 index번째 타격이 받는 배율.
         *
         * **마지막 타격이 나머지를 받는다.** 총 배율을 N으로 나눠 N번 더하면
         * 부동소수점에서 원래 값과 미세하게 어긋나는데, 그러면 "총 데미지 불변"을
         * 검사할 수가 없다 - 오차인지 설계 변경인지 구분되지 않는다.
         *
         * 나눗셈을 한 번만 하고 마지막에서 빼면 합이 **정확히** 원래 배율이다.
         *
         * @param hitIndex 0부터. HitCount - 1 이 마지막
         */
        public static double HitDamageShare(int skillIndex, int hitIndex, double totalMultiplier)
        {
            if (skillIndex < 0 || skillIndex >= Skills.Length) return totalMultiplier;

            var spec = Skills[skillIndex];
            int hits = spec.Shape == SkillShape.MultiHit ? Math.Max(1, spec.HitCount) : 1;
            if (hits <= 1) return totalMultiplier;

            double share = totalMultiplier / hits;
            if (hitIndex < hits - 1) return share;

            // 마지막: 앞선 것들을 빼서 합을 정확히 맞춘다
            return totalMultiplier - share * (hits - 1);
        }

        /** 이 오의가 한 번 시전될 때 나가는 타격 수 (단일 대상 기준) */
        public static int HitsPerCast(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= Skills.Length) return 1;
            var spec = Skills[skillIndex];
            return spec.Shape == SkillShape.MultiHit ? Math.Max(1, spec.HitCount) : 1;
        }

        public static int Count { get { return Skills.Length; } }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Skills.Length; i++)
                if (Skills[i].Id == id) return i;
            return -1;
        }

        /**
         * @brief 이 캐릭터 레벨에서 열려 있는가. **스테이지 게이트는 잠긴 것으로 센다.**
         *
         * 최전선을 안 받는 옛 호출부가 신규 오의를 열지 않게 하려는 것이다.
         * 조용히 여는 쪽이 아니라 조용히 잠그는 쪽으로 떨어뜨린다 - 잠긴 것은
         * 화면에서 곧바로 보이지만, 열려서는 안 될 것이 열린 것은 밸런스가
         * 어긋난 뒤에야 드러난다.
         */
        public static bool IsUnlockedAt(int index, int characterLevel)
        {
            return IsUnlockedAt(index, characterLevel, 0);
        }

        /**
         * @brief 이 레벨·최전선에서 열려 있는가.
         *
         * @param frontierStage 최전선 스테이지(37단계 maxStageReached). 지금
         *                      서 있는 스테이지가 아니다 - 아래로 되돌아가서
         *                      오의가 잠기면 그것은 해금이 아니라 벌이다
         */
        public static bool IsUnlockedAt(int index, int characterLevel, int frontierStage)
        {
            return IsUnlockedAt(index, characterLevel, frontierStage, 0);
        }

        /**
         * @brief 이 레벨·최전선·**보유 상태**에서 열려 있는가 (50단계).
         *
         * @param gachaOwned 카탈로그 인덱스 비트마스크. 뽑기로 얻은 오의의
         *                   비트가 서 있다. **0이 기본값이고 그것이 계약이다** -
         *                   마스크를 안 넘긴 호출부에서는 가챠 몫이 잠겨 보인다.
         *
         * 조용히 잠그는 쪽으로 떨어뜨리는 것은 49단계가 스테이지 게이트에서
         * 내린 판단 그대로다 - 잠긴 것은 화면에서 곧바로 보이지만, 열려서는
         * 안 될 것이 열린 것은 밸런스가 어긋난 뒤에야 드러난다.
         *
         * 그리고 이 기본값이 **밴드의 불변을 구조로 지킨다.** 기준 구성
         * (ReferenceLoadout)과 심층 구성(DeepLoadout)이 마스크 0으로 지어지므로
         * 가챠 몫은 밴드가 가정하는 세계에 아예 없고, 49단계가 잰 오의 몫
         * 49.0%가 한 비트도 안 움직인다.
         */
        public static bool IsUnlockedAt(int index, int characterLevel, int frontierStage,
                                        int gachaOwned)
        {
            if (index < 0 || index >= Skills.Length) return false;

            var spec = Skills[index];
            if (spec.GachaGated) return (gachaOwned & (1 << index)) != 0;
            if (spec.StageGated) return frontierStage >= spec.UnlockStage;
            return characterLevel >= spec.UnlockLevel;
        }

        /**
         * @brief 첫 스킬이 열리는 레벨. 하단 "스킬" 버튼이 켜지는 조건이다.
         *
         * **레벨 게이트만 센다.** 스테이지 게이트의 UnlockLevel은 0이라, 같이
         * 세면 이 값이 0이 되고 하단 탭이 처음부터 열린다
         */
        public static int PanelUnlockLevel
        {
            get
            {
                int lowest = int.MaxValue;
                foreach (var skill in Skills)
                    if (!skill.StageGated && skill.UnlockLevel < lowest) lowest = skill.UnlockLevel;
                return lowest == int.MaxValue ? 1 : lowest;
            }
        }

        /** 이 레벨에서 이 스킬의 초당 환산 기여. 잠겨 있으면 0 */
        public static double RateAt(int index, int skillLevel, int characterLevel)
        {
            return RateAt(index, skillLevel, characterLevel, 0);
        }

        public static double RateAt(int index, int skillLevel, int characterLevel, int frontierStage)
        {
            return RateAt(index, skillLevel, characterLevel, frontierStage, 0);
        }

        public static double RateAt(int index, int skillLevel, int characterLevel,
                                    int frontierStage, int gachaOwned)
        {
            if (!IsUnlockedAt(index, characterLevel, frontierStage, gachaOwned)) return 0d;

            var spec = Skills[index];
            if (spec.CooldownSeconds <= 0d) return 0d;

            return SkillCurve.CappedMultiplierAtLevel(spec.BaseMultiplier, skillLevel)
                   / spec.CooldownSeconds;
        }

        /** 상한까지 올린 이 오의의 초당 환산 기여. 장착 순서를 정하는 값이다 */
        public static double CeilingRateOf(int index)
        {
            if (index < 0 || index >= Skills.Length) return 0d;

            var spec = Skills[index];
            if (spec.CooldownSeconds <= 0d) return 0d;
            return SkillCurve.CeilingFor(spec.BaseMultiplier) / spec.CooldownSeconds;
        }

        // ---------------------------------------------------------------- 장착 (49단계)

        /**
         * @brief **밴드가 보는 장착 구성.** 기대 곡선과 시뮬레이션이 함께 읽는다.
         *
         * ## 왜 "고르는 것"이 여기 상수처럼 적히는가
         *
         * 장착은 플레이어의 선택인데 밴드는 하나의 곡선을 지켜야 한다. 보통
         * 이런 자리는 "최댓값을 가정"으로 풀리고, 그러면 나머지 선택지가 전부
         * 함정이 된다(20단계 골드 축의 교훈).
         *
         * 여기서는 그 문제가 **없다**. 신규 다섯의 상한 기여가 전부 같으므로
         * (ExpansionRate) 4번 슬롯에 무엇을 끼우든 이 함수가 내는 기여 합이
         * 같다. 고르는 것이 밴드를 안 움직이고, 그것이 슬롯 설계의 배당금이다.
         *
         * 순서는 **상한 기여 내림차순, 같으면 표 순서**다. 실제 레벨이 아니라
         * 상한을 보는 이유는 순서가 흔들리면 안 되기 때문이다 - 지금 레벨로
         * 정렬하면 아직 안 산 오의가 영원히 장착되지 않고(비장착에는 골드를
         * 안 쓰므로) 레벨이 안 올라 영원히 순서가 안 바뀐다.
         *
         * @param characterLevel 레벨 게이트를 재는 값
         * @param frontierStage  스테이지 게이트와 슬롯 수를 정하는 값
         * @param equipped       채워 줄 배열. 길이는 SkillCurve.MaxSlots 이상
         * @return 실제로 채워진 슬롯 수
         */
        /**
         * @brief 초당 기여가 "같다"고 볼 상대 오차.
         *
         * 1e-9는 double의 정밀도(1e-16)보다 일곱 자리 크고, 이 게임에서 뜻이
         * 있는 차이(가장 가까운 두 오의가 0.180 대 0.250)보다 여덟 자리 작다.
         * 그 사이 아무 값이나 같은 결과를 낸다.
         */
        private const double TieEpsilon = 1e-9d;

        /**
         * @brief 이 기여가 지금까지의 최선보다 **뜻이 있게** 큰가.
         *
         * 게임 쪽(SkillSystem.FillEmptySlots)이 같은 함수를 지난다. 두 곳이
         * 각자 부등호를 쓰면 화면의 기본 구성과 밴드가 가정하는 구성이
         * 부동소수점 잡음으로 갈릴 수 있고, 그 갈림은 화면에도 로그에도
         * 안 남는다.
         */
        public static bool IsBetterRate(double rate, double best)
        {
            return rate > best * (1d + TieEpsilon);
        }

        public static int ReferenceLoadout(int characterLevel, int frontierStage, int[] equipped)
        {
            return ReferenceLoadout(characterLevel, frontierStage, equipped,
                                    SkillCurve.SlotsAtStage(frontierStage));
        }

        /**
         * @param maxSlots 열린 자리 수를 밖에서 정한다. 4번 슬롯이 없는 비교군
         *                 (죽은 버튼 검사)이 셋으로 눌러 부른다
         */
        public static int ReferenceLoadout(int characterLevel, int frontierStage,
                                           int[] equipped, int maxSlots)
        {
            return ReferenceLoadout(characterLevel, frontierStage, equipped, maxSlots, 0);
        }

        /**
         * @param gachaOwned 뽑기로 얻은 오의의 비트마스크 (50단계).
         *
         * **기본값 0이 밴드가 보는 세계다.** 가챠 몫 둘은 초당 기여가 신규
         * 셋과 동률이고(ExpansionRate) 동률은 표 순서로 갈리므로, 마스크를
         * 켜도 기준 구성은 안 바뀐다 - 표에서 뒤에 서 있기 때문이다. 그래도
         * 마스크를 받는 이유는 **화면 쪽 기본 구성**(SkillSystem.FillEmptySlots)이
         * 같은 함수를 지나야 하기 때문이고, 그 두 곳이 갈리면 아무것도 안
         * 만진 플레이어가 밴드 밖에 선다.
         */
        public static int ReferenceLoadout(int characterLevel, int frontierStage,
                                           int[] equipped, int maxSlots, int gachaOwned)
        {
            if (equipped == null) return 0;

            int slots = Math.Min(maxSlots, equipped.Length);
            if (slots < 0) slots = 0;
            int filled = 0;

            for (int slot = 0; slot < slots; slot++)
            {
                int best = -1;
                double bestRate = 0d;

                for (int i = 0; i < Skills.Length; i++)
                {
                    if (!IsUnlockedAt(i, characterLevel, frontierStage, gachaOwned)) continue;

                    bool taken = false;
                    for (int f = 0; f < filled; f++)
                        if (equipped[f] == i) { taken = true; break; }
                    if (taken) continue;

                    double rate = CeilingRateOf(i);

                    // **동률 판정에 여유를 둔다.** 신규 다섯의 초당 기여는
                    // 설계상 정확히 같은 값이지만(ExpansionRate), 배율과
                    // 쿨다운을 따로 적어 나눈 결과라 부동소수점에서 마지막
                    // 비트가 갈린다 - 1.44/8과 0.99/5.5가 정확히 같은 double이
                    // 아니다. 맨 부등호로 두면 그 잡음이 장착 순서를 정하게
                    // 되고, 실제로 그렇게 나왔다(혈파동 대신 낙혈이 뽑혔다).
                    //
                    // 여유를 두면 동률은 표 순서로 갈린다 - 뜻이 있는 규칙이
                    // 뜻이 없는 잡음을 이긴다
                    if (!IsBetterRate(rate, bestRate)) continue;

                    best = i;
                    bestRate = rate;
                }

                if (best < 0) break;
                equipped[filled++] = best;
            }

            for (int i = filled; i < equipped.Length; i++) equipped[i] = -1;
            return filled;
        }

        /**
         * @brief 심층(모든 오의 해금 + 슬롯 4)의 장착 구성. 기대 곡선이 읽는다.
         *
         * 캐시해 두는 이유는 이 값이 상수이기 때문이다 - 심층에서는 레벨도
         * 최전선도 게이트를 이미 지났다. YodoAffinityCurve가 매 호출마다 다시
         * 정렬하지 않도록 한 번만 짓는다.
         */
        public static readonly int[] DeepLoadout = BuildDeepLoadout();

        /**
         * @brief 4번 슬롯이 열리기 **전**의 구성. 기존 셋이다.
         *
         * 기대 곡선이 st41~50 구간을 물을 때 쓴다 - 요도는 st41에 봉인되기
         * 시작하는데 4번 슬롯은 st51에 열리므로, 그 열 스테이지 동안 상성은
         * 존재하고 자리는 셋이다. 두 값이 겹치는 이 구간을 놓치면 기대 곡선이
         * 실측과 0.25% 갈리고(실제로 갈렸다), 그 차이가 그대로 보정과 실제의
         * 차가 된다.
         */
        public static readonly int[] BaseLoadout = BuildBaseLoadout();

        private static int[] BuildBaseLoadout()
        {
            var slots = new int[SkillCurve.MaxSlots];
            int count = ReferenceLoadout(int.MaxValue, SkillCurve.ExpansionStage - 1,
                                         slots, SkillCurve.BaseSlots);

            var trimmed = new int[count];
            Array.Copy(slots, trimmed, count);
            return trimmed;
        }

        /**
         * @brief 이 최전선에서 밴드가 가정하는 구성.
         *
         * 기대 곡선(YodoCurve.ExpectedPowerFactorAtStage)과 시뮬레이션이
         * **같은 집합**을 봐야 둘이 안 갈린다.
         */
        public static int[] LoadoutAtStage(int frontierStage)
        {
            return frontierStage >= SkillCurve.ExpansionStage ? DeepLoadout : BaseLoadout;
        }

        /** 이 구성이 상한에서 내는 초당 환산 기여의 합 */
        public static double CeilingRateOf(int[] loadout)
        {
            if (loadout == null) return 0d;

            double rate = 0d;
            for (int slot = 0; slot < loadout.Length; slot++) rate += CeilingRateOf(loadout[slot]);
            return rate;
        }

        private static int[] BuildDeepLoadout()
        {
            var slots = new int[SkillCurve.MaxSlots];
            int count = ReferenceLoadout(int.MaxValue, int.MaxValue, slots);

            var trimmed = new int[count];
            Array.Copy(slots, trimmed, count);
            return trimmed;
        }

        /** 이 구성에 이 오의가 들어 있는가 */
        public static bool IsEquipped(int[] equipped, int index)
        {
            if (equipped == null) return false;
            for (int i = 0; i < equipped.Length; i++)
                if (equipped[i] == index) return true;
            return false;
        }

        /**
         * @brief 지금 스킬 전체가 만드는 초당 환산 공격 횟수.
         *
         * CombatStats가 공격속도에 **더하는** 값이다. 자동 공격 한 대가 공격력
         * x1이고 스킬 한 번이 공격력 x배율이므로, 쿨다운으로 나누면 두 값이
         * 같은 단위가 된다 - 그래서 더할 수 있다.
         *
         * 치명타와 스탯 포인트 증폭은 여기 들어가지 않는다. 곱해지는 자리가
         * 괄호 밖(공격력, 치명타 계수)이라 자동으로 상속된다. 지시가 요구한
         * "치명타·증폭 상속"이 구조에서 나온다.
         */
        public static double CastRate(int[] skillLevels, int characterLevel)
        {
            if (skillLevels == null) return 0d;

            double rate = 0d;
            int count = Math.Min(skillLevels.Length, Skills.Length);
            for (int i = 0; i < count; i++)
                rate += RateAt(i, skillLevels[i], characterLevel);

            return rate;
        }
    }
}
