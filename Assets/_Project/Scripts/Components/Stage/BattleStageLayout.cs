using Onikiri.Core;
using UnityEngine;

namespace Onikiri.Battle
{
    /**
     * @brief 전투 스테이지를 카메라가 아니라 UI BattleArea 밴드에 고정한다.
     *
     * CropFrame.None에서는 세로가 긴 기기일수록 카메라가 월드를 더 보여주므로,
     * 고정된 월드 좌표는 9:19.5 / 9:21 기기에서 전투 밴드를 벗어난다. 그래서 해상도가
     * 바뀔 때마다 밴드의 실제 화면 rect를 읽어 배경과 지면선을 그 안에 다시 앉힌다.
     *
     * Pixel Perfect Camera 뒤(실행 순서 1000)에 돈다.
     */
    [ExecuteAlways]
    [DefaultExecutionOrder(1000)]
    public sealed class BattleStageLayout : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private RectTransform battleArea;
        [Tooltip("패럴랙스 레이어의 부모. 이 원점이 배경의 밑단이 된다")]
        [SerializeField] private Transform backgroundRoot;
        [Tooltip("지면 표면에 놓이는 빈 트랜스폼. 캐릭터가 여기서 Y를 가져간다")]
        [SerializeField] private Transform groundAnchor;

        [Header("배경")]
        [Tooltip("배경 밑단에서 그려진 지면 표면까지의 높이 (원본 픽셀)")]
        [SerializeField] private float groundSurfacePixels = 30f;
        [Tooltip("배경 아트의 원본 픽셀 높이. 세로 커버 여부를 보고하는 데 쓴다")]
        [SerializeField] private float backgroundPixelHeight = 180f;
        [Tooltip("선택 사항. 지정하면 레이아웃이 흔들리지 않은 카메라 위치를 따라간다. " +
                 "그래야 타격 흔들림이 스스로 상쇄되지 않고 실제로 화면을 움직인다")]
        [SerializeField] private ScreenShake cameraShake;
        [Tooltip("카메라 전체를 덮도록 타일링되는 하늘. 모든 패럴랙스 레이어보다 뒤")]
        [SerializeField] private SpriteRenderer skyFill;
        [Tooltip("카메라 경계 바깥으로 더 그리는 하늘 여유분. 이음매의 반올림을 가린다")]
        [SerializeField] private float skyOverscan = 1f;

        /**
         * @brief 타격 흔들림을 제거한 카메라 위치.
         *
         * 여기서는 월드 콘텐츠를 전부 카메라 기준으로 배치하므로, 흔들린 위치를 따라가면
         * 배경이 카메라와 똑같이 움직여 흔들림이 보이지 않게 된다.
         */
        private Vector3 CameraBasePosition
        {
            get { return cameraShake != null ? cameraShake.BasePosition : targetCamera.transform.position; }
        }

        /** 캐릭터가 서는 지면의 월드 Y */
        public float GroundY { get; private set; }

        /**
         * @brief 배경이 바뀔 때 지면선 기준을 갈아끼운다.
         *
         * 팩마다 지면 그림의 두께가 다르다 - 지역 1은 밑단에서 표면까지 24px,
         * 가을숲은 타일에서 구운 스트립이라 64px다. 배경만 갈고 이 값을 두면
         * **캐릭터가 흙 속에 서거나 공중에 뜬다.**
         *
         * 인스펙터 값이 아니라 배경 세트가 출처가 된다는 뜻이고, 그래야 지역이
         * 늘어날 때마다 씬을 손보지 않아도 된다.
         */
        public void SetBackgroundMetrics(float surfacePixels, float pixelHeight)
        {
            groundSurfacePixels = surfacePixels;
            backgroundPixelHeight = pixelHeight;
            Apply();
        }

        /**
         * @brief 배경이 바뀌면 하늘 채움도 새것으로 갈아끼운다.
         *
         * ## 왜 필요한가 — 21단계부터 조용히 깨져 있었다
         *
         * 이 참조는 씬을 빌드할 때 한 번 꽂힌다. 그런데 {@link BackgroundStage}는 지역이
         * 바뀔 때 하늘 채움 오브젝트를 **지우고 새로 만든다.** 그러면 여기 들고 있던
         * 것은 파괴된 오브젝트가 되고, `UpdateSkyFill`이 첫 줄에서 돌아나간다 -
         * 그 뒤로 하늘은 카메라 크기로 늘어나지 않고 원본 크기(12x6.75u)에 멈춘다.
         *
         * **지역 1과 2에서는 아무도 눈치채지 못했다.** 두 지역 모두 카메라 클리어 색이
         * 자기 하늘과 비슷해서, 하늘이 덮지 못한 자리에 비슷한 색이 비쳤기 때문이다.
         * 24단계에 봄숲(밝은 하늘 + 어두운 클리어)이 들어오면서 화면 위아래에 회색
         * 띠로 드러났다.
         *
         * 세로 커버가 이 한 줄에 달려 있다. 배경 아트는 216px(6.75u)인데 9:19.5 기기의
         * 전투 밴드가 6.01u, 9:21은 더 크다 - 밴드는 덮지만 그 위 카메라 영역은 하늘
         * 채움만이 덮을 수 있다.
         */
        public void SetSkyFill(SpriteRenderer renderer)
        {
            skyFill = renderer;
            Apply();
        }

        /** 월드 단위로 표현한 전투 밴드 */
        public BattleLayout.Band Band { get; private set; }

        /**
         * @brief 배경 아트가 밴드를 세로로 완전히 덮으면 true.
         *
         * false면 그 위 여백이 카메라 클리어 색으로 보이고 있다는 뜻이다. 클리어 색이
         * 하늘과 같기 때문에만 문제가 되지 않는다.
         */
        public bool BackgroundCoversBand { get; private set; }

