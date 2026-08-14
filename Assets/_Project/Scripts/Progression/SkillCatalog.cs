using System;

namespace Onikiri.Progression
{
    /**
     * @brief 오의가 데미지를 **어디에** 뿌리는가.
     *
     * 27단계에 생겼다. 26단계에는 셋 다 단일 대상 한 방이었고, 색만 다른 같은
     * 아크가 떴다 - 화면에서 "무엇이 나갔는지"가 구분되지 않았고 그것이 뽕맛이
     * 없다는 소감의 원인이었다.
     *
     * **총 데미지는 바뀌지 않는다.** 배율·쿨다운·상한은 26단계 값 그대로이고,
     * 거동이 정하는 것은 그 데미지를 시간(다타)과 공간(관통·광역)에 어떻게
     * 펴는가뿐이다. 그래서 밸런스 가드가 성립한다 - **보스는 언제나 단일
     * 대상**이므로 어느 거동이든 보스에게 들어가는 총량이 같다.
     *
     * ## 15종 재설계에 축이 셋으로 갈렸다
     *
     * 27단계의 `SkillShape`는 범위와 분할을 한 열거형에 담았다(MultiHit / Pierce /
     * Screen). 여덟일 때는 충분했는데 열다섯이 되면서 표현할 수 없는 조합이 생겼다:
     *
     *     검진      전방 장판을 **네 틱**에 나눠  (Field  x MultiHit)
     *     귀신난무  화면 전체를 **다섯 번**       (Screen x MultiHit)
     *
     * 한 축에서는 둘 중 하나만 고를 수 있으므로 축을 나눈다. 그리고 데미지 공식
     * 밖의 거동(흡인·처형)은 셋째 축(SkillSpecial)이 맡는다 - 그것까지 여기 섞으면
     * 열거형이 열 칸 넘게 늘고, 무엇보다 **총량을 바꾸는 것과 안 바꾸는 것**이
     * 한 축에 서게 된다.
     *
     *     SkillArea     어디를 때리는가   (총량 불변)
     *     SkillSplit    몇 번에 나누는가  (총량 불변)
     *     SkillSpecial  그 밖의 거동      (총량 불변 - 위치와 연출만)
     */
    public enum SkillArea
    {
        /** 최근접 하나. 27단계의 MultiHit이 서 있던 자리다 */
        Single,

        /** 전방 일렬 관통. 경로의 모든 대상이 **각자 총 배율**을 받는다 */
        Pierce,

        /** 화면 전체. 살아 있는 모든 대상이 각자 총 배율을 받는다 */
        Screen,

        /**
         * @brief 시전자 중심 원. **뒤쪽도 본다** - Pierce와 갈리는 유일한 점이다.
         *
         * 관통은 `dx ∈ [-0.3, range]`로 앞만 보는데(PlayerCombat.DeliverSkillLane),
         * 원은 거리로 재므로 시전자 뒤 반경까지 든다. 그리고 **가까울수록 세로가
         * 넓다** - 원의 성질이고, 그것이 이 거동의 값이다(떠 있는 도깨비불).
         */
        Around,

        /**
         * @brief 전방 장판. **시간에 걸쳐 여러 번 판정한다.**
         *
         * Pierce와 판정 모양은 같지만(가로 x 세로 창) 한 번에 끝나지 않는다.
         * 장판은 **위치에 걸린다** - 대상이 죽어도 재타깃하지 않고, 그 자리에
         * 들어온 다른 적이 남은 틱을 받는다.
         */
        Field,

        /**
         * @brief 특수 거동이 **미리 확정한 목록**만. 지금은 흡인(Pull)뿐이다.
         *
         * Screen과 갈라 두는 이유가 밸런스다. 화면 전체에 주면 같은 초당 기여로
         * 광역 오의보다 무조건 나아지고(같은 광역 + 끌어모으기), 그것은
         * "뽑기가 파워를 안 판다"를 잡몹 층에서 깨는 것이다. 목록의 상한
         * (PullTargetCount)이 그 자리를 막는다.
         */
        Captured
    }

