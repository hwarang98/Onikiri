using System;
using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 디스크에 기록되는 세이브 한 벌.
     *
     * JsonUtility가 읽고 쓰므로 전부 public 필드이고, 프로퍼티나 딕셔너리는 쓸 수 없다.
     * BigDouble은 [Serializable] 구조체라 그대로 들어간다.
     *
     * 강화 레벨을 id 배열 + 레벨 배열로 나눠 담은 것도 같은 제약 때문이다. 인덱스가
     * 아니라 id로 저장하는 이유는, 나중에 강화 목록의 순서가 바뀌거나 중간에 하나가
     * 추가돼도 예전 세이브가 엉뚱한 트랙에 레벨을 밀어넣지 않게 하기 위해서다.
     */
    [Serializable]
    public sealed class SaveData
    {
        /**
         * @brief 형식 버전.
         *
         *   1  8단계. 스테이지는 10처치마다 자동으로 올랐다
         *   2  9단계. 보스가 스테이지 게이트가 됐고 bossKillCount가 생겼다
         *   3  10단계. 치명타 확률·피해 축이 생겼다
         *   4  11단계. 체력·체력회복 축이 생겼다
         *   5  12단계. 경험치/레벨/스탯 포인트가 생겼다
         *   6  20단계. 골드 획득량 축이 생겼다
         *   7  26단계. 발도 오의 셋(레벨 + 자동 시전 토글)이 생겼다
         *   8  31단계. 퀘스트(일일/반복/업적) 진행·수령과 보석 잔액이 생겼다
         *   9  32단계. 장비 두 슬롯(등급 + 단련 레벨)이 생겼다
         *   10 33단계. 전직 티어가 생겼다
         *   11 펫 스텝. 동료 셋(해금 + 레벨)과 액티브 선택이 생겼다
         *   12 37단계. 스테이지 재선택 - 최전선(maxStageReached)이 생겼다
         *   13 43단계. 심화 축 둘(초월 치명타·연격)이 생겼다
         *   14 44단계. 요도 넷(혼·티어·발견)과 파편이 생겼다
         *   15 46단계. 뽑기(천장 카운터·누적·일일 무료 쿨)가 생겼다
         *   16 47단계. 희귀도 사다리 - 자루별 혼격과 전설 妖刀 보유
         *   17 49단계. 오의 장착 슬롯 - 어느 오의를 어느 자리에 끼웠는가
         *   18 50단계. 오의 뽑기 - 스킬 XP · 가챠 몫 보유 · 그 배너의 천장/무료 쿨
         *   19 54단계. 리더보드 - 플레이어가 정한 이름
         *   20 15종 재설계. ★5 하드 천장 · 온보딩 10연 수령 · 그 오의의 첫 장착
         *
         *   21 승급 재설계. 신규 필드 없음 - `evolutionTier`의 **뜻**이
         *      "재화로 산 티어"에서 "귀문을 돌파해 보유한 티어"로 바뀌고,
         *      그 변환이 마이그레이션을 필요로 한다
         *
         * 모르는(더 높은) 버전이면 새 게임으로 시작한다. 낮은 버전은 Migrate가 올린다.
         */
        public const int CurrentVersion = 21;

        public int version = CurrentVersion;

        public BigDouble gold;
        public BigDouble lifetimeGold;

        public string[] upgradeIds = new string[0];
        public int[] upgradeLevels = new int[0];

        /** 현재 스테이지. 1부터 */
        public int stage = 1;

        /**
         * @brief 이번 스테이지에서 잡은 잡몹 수.
         *
         * 9단계부터 상한(10)에 닿은 채로 머무를 수 있다. 그 상태가 "보스가 열렸다"는
         * 뜻이고, 보스를 잡아야 0으로 돌아간다.
         */
        public int killsThisStage;

        /** 지금까지 잡은 보스 수 */
        public int bossKillCount;

        /**
         * @brief 최고 도달 스테이지 - 최전선 (v12, 37단계 스테이지 재선택).
         *
         * stage는 "지금 서 있는 곳"이 됐고 이쪽이 "여기까지 왔다"다. 해금과
         * 업적 도달 지표가 이쪽을 읽는다. stage보다 작을 수 없다.
         */
        public int maxStageReached = 1;

        // ---------------------------------------------------------------- 12단계

        /** 캐릭터 레벨. 1부터 */
        public int characterLevel = 1;

        /** 현재 레벨에서 모은 경험치. 초과분은 레벨업할 때 다음 레벨로 넘어간다 */
        public BigDouble exp;

        /**
         * @brief 각 증폭 축에 찍은 스탯 포인트.
         *
         * 남은 포인트는 저장하지 않는다. 레벨에서 총 지급량이 나오고 여기서 쓴 양이
         * 나오므로, 남은 양을 따로 적으면 셋이 어긋났을 때 무엇이 맞는지 알 수 없다.
         * CharacterLevel.UnspentPoints 참고.
         */
        public int attackPoints;
        public int healthPoints;

        /**
         * @brief 마지막으로 저장한 시각 (UTC ticks).
         *
         * 로컬 시간이 아니라 UTC다. 시간대를 넘나들거나 서머타임이 바뀌면 로컬 시간은
         * 뒤로 갈 수 있고, 그러면 방치 보상이 음수가 되거나 몇 시간이 공짜로 생긴다.
         */
        public long lastQuitUtcTicks;

        /**
         * @brief 저장 시점의 초당 골드 추정치.
         *
         * 방치 보상을 계산하려면 "그때 얼마나 벌고 있었는가"가 필요한데, 그 값은
         * 스탯·스테이지·스폰 속도가 전부 있어야 나온다. 복귀 시점에는 세이브를 아직
         * 적용하기 전이라 그 조건이 갖춰지지 않으므로, 나갈 때 계산해서 함께 적는다.
         */
        public double goldPerSecond;

        /** 같은 이유로 함께 적는 초당 경험치. 방치 보상이 골드와 나란히 계산된다 */
        public double expPerSecond;

        // ---------------------------------------------------------------- 26단계

        /**
         * @brief 발도 오의의 레벨. 강화 축과 **같은 방식**(id 배열 + 레벨 배열)이다.
         *
         * 강화 배열에 섞지 않은 이유는 복원 대상이 다르기 때문이다. 강화는
         * UpgradeSystem이, 오의는 SkillSystem이 되돌린다. 한 배열에 섞으면 복원
         * 쪽이 "내 것이 아닌 id는 조용히 건너뛴다"에 의존하게 되는데, 그 규칙은
         * **모르는 id를 무시하는 안전장치**이지 두 시스템을 가르는 수단이 아니다.
         * 섞어두면 한쪽 시스템이 씬에서 빠졌을 때 그쪽 레벨이 조용히 사라진다.
         */
        public string[] skillIds = new string[0];
        public int[] skillLevels = new int[0];

        /**
         * @brief 장착 구성 (49단계). 자리 순서대로 오의 id, 빈 자리는 빈 문자열.
         *
         * **레벨 배열과 따로 두는 것이 요점이다.** 보유(레벨)와 장착은 다른
         * 물건이다 - 여덟 자루를 다 갖고 넷만 끼운다. 한 배열에 섞으면 "끼우지
         * 않은 오의의 레벨"을 적을 자리가 없어지고, 그러면 슬롯을 바꿀 때마다
         * 레벨이 사라진다.
         *
         * 길이는 SkillCurve.MaxSlots다. 슬롯이 몇 개 열려 있는지는 저장하지
         * 않는다 - 최전선에서 유도되는 값이라(SkillCurve.SlotsAtStage) 저장하면
         * 두 출처가 갈릴 수 있다. 43단계가 심화 축 해금 상태를 저장하지 않은
         * 것과 같은 규칙이다.
         */
        public string[] skillEquipped = new string[0];

        /**
         * @brief 자동 시전이 켜져 있는가.
         *
         * 기본값 true다. JsonUtility는 없는 필드를 bool 기본값(false)으로 채우는데,
         * 그러면 v6 세이브가 **오의가 꺼진 채로** 올라온다 - 마이그레이션이
         * 밸런스를 바꾸는 셈이다. Migrate가 v6 -> v7에서 명시적으로 true를 넣는다.
         */
        public bool skillAutoCast = true;

        // ---------------------------------------------------------------- 31단계

        /**
         * @brief 보석 잔액.
         *
         * BigDouble이 아니라 long이다 - 파밍으로 늘지 않는 재화이기 때문이다.
         * GemWallet 주석 참고.
         */
        public long gems;

        /**
         * @brief 퀘스트 수령 상태. **id 배열 + 값 배열**이다.
         *
         * 강화 축·오의와 같은 방식이고 같은 이유다 - 목록 중간에 퀘스트가 하나
         * 추가되면 인덱스 저장은 엉뚱한 퀘스트에 수령 표시를 밀어 넣는다.
         *
         * 값의 뜻은 종류마다 다르다:
         *   일일/업적  0 = 미수령, 1 = 수령
         *   반복       지금까지 받은 티어 수
         *
         * 세 종류를 한 배열에 섞는다. 오의를 강화 배열에 섞지 않은 것과 달라
         * 보이지만 기준은 같다 - **복원하는 주체가 하나인가**다. 퀘스트는 셋 다
         * QuestSystem이 되돌리므로 나눌 이유가 없다.
         */
        public string[] questIds = new string[0];
        public int[] questClaims = new int[0];

        /** 오늘치 카운터. 자정에 0으로 돌아간다 */
        public double questTodayMobKills;
        public double questTodayBossKills;
        public double questTodaySkillCasts;
        public double questTodayUpgrades;
        public BigDouble questTodayGold;

        /** 누적 카운터. 반복 퀘스트의 티어가 이것으로 열린다 */
        public double questTotalMobKills;
        public double questTotalBossKills;
        public double questTotalSkillCasts;
        public double questTotalUpgrades;
        public BigDouble questTotalGold;

        /**
         * @brief 마지막 일일 리셋의 기준 날짜 (UTC ticks).
         *
         * lastQuitUtcTicks와 같은 이유로 UTC다 - 시간대를 넘나들면 로컬 자정이
         * 하루에 두 번 오거나 건너뛴다.
         */
        public long lastDailyResetUtcTicks;

        // ---------------------------------------------------------------- 32단계

        /**
         * @brief 장비 슬롯. **id 배열 + 등급 배열 + 단련 레벨 배열**이다.
         *
         * 강화 축·오의·퀘스트와 같은 방식이고 같은 이유다 - 슬롯이 중간에 하나
         * 늘면(액세서리) 인덱스 저장은 엉뚱한 슬롯에 등급을 밀어 넣는다.
         *
         * **장착 플래그가 없다.** 인벤토리가 없어서 슬롯이 곧 장비이고, 등급업은
         * 새 물건을 얻는 것이 아니라 그 자리의 물건이 바뀌는 것이다
         * (EquipmentSystem 주석). 여벌과 교체는 드랍 재료가 들어오는 다음
         * 스텝의 일이고, 그때 배열이 하나 더 는다.
         */
        public string[] equipmentIds = new string[0];
        public int[] equipmentGrades = new int[0];
        public int[] equipmentLevels = new int[0];

        // ---------------------------------------------------------------- 33단계

        /**
         * @brief 전직 티어. 0 = 로닌.
         *
         * 배열이 아니라 낱개 int다. 강화·오의·장비가 id 배열을 쓰는 이유는
         * "목록 중간에 하나가 추가되면 인덱스가 밀린다"인데, 전직은 목록이
         * 아니라 사다리 하나다 - 티어가 중간에 추가되는 날은 밸런스 전체를
         * 다시 유도하는 날이고, 그때 세이브 형식이 지켜줄 수 있는 것이 없다.
         *
         * ## v21에서 **뜻이 바뀐다. 필드는 안 바뀐다**
         *
         *   v20까지  "재화(보석 + 골드)로 구매한 티어"
         *   v21부터  "귀문을 돌파해 보유한 티어"
         *
         * 값 범위(0~6)도, 읽는 곳(`EvolutionSystem.Tier` -> 배수·이름·외형·오라)도
         * 그대로다. 그런데도 버전을 올리는 이유는 **마이그레이션이 필요하기**
         * 때문이다 - 이미 st200을 지난 플레이어의 `evolutionTier`가 0일 수 있고,
         * 새 뜻에서 그 값은 거짓이다.
         *
         * 변환 규칙은 `GateEvolutionTierFor`에 있다. **아직 아무도 부르지 않는다** -
         * 실제 귀문 전투가 없는 상태에서 라이브 세이브를 변환하면 진행을 되돌릴
         * 수 없는 방향으로 상태만 앞서간다.
         */
        public int evolutionTier;

        // ---------------------------------------------------------------- 펫

        /**
         * @brief 펫 셋. **id 배열 + 해금 배열 + 레벨 배열**이다.
         *
         * 강화 축·오의·장비와 같은 방식이고 같은 이유다 - 펫이 중간에 하나
         * 늘면(가챠) 인덱스 저장은 엉뚱한 펫에 해금 표시를 밀어 넣는다.
         *
         * 해금이 int(0/1)인 것은 questClaims와 같은 제약이다 - JsonUtility가
         * bool 배열을 못 담는 것은 아니지만, 퀘스트 수령 상태가 이미 int 값
         * 배열로 통일돼 있고 형식이 갈릴 이유가 없다.
         *
         * 레벨은 해금 전에도 1이다. "기본값이라서 1인 것"과 "저장된 적이
         * 없어서 없는 것"이 구분돼야 한다는 규칙 그대로다.
         */
        public string[] petIds = new string[0];
        public int[] petUnlocked = new int[0];
        public int[] petLevels = new int[0];

        /**
         * @brief **레거시.** 단일 출전 시절의 액티브 동료 id.
         *
         * 다중 출전(보유 전원 출전)으로 바뀌면서 뜻을 잃었다. 필드를 지우지
         * 않는 이유는 세이브 형식의 규칙이다 - 필드 삭제는 마이그레이션이
         * 아니라 형식 파괴이고, JsonUtility는 모르는 필드를 조용히 버리므로
         * 남겨 두는 비용이 0이다. 복원 쪽은 읽지 않는다(PetSystem.Restore).
         */
        public string activePetId = "";

        // ---------------------------------------------------------------- 44단계

        /**
         * @brief 요도 넷. **id 배열 + 혼 배열 + 티어 배열 + 발견 배열**이다.
         *
         * 강화 축·오의·장비·동료와 같은 방식이고 같은 이유다 - 다섯째 지역이
         * 생겨 요도가 하나 늘면 인덱스 저장은 엉뚱한 칼에 티어를 밀어 넣는다.
         *
         * **혼이 long인 것은 GemWallet과 같은 판단이다.** 파밍으로 늘지 않고
         * 대요괴 처치로만 들어오므로(40스테이지에 넷) 평생 만 단위를 넘지
         * 않는다. BigDouble로 두면 "언젠가 폭증할 수 있는 값"처럼 보이고,
         * 그러면 다음 스텝이 혼 가격을 지수로 설계하고 싶어진다 - 그 순간
         * 혼은 두 번째 골드가 된다.
         *
         * 발견(discovered)을 따로 적는 이유는 도감 때문이다. 티어와 혼 수만으로는
         * **"한 번 받았다가 다 써버린 상태"**와 "한 번도 못 본 상태"가 구분되지
         * 않는다. 앞의 것은 도감에 이름이 떠야 하고 뒤의 것은 실루엣이어야
         * 한다 - 41단계 잠긴 미리보기 규칙이 요구하는 구분이다.
         * 형식이 int(0/1)인 것은 questClaims·petUnlocked와 같은 이유다.
         */
        public string[] yodoIds = new string[0];
        public long[] yodoSouls = new long[0];
        public int[] yodoTiers = new int[0];
        public int[] yodoDiscovered = new int[0];

        /** 합성 재료. 종류가 없어서 낱개 값이다 - 전직 티어와 같은 자리 */
        public long yodoShards;

        // ---------------------------------------------------------------- 46단계

        /**
         * @brief 마지막 혼 정수 뒤로 돌린 뽑기 수. **천장의 카운터다.**
         *
         * 오의 쿨다운·영체 순번은 저장하지 않는데 이것은 저장한다. 기준이
         * 다르기 때문이다 - 저쪽은 **런타임이 다시 만들 수 있는 값**이고
         * 이쪽은 **플레이어가 지불한 것**이다. 29회에서 껐다 켰더니 0으로
         * 돌아가면 그것은 리셋이 아니라 몰수이고, 세이브가 지켜야 할 것의
         * 정의에 정확히 들어맞는다.
         */
        public int gachaPity;

        /** 지금까지 돌린 총 횟수. 상점 표시용 - 밸런스에는 쓰이지 않는다 */
        public int gachaTotalPulls;

        /**
         * @brief 마지막으로 무료 뽑기를 쓴 **퀘스트일** (UTC ticks).
         *
         * lastDailyResetUtcTicks와 같은 형식이고 같은 경계(KST 04:00)다.
         * 시각이 아니라 날짜를 적는 이유는 GachaSystem 쪽 주석에 있다 -
         * 시각으로 24시간을 재면 리셋이 매일 조금씩 늦어진다.
         *
         * 0이면 "아직 한 번도 안 썼다"이고, 그래서 마이그레이션이 이 값을
         * 0으로 두는 것만으로 기존 플레이어가 접속 즉시 무료 뽑기를 하나
         * 들고 시작한다.
         */
        public long gachaFreePullDayTicks;

        // ---------------------------------------------------------------- 47단계

        /**
         * @brief 요도 넷의 혼격. yodoIds와 **같은 순서**다.
         *
         * 티어와 나란히 서는 두 번째 숫자이고(YodoSystem.Blade.rarity),
         * 그래서 자기 배열로 둔다 - 티어에 접어 넣으면(예: tier * 10 +
         * rarity) 세이브를 사람이 못 읽고, 상한이 바뀌는 날 인코딩이
         * 통째로 무효가 된다.
         *
         * 비어 있으면 전부 0이다 - v15까지의 세이브가 정확히 그 상태이고,
         * 혼격 0의 기여가 정확히 1이라 마이그레이션이 밸런스를 안 바꾼다.
         */
        public int[] yodoRarities = new int[0];

        /**
         * @brief 전설 妖刀의 id와 사본 수. **보스 요도와 별개 배열이다.**
         *
         * 한 배열에 섞지 않는 이유는 런타임과 같다(YodoSystem.LegendaryBlade
         * 주석) - YodoCatalog.Count가 세계 순환의 길이를 유도하는 값이라,
         * 전설이 그 표에 들어가면 한 바퀴가 40에서 60스테이지가 된다.
         *
         * id를 함께 적는 것은 표의 순서가 바뀌어도 보유가 안 섞이게 하려는
         * 것이고, 강화 축·오의·장비·동료·요도가 전부 같은 규칙이다.
         */
        public string[] legendaryYodoIds = new string[0];
        public int[] legendaryYodoCopies = new int[0];

        // ---------------------------------------------------------------- 50단계

        /**
         * @brief 뽑기로 얻은 오의의 id. **보유한 것만 적는다.**
         *
         * 미보유를 0으로 함께 적는 방식(petUnlocked · yodoDiscovered)을 안
         * 쓰는 이유는 값이 없기 때문이다. 저쪽 배열들은 해금 옆에 레벨·티어가
         * 나란히 서지만 여기 있는 것은 bool 하나뿐이라, 있는 것만 적으면
         * 세이브가 스스로 설명된다.
         *
         * 오의 레벨(skillLevels)에 접어 넣지 않는 이유는 47단계가 혼격을
         * 티어에 안 접은 것과 같다 - 레벨 0을 "미보유"로 쓰면 레벨이 1부터라는
         * 규칙이 이 두 오의에서만 깨지고, 그 예외는 읽는 쪽 모두에 퍼진다.
         */
        public string[] gachaSkillIds = new string[0];

        /**
         * @brief 아직 레벨로 바뀌지 않은 스킬 XP. **하나의 풀이다.**
         *
         * 오의별로 나눠 담지 않는 이유는 SkillSystem.skillXp 주석에 있다 -
         * 나누면 뽑기가 타겟팅을 갖는다.
         *
         * long인 것은 GemWallet·yodoSouls와 같은 판단이다. 파밍으로 늘지
         * 않고 뽑기로만 들어오므로(한 번에 6~240) 평생 십만 단위를 안 넘는다.
         */
        public long skillXp;

        /**
         * @brief 오의 뽑기의 천장 카운터. **요도 뽑기(gachaPity)와 다른 값이다.**
         *
         * 한 칸에 접지 않는 이유는 두 배너가 다른 상품이기 때문이다
         * (SkillGachaSystem 머리 주석) - 합치면 요도를 스물아홉 번 돌린
         * 사람이 오의 해금을 한 번에 받는다.
         */
        public int skillGachaPity;

        public int skillGachaTotalPulls;

        /** 마지막으로 오의 무료 뽑기를 쓴 퀘스트일. 요도 쪽과 같은 형식·같은 경계 */
        public long skillGachaFreePullDayTicks;

        // ---------------------------------------------------------------- v20

        /**
         * @brief 마지막 ★5 뒤로 돌린 뽑기 수 (15종 재설계).
         *
         * `skillGachaPity`와 한 칸에 접지 않는 이유가 두 배너를 안 접은 이유와
         * 같다 - **서로 다른 것을 센다.** 소프트는 "★4 이상"을 재고 이쪽은
         * "★5만"을 재므로, 영웅을 아무리 받아도 이 값은 안 줄어든다.
         */
        public int skillGachaAwakenPity;

        /**
         * @brief st14 온보딩 무료 10연을 받았는가.
         *
         * v19에서 오면 **false**다. 그래서 기존 플레이어 전원이 업데이트 후
         * 한 번 받는다 - 하드 천장을 0에서 시작시키는 것에 대한 보상이고,
         * 버그가 아니라 의도한 선물이다(Migrate 주석).
         */
        public bool skillGachaIntroClaimed;

        /**
         * @brief 온보딩으로 받은 오의를 **처음 장착했는가.**
         *
         * 파생 조건(보유 && 미장착)으로는 1회성이 안 되므로 칸을 하나 쓴다 -
         * 그 조건이면 플레이어가 나중에 그 오의를 뺄 때 온보딩 안내가
         * 되살아난다. 안내를 한 번만 띄우려면 "띄웠다"를 기억해야 한다.
         */
        public bool skillGachaIntroEquipDone;

        // ---------------------------------------------------------------- 54단계

        /**
         * @brief 플레이어가 정한 이름 (v19, 리더보드).
         *
         * **안 정했으면 빈 문자열이다.** 기본 이름("이름없는 무사")을 여기에
         * 적지 않는 이유는 그 순간 "안 정한 사람"과 "기본 이름을 직접 고른
         * 사람"이 구분되지 않아, 랭킹 첫 진입에서 입력을 띄울지 판단할 근거가
         * 사라지기 때문이다. 표시는 PlayerProfile.Name이 대신한다.
         *
         * 도달층은 여기 없다 - maxStageReached가 이미 그 값이다(52단계).
         * 리더보드가 새로 저장하는 상태는 이름 하나뿐이다.
         */
        public string playerName = string.Empty;

        public static SaveData NewGame()
        {
            return new SaveData
            {
                version = CurrentVersion,
                gold = BigDouble.Zero,
                lifetimeGold = BigDouble.Zero,
                stage = 1,
                killsThisStage = 0,
                bossKillCount = 0,
                characterLevel = 1,
                exp = BigDouble.Zero,
                attackPoints = 0,
                healthPoints = 0,
                lastQuitUtcTicks = 0L,
                goldPerSecond = 0d,
                skillAutoCast = true,

                // 새 게임은 퀘스트 0진행 · 보석 0이다. 리셋 기준 시각도 0으로 두고
                // QuestSystem이 첫 프레임에 오늘 날짜를 적는다 - 여기서 UtcNow를
                // 넣으면 SaveData가 시계를 읽게 되고, 그러면 테스트가 시각을
                // 넘겨줄 수 없다
                gems = 0L,
                lastDailyResetUtcTicks = 0L
            };
        }

        /**
         * @brief 예전 형식의 세이브를 현재 형식으로 올린다.
         *
         * 버전이 다르다고 새 게임으로 되돌리지 않는다. 8단계까지는 그렇게 했는데,
         * 그건 형식을 손댈 때마다 플레이어의 진행을 지운다는 뜻이다. 실제 배포에서는
         * 그 한 줄이 "업데이트했더니 처음부터"가 된다.
         *
         * 올릴 수 없는 버전(미래 버전, 손상)만 false를 반환한다.
         *
         * @return 마이그레이션 후 쓸 수 있는 데이터면 true
         */
        public static bool Migrate(SaveData data)
        {
            if (data == null) return false;
            if (data.version > CurrentVersion) return false;
            if (data.version == CurrentVersion) return true;

            if (data.version <= 1)
            {
                // v1에는 보스가 없었다. 그동안 오른 스테이지 수를 보스 처치 수로
                // 친다. 실제로 잡은 것은 아니지만 이 값이 뜻하는 것은 "몇 개의
                // 스테이지를 넘었는가"이고, v1 플레이어에게 그것은 stage - 1이 맞다.
                // 0으로 두면 통계가 진행과 어긋난 채로 남는다
                data.bossKillCount = Mathf.Max(0, data.stage - 1);

                // v1에서 killsThisStage는 0~9였다. 10은 스테이지가 오르는 순간이라
                // 저장될 수 없었다. 그대로 들어와도 v2에서는 "보스가 열린 상태"로
                // 읽혀 문제가 없으므로 손대지 않는다
                data.version = 2;
            }

            if (data.version == 2)
            {
                // v2에는 치명타 축이 없었다. 목록에 없는 트랙은 UpgradeSystem이
                // 조용히 건너뛰므로 사실 아무것도 하지 않아도 동작한다. 그런데
                // 그러면 세이브에 그 축이 **없는** 상태로 남아, 처음 저장될 때까지
                // "레벨 1이라서 없는 것"과 "저장된 적이 없어서 없는 것"이 구분되지
                // 않는다. 명시적으로 레벨 1을 적어 넣는다.
                EnsureTrack(data, UpgradeSystem.CritRateId);
                EnsureTrack(data, UpgradeSystem.CritDamageId);
                data.version = 3;
            }

            if (data.version == 3)
            {
                // v3에는 생존 축이 없었다. v2 -> v3 과 같은 이유로 명시적으로
                // 레벨 1을 적어 넣는다
                EnsureTrack(data, UpgradeSystem.HealthId);
                EnsureTrack(data, UpgradeSystem.HealthRegenId);
                data.version = 4;
            }

            if (data.version == 4)
            {
                // v4에는 레벨이 없었다. 지나온 진행만큼 레벨을 소급해줄 수도 있지만
                // 그러지 않는다. 소급하면 그만큼의 스탯 포인트가 함께 들어오고,
                // 그것은 v4 플레이어가 12단계 밸런스로 계산되지 않은 증폭을 얹은 채
                // 다음 보스를 만난다는 뜻이다. 레벨 1에서 시작하되 경험치는 지금
                // 스테이지에서 벌리므로 몇 분이면 따라잡는다
                //
                // 필드가 JsonUtility 기본값(0)으로 들어오는 경우가 있어 명시적으로
                // 1을 넣는다. 0이면 ExpCurve.RequiredForLevel이 Lv.1과 같은 값을
                // 내주긴 하지만, 레벨 표시가 "Lv.0"이 된다
                if (data.characterLevel < 1) data.characterLevel = 1;
                data.exp = BigDouble.Zero;
                data.attackPoints = 0;
                data.healthPoints = 0;
                data.version = 5;
            }

            if (data.version == 5)
            {
                // v5에는 골드 획득 축이 없었다. 앞의 축들과 같은 이유로 레벨 1을
                // 명시적으로 적어 넣는다 - 그래야 "레벨 1이라서 없는 것"과 "저장된
                // 적이 없어서 없는 것"이 구분된다.
                //
                // 레벨 1의 배수가 1배이므로(GoldGainCurve.BaseValue) 이 마이그레이션은
                // **예전 플레이어의 골드 수입을 바꾸지 않는다.** 새 축이 생겼다고
                // 기존 진행의 벌이가 달라지면 그것은 마이그레이션이 아니라 밸런스
                // 변경이다
                EnsureTrack(data, UpgradeSystem.GoldGainId);
                data.version = 6;
            }

            if (data.version == 6)
            {
                // v6에는 오의가 없었다. 앞의 축들과 같은 이유로 레벨 1을 명시적으로
                // 적어 넣는다 - "레벨 1이라서 없는 것"과 "저장된 적이 없어서 없는
                // 것"이 구분돼야 한다.
                //
                // **레벨 1이 곧 해금 직후 상태다.** 배율이 0이 아니라 기본값이므로
                // 이 마이그레이션은 예전 플레이어에게 오의를 공짜로 주는 것처럼
                // 보이는데, 그것이 맞다 - 해금 조건은 캐릭터 레벨이고 그 레벨은
                // 이미 갖고 있다. 새 시스템이 열리는 것과 레벨을 소급해 주는 것은
                // 다른 일이다(v4 -> v5가 레벨을 소급하지 않은 것과 같은 구분).
                foreach (var skill in SkillCatalog.Skills) EnsureSkill(data, skill.Id);

                // JsonUtility가 없는 bool을 false로 채운다. 명시하지 않으면 v6
                // 플레이어가 오의가 꺼진 채로 올라오고, 그것은 마이그레이션이
                // 아니라 밸런스 변경이다
                data.skillAutoCast = true;
                data.version = 7;
            }

            if (data.version == 7)
            {
                // v7에는 퀘스트와 보석이 없었다. **소급하지 않는다** - 지금까지
                // 잡은 요괴를 누적 카운터에 넣어주면 예전 플레이어가 접속하자마자
                // 반복 퀘스트 티어 수십 개를 한꺼번에 받는다.
                //
                // 그것이 관대해 보이지만 사실은 두 가지가 어긋난다. 하나는 리텐션이다 -
                // 퀘스트는 "내일 또 올 이유"인데 첫날에 몇 달치가 열리면 그 이유가
                // 사라진다. 다른 하나는 밴드다. 업적 골드/경험치가 한 스테이지에
                // 몰려 떨어지면 시뮬레이션이 계산한 분포와 달라진다.
                //
                // v4 -> v5가 레벨을 소급하지 않은 것과 같은 판단이다: **새 시스템이
                // 열리는 것과 지나간 플레이를 소급하는 것은 다른 일이다.**
                //
                // 업적은 예외처럼 보일 수 있다. 이미 20스테이지인 플레이어에게
                // "5스테이지 도달"이 미수령으로 뜨는데, 그것은 맞다 - 조건은 이미
                // 만족했으므로 **곧바로 받을 수 있는 상태**로 열린다. 조건 판정이
                // 카운터가 아니라 현재 상태를 읽기 때문에 저절로 그렇게 된다
                // (QuestSystem.CurrentStateOf).
                data.gems = 0L;
                data.questIds = new string[0];
                data.questClaims = new int[0];

                data.questTodayMobKills = 0d;
                data.questTodayBossKills = 0d;
                data.questTodaySkillCasts = 0d;
                data.questTodayUpgrades = 0d;
                data.questTodayGold = BigDouble.Zero;

                data.questTotalMobKills = 0d;
                data.questTotalBossKills = 0d;
                data.questTotalSkillCasts = 0d;
                data.questTotalUpgrades = 0d;
                data.questTotalGold = BigDouble.Zero;

                // 0으로 두면 QuestSystem이 첫 프레임에 오늘 날짜를 적는다. UtcNow를
                // 여기서 읽지 않는 이유는 위 NewGame과 같다
                data.lastDailyResetUtcTicks = 0L;

                data.version = 8;
            }

            if (data.version == 8)
            {
                // v8에는 장비가 없었다. 두 슬롯을 **1등급 Lv.1**로 명시적으로
                // 적어 넣는다. 앞의 축들과 같은 이유다 - 그래야 "기본값이라서
                // 없는 것"과 "저장된 적이 없어서 없는 것"이 구분된다.
                //
                // **이 마이그레이션은 밸런스를 바꾸지 않는다.** 1등급 Lv.1의
                // 배수가 정확히 1배이기 때문이고(EquipmentCurve.ValueAt), 그것은
                // GoldGainCurve.BaseValue가 1인 것과 같은 설계다. 새 축이 생겼다고
                // 기존 진행의 스탯이 달라지면 그것은 마이그레이션이 아니라 밸런스
                // 변경이다.
                //
                // 소급하지 않는 것도 v7 -> v8과 같다. 이미 30스테이지인 플레이어가
                // 접속하자마자 5등급을 갖고 있으면, 보석 소비처를 만들어놓고 그
                // 소비처를 통과할 이유를 함께 지우는 셈이다
                foreach (var slot in EquipmentCatalog.Slots) EnsureEquipment(data, slot.Id);
                data.version = 9;
            }

            if (data.version == 9)
            {
                // v9에는 전직이 없었다. **0티어(로닌)를 명시적으로 적어 넣는다.**
                //
                // 이 마이그레이션은 밸런스를 바꾸지 않는다 - 티어 0의 배수가
                // 정확히 1배다(EvolutionCurve.AttackMultiplierAt). 장비 v8 -> v9가
                // 1등급 Lv.1(배수 1배)을 적어 넣은 것과 같은 성질이다.
                //
                // 소급하지 않는 것도 같다. 이미 Lv.74인 플레이어에게 티어를
                // 얹어주면 보석 소비처를 만들어놓고 통과할 이유를 지우는 셈이고,
                // 진화 연출(로닌 -> 데몬사무라이)을 볼 기회도 함께 사라진다.
                //
                // JsonUtility가 없는 int를 0으로 채우므로 사실 아무것도 안 해도
                // 값은 같다. 그래도 명시하는 이유는 앞의 모든 버전과 같다 -
                // "0이라서 없는 것"과 "저장된 적이 없어서 없는 것"이 코드에서
                // 구분돼야 한다.
                data.evolutionTier = 0;
                data.version = 10;
            }

            if (data.version == 10)
            {
                // v10에는 펫이 없었다. 세 마리를 **잠금 + Lv.1**로 명시적으로
                // 적어 넣고 액티브는 비워 둔다.
                //
                // **이 마이그레이션은 밸런스를 바꾸지 않는다.** 잠긴 펫의
                // 기여가 정확히 0이기 때문이다 - 장비 1등급 Lv.1(배수 1배),
                // 전직 0티어(배수 1배)와 같은 성질이다.
                //
                // 소급하지 않는 것도 같다. 이미 st40인 플레이어에게 펫을
                // 쥐여주면 보석 소비처를 만들어놓고 통과할 이유를 지우는
                // 셈이고, 동료가 화면에 처음 걸어 들어오는 순간도 함께
                // 사라진다. 조건(st31)은 이미 넘겼으므로 접속하자마자 해금
                // 버튼이 눌리는 상태로 열린다 - v7 -> v8 업적과 같은 결이다.
                foreach (var pet in PetCatalog.Pets) EnsurePet(data, pet.Id);

                // JsonUtility가 없는 string을 null로 채운다. 명시적으로 빈
                // 문자열을 넣는다 - "아무도 없다"가 null과 ""로 두 가지가
                // 되면 읽는 쪽마다 검사가 갈린다
                if (data.activePetId == null) data.activePetId = "";

                data.version = 11;
            }

            if (data.version == 11)
            {
                // v11에는 최전선이 없었다. **지금 서 있는 스테이지가 곧 최전선이다** -
                // v11까지는 스테이지가 내려가는 경로가 존재하지 않았으므로 이 등식은
                // 참이고, 그래서 이 마이그레이션은 아무 상태도 지어내지 않는다.
                //
                // 밸런스도 바꾸지 않는다. 최전선은 해금 판정과 재선택 상한에만
                // 쓰이는 기록이고, 그 판정들은 지금까지 stage로 하던 것과 같은
                // 답을 낸다.
                data.maxStageReached = Mathf.Max(1, data.stage);
                data.version = 12;
            }

            if (data.version == 12)
            {
                // 43단계 - 강화 축이 미세화됐다(슬레이어식 수천 레벨). 값 등가
                // 재스케일이라 **레벨 숫자도 함께 환산해야** 구세이브의 파워가
                // 보존된다: 옛 한 레벨 = 새 여덟 칸(공격력·치명타피해·체력·회복),
                // 치명타 확률은 옛 +0.5%p가 새 +0.088%p라 5.676칸이다.
                // 환산 없이 두면 공격력 Lv.84가 x1.0143^83 = 3.2배가 되어
                // (옛 값은 x1.12^83 = 만二천 배) 진행이 통째로 무너진다.
                //
                // 공격속도(아트 상한)와 골드 획득(밴드 손잡이)은 곡선이 그대로라
                // 환산도 없다.
                ConvertLevel(data, UpgradeSystem.AttackPowerId, 8d);
                ConvertLevel(data, UpgradeSystem.CritDamageId, 8d);
                ConvertLevel(data, UpgradeSystem.HealthId, 8d);
                ConvertLevel(data, UpgradeSystem.HealthRegenId, 8d);

                // 0.005 = 42단계까지의 치명타 스텝. 새 스텝과의 비가 환산 계수다
                ConvertLevel(data, UpgradeSystem.CritRateId, 0.005d / CritRateCurve.Step);

                // 심화 축 둘. 레벨 1의 값이 곧 무보정 상태(배수 1 / 확률 0)라
                // 이 승격은 밸런스를 바꾸지 않는다. 해금 상태는 저장하지 않는다 -
                // 치명타 확률 트랙의 상한 도달에서 유도되는 값이고, 유도되는
                // 것을 저장하면 언젠가 두 값이 갈린다(챕터를 스테이지에서
                // 유도하는 것과 같은 규칙)
                EnsureTrack(data, UpgradeSystem.TranscendId);
                EnsureTrack(data, UpgradeSystem.ComboId);
                data.version = 13;
            }

            if (data.version == 13)
            {
                // v13에는 요도가 없었다. 네 자루를 **미봉인(티어 0) · 혼 0 ·
                // 미발견**으로 명시적으로 적어 넣고 파편도 0으로 둔다.
                //
                // **이 마이그레이션은 밸런스를 바꾸지 않는다.** 티어 0의
                // 배수가 정확히 1배이고(YodoCurve.TierValue) 세트 보너스도
                // 0자루에서 1배다 - 장비 v8 -> v9(1등급 Lv.1), 전직 v9 -> v10
                // (0티어), 펫 v10 -> v11(잠금)과 같은 성질이다.
                //
                // **소급하지 않는 것도 같다.** 이미 st200인 플레이어는 지나온
                // 순환에서 대요괴를 열 번도 넘게 벴지만, 그 처치를 혼으로
                // 쳐주면 접속하자마자 오니키리가 완성된다 - 도감이 채워지는
                // 과정 전체가 사라지는 셈이고, 그것이 이 스텝이 만든 것의
                // 전부다. 조건(st41)은 이미 넘겼으므로 요도 탭은 열린 채로
                // 시작하고, 다음 순환에서 첫 혼이 떨어진다. v7 -> v8 업적이
                // "곧바로 받을 수 있는 상태로 열린다"였던 것과 같은 결이다.
                foreach (var blade in YodoCatalog.Blades) EnsureYodo(data, blade.Id);
                data.yodoShards = 0L;

                data.version = 14;
            }

            if (data.version == 14)
            {
                // v14에는 뽑기가 없었다. 천장 카운터 0 · 누적 0 · 무료 뽑기
                // **미사용**으로 명시적으로 적어 넣는다.
                //
                // **이 마이그레이션은 밸런스를 바꾸지 않는다.** 뽑기는
                // 요도의 재료만 주고, 한 번도 안 돌린 상태의 기여가 정확히
                // 0이다 - 장비 v8 -> v9(1등급 Lv.1), 전직 v9 -> v10(0티어),
                // 펫 v10 -> v11(잠금), 요도 v13 -> v14(미봉인)와 같은 성질이다.
                //
                // **소급하지 않는 것도 같다.** 이미 st200인 플레이어에게
                // 지나온 날수만큼 무료 뽑기를 쌓아주면 접속하자마자 200회가
                // 돌아가고, 그것은 천장(GachaCurve.PityPulls)을 여섯 번
                // 지나는 양이라 요도가 통째로 리드 상한까지 올라간다.
                // 조건(st41)은 이미 넘겼으므로 상점은 열린 채로 시작하고
                // **오늘치 무료 뽑기 하나**를 곧바로 쓸 수 있다 - v7 -> v8
                // 업적이 "곧바로 받을 수 있는 상태로 열린다"였던 것과 같은 결이다.
                data.gachaPity = 0;
                data.gachaTotalPulls = 0;
                data.gachaFreePullDayTicks = 0L;

                data.version = 15;
            }

            if (data.version == 15)
            {
                // v15에는 희귀도 사다리가 없었다. 혼격은 **전부 0**이고
                // 전설은 **한 자루도 없다.**
                //
                // **이 마이그레이션도 밸런스를 바꾸지 않는다.** 혼격 0의
                // 배수가 정확히 1이고(YodoRarityCurve.ValueAt) 미보유 전설의
                // 배수도 정확히 1이라(LegendaryYodoCurve.PowerAt), 승격 직후의
                // 기여가 0이다 - 장비 v8->v9, 전직 v9->v10, 펫 v10->v11,
                // 요도 v13->v14, 뽑기 v14->v15와 같은 성질이고 이유도 같다.
                //
                // **소급도 없다.** 이미 뽑기를 200회 돌린 플레이어에게 그
                // 회수만큼 ★4·★5를 나눠 주면 접속 즉시 혼격이 상한까지
                // 차오르고, 그것은 이 스텝이 판 재고를 통째로 지우는 일이다.
                // 지나온 뽑기는 그때의 표로 이미 값을 받았다.
                //
                // 천장 카운터(gachaPity)는 **그대로 둔다.** 지키는 대상이
                // ★3에서 ★4+로 승격했지만 카운터의 뜻("마지막 보장 뒤로
                // 몇 번 돌렸는가")은 같고, 0으로 되돌리면 29회에서 승격을
                // 맞은 플레이어의 지불이 몰수된다 - v14 -> v15가 그 값을
                // 저장하기로 한 이유가 여기서도 그대로 성립한다.
                data.yodoRarities = new int[data.yodoIds != null ? data.yodoIds.Length : 0];

                data.legendaryYodoIds = new string[0];
                data.legendaryYodoCopies = new int[0];
                EnsureLegendaryYodo(data, LegendaryYodoCatalog.WhiteMaskId);
                EnsureLegendaryYodo(data, LegendaryYodoCatalog.ThousandHandId);

                data.version = 16;
            }

            if (data.version == 16)
            {
                // v16에는 장착이라는 개념이 없었다. 오의 셋은 **전부 상시
                // 발동**이었고, 그것은 곧 "셋이 세 자리에 끼워져 있다"와 같다.
                //
                // **그래서 값을 지어내지 않는다.** 빈 배열로 두면 SkillSystem이
                // FillEmptySlots로 기준 구성(상한 기여 내림차순)을 채우는데,
                // st51 아래에서는 열린 자리가 셋이고 열린 오의도 그 셋뿐이라
                // 결과가 언제나 귀참·일섬·연참이다 - 마이그레이션이 하는 일이
                // 없는 것이 정답이다.
                //
                // **소급도 없다.** 이미 심층에 있는 플레이어는 접속하는 순간
                // 4번 슬롯이 열리고 신규 다섯이 전부 해금되지만, 그것은 소급이
                // 아니라 **게이트가 최전선 하나뿐**이기 때문이다(SkillCurve.
                // ExpansionStage). 요도가 st41 게이트 하나로 열린 것과 같다.
                // 신규 오의의 레벨은 다 1에서 시작하므로 얻는 것은 자리 하나이고,
                // 그 자리의 값이 이 스텝의 밴드 재기준이 잰 크기 그대로다.
                data.skillEquipped = new string[0];

                // **신규 오의의 칸은 여기서 만든다.** v6 -> v7이 그때의 오의
                // 셋에 칸을 만들어 준 것과 같은 처리다 - 그 단계는 v6 이하만
                // 지나므로 v7~v16 세이브에는 49단계의 다섯이 아예 없다.
                //
                // 레벨은 1이다(EnsureSkill의 기본값). 소급이 아니라 **없던
                // 것이 생기는 것**이고, 레벨 1의 오의는 상한까지 열한 칸을
                // 골드로 사야 한다 - 심층 플레이어도 예외가 아니다
                foreach (var skill in SkillCatalog.Skills) EnsureSkill(data, skill.Id);

                data.version = 17;
            }

            if (data.version == 17)
            {
                // v17에는 오의 뽑기가 없었다. 천장 0 · 누적 0 · 무료 뽑기
                // **미사용** · XP 0으로 명시적으로 적어 넣는다.
                //
                // **이 마이그레이션은 밸런스를 바꾸지 않는다.** XP 0의 기여가
                // 정확히 0이고, 미보유 오의의 기여도 정확히 0이다 - 장비
                // v8->v9(1등급 Lv.1), 전직 v9->v10(0티어), 펫 v10->v11(잠금),
                // 요도 v13->v14(미봉인), 뽑기 v14->v15(0회)와 같은 성질이다.
                //
                // **소급도 없다.** 지나온 날수만큼 무료 뽑기를 쌓아주면
                // 접속하자마자 천장을 여러 번 지나 오의 둘이 통째로 열리고,
                // 그것은 이 스텝이 판 재고를 지우는 일이다.
                data.skillXp = 0L;
                data.skillGachaPity = 0;
                data.skillGachaTotalPulls = 0;
                data.skillGachaFreePullDayTicks = 0L;

                // ---- 그런데 **이미 가진 것은 유지한다.**
                //
                // 49단계에서 혈폭·혈조는 최전선 st51의 스테이지 게이트였다.
                // 50단계가 그 게이트를 뽑기로 옮기므로, 손대지 않으면 이미
                // st51을 넘긴 플레이어가 **갖고 있던 오의 둘을 잃는다.**
                //
                // 소급이 아니라 보존이다. 위 세 줄이 "지나온 플레이를 값으로
                // 쳐주지 않는다"는 규칙이라면, 이 줄은 "이미 준 것을 도로
                // 뺏지 않는다"는 다른 규칙이다 - v12 -> v13이 강화 레벨을
                // 환산해 파워를 보존한 것과 같은 자리이고, 47단계가 천장
                // 카운터를 안 건드린 이유("지불한 것을 몰수하지 않는다")의
                // 연장이다.
                //
                // 기준이 st51인 것은 그것이 **v17의 게이트 그 자체**이기
                // 때문이다. 여기서 다른 값을 고르면 마이그레이션이 v17 세계의
                // 사실이 아니라 새 판단을 지어내는 것이 된다.
                if (data.maxStageReached >= SkillCurve.ExpansionStage)
                {
                    // v17 세계의 가챠 몫은 혈폭·혈조 둘이었다. 15종 재설계로
                    // 표준 풀이 다섯이 됐지만 **여기서 주는 것은 그때의 둘**이다 -
                    // 마이그레이션은 v17이 실제로 갖고 있던 것을 보존하는
                    // 자리이지 새 판단을 지어내는 자리가 아니다
                    data.gachaSkillIds = new[]
                    {
                        SkillCatalog.BloodBurstId,
                        SkillCatalog.BloodWhipId
                    };
                }
                else
                {
                    data.gachaSkillIds = new string[0];
                }

                data.version = 18;
            }

            if (data.version == 18)
            {
                // v18에는 이름이 없었다. **빈 문자열로 둔다** - 지어내지
                // 않는다는 규칙(49단계 skillEquipped)과 같은 자리다.
                //
                // 여기서 기본 이름을 적어 넣으면 옛 플레이어 전원이 "이름을
                // 직접 고른 사람"이 되어, 랭킹 첫 진입에서 이름 입력을 띄우는
                // 판단(PlayerProfile.HasChosenName)이 그들에게만 영원히 거짓이
                // 된다. 표시할 이름이 필요한 자리는 PlayerProfile.Name이
                // 기본값으로 채운다 - 세이브가 그 값을 굳힐 이유가 없다.
                //
                // 밸런스·진행은 한 글자도 안 바뀐다. 도달층은 v12부터 있던
                // maxStageReached 그대로이고, 리더보드는 그것을 읽기만 한다.
                data.playerName = string.Empty;
                data.version = 19;
            }

            if (data.version == 19)
            {
                // ---- 세 칸 다 기본값이다. **소급하지 않는다.**
                //
                // `skillGachaAwakenPity`를 누적 뽑기 수(min(total, 99))로
                // 소급하고 싶어지는데, 그 방식은 아무것도 인정하지 못한다.
                // 하드 천장이 재는 것은 **마지막 ★5 이후**의 횟수인데
                // `skillGachaTotalPulls`는 누적일 뿐이다 - 200회를 돌며 ★5를
                // 세 번 받은 플레이어와 한 번도 못 받은 플레이어가 같은 200을
                // 갖는다. 전자에게 99를 주면 과보상이고, 후자는 200회를
                // 기다렸는데 "1회 남았다"는 말을 듣는다.
                //
                // 게다가 v19의 ★5는 개안이라 **해금 이력이 저장되지 않았다.**
                // 복원할 원본이 세이브 안에 없다.
                data.skillGachaAwakenPity = 0;

                // ---- 그 대신 **전원에게 무료 10연을 준다.**
                //
                // 하드 천장을 0에서 시작시키는 것의 보상이다. v14->v15가 요도
                // 무료 뽑기를, v17->v18이 오의 무료 뽑기를 접속 즉시 하나씩
                // 쥐어 준 것과 같은 자리 - 새 시스템이 열리는 것이지 소급이
                // 아니다.
                data.skillGachaIntroClaimed = false;
                data.skillGachaIntroEquipDone = false;

                // ---- **신규 일곱의 칸을 만든다.**
                //
                // v6 -> v7과 v16 -> v17이 각자 그때의 카탈로그에 칸을 만들어
                // 준 것과 같은 처리다. 그 두 단계는 v19 세이브를 안 지나므로,
                // 여기서 안 만들면 열다섯 중 여덟만 칸을 가진 채로 남는다.
                //
                // 런타임이 다음 저장에서 채워 주기는 한다(SkillSystem이 슬롯을
                // 열다섯 개 들고 있다). 그래도 여기서 만드는 이유는 **세이브가
                // 스스로 일관되어야** 하기 때문이다 - 로드 직후와 저장 직후의
                // 세이브가 다르면 그 차이를 아는 사람이 아무도 없다.
                //
                // 레벨은 1이다. 소급이 아니라 없던 것이 생기는 것이고, 어차피
                // 뽑기로 열기 전에는 목록에 안 뜬다(GachaGated)
                foreach (var skill in SkillCatalog.Skills) EnsureSkill(data, skill.Id);

                // ---- 신규 오의 일곱은 여기서 손댈 것이 없다.
                //
                // 세이브가 id 병렬 배열이라(skillIds/skillLevels) 옛 세이브에
                // 없는 id는 Lv.1 미보유로 자연 처리된다. 다만 **다음 저장에서
                // 열다섯이 전부 수집되는지**는 별개 문제라 테스트가 잰다
                // (TheSaveV20_RoundTripsAllFifteenSkillIds).
                data.version = 20;
            }

            if (data.version == 20)
            {
                /**
                 * @brief v20 -> v21. **의미 변경의 마이그레이션이다.**
                 *
                 * 필드는 하나도 안 늘었다. `evolutionTier`가 "재화로 산 티어"에서
                 * "귀문을 돌파해 보유한 티어"로 뜻만 바뀌었고, 새 뜻에서 옛 값이
                 * 거짓일 수 있어 변환이 필요하다 - st200을 지난 플레이어의
                 * 티어가 0인 세이브가 실제로 존재한다.
                 *
                 *     evolutionTier = max(기존, gateStage < maxStageReached 인 문의 수)
                 *
                 * `max`인 이유가 셋이다 - ① 산 것을 뺏지 않는다 ② 진행을
                 * 되돌리지 않는다 ③ 밴드가 게이트 기반 티어를 가정하므로 문을
                 * 지난 플레이어는 그 티어를 가져야 계약이 성립한다.
                 *
                 * 부등호가 `<`인 것이 이 마이그레이션의 전부다. `maxStageReached`는
                 * 최전선이지 클리어 기록이 아니라, 등호를 쓰면 st30에 **도착만**
                 * 한 플레이어가 일문을 전투 없이 받는다.
                 *
                 * 멱등이다 - `max`라 두 번 돌려도 안 움직인다. Firebase의 `max`
                 * 병합과 같은 규칙이라 클라이언트와 서버가 다른 산수를 하지도 않는다.
                 *
                 * 이름·외형·오라는 따로 저장하지 않는다. 전부 이 한 값에서
                 * 유도되므로(`EvolutionCatalog`) 티어가 오르는 순간 함께 온다.
                 */
                ApplyGateEvolutionTier(data);
                data.version = 21;
            }

            data.version = CurrentVersion;
            return true;
        }

        // ---------------------------------------------------------------- v21 (예정)

        /**
         * @brief **v20 -> v21 마이그레이션. 설계만 되어 있고 아직 연결하지 않았다.**
         *
         * ## 왜 함수는 있는데 `Migrate`가 안 부르는가
         *
         * 실제 귀문 전투가 없기 때문이다. `CurrentVersion`을 21로 올리면 다음
         * 실행에서 라이브 세이브가 전부 변환되고, 그 변환은 **되돌릴 수 없다**
         * (v21 세이브를 v20 클라이언트가 읽으면 `Migrate`가 false를 내고 새
         * 게임이 된다). 전투도 게이트도 없는 상태에서 그 문을 열 이유가 없다.
         *
         * 그래서 2단계는 **함수와 계약만** 둔다. 3단계가 `CurrentVersion = 21`,
         * `Migrate`의 v20 분기, `StageProgress`의 게이트 판정, `BossFight`의
         * 귀문 모드를 **하나의 원자적 변경**으로 붙인다 - 넷 중 하나만 들어간
         * 빌드가 나가면 진행이 막히거나 티어가 거짓이 된다.
         *
         * ## 규칙
         *
         *     evolutionTier = max(기존 evolutionTier, 지나온 문의 수)
         *     지나온 문의 수 = gateStage < maxStageReached 인 문의 개수
         *
         * `max`인 이유가 셋이다 - ① 산 것을 뺏지 않는다 ② 진행을 되돌리지
         * 않는다 ③ **밴드가 게이트 기반 티어를 가정하므로**, 문을 지난
         * 플레이어는 그 티어를 가져야 계약이 성립한다.
         *
         * 부등호가 `<`인 것이 이 마이그레이션의 전부다. `maxStageReached`는
         * 최전선이지 클리어 기록이 아니다 - st30에 **도착한** 상태는 st30을
         * 클리어한 상태가 아니므로, 등호를 쓰면 문 하나가 공짜로 열린다.
         *
         * 멱등이다 - `max`라 두 번 돌려도 안 움직인다. Firebase의 `max` 병합과
         * 같은 규칙이라 클라이언트와 서버가 다른 산수를 하지도 않는다.
         *
         * 기본 이름·외형·오라는 따로 저장하지 않는다. 전부 이 한 값에서
         * 유도되므로(`EvolutionCatalog`), 티어가 오르는 순간 함께 온다.
         */
        public static int GateEvolutionTierFor(int existingTier, int maxStageReached)
        {
            int fromGates = PromotionTrialCatalog.TierAtFrontier(maxStageReached);
            int merged = Mathf.Max(existingTier, fromGates);

            // 세이브 손상이 스탯이 되지 않게 상한에서 자른다 -
            // EvolutionSystem.Restore와 같은 규칙이다
            return Mathf.Clamp(merged, 0, EvolutionCurve.MaxTier);
        }

        /**
         * @brief 위 규칙을 세이브에 적용한다. **3단계의 v20 -> v21 분기가 부를 자리다.**
         *
         * `maxStageReached`가 `stage`보다 작게 저장된 세이브가 있을 수 있어
         * (v11 이전에서 올라온 값이 손상된 경우) 둘 중 큰 쪽을 최전선으로 읽는다 -
         * `StageProgress.MaxStageReached`와 같은 판단이다.
         *
         * @return 티어가 실제로 올랐으면 true
         */
        public static bool ApplyGateEvolutionTier(SaveData data)
        {
            if (data == null) return false;

            int frontier = Mathf.Max(data.maxStageReached, data.stage);
            int before = data.evolutionTier;

            data.evolutionTier = GateEvolutionTierFor(before, frontier);
            return data.evolutionTier != before;
        }

        /**
         * @brief 세이브에 없는 전설 요도를 **미보유**로 추가한다.
         *
         * EnsureYodo와 같은 규칙이다 - 이미 있으면 건드리지 않는다(멱등).
         * 마이그레이션이 두 번 돌 때 사본이 0으로 되돌아가면 200회에 한 번
         * 나오는 것이 사라진다.
         */
        private static void EnsureLegendaryYodo(SaveData data, string id)
        {
            if (data.legendaryYodoIds == null) data.legendaryYodoIds = new string[0];
            if (data.legendaryYodoCopies == null) data.legendaryYodoCopies = new int[0];

            for (int i = 0; i < data.legendaryYodoIds.Length; i++)
                if (data.legendaryYodoIds[i] == id) return;

            int index = data.legendaryYodoIds.Length;
            Array.Resize(ref data.legendaryYodoIds, index + 1);
            Array.Resize(ref data.legendaryYodoCopies, index + 1);

            data.legendaryYodoIds[index] = id;
            data.legendaryYodoCopies[index] = 0;
        }

        /**
         * @brief 세이브에 없는 요도를 미봉인 + 혼 0으로 추가한다.
         *
         * EnsureTrack·EnsureSkill·EnsureEquipment·EnsurePet과 같은 규칙이다.
         * 이미 있으면 건드리지 않는다(멱등) - 마이그레이션이 두 번 돌 때
         * 티어가 0으로 되돌아가면 한 바퀴가 통째로 사라진다.
         */
        private static void EnsureYodo(SaveData data, string id)
        {
            if (data.yodoIds == null) data.yodoIds = new string[0];
            if (data.yodoSouls == null) data.yodoSouls = new long[0];
            if (data.yodoTiers == null) data.yodoTiers = new int[0];
            if (data.yodoDiscovered == null) data.yodoDiscovered = new int[0];

            for (int i = 0; i < data.yodoIds.Length; i++)
                if (data.yodoIds[i] == id) return;

            var ids = new string[data.yodoIds.Length + 1];
            var souls = new long[ids.Length];
            var tiers = new int[ids.Length];
            var discovered = new int[ids.Length];

            for (int i = 0; i < data.yodoIds.Length; i++)
            {
                ids[i] = data.yodoIds[i];
                // 예전 세이브의 배열 길이가 어긋나 있을 수 있다. 짧은 쪽을
                // 넘어가면 0으로 채운다 - 손상된 파일이 예외를 던지지 않게
                souls[i] = i < data.yodoSouls.Length ? data.yodoSouls[i] : 0L;
                tiers[i] = i < data.yodoTiers.Length ? data.yodoTiers[i] : 0;
                discovered[i] = i < data.yodoDiscovered.Length ? data.yodoDiscovered[i] : 0;
            }

            ids[ids.Length - 1] = id;
            souls[souls.Length - 1] = 0L;
            tiers[tiers.Length - 1] = 0;
            discovered[discovered.Length - 1] = 0;

            data.yodoIds = ids;
            data.yodoSouls = souls;
            data.yodoTiers = tiers;
            data.yodoDiscovered = discovered;
        }

        /**
         * @brief 강화 레벨을 미세화 격자로 환산한다 (43단계 전용).
         *
         * 새 레벨 = (옛 레벨 - 1) x 계수 + 1. 값 등가다 - 옛 Lv.84의 배수와
         * 새 Lv.665의 배수가 같다(스텝이 계수 제곱근 관계라서). 반올림 오차는
         * 반 칸(공격력 기준 +-0.7%) 이내다.
         *
         * **v12 블록 안에서만 부른다.** 멱등성은 버전 게이트가 지킨다 -
         * v13 세이브는 이 길을 다시 지나지 않으므로 두 번 곱해질 수 없다.
         */
        private static void ConvertLevel(SaveData data, string id, double factor)
        {
            if (data.upgradeIds == null || data.upgradeLevels == null) return;

            for (int i = 0; i < data.upgradeIds.Length && i < data.upgradeLevels.Length; i++)
            {
                if (data.upgradeIds[i] != id) continue;

                int old = Mathf.Max(1, data.upgradeLevels[i]);
                data.upgradeLevels[i] = (int)Math.Round((old - 1) * factor) + 1;
                return;
            }
        }

        /**
         * @brief 세이브에 없는 펫을 잠금 + Lv.1로 추가한다.
         *
         * EnsureTrack·EnsureSkill·EnsureEquipment와 같은 규칙이다. 이미 있으면
         * 건드리지 않는다(멱등) - 마이그레이션이 두 번 돌 때 해금이 잠금으로
         * 되돌아가면 보석이 사라진다.
         */
        private static void EnsurePet(SaveData data, string id)
        {
            if (data.petIds == null) data.petIds = new string[0];
            if (data.petUnlocked == null) data.petUnlocked = new int[0];
            if (data.petLevels == null) data.petLevels = new int[0];

            for (int i = 0; i < data.petIds.Length; i++)
                if (data.petIds[i] == id) return;

            var ids = new string[data.petIds.Length + 1];
            var unlocked = new int[ids.Length];
            var levels = new int[ids.Length];

            for (int i = 0; i < data.petIds.Length; i++)
            {
                ids[i] = data.petIds[i];
                // 예전 세이브의 배열 길이가 어긋나 있을 수 있다. 짧은 쪽을
                // 넘어가면 잠금/Lv.1로 채운다 - 손상된 파일이 예외를 던지지 않게
                unlocked[i] = i < data.petUnlocked.Length ? data.petUnlocked[i] : 0;
                levels[i] = i < data.petLevels.Length ? data.petLevels[i] : 1;
            }

            ids[ids.Length - 1] = id;
            unlocked[unlocked.Length - 1] = 0;
            levels[levels.Length - 1] = 1;

            data.petIds = ids;
            data.petUnlocked = unlocked;
            data.petLevels = levels;
        }

        /**
         * @brief 세이브에 없는 장비 슬롯을 1등급 Lv.1로 추가한다.
         *
         * EnsureTrack·EnsureSkill과 같은 규칙이다. 이미 있으면 건드리지 않는다
         * (멱등) - 저장 실패 후 재시도 같은 경로에서 마이그레이션이 실제로 두 번
         * 돌 수 있고, 그때 등급이 1로 초기화되면 보석이 사라진다.
         */
        private static void EnsureEquipment(SaveData data, string id)
        {
            if (data.equipmentIds == null) data.equipmentIds = new string[0];
            if (data.equipmentGrades == null) data.equipmentGrades = new int[0];
            if (data.equipmentLevels == null) data.equipmentLevels = new int[0];

            for (int i = 0; i < data.equipmentIds.Length; i++)
                if (data.equipmentIds[i] == id) return;

            var ids = new string[data.equipmentIds.Length + 1];
            var grades = new int[ids.Length];
            var levels = new int[ids.Length];

            for (int i = 0; i < data.equipmentIds.Length; i++)
            {
                ids[i] = data.equipmentIds[i];
                // 예전 세이브의 배열 길이가 어긋나 있을 수 있다. 짧은 쪽을
                // 넘어가면 1로 채운다 - 손상된 파일이 예외를 던지지 않게
                grades[i] = i < data.equipmentGrades.Length ? data.equipmentGrades[i] : 1;
                levels[i] = i < data.equipmentLevels.Length ? data.equipmentLevels[i] : 1;
            }

            ids[ids.Length - 1] = id;
            grades[grades.Length - 1] = 1;
            levels[levels.Length - 1] = 1;

            data.equipmentIds = ids;
            data.equipmentGrades = grades;
            data.equipmentLevels = levels;
        }

        /**
         * @brief 세이브에 없는 오의를 레벨 1로 추가한다. EnsureTrack과 같은 규칙.
         *
         * 이미 있으면 건드리지 않는다(멱등). 배열을 따로 두는 이유는 위
         * skillIds 주석 참고.
         */
        private static void EnsureSkill(SaveData data, string id)
        {
            if (data.skillIds == null) data.skillIds = new string[0];
            if (data.skillLevels == null) data.skillLevels = new int[0];

            for (int i = 0; i < data.skillIds.Length; i++)
                if (data.skillIds[i] == id) return;

            var ids = new string[data.skillIds.Length + 1];
            var levels = new int[ids.Length];

            for (int i = 0; i < data.skillIds.Length; i++)
            {
                ids[i] = data.skillIds[i];
                levels[i] = i < data.skillLevels.Length ? data.skillLevels[i] : 1;
            }

            ids[ids.Length - 1] = id;
            levels[levels.Length - 1] = 1;

            data.skillIds = ids;
            data.skillLevels = levels;
        }

        /**
         * @brief 세이브에 없는 강화 트랙을 레벨 1로 추가한다.
         *
         * 이미 있으면 건드리지 않는다. 마이그레이션이 두 번 돌아도(멱등성) 레벨이
         * 초기화되지 않아야 한다 - 저장 실패 후 재시도 같은 경로에서 실제로 두 번
         * 돌 수 있다.
         */
        private static void EnsureTrack(SaveData data, string id)
        {
            if (data.upgradeIds == null) data.upgradeIds = new string[0];
            if (data.upgradeLevels == null) data.upgradeLevels = new int[0];

            for (int i = 0; i < data.upgradeIds.Length; i++)
                if (data.upgradeIds[i] == id) return;

            var ids = new string[data.upgradeIds.Length + 1];
            var levels = new int[ids.Length];

            for (int i = 0; i < data.upgradeIds.Length; i++)
            {
                ids[i] = data.upgradeIds[i];
                // 예전 세이브의 두 배열 길이가 어긋나 있을 수 있다. 짧은 쪽을 넘어가면
                // 레벨 1로 채운다 - 손상된 파일이 예외를 던지지 않게
                levels[i] = i < data.upgradeLevels.Length ? data.upgradeLevels[i] : 1;
            }

            ids[ids.Length - 1] = id;
            levels[levels.Length - 1] = 1;

            data.upgradeIds = ids;
            data.upgradeLevels = levels;
        }

        /** 저장된 시각. 없으면 null (첫 실행이라 방치 보상이 없다) */
        public DateTime? LastQuitUtc
        {
            get
            {
                if (lastQuitUtcTicks <= 0L) return null;
                // 손상된 파일이 DateTime 생성자에서 예외를 던지지 않게 범위를 확인한다
                if (lastQuitUtcTicks < DateTime.MinValue.Ticks || lastQuitUtcTicks > DateTime.MaxValue.Ticks)
                {
                    Debug.LogWarning("[Onikiri] Save has an out-of-range timestamp; ignoring it.");
                    return null;
                }
                return new DateTime(lastQuitUtcTicks, DateTimeKind.Utc);
            }
        }

        /** 마지막 일일 리셋 날짜. 없으면 null (아직 한 번도 리셋한 적이 없다) */
        public DateTime? LastDailyResetUtc
        {
            get
            {
                if (lastDailyResetUtcTicks <= 0L) return null;
                if (lastDailyResetUtcTicks < DateTime.MinValue.Ticks
                    || lastDailyResetUtcTicks > DateTime.MaxValue.Ticks)
                {
                    Debug.LogWarning("[Onikiri] Save has an out-of-range daily reset stamp; ignoring it.");
                    return null;
                }
                return new DateTime(lastDailyResetUtcTicks, DateTimeKind.Utc);
            }
        }
    }
}
