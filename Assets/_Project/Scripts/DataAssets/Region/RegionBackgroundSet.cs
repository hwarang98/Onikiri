using System;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 지역 하나가 쓰는 배경 레이어 한 벌.
     *
     * ## 왜 데이터로 옮겼는가
     *
     * 20단계까지 배경은 `BattleStageBuilder` 안의 문자열 배열과 `switch` 문이었다.
     * 레이어 이름으로 속도를 찾고 틴트를 찾는 구조라, 지역이 둘이 되는 순간
     * 그 switch가 두 팩의 레이어 이름을 모두 알아야 한다 - 지역이 넷이면 넷 다.
     *
     * 보스 배치를 `RegionConfig`로 옮긴 것과 같은 이유다. 지역마다 달라지는 것은
     * 코드가 아니라 데이터다.
     *
     * ## 순서가 곧 깊이다
     *
     * `layers`의 순서가 뒤에서 앞이고, 그것이 정렬 순서와 패럴랙스 배정의
     * 유일한 출처다. 이름으로 찾지 않으므로 팩마다 레이어가 `1.png`처럼
     * 의미 없는 이름이어도 상관없다 - 가을숲이 정확히 그렇다.
     */
    [CreateAssetMenu(fileName = "Background_", menuName = "Onikiri/Region Background Set")]
    public sealed class RegionBackgroundSet : ScriptableObject
    {
        /**
         * @brief 레이어 하나의 배치값.
         *
         * 깊이 틴트를 이름이 아니라 값으로 들고 있는 이유는, 팩마다 원본 톤이
         * 달라서 같은 "중경"이라도 눌러야 할 양이 다르기 때문이다. 지역 1은
         * 밝고 따뜻한 팩이라 자줏빛으로 눌렀지만, 가을숲은 이미 남색이라
         * 같은 값을 곱하면 검게 죽는다.
         */
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("씬에 만들어질 오브젝트 이름. 팩의 파일명과 같을 필요는 없다")]
            public string name = "Layer";

            public Sprite sprite;

            [Tooltip("지면(1.0) 대비 스크롤 상대 속도. 0이면 고정 - 무한히 먼 하늘")]
            [Range(0f, 1f)]
            public float scrollSpeed = 0.5f;

            [Tooltip("이 레이어에 곱할 색. 깊이감은 이 값들의 차이에서 나온다")]
            public Color tint = Color.white;

            /**
             * @brief 카메라 전체를 덮는 배경 채움인가.
             *
             * 하늘처럼 스크롤하지 않고 화면을 통째로 칠하는 레이어다. 다른
             * 레이어는 전투 밴드에 밑단이 붙지만 이것은 BattleStageLayout이
             * 카메라 크기로 늘린다.
             */
            public bool isSkyFill;

            [Tooltip("파이터보다 앞에 그린다. 발을 가로지르는 풀 같은 것")]
            public bool isGroundCover;

            /**
             * @brief 밴드 바닥이 아니라 **지면 표면**에 밑단을 맞춘다.
             *
             * 나무처럼 땅에 뿌리내린 것에 쓴다. 기본은 false로, 하늘·산·안개처럼
             * 지평선 너머에 있는 것들은 밴드 바닥이 맞다.
             *
             * ## 왜 필요한가
             *
             * 지역 1은 지면 그림이 얇아서(0.75u) 나무 밑동이 조금 가려지는 정도라
             * 아무도 눈치채지 못했다. 가을숲은 지면이 2u라 **단풍 줄기가 통째로
             * 잘리고 잎 덩어리만 남았다** - 나무가 공중에 뜬 그림이 된다.
             *
             * 지면 두께는 팩마다 다르므로 "얼마나 올릴지"를 손으로 적으면 팩을
             * 바꿀 때마다 다시 재야 한다. 그 값은 이미 groundSurfacePixels에
             * 있으므로 여기서는 **올릴지 말지만** 정한다.
             */
            public bool sitsOnGround;

            /**
             * @brief 캔버스 바닥에서 **그려진 밑동**까지의 빈 줄 수 (원본 픽셀).
             *
             * `sitsOnGround`가 켜진 레이어에서만 쓴다. 캔버스 바닥을 지면선에 맞추는
             * 것만으로는 부족하기 때문이다 - 팩마다 아트가 캔버스 안 어디서 시작하는지가
             * 다르다.
             *
             *     가을 Trees.png    바닥 여백  0px  -> 캔버스 바닥 = 밑동
             *     봄   Color 1.png  바닥 여백 16px  -> 캔버스 바닥보다 0.5u 위가 밑동
             *
             * 이 값을 안 보면 봄 벚꽃이 정확히 16px(0.5u) 떠서 심어지지 않은 그림이 된다.
             * 실제로 24단계에 그렇게 나왔다.
             *
             * 요괴의 `EnemyDefinition.artBottomOffset`과 같은 값이고 같은 이유다 -
             * 캔버스가 아니라 **그려진 것**을 기준으로 세워야 한다. 손으로 적지 않고
             * 빌더가 픽셀을 실측해 채운다.
             */
            public float artBottomPixels;

            /**
             * @brief 랜드마크 재등장 목표 주기 (초). 0이면 빈틈없이 이어 붙인다.
             *
             * 탑이나 초롱처럼 그림의 일부만 차지하는 레이어에 쓴다. 사본을
             * 넓게 벌려도 배경에 구멍이 나지 않고, 벌린 만큼 같은 것이 다시
             * 나오기까지의 시간이 길어진다.
             *
             * 거리가 아니라 시간인 이유는 17단계에서 배운 것이다 - 전진 속도가
             * 바뀔 때마다 거리를 고쳐야 했고, 세 번 다 같은 산수였다.
             */
            public float landmarkRepeatSeconds;
        }

        [Tooltip("에디터에서 알아보기 위한 이름. 화면에는 안 뜬다")]
        public string displayName = "지역 1 배경";

        [Tooltip("뒤에서 앞 순서. 이 순서가 정렬 순서를 정한다")]
        public Layer[] layers = new Layer[0];

        [Tooltip("카메라 클리어 색. 배경이 덮지 못한 곳에 비친다")]
        public Color clearColor = new Color32(0x2A, 0x27, 0x40, 0xFF);

        /**
         * @brief 배경 밑단에서 걸을 수 있는 표면까지의 높이 (원본 픽셀).
         *
         * 지면 앵커가 이 값으로 정해진다. 팩마다 지면 그림의 두께가 다르므로
         * 데이터여야 한다 - 지역 1은 24px(353개 열의 최빈값), 가을숲은 타일
         * 한 칸이 표면이라 32px다.
         */
        public float groundSurfacePixels = 24f;

        /** 배경 스프라이트의 세로 크기 (원본 픽셀). 밴드에 맞추는 배율을 여기서 낸다 */
        public float backgroundPixelHeight = 180f;
    }
}
