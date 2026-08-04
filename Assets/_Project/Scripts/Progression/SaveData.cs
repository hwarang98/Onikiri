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
        /** 형식이 바뀌면 올린다. 읽을 때 모르는 버전이면 새 게임으로 시작한다 */
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        public BigDouble gold;
        public BigDouble lifetimeGold;

        public string[] upgradeIds = new string[0];
        public int[] upgradeLevels = new int[0];

        public int stage = 1;
        public int killsThisStage;

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
                lastQuitUtcTicks = 0L,
                goldPerSecond = 0d
            };
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