    /**
     * @brief 총 배율을 **몇 번에 나눠** 넣는가.
     *
     * 27단계에는 이것이 SkillArea와 한 축에 섞여 있었다(SkillShape.MultiHit).
     * 그 세계에서는 "화면 전체를 다섯 번"이나 "장판을 네 틱"을 적을 수가 없었다 -
     * 열거형 하나가 범위와 분할을 동시에 말하므로 둘 중 하나만 고를 수 있었다.
     *
     * 쪼개면서 기존 여덟의 뜻은 한 톨도 안 바뀐다. `MultiHit`이던 셋은
     * `Single + MultiHit`이 되고 나머지는 `+ Once`가 붙을 뿐이다.
     * `SkillRosterContractTests`가 그 등가를 비트 단위로 잠근다.
     */
    public enum SkillSplit
    {
        /** 한 번에 총 배율 전부 */
        Once,

        /**
         * @brief HitCount 번에 나눠 넣는다. **합이 정확히 총 배율이어야 한다.**
         *
         * 나눗셈이라 마지막 타격이 **나머지를 받는다**(SkillCatalog.HitDamageShare) -
         * 배율을 셋으로 나눠 셋을 더하면 부동소수점에서 원래 값과 미세하게
         * 어긋나고, 그 어긋남이 "총 데미지 불변"을 검사할 수 없게 만든다.
         */
        MultiHit
    }

    /**
     * @brief 데미지 공식 **밖**의 거동. 이름이나 VfxId로 추측하지 않는다.
     *
     * 이 축이 없으면 구현이 반드시 문자열 추측으로 흐른다 - "id가
     * `skill_abyss_pull`이면 끌어당긴다" 같은 코드가 생기고, 그것은 id를 바꾸는
     * 날(혹은 비슷한 접두사가 생기는 날) 조용히 틀린다. `skill_oni`·
     * `skill_oni_dance`·`skill_oni_advent`가 이미 접두사를 나눠 쓰고 있다.
     *
     * **피해량은 이 축이 안 건드린다.** 끌어당김은 위치를 옮기고, 처형은 연출을
     * 튼다. 둘 다 총 배율은 그대로다 - 그래야 초당 환산 기여 하나로 잴 수 있고,
     * 그 저울이 ExpansionRate 동률 계약의 근거다.
     */
    public enum SkillSpecial
    {
        None,

        /**
         * @brief 대상을 골라 한 점으로 끌어모은다 (나락인력).
         *
         * 시전 시각에 목록을 **확정(capture)**하고 그 목록만 피해를 받는다
         * (SkillArea.Captured). 흡인 중에 죽은 대상은 목록에서 빠지되
         * **총량을 남은 대상에 몰아주지 않는다** - 몰아주면 적이 적을수록
         * 세지는 오의가 되고, 그것은 총량 계약이 아니다.
         */
        Pull,

        /**
         * @brief 처형 연출 판정 (참수). **피해량을 안 바꾼다.**
         *
         * 실제 즉사는 남은 체력과 무관한 처치라 초당 환산 기여로 잴 수가 없다.
         * 그 순간 이 오의만 저울 밖에 서고 ExpansionRate 계약이 깨진다.
         * 그래서 남긴 것은 연출뿐이고, 조건은 ExecutionThreshold가 정한다.
         */
        Execution
    }

