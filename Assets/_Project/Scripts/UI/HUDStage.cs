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

        private void Refresh()
        {
            if (label == null || progress == null) return;

            // 할당량을 채우면 "10/10" 대신 보스를 알린다. 멈춘 숫자는 진행이 끝났다는
            // 것만 말하고 다음에 무엇을 해야 하는지는 말하지 않는데, 이 게임에서
            // 그 다음은 화면 어딘가의 버튼을 누르는 것이라 안내가 필요하다
            label.text = progress.IsBossReady
                ? prefix + progress.Stage + "  보스"
                : prefix + progress.Stage + "  " + progress.KillsThisStage + "/" + progress.KillsRequired;
        }
    }
}
