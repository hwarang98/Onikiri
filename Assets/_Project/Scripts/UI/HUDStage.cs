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

            label.text = prefix + progress.Stage +
                         "  " + progress.KillsThisStage + "/" + progress.KillsRequired;
        }
    }
}