    /**
     * @brief 오의의 **전투 계열**. 뽑기 희귀도(GachaCurve.Grade)와 다른 축이다.
     *
     * ## 왜 셋째 축이 필요한가
     *
     * 로스터가 열다섯이 되면 목록 한 화면에 다 안 들어간다. 나누는 자를 무엇으로
     * 할 것인가에서 **뽑기 희귀도는 답이 아니다** - 진행으로 열리는 여섯(연참·일섬·
     * 귀참·혈파동·낙혈·혈륜)에는 희귀도가 아예 없고, 있다 해도 그것은 "얼마나
     * 얻기 어려운가"이지 "무엇을 하는가"가 아니다.
     *
     * 계열은 **전투에서의 역할**로 나눈다. 검식은 짧고 잦게(쿨 2.5~7초), 귀오의는
     * 길고 무겁게(11~19초), 혈식은 그 사이에서 변칙을 맡는다.
     *
     * ## 이름·id로 추측하지 않는다
     *
     * `skill_blood*`가 전부 혈식인 것은 우연에 가깝다 - 혈조·혈폭은 뽑기 몫이고
     * 혈파동·낙혈·혈륜은 진행 몫이라 두 무리의 성질이 갈리는데, 문자열은 그 둘을
     * 같게 본다. 그리고 신규 일곱 중 `skill_oni_dance`·`skill_oni_advent`는 접두사가
     * 귀참(`skill_oni`)과 겹친다 - 문자열로 가르면 `StartsWith`가 셋을 한 무리로
     * 묶는다.
     *
     * 그래서 **명시적 데이터**로 둔다. 45단계가 상성을 혼 id로 못 박은 것과 같은
     * 자리이고, 같은 이유다.
     *
     * ## 표시 이름이 여기 없는 이유
     *
     * 열거형 이름(SwordForm)은 코드가 읽는 것이고 화면에 뜨는 것은 "검식"이다.
     * 그 문자열은 아래 FamilyNames 표에 있고, 그 표를 문자셋 하베스트가 읽는다
     * (FontCharsetBuilder.DisplayNames) - 화면에 서는 글자의 출처가 하나여야
     * 아틀라스에 □이 안 생긴다.
     */
    public enum SkillFamily
    {
        /** 검식 - 빠르고 안정적인 검술 */
        SwordForm,

        /** 혈식 - 피와 요괴의 힘, 변칙 기술 */
        BloodForm,

        /** 귀오의 - 흐름을 바꾸는 필살기 */
        OniSecret
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

        /**
         * @brief 이 오의의 전투 계열. **명시적 데이터다 - id로 추측 금지**
         *
         * 이유는 SkillFamily 머리 주석에 있다. 기본값이 SwordForm(0)인 것은
         * 열거형의 첫 칸이라 그런 것뿐이고 뜻이 없다 - 표의 모든 행이 이 필드를
         * 명시해야 하고, `SkillRosterContractTests`가 빠진 행을 잡는다.
         */
        public SkillFamily Family;

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

        // ------------------------------------------------------------ 거동 (27단계 -> 15종 재설계)

        /** 어디를 때리는가. 총량은 바꾸지 않는다 */
        public SkillArea Area;

        /** 총 배율을 몇 번에 나눠 넣는가. 총량은 바꾸지 않는다 */
        public SkillSplit Split;

        /** 데미지 공식 밖의 거동. 기본은 None이고, 표의 모든 행이 명시한다 */
        public SkillSpecial Special;

        /**
         * @brief `SkillSpecial.Pull`이 한 번에 잡는 최대 대상 수.
         *
         * **`SkillSpec`에 있는 이유는 이것이 총량 계약이기 때문이다.** 흡인 범위나
         * 도착 위치는 화면 배치라 안무(Choreography)에 있지만, "몇 명이 맞는가"는
         * 이 오의가 잡몹 무리에 넣는 총량의 상한이고 그 값을 테스트가 읽어야 한다.
         *
         * Pull이 아닌 오의에서는 0이고 아무 뜻이 없다.
         */
        public int PullTargetCount;

        /**
         * @brief `SkillSpecial.Execution`의 빈사 판정 기준 (체력 비율).
         *
         * 공격 **전** 체력이 이 비율 이하이거나 이 공격으로 죽으면 처형 연출이
         * 뜬다. 연출만 바뀌고 피해량은 그대로다 - 그 등호를 계약 테스트가
         * 잰다(TheExecution_DoesNotChangeDamage).
         *
         * 보스는 이 판정을 아예 안 지난다. 값이 아니라 규칙이라 여기 안 적는다.
         */
        public double ExecutionThreshold;

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

        // ------------------------------------------------------------ 15종 재설계

