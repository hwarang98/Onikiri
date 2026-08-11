using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 상단 바의 스테이지 표시.
     *
     * 스테이지와 그 안의 처치 진행도를 함께 보여준다. 처치 수를 빼면 스테이지가
     * 갑자기 오르는 것처럼 보이고, 방치 화면에서 "지금 뭔가 진행되고 있다"는 유일한
     * 신호가 사라진다.
     */
    public sealed class HUDStage : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        [Tooltip("칩의 심볼. 보스 스테이지에서 깃발이 해골로 바뀐다 (2b)")]
        [SerializeField] private Image icon;
        [SerializeField] private Sprite flagSprite;
        [SerializeField] private Sprite skullSprite;

        private StageProgress progress;

        private void Start()
        {
            progress = StageProgress.Instance;
            if (progress == null)
            {
                Debug.LogWarning("[Onikiri] HUDStage found no StageProgress.");
                return;
            }

            progress.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        /**
         * @brief "지역 1 · 7/10" + 보스면 해골.
         *
         * 17단계에서 지역을 드러냈다. 그전에는 "스테이지 37"처럼 연속 번호만
         * 보여줬는데, 그 숫자는 **지금 어디쯤인지도 얼마나 남았는지도** 말하지
         * 않는다. 37이 큰 값인지 작은 값인지 알 방법이 없다.
         *
         * 내부 진행은 여전히 연속(stage)이고 여기서만 환산한다. 세이브에 지역
         * 필드를 만들지 않은 이유는 BossCurve.RegionOf 주석에 적었다.
         *
         * "클리어"는 뺐다(2b 압축). 되돌아간 스테이지의 상태는 재선택 화면과
         * 도전 버튼 부재가 이미 말하고 있고, 좁은 칩에서 네 글자 값을 못 한다.
         */
        private void Refresh()
        {
            if (label == null || progress == null) return;

            int stage = progress.Stage;
            int region = BossCurve.RegionOf(stage);
            int local = BossCurve.StageInRegion(stage);

            // "스테이지"라는 단어는 뺀다. "지역 1 · 7/10"이면 그 7이 스테이지라는
            // 것이 자리로 읽히고, 그만한 폭 값을 하지 않는다
            label.text = "지역 " + region + " · " + local + "/" + BossCurve.RegionLength;

            // 보스 스테이지의 심볼은 해골이다. 최전선에서만 - 되돌아간 스테이지는
            // 보스가 잠겨 있으므로(재선택 규칙) 해골이 거짓말이 된다
            if (icon != null && flagSprite != null && skullSprite != null)
            {
                bool bossHere = BossCurve.IsChapterBoss(stage) && progress.IsAtFrontier;
                var wanted = bossHere ? skullSprite : flagSprite;
                if (icon.sprite != wanted) icon.sprite = wanted;
            }
        }
    }
}
