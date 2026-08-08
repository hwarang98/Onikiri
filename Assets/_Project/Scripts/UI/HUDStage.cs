using Onikiri.Progression;
using TMPro;
using UnityEngine;

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
        [SerializeField] private string prefix = "스테이지 ";

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
         * @brief "지역 1 · 스테이지 7/10" + 잡몹 진행.
         *
         * 17단계에서 지역을 드러냈다. 그전에는 "스테이지 37"처럼 연속 번호만
         * 보여줬는데, 그 숫자는 **지금 어디쯤인지도 얼마나 남았는지도** 말하지
         * 않는다. 37이 큰 값인지 작은 값인지 알 방법이 없다.
         *
         * 내부 진행은 여전히 연속(stage)이고 여기서만 환산한다. 세이브에 지역
         * 필드를 만들지 않은 이유는 BossCurve.RegionOf 주석에 적었다.
         */
        private void Refresh()
        {
            if (label == null || progress == null) return;

            int stage = progress.Stage;
            int region = BossCurve.RegionOf(stage);
            int local = BossCurve.StageInRegion(stage);

            // "스테이지"라는 단어는 뺀다. "지역 1 · 7/10"이면 그 7이 스테이지라는
            // 것이 자리로 읽히고, 55pt 글자에서 네 글자는 112px이라 상단 바에서
            // 그만한 값을 하지 않는다
            string place = "지역 " + region + " · " + local + "/" + BossCurve.RegionLength;

            // 할당량을 채우면 처치 수 대신 보스를 알린다. 멈춘 숫자는 진행이 끝났다는
            // 것만 말하고 다음에 무엇을 해야 하는지는 말하지 않는데, 이 게임에서
            // 그 다음은 화면 어딘가의 버튼을 누르는 것이라 안내가 필요하다
            label.text = progress.IsBossReady
                ? place + "  보스"
                : place + "  처치 " + progress.KillsThisStage + "/" + progress.KillsRequired;
        }
    }
}