        /**
         * @brief 신규 일곱. **전부 뽑기가 연다**(GachaGated).
         *
         * ## id 관례가 갈리는 것을 알고 둔다
         *
         * 기존 혈식 다섯은 `skill_bloodwave`처럼 붙여 쓰고 이 일곱은
         * `skill_deep_thrust`처럼 밑줄로 끊는다. 통일하려면 기존 id를 바꿔야
         * 하는데, 세이브가 이 문자열로 레벨과 보유를 가르므로(SaveData 주석)
         * 바꾸는 순간 기존 플레이어의 진행이 사라진다. **표기 일관성보다 그
         * 대가가 크다** - 새로 추가되는 것만 밑줄 규칙을 따르고 이 주석이
         * 이유를 남긴다.
         *
         * 그리고 접두사로 계열을 추측하면 안 되는 이유가 여기 그대로 보인다 -
         * `skill_oni`(귀참)·`skill_oni_dance`·`skill_oni_advent` 셋이 접두사를
         * 나눠 쓴다. `StartsWith`로 가르면 셋이 한 무리가 된다(SkillFamily 주석).
         */
        public const string DeepThrustId = "skill_deep_thrust";
        public const string MoonArcId = "skill_moon_arc";
        public const string SwordFieldId = "skill_sword_field";
        public const string OniDanceId = "skill_oni_dance";
        public const string AbyssPullId = "skill_abyss_pull";
        public const string DecapitateId = "skill_decapitate";
        public const string OniAdventId = "skill_oni_advent";

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
                Id = ChainSlashId, DisplayName = "연참", Family = SkillFamily.SwordForm,
                BaseMultiplier = 0.54d, CooldownSeconds = 3d,
                UnlockLevel = 10, UnlockStage = 8,
                IconFile = "Icon076",           // 붉은 타일 + 흰 삼연 참격
                SlashRgba = 0xA8D8FFFFu,        // 창백한 청백 (평타 #FFF4D6 과 거리 0.39)
                Area = SkillArea.Single, Split = SkillSplit.MultiHit, HitCount = 3, Weight = 0,
                Special = SkillSpecial.None
            },
            new SkillSpec {
                Id = FlashId, DisplayName = "일섬", Family = SkillFamily.SwordForm,
                BaseMultiplier = 1.50d, CooldownSeconds = 6d,
                UnlockLevel = 15, UnlockStage = 15,
                IconFile = "Icon140",           // 어두운 타일 + 붉은 단발 참격
                SlashRgba = 0xFF4A3AFFu,        // 선명한 적 (처치 #FF8A7A 과 거리 0.36)
                Area = SkillArea.Pierce, Split = SkillSplit.Once, HitCount = 1, Weight = 1,
                Special = SkillSpecial.None
            },
            new SkillSpec {
                Id = OniCleaveId, DisplayName = "귀참", Family = SkillFamily.OniSecret,
                BaseMultiplier = 3.52d, CooldownSeconds = 11d,
                UnlockLevel = 20, UnlockStage = 21,
                IconFile = "Icon118",           // 오니 뿔
                SlashRgba = 0xFF9500FFu,        // 깊은 호박빛 금 (치명타 #FFD34D 과 거리 0.39)
                Area = SkillArea.Screen, Split = SkillSplit.Once, HitCount = 1, Weight = 2,
                Special = SkillSpecial.None
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
                Id = BloodWaveId, DisplayName = "혈파동", Family = SkillFamily.BloodForm,
                BaseMultiplier = 1.44d, CooldownSeconds = 8d,
                UnlockLevel = 0, UnlockStage = BloodWaveStage,
                IconFile = "Icon058",           // 붉은 타일 + 사방으로 퍼지는 방사
                SlashRgba = 0xE8446EFFu,        // 선명한 로즈
                Area = SkillArea.Screen, Split = SkillSplit.Once, HitCount = 1, Weight = 1,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.WaveRing
            },
            new SkillSpec {
                Id = BloodFallId, DisplayName = "낙혈", Family = SkillFamily.BloodForm,
                BaseMultiplier = 0.99d, CooldownSeconds = 5.5d,
                UnlockLevel = 0, UnlockStage = BloodFallStage,
                IconFile = "Icon056",           // 위에서 떨어지는 핏줄기
                SlashRgba = 0xB02060FFu,        // 짙은 자적
                Area = SkillArea.Pierce, Split = SkillSplit.Once, HitCount = 1, Weight = 1,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.Wave
            },
            new SkillSpec {
                Id = BloodWheelId, DisplayName = "혈륜", Family = SkillFamily.BloodForm,
                BaseMultiplier = 0.81d, CooldownSeconds = 4.5d,
                UnlockLevel = 0, UnlockStage = BloodWheelStage,
                IconFile = "Icon062",           // 회전하는 톱니 고리
                SlashRgba = 0xC8304CFFu,        // 하베스트의 중심색
                Area = SkillArea.Single, Split = SkillSplit.MultiHit, HitCount = 5, Weight = 0,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.Vortex
            },
            // 아래 둘은 **뽑기가 연다**(50단계). UnlockStage가 st41인 것은
            // 열리는 자리가 아니라 골드 비용의 기준점이고, 뽑기로만 열리는
            // 오의의 "가장 이른 획득 가능 시점"이 곧 상점이 열리는 칸이다
            // (SkillSpec.GachaGated 주석).
            new SkillSpec {
                Id = BloodBurstId, DisplayName = "혈폭", Family = SkillFamily.BloodForm,
                BaseMultiplier = 1.71d, CooldownSeconds = 9.5d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon083",           // 터져 오르는 폭발 기둥
                SlashRgba = 0xD81E7AFFu,        // 자홍
                Area = SkillArea.Single, Split = SkillSplit.Once, HitCount = 1, Weight = 2,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.Burst
            },
            new SkillSpec {
                Id = BloodWhipId, DisplayName = "혈조", Family = SkillFamily.BloodForm,
                BaseMultiplier = 0.45d, CooldownSeconds = 2.5d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon082",           // 휘어 감기는 갈고리
                SlashRgba = 0x96285EFFu,        // 어두운 자적
                Area = SkillArea.Pierce, Split = SkillSplit.Once, HitCount = 1, Weight = 0,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.Whip
            },

