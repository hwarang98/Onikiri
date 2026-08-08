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
         *
         * 모르는(더 높은) 버전이면 새 게임으로 시작한다. 낮은 버전은 Migrate가 올린다.
         */
        public const int CurrentVersion = 9;

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

            data.version = CurrentVersion;
            return true;
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
