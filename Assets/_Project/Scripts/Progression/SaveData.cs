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
         *
         * 모르는(더 높은) 버전이면 새 게임으로 시작한다. 낮은 버전은 Migrate가 올린다.
         */
        public const int CurrentVersion = 4;

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
                lastQuitUtcTicks = 0L,
                goldPerSecond = 0d
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

            data.version = CurrentVersion;
            return true;
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
    }
}