            // ---------------------------------------------------------- 15종 재설계
            //
            // 일곱 다 `배율/쿨 = 0.180`이다. 표에서는 안 보이므로 옆에 적어 둔다.
            //
            //   심격      0.72/4.0  = 0.180   최근접 단일 강타      (기존 거동)
            //   회월참    0.90/5.0  = 0.180   시전자 중심 원        (Around)
            //   검진      1.26/7.0  = 0.180   전방 장판 4틱         (Field x MultiHit)
            //   귀신난무  2.16/12.0 = 0.180   화면 광역 5타         (Screen x MultiHit)
            //   나락인력  2.52/14.0 = 0.180   끌어모음 + 폭발       (Captured + Pull)
            //   참수      2.88/16.0 = 0.180   처형 연출형 단일      (Execution)
            //   귀왕강림  3.42/19.0 = 0.180   최장 쿨 필살기        (기존 거동)
            //
            // 쿨다운 4·5·7·12·14·16·19초가 기존 여덟(2.5~11초)의 사이와 위를
            // 메운다. 계열 정체성 = 검식은 짧고 잦게, 귀오의는 길고 무겁게.
            //
            // **귀왕강림(x3.42)이 귀참(x3.52)을 안 넘는다.** 귀참은 진행 확정
            // 스킬이자 게임의 이름이라, 뽑기 스킬이 그 위에 서면 "뽑기가 파워를
            // 판다"의 체감 버전이 된다. 귀왕강림의 값은 최장 쿨의 무게와 전용
            // 화면 번쩍이지 최고 단발 피해가 아니다.

