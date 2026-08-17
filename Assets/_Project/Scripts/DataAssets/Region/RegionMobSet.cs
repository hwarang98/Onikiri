using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역 하나의 잡몹 한 벌. 배경 세트(RegionBackgroundSet)와 같은 자리다.
     *
     * 36단계 전에는 잡몹이 씬에 한 번 배선된 세 종류로 고정이라, 요괴 소굴에서도
     * 봄숲의 초록 등딱지가 나왔다. 배경이 지역마다 바뀌는데 그 위를 걷는 것이
     * 같으면 "새 지역"은 벽지만 바뀐 것이 된다.
     *
     * ## 스탯은 여기 없다
     *
     * 각 정의(EnemyDefinition)가 자기 가중치·체력·골드를 들고 있고, **모든 지역
     * 풀의 가중 평균은 같아야 한다** (주력 w5: HP12/골드5 + 부몹 w4: HP17/골드6
     * = 평균 128/9, 49/9 - 8단계부터 쓰던 필드 평균 그대로). 보스 체력과 방치
     * 보상이 스포너의 가중 평균에서 유도되기 때문에, 평균이 지역마다 다르면
     * 지역을 넘는 순간 밸런스가 움직인다. RegionMobPoolTests가 이 평균을 지킨다.
     */
    [CreateAssetMenu(fileName = "RegionMobs_", menuName = "Onikiri/Region Mob Set")]
    public sealed class RegionMobSet : ScriptableObject
    {
        [Tooltip("이 지역에서 스폰되는 잡몹 정의들. 가중치는 각 정의가 들고 있다")]
        public EnemyDefinition[] mobs;
    }
}