        private void OnEnable()
        {
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        /**
         * @brief 참조가 빠졌다고 이미 알렸는지.
         *
         * 매 프레임 도는 코드라 그냥 로그를 남기면 콘솔이 잠기지만, 조용히 넘어가면
         * 배경이 마지막으로 성공했을 때의 좌표에 얼어붙은 채로 남아 원인을 찾기 어렵다.
         * 한 번만 알린다.
         */
        private bool reportedMissingReferences;

        private void Apply()
        {
            if (targetCamera == null || battleArea == null)
            {
                if (!reportedMissingReferences)
                {
                    reportedMissingReferences = true;
                    Debug.LogError("[Onikiri] BattleStageLayout is missing " +
                                   (targetCamera == null ? "targetCamera " : "") +
                                   (battleArea == null ? "battleArea " : "") +
                                   "- the stage will stay frozen at its last good position.", this);
                }
                return;
            }
            reportedMissingReferences = false;

            int screenWidth = targetCamera.pixelWidth;
            int screenHeight = targetCamera.pixelHeight;
            if (screenWidth <= 0 || screenHeight <= 0) return;

            // 해상도가 바뀔 때만이 아니라 매 프레임 다시 계산한다. 첫 결과를 캐싱했더니
            // 캔버스와 카메라가 안정되기 전의 잘못된 값을 물고 영영 고치지 않았다
            int pixelRatio = BattleLayout.PixelRatio(
                screenWidth, screenHeight, DisplayConfig.ReferenceWidth, DisplayConfig.ReferenceHeight);
            float cameraWorldHeight = BattleLayout.CameraWorldHeight(
                screenHeight, pixelRatio, DisplayConfig.PixelsPerUnit);
            float cameraWorldWidth = screenWidth / (float)(pixelRatio * DisplayConfig.PixelsPerUnit);

            Band = MeasureBand(screenHeight, cameraWorldHeight);
            GroundY = BattleLayout.GroundY(Band, groundSurfacePixels, DisplayConfig.PixelsPerUnit);

            float backgroundTop = Band.Bottom + backgroundPixelHeight / DisplayConfig.PixelsPerUnit;
            BackgroundCoversBand = backgroundTop >= Band.Top;

            if (backgroundRoot != null) SetY(backgroundRoot, Band.Bottom);
            if (groundAnchor != null) SetY(groundAnchor, GroundY);

            UpdateSkyFill(cameraWorldWidth, cameraWorldHeight);
        }

        /**
         * @brief 하늘을 카메라 전체를 덮도록 크기 조정한다.
         *
         * 이전에는 180px 배경 위 영역이 그저 Sky.png와 우연히 같은 카메라 클리어 색이었다.
         * 하늘이 단색으로 남아 있는 동안만 통하는 방식이라, 거기에 뭔가를 그리는 순간
         * 깨진다. 실제 하늘 아트를 전체 화면에 타일링하면 그 의존이 사라지고, 원본이
         * 균일한 톤이라 반복 이음매도 보이지 않는다.
         */
        private void UpdateSkyFill(float cameraWorldWidth, float cameraWorldHeight)
        {
            if (skyFill == null) return;

            if (skyFill.drawMode != SpriteDrawMode.Tiled)
            {
                skyFill.drawMode = SpriteDrawMode.Tiled;
                skyFill.tileMode = SpriteTileMode.Continuous;
            }

            var size = new Vector2(cameraWorldWidth + skyOverscan * 2f, cameraWorldHeight + skyOverscan * 2f);
            if ((skyFill.size - size).sqrMagnitude > 0.0001f) skyFill.size = size;

            var cameraPosition = CameraBasePosition;
            var desired = new Vector3(cameraPosition.x, cameraPosition.y, skyFill.transform.position.z);
            if ((skyFill.transform.position - desired).sqrMagnitude > 0.0001f)
                skyFill.transform.position = desired;
        }

        /**
         * @brief 밴드를 UI rect에서 직접 읽는다.
         *
         * 나중에 Safe Area 처리가 밴드를 움직이기 시작해도 그대로 맞는다.
         *
         * 카메라의 월드 높이는 Camera.orthographicSize를 읽지 않고 화면 크기에서 유도한다.
         * Pixel Perfect Camera는 실제로 렌더링할 때만 그 값을 갱신하므로, 첫 프레임과
         * 에디터 모드에서는 낡은 값을 준다. BattleLayout.PixelRatio는 대상 해상도 전부에서
         * Unity의 pixelRatio와 일치함을 확인했다.
         */
        private BattleLayout.Band MeasureBand(int screenHeight, float cameraWorldHeight)
        {
            var corners = new Vector3[4];
            battleArea.GetWorldCorners(corners);

            var canvas = battleArea.GetComponentInParent<Canvas>();
            Camera uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera
                : null;

            float bottomScreenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]).y;
            float topScreenY = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[1]).y;
            float cameraCenterY = CameraBasePosition.y;

            return BattleLayout.ComputeBand(bottomScreenY, topScreenY, screenHeight, cameraWorldHeight, cameraCenterY);
        }

        private static void SetY(Transform target, float y)
        {
            var position = target.position;
            if (Mathf.Approximately(position.y, y)) return;
            position.y = y;
            target.position = position;
        }

        /** 캐릭터의 피벗(발)이 지면선에 놓이도록 배치한다 */
        public void PlaceOnGround(Transform character, float worldX)
        {
            if (character == null) return;
            character.position = new Vector3(worldX, GroundY, character.position.z);
        }
    }
}