            new SkillSpec {
                Id = DeepThrustId, DisplayName = "심격", Family = SkillFamily.SwordForm,
                BaseMultiplier = 0.72d, CooldownSeconds = 4d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon016",           // 세로로 뻗는 관통 빔 (파란 타일)
                SlashRgba = 0x4FA0E6FFu,        // 깊은 하늘
                Area = SkillArea.Single, Split = SkillSplit.Once, HitCount = 1, Weight = 0,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.Thrust
            },
            new SkillSpec {
                Id = MoonArcId, DisplayName = "회월참", Family = SkillFamily.SwordForm,
                BaseMultiplier = 0.90d, CooldownSeconds = 5d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon044",           // 초승달 곡선 (파란 타일)
                SlashRgba = 0x7CE0D8FFu,        // 청록 달빛
                Area = SkillArea.Around, Split = SkillSplit.Once, HitCount = 1, Weight = 1,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.MoonArc
            },
            new SkillSpec {
                Id = SwordFieldId, DisplayName = "검진", Family = SkillFamily.SwordForm,
                BaseMultiplier = 1.26d, CooldownSeconds = 7d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon001",           // 교차한 무기 + X 참격 (파란 타일)
                SlashRgba = 0x3F63D2FFu,        // 짙은 강철청
                // 4틱 전체의 합이 x1.26이다. 틱당 x0.315이고 마지막 틱이
                // 나머지를 받는다(HitDamageShare) - 장판이라고 총량이 늘지 않는다
                Area = SkillArea.Field, Split = SkillSplit.MultiHit, HitCount = 4, Weight = 1,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.SwordField
            },
            new SkillSpec {
                Id = OniDanceId, DisplayName = "귀신난무", Family = SkillFamily.OniSecret,
                BaseMultiplier = 2.16d, CooldownSeconds = 12d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon145",           // 사방으로 뻗는 금빛 섬광 (어두운 타일)
                SlashRgba = 0xFF7A1AFFu,        // 선명한 주황금
                // 5타 전체의 합이 x2.16. **각 적이 받는 5타의 합**이라
                // 화면에 몇이 서 있든 한 마리가 받는 총량은 같다
                Area = SkillArea.Screen, Split = SkillSplit.MultiHit, HitCount = 5, Weight = 2,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.OniDance
            },
            new SkillSpec {
                Id = AbyssPullId, DisplayName = "나락인력", Family = SkillFamily.OniSecret,
                BaseMultiplier = 2.52d, CooldownSeconds = 14d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon183",           // 빨아들이는 검은 구 (자주 타일)
                SlashRgba = 0xB87333FFu,        // 청동금
                // 화면 전체가 아니라 **흡인이 확정한 목록**만 맞는다. 화면
                // 전체면 같은 초당 기여로 귀참·혈파동보다 무조건 나아지고,
                // 그것은 "뽑기가 파워를 안 판다"를 잡몹 층에서 깨는 것이다
                Area = SkillArea.Captured, Split = SkillSplit.Once, HitCount = 1, Weight = 2,
                Special = SkillSpecial.Pull, PullTargetCount = 8,
                VfxId = SkillVfx.AbyssPull
            },
            new SkillSpec {
                Id = DecapitateId, DisplayName = "참수", Family = SkillFamily.OniSecret,
                BaseMultiplier = 2.88d, CooldownSeconds = 16d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon165",           // 후드를 쓴 사신 (자주 타일)
                SlashRgba = 0xE8B000FFu,        // 짙은 금
                // **즉사가 아니다.** 남은 체력과 무관한 처치는 초당 환산 기여로
                // 잴 수가 없어서 이 오의만 저울 밖에 선다. 남긴 것은 연출뿐이고
                // 피해량은 x2.88 그대로다(TheExecution_DoesNotChangeDamage)
                Area = SkillArea.Single, Split = SkillSplit.Once, HitCount = 1, Weight = 2,
                Special = SkillSpecial.Execution, ExecutionThreshold = 0.30d,
                VfxId = SkillVfx.Decapitate
            },
            new SkillSpec {
                Id = OniAdventId, DisplayName = "귀왕강림", Family = SkillFamily.OniSecret,
                BaseMultiplier = 3.42d, CooldownSeconds = 19d,
                UnlockLevel = 0, UnlockStage = GachaCurve.UnlockStage, GachaGated = true,
                IconFile = "Icon172",           // 뿔 달린 오니 두개골 (자주 타일)
                SlashRgba = 0xE85A00FFu,        // 깊은 주적금
                Area = SkillArea.Screen, Split = SkillSplit.Once, HitCount = 1, Weight = 2,
                Special = SkillSpecial.None,
                VfxId = SkillVfx.OniAdvent
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

            /**
             * @brief 15종 재설계의 일곱. 전부 같은 Pozac 팩에서 새로 굽는다.
             *
             * ## 왜 기존 클립을 안 빌리는가
             *
             * `EverySkillEffect_BelongsToExactlyOneSkill`은 **빈 VfxId를
             * 건너뛴다**. 그러니 일곱을 비워 두는 것 자체는 그 검사를 통과한다.
             *
             * 문제는 색 검사다. 비워 두면 `SkillColors_AreDistinctFromEachOther`가
             * 이 일곱을 연참·일섬·귀참(전부 빈 VfxId)과 **같은 그림**으로 보고
             * 한계를 0.08에서 **0.25로 올린다.** RGB 정육면체에 열 색을 0.25씩
             * 벌려 넣을 수 없어서 여섯 쌍이 실패한다(귀참↔참수 0.139 등).
             *
             * 즉 고유 VFX는 테스트가 강제하는 의무가 아니라 **연출 품질을 위한
             * 설계 결정**이고, 그 결정이 열다섯 색을 성립시킨다.
             */
            public const string Thrust = "thrust";
            public const string MoonArc = "moon_arc";
            public const string SwordField = "sword_field";
            public const string OniDance = "oni_dance";
            public const string AbyssPull = "abyss_pull";
            public const string Decapitate = "decapitate";
            public const string OniAdvent = "oni_advent";
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
            int hits = spec.Split == SkillSplit.MultiHit ? Math.Max(1, spec.HitCount) : 1;
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
            return spec.Split == SkillSplit.MultiHit ? Math.Max(1, spec.HitCount) : 1;
        }

