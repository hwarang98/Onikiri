using Onikiri.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 캐릭터 탭의 심볼을 현재 경지의 초상으로 바꾼다 (39단계).
     *
     * 38단계의 인물 글리프는 "여기가 내 캐릭터"를 말했지만 누구나 같은 인물이었다.
     * 경지가 오르면 전투 화면의 사무라이가 바뀌는데 탭은 그대로라, "나"가 화면
     * 둘로 갈라져 있었다. 탭이 초상을 쓰면 전직의 보상(새 모습)이 항상 켜져 있는
     * 하단 바에도 남는다 - 글리프 교체 1순위로 적어둔 자리다(UiGlyphBuilder).
     *
     * ## 배치는 EvolutionPanel.ApplyPortrait의 축소판이다
     *
     * 고정 픽셀 배율 + 실측 잉크 기준선. 다른 점은 **정렬 기준이 발이 아니라
     * 머리**라는 것이다 - 64px 칸에 전신(원본 34~52px의 2배)이 다 들어가지
     * 않으므로 다리를 자르고 상반신을 남긴다. 초상(portrait)이라는 말 그대로다.
     * 넘치는 부분은 빌더가 씌운 RectMask2D가 자른다.
     *
     * ## LockedTab과 싸우지 않는다
     *
     * 같은 탭의 LockedTab은 자기 icon(글리프)을 이벤트마다 다시 그린다. 그
     * 이미지를 뺏으면 두 컴포넌트가 한 이미지를 서로 덮어쓴다 - 그래서 빌더가
     * 캐릭터 탭의 글리프를 비우고(normalIcon = null), 이 컴포넌트는 **자기
     * 전용 이미지**를 따로 받는다. 경지 이벤트에만 반응하면 충돌 지점이 없다.
     */
    public sealed class CharacterTabPortrait : MonoBehaviour
    {
        [Tooltip("초상을 그릴 전용 이미지. 마스크 안에 있다")]
        [SerializeField] private Image portrait;

        [Tooltip("티어별 초상. 빌더가 EvolutionAppearance의 idle 첫 프레임을 적는다")]
        [SerializeField] private Sprite[] tierPortraits;

        /** 티어별 (발선, 가로 중심, 머리선). 빌더가 그려진 픽셀에서 실측한다 */
        [SerializeField] private float[] tierInkTop;
        [SerializeField] private float[] tierInkCenter;

        [Tooltip("원본 아트 1픽셀 = 캔버스 몇 픽셀인가. 정수여야 픽셀이 안 뭉갠다")]
        [SerializeField] private float pixelScale = 2f;

        [Tooltip("머리선을 칸 위에서 얼마나 내리는가")]
        [SerializeField] private float topInset = 4f;

        private EvolutionSystem evolution;
        private Vector2 basePosition;
        private bool baseCaptured;
        private int appliedTier = -1;

        private void Start()
        {
            evolution = EvolutionSystem.Instance;
            if (evolution != null) evolution.Changed += Refresh;
            Refresh();
        }

        private void OnEnable()
        {
            if (evolution == null) evolution = EvolutionSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (evolution != null) evolution.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (portrait == null || tierPortraits == null || tierPortraits.Length == 0) return;

            int tier = evolution != null ? evolution.Tier : 0;
            if (tier == appliedTier) return;

            int index = Mathf.Clamp(tier, 0, tierPortraits.Length - 1);
            var sprite = tierPortraits[index];
            if (sprite == null) return;

            appliedTier = tier;

            if (!baseCaptured)
            {
                basePosition = portrait.rectTransform.anchoredPosition;
                baseCaptured = true;
            }

            portrait.sprite = sprite;
            portrait.color = Color.white;   // 픽셀아트에 틴트를 곱하지 않는다

            var rect = portrait.rectTransform.rect;
            float cellW = sprite.rect.width;
            float cellH = sprite.rect.height;
            if (cellW <= 0f || cellH <= 0f) return;

            float topNorm = ValueAt(tierInkTop, index, 1f);
            float centerNorm = ValueAt(tierInkCenter, index, 0.5f);

            // preserveAspect가 셀을 칸에 맞춘 크기. EvolutionPanel과 같은 상쇄로
            // 원본 픽셀 배율을 고정한다
            float fit = Mathf.Min(rect.width / cellW, rect.height / cellH);
            float shownW = cellW * fit;
            float shownH = cellH * fit;
            float zoom = pixelScale / fit;

            portrait.rectTransform.localScale = Vector3.one * zoom;

            // 피벗 (0.5, 1) 기준. preserveAspect는 남는 세로를 피벗으로 나누므로
            // 셀은 칸 위 모서리에 붙는다(EvolutionPanel과 같은 함정 - 가운데가
            // 아니다). 칸 위에서 머리선까지의 거리를 재서 머리를 topInset에
            // 맞춘다 - 다리는 마스크 밖으로 잘린다
            float headFromTop = zoom * (1f - topNorm) * shownH;
            float dx = -(centerNorm - 0.5f) * shownW * zoom;

            portrait.rectTransform.anchoredPosition =
                basePosition + new Vector2(dx, headFromTop - topInset);
        }

        private static float ValueAt(float[] values, int index, float fallback)
        {
            if (values == null || index < 0 || index >= values.Length) return fallback;
            float value = values[index];
            return value < 0f ? fallback : value;
        }
    }
}
