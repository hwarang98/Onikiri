using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 배경 레이어를 세우고, 지역이 바뀌면 통째로 갈아끼운다.
     *
     * ## 왜 런타임 코드인가
     *
     * 20단계까지 배경은 에디터 빌더가 씬에 만들어두는 정적 구조였다. 지역이
     * 하나뿐이라 그것으로 충분했다.
     *
     * 지역이 둘이 되면 **런타임에 교체**해야 한다. 그때 생성 코드가 에디터에
     * 하나, 런타임에 하나로 갈라지면 둘이 조용히 어긋난다 - 이 프로젝트가
     * 여러 번 겪은 종류의 사고다(보스 체력 계산이 두 곳에 있던 것, 처치 속도
     * 상한이 두 곳에 있던 것).
     *
     * 그래서 생성은 여기 하나뿐이고, 에디터 빌더는 초기 세트를 세울 때 이것을
     * 부른다. BossFight가 런타임에 보스를 스폰하고 빌더는 배선만 하는 것과
     * 같은 구조다.
     *
     * ## 정렬 순서
     *
     * `set.layers`의 배열 순서가 유일한 출처다. 이름으로 찾지 않으므로 팩마다
     * 레이어 이름이 `1.png`처럼 의미 없어도 상관없다.
     */
    public sealed class BackgroundStage : MonoBehaviour
    {
        [Tooltip("레이어가 만들어질 부모. 전투 밴드 바닥에 밑단이 맞춰진다")]
        [SerializeField] private Transform layerRoot;

        [Tooltip("하늘 채움이 들어갈 부모. 카메라 크기로 늘어나므로 밴드 밖이다")]
        [SerializeField] private Transform skyParent;

        [Tooltip("전진 속도의 출처. 랜드마크 간격을 이 값에서 유도한다")]
        [SerializeField] private StageAdvance advance;

        [SerializeField] private Camera targetCamera;

        /** 지금 세워져 있는 세트. 같은 것을 다시 요청하면 아무 일도 하지 않는다 */
        public RegionBackgroundSet Current { get; private set; }

        /** 하늘 채움 렌더러. BattleStageLayout이 카메라 크기로 늘린다 */
        public SpriteRenderer SkyFill { get; private set; }

        /** 배경이 바뀐 뒤 발생. 레이아웃이 다시 맞춰야 한다 */
        public event System.Action Rebuilt;

        private const string SkyFillName = "SkyFill";

        /**
         * @brief 세트를 세운다. 이미 같은 세트면 건드리지 않는다.
         *
         * 같은 세트를 걸러내는 것이 중요하다. 스테이지가 오를 때마다 불리는데
         * 매번 다시 만들면 스크롤 위치가 초기화되어 **배경이 한 프레임 튄다.**
         * 지역 안에서는 배경이 이어져야 한다.
         */
        public void Apply(RegionBackgroundSet set, bool force = false)
        {
            if (set == null) return;
            if (!force && Current == set) return;

            Current = set;
            Rebuild(set);

            var handler = Rebuilt;
            if (handler != null) handler();
        }

        private void Rebuild(RegionBackgroundSet set)
        {
            ClearChildren(layerRoot);
            ClearSky();

            if (targetCamera != null) targetCamera.backgroundColor = set.clearColor;

            int order = SortingOrders.BackgroundBase;

            foreach (var layer in set.layers)
            {
                if (layer == null || layer.sprite == null) continue;

                if (layer.isSkyFill)
                {
                    BuildSkyFill(layer);
                    continue;
                }

                BuildScrollingLayer(set, layer, layer.isGroundCover
                    ? SortingOrders.GroundCover
                    : order++);
            }
        }

        private void BuildSkyFill(RegionBackgroundSet.Layer layer)
        {
            var parent = skyParent != null ? skyParent : transform;

            var go = new GameObject(SkyFillName);
            go.transform.SetParent(parent, false);

            SkyFill = go.AddComponent<SpriteRenderer>();
            SkyFill.sprite = layer.sprite;
            SkyFill.color = layer.tint;
            SkyFill.sortingOrder = SortingOrders.SkyFill;
        }

        private void BuildScrollingLayer(RegionBackgroundSet set,
                                         RegionBackgroundSet.Layer layer, int sortingOrder)
        {
            var parent = layerRoot != null ? layerRoot : transform;

            var go = new GameObject(layer.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            float pieceWidth = layer.sprite.bounds.size.x;
            float span = pieceWidth * SpacingFor(layer, pieceWidth);

            int count = PieceCountFor(span);
            var pieces = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                var pieceObject = new GameObject(layer.name + "_" + i);
                pieceObject.transform.SetParent(go.transform, false);

                var renderer = pieceObject.AddComponent<SpriteRenderer>();
                renderer.sprite = layer.sprite;
                renderer.color = layer.tint;
                renderer.sortingOrder = sortingOrder;

                // 스프라이트가 중앙 피벗이므로 자기 높이의 절반만큼 올려야
                // 밑단이 루트 원점(밴드 바닥)에 온다.
                //
                // 땅에 서는 레이어는 지면 두께만큼 더 올린다 - 안 그러면 나무
                // 줄기가 지면에 잘려 잎만 공중에 뜬다.
                //
                // 다만 올리는 것은 **캔버스가 아니라 그려진 밑동**이다. 캔버스 바닥을
                // 지면선에 맞추면, 아트 아래에 투명 여백이 있는 팩에서 그만큼 뜬다 -
                // 봄 벚꽃(여백 16px)이 정확히 0.5u 떠 있었다. 여백을 빼면 밑동이
                // 지면선에 온다. Layer.artBottomPixels 참고
                float lift = layer.sitsOnGround
                    ? (set.groundSurfacePixels - layer.artBottomPixels) / DisplayConfig.PixelsPerUnit
                    : 0f;

                pieceObject.transform.localPosition =
                    new Vector3(i * span, layer.sprite.bounds.extents.y + lift, 0f);

                pieces[i] = pieceObject.transform;
            }

            var scroller = go.AddComponent<ParallaxScroller>();
            scroller.Configure(layer.scrollSpeed, span, pieces);
        }

        /**
         * @brief 랜드마크 사본 간격 배수. 1이면 빈틈없이 이어 붙인다.
         *
         * 간격을 거리가 아니라 **시간**에서 유도한다. 17단계에서 전진 속도를
         * 세 번 바꿨고 그때마다 같은 산수로 거리를 고쳤다 - 규칙적으로 고쳐야
         * 하는 것은 상수가 아니라 계산이다.
         */
        private float SpacingFor(RegionBackgroundSet.Layer layer, float pieceWidth)
        {
            if (layer.landmarkRepeatSeconds <= 0.001f) return 1f;
            if (pieceWidth <= 0.001f) return 1f;

            float advanceSpeed = advance != null ? advance.ScrollSpeed : 4.5f;
            float layerSpeed = layer.scrollSpeed * advanceSpeed;
            if (layerSpeed <= 0.001f) return 1f;

            // 최소 1배. 계산이 1 밑으로 내려가면 사본이 겹쳐 배경에 구멍이 난다
            return Mathf.Max(1f, layer.landmarkRepeatSeconds * layerSpeed / pieceWidth);
        }

        /**
         * @brief 무한 스크롤에 쓰는 사본 수. 폭에서 계산한다.
         *
         * 상수 2로 두면 화면보다 좁은 레이어에서 오른쪽에 구멍이 난다. 가을숲
         * 지면이 타일 순환에서 구운 스트립이라 정확히 그 경우가 됐다.
         */
        private static int PieceCountFor(float span)
        {
            if (span <= 0.001f) return 2;

            float visible = DisplayConfig.CameraWorldHeight
                            * DisplayConfig.ReferenceWidth / DisplayConfig.ReferenceHeight;

            return Mathf.Max(2, Mathf.CeilToInt(visible / span) + 1);
        }

        private void ClearSky()
        {
            var parent = skyParent != null ? skyParent : transform;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name == SkyFillName) DestroyNow(child.gameObject);
            }
            SkyFill = null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyNow(parent.GetChild(i).gameObject);
        }

        /**
         * @brief 에디터 빌드와 플레이 양쪽에서 즉시 지운다.
         *
         * `Destroy`는 프레임 끝에 지우므로, 빌더가 곧바로 새 레이어를 만들면
         * 옛것과 새것이 한 프레임 겹친다. 에디터에서는 아예 저장되지 않아
         * 리빌드마다 사본이 쌓인다 - 11단계 유령 행과 같은 사고다.
         */
        private static void DestroyNow(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