        // ---------------------------------------------------------------- 계열 표시

        /**
         * @brief 계열 탭의 **주 표기**. 열거형 순서와 자리가 맞물린다.
         *
         * ## 왜 런타임 어셈블리에 있는가
         *
         * 이 표를 UI(에디터 빌더)에 두고 싶어지는데, 그러면 문자셋 하베스트
         * (FontCharsetBuilder.DisplayNames)가 에디터 타입을 참조하게 된다. 그것은
         * SkillVfx 상수를 런타임에 둔 이유와 같은 함정이다 - 빌드에서 빠지거나,
         * 하베스트가 이 표를 못 읽어 "검식"의 `식`이 아틀라스에서 누락된다.
         *
         * **화면에 서는 글자의 출처는 하나여야 한다.** 오의 이름·퀘스트 제목·
         * 가이드 문구가 전부 코드에 있고 하베스트가 그 코드를 읽는 것과 같은 규칙이다.
         *
         * ## 아틀라스 주의
         *
         * `식`은 2026-08-14 시점 FontCharset.txt에 **없다.** 이 표를 하베스트에
         * 물리고 `Rebuild Font Charset` -> `Build Pixel Font Assets`를 돌리기 전까지
         * 탭이 "검□ / 혈□ / 귀오의"로 뜬다. 구현 2단계가 그 순서를 지킨다.
         */
        public static readonly string[] FamilyNames = { "검식", "혈식", "귀오의" };

        /**
         * @brief 탭의 **작은 부제**. 주 표기 아래 반 크기로 선다.
         *
         * 뽑기 희귀도(GachaCurve.Grade - 일반·고급·희귀·영웅·전설)와 겹치는 낱말이
         * "일반" 하나뿐인 것을 알고 고른 값이다. 겹치는 그 하나를 화면에서 가르는
         * 규칙은 자리로 정한다:
         *
         *     뽑기 결과·확률 정보  등급의 낱말만 쓴다 (계열 부제 금지)
         *     스킬 목록            계열의 낱말만 쓴다 (별 표시 금지)
         *
         * 부제를 아예 안 쓰는 안도 있었지만, 계열 이름 셋만으로는 "무엇이 더 귀한가"가
         * 안 읽힌다 - 검식·혈식·귀오의는 **역할**의 이름이라 서열을 안 말한다.
         */
        public static readonly string[] FamilySubtitles = { "일반", "상급", "각성" };

        /** 이 계열의 주 표기. 범위 밖이면 빈 문자열 - 화면이 □ 대신 빈칸을 그린다 */
        public static string NameOf(SkillFamily family)
        {
            int index = (int)family;
            return index >= 0 && index < FamilyNames.Length ? FamilyNames[index] : string.Empty;
        }

        public static string SubtitleOf(SkillFamily family)
        {
            int index = (int)family;
            return index >= 0 && index < FamilySubtitles.Length
                ? FamilySubtitles[index] : string.Empty;
        }

        /** 이 계열에 든 오의 수. 탭이 빈 목록을 그리지 않게 미리 센다 */
        public static int CountOf(SkillFamily family)
        {
            int total = 0;
            for (int i = 0; i < Skills.Length; i++)
                if (Skills[i].Family == family) total++;
            return total;
        }

        // ----------------------------------------------------------------

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
