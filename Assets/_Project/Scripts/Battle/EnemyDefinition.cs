using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 적 한 종류의 데이터.
     *
     * 스탯과 프레임을 코드가 아니라 에셋에 두어, 밸런싱과 새 요괴 추가에 재컴파일이
     * 필요하지 않게 한다 (사양서 5장).
     */
    [CreateAssetMenu(menuName = "Onikiri/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("식별")]
        public string displayName = "Chochin-obake";

        [Header("애니메이션")]
        [Tooltip("큐에서 대기 중에 반복 재생할 프레임")]
        public Sprite[] idleFrames;

        /**
         * @brief 걸어 들어오는 동안 재생할 프레임. **비어 있으면 idle을 쓴다.**
         *
         * 21단계까지는 이 칸이 없어서 모두가 idle로 걸어왔다. 잡몹은 작고 빨리
         * 지나가서 티가 안 났는데, 처형인처럼 크고 느린 보스가 **가만히 선 자세로
         * 미끄러져 오니** 곧바로 드러났다.
         *
         * 비어 있어도 되는 칸이다. 팩에 걷기 시트가 없는 요괴가 대부분이고
         * (도깨비불은 떠다닌다), 그런 경우 지금까지와 똑같이 동작해야 한다.
         */
        [Tooltip("걸어오는 동안 반복 재생할 프레임. 비우면 idle로 대체된다")]
        public Sprite[] walkFrames;
        [Tooltip("피격 시 짧게 재생. 비어 있으면 색 플래시로 대체된다")]
        public Sprite[] hurtFrames;
        [Tooltip("사망 시 한 번 재생. 마지막 프레임에서 풀로 반환된다")]
        public Sprite[] deathFrames;

        public float frameRate = 12f;

        [Header("스폰")]
        [Tooltip("이 종류가 등장할 상대 확률. 화면의 크기 구성을 결정한다. " +
                 "작은 필러는 높은 가중치, 정예는 낮은 가중치")]
        public float spawnWeight = 1f;

        [Header("전투")]
        [Tooltip("BigDouble인 이유는 후반 요괴 체력이 long 범위를 벗어나기 때문. " +
                 "데미지 숫자를 NumberFormatter로 찍는 이유도 같다")]
        public BigDouble maxHealth = BigDouble.FromDouble(12d);

        [Tooltip("이 요괴의 체력이 0이 되는 순간 지급할 골드")]
        public BigDouble goldReward = BigDouble.One;

        [Tooltip("오른쪽에서 걸어 들어올 때의 초당 이동 거리 (world units)")]
        public float moveSpeed = 1.1f;

        [Tooltip("큐에 정렬된 적 사이의 가로 간격 (world units)")]
        public float queueSpacing = 0.75f;

        [Header("공격 (보스 전용)")]
        [Tooltip("공격 시 재생할 프레임. 비어 있으면 idle 유지 + 이펙트로 대체한다. " +
                 "잡몹은 플레이어를 공격하지 않으므로 비워 둔다")]
        public Sprite[] attackFrames;

        /**
         * @brief 공격 클립 하나. 배열의 배열을 유니티가 직렬화하지 못해 감싼 것이다.
         */
        [System.Serializable]
        public sealed class AttackVariant
        {
            [Tooltip("이 공격의 프레임")]
            public Sprite[] frames;
        }

        /**
         * @brief **추가** 공격 동작들. 매 주기에 하나를 무작위로 고른다.
         *
         * 비워 두는 것이 기본이고, 비어 있으면 `attackFrames` 하나만 도는
         * 지금까지의 동작 그대로다. 잡몹과 대부분의 보스가 여기에 해당한다.
         *
         * 다크 사무라이(Inimig 9)만 채운다. 그 팩의 오의 블록에는 서로 다른
         * 공격이 넷 들어 있는데(혈참·혈조·혈륜·혈파) 한 벌만 쓰면 2초마다
         * 같은 그림이 돌아 금방 벽지가 된다.
         *
         * **길이가 서로 달라도 된다.** 예비 동작 시간은 고른 클립에서 그때그때
         * 유도한다(Enemy.UpdateAttack). 피해량과 주기는 어느 것을 고르든 같다 -
         * 고르는 것은 그림뿐이다.
         */
        [Tooltip("추가 공격 동작. 매 주기에 attackFrames와 함께 무작위로 고른다. " +
                 "비우면 attackFrames 하나만 쓴다")]
        public AttackVariant[] attackVariants;

        [Tooltip("공격 사이의 간격 (초). 0이면 공격하지 않는다")]
        public float attackInterval;

        [Tooltip("칼이 닿기까지 공격 애니메이션에서 지나가는 비율")]
        [Range(0f, 1f)]
        public float attackImpactPoint = 0.55f;

        /**
         * @brief 공격 그림이 **없을 때** 쓰는 예비 동작 시간 (초).
         *
         * 공격 프레임이 있으면 예비 동작 길이는 클립에서 나온다(attackImpactPoint).
         * 이 값은 클립이 아예 없는 요괴 - 잡몹 팩 대부분이다 - 가 확대판 보스로
         * 설 때만 쓰인다. 그때 "친다"를 알리는 것은 참격 이펙트뿐이고, 이펙트가
         * 타격과 같은 순간에 뜨면 이미 맞은 뒤라 예고가 되지 않는다.
         *
         * **피해량과 주기는 이 값을 보지 않는다.** 옮기는 것은 그림이 뜨는
         * 시각뿐이다 (Enemy.UpdateAttack 참고).
         */
        [Tooltip("공격 프레임이 없을 때 이펙트를 타격보다 얼마나 먼저 띄울지 (초). " +
                 "0이면 타격과 동시에 뜬다. 피해량·주기와 무관하다")]
        public float attackTelegraphSeconds;

        [Header("연출")]
        [Tooltip("지면선 위 높이. 걷지 않고 떠다니는 요괴용")]
        public float hoverHeight = 0.12f;

        [Tooltip("스프라이트 피벗(캔버스 하단)과 가장 아래 그려진 픽셀 사이의 거리. " +
                 "빌드 단계가 아트에서 측정해 채운다. 손으로 고치지 말 것")]
        public float artBottomOffset;

        /**
         * @brief 원본 아트가 **왼쪽을 보고** 그려졌는가.
         *
         * 적은 오른쪽에서 와서 왼쪽의 플레이어를 바라본다. 지금까지 쓴 팩들이
         * 전부 오른쪽을 보고 그려져 있어서 `Enemy`가 무조건 좌우를 뒤집었는데,
         * 그것은 팩의 성질을 코드에 굳혀둔 것이었다.
         *
         * 처형인 팩이 왼쪽을 보고 그려져 있어서 드러났다 - 뒤집으니 **플레이어에게
         * 등을 돌린 채** 싸웠다. 아트가 어느 쪽을 보는지는 팩마다 다르므로
         * 데이터여야 한다.
         */
        [Tooltip("원본 아트가 왼쪽을 보고 그려졌으면 체크. 적은 왼쪽을 바라봐야 하므로 " +
                 "이 값이 참이면 좌우를 뒤집지 않는다")]
        public bool artFacesLeft;
    }
}
