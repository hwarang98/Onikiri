using Onikiri.Battle;
using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;

namespace Onikiri.UI
{
    /**
     * @brief 스테이지를 넘은 직후 뜨는 배너.
     *
     * 17단계까지 보스를 잡으면 곧바로 잡몹 파밍으로 돌아갔다. 방금 무엇을
     * 해냈는지가 화면에서 한 프레임 만에 지워졌고, 소감의 "나아가는 느낌이
     * 없다"에는 그것도 섞여 있다 - 진행했는데 진행한 표시가 없다.
     *
     * 배너는 두 가지를 말한다: **어디를 넘었는가**와 **무엇을 받았는가.**
     * 지역 피날레는 문구와 시간이 다르다 - 스테이지를 넘는 것과 지역을 넘는
     * 것은 다른 사건이다.
     */
    public sealed class ClearBanner : MonoBehaviour
    {
        [SerializeField] private BossFight fight;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text detailLabel;

        [SerializeField] private Color stageColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        [Tooltip("지역 클리어의 제목 색. 금색으로 사건의 크기를 가른다")]
        [SerializeField] private Color regionColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        private void Start()
        {
            if (fight != null) fight.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (fight != null) fight.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (fight == null || root == null) return;

            bool show = fight.Current == BossFight.Phase.Cleared;
            if (root.activeSelf != show) root.SetActive(show);
            if (!show) return;

            int stage = fight.ClearedStage;

            if (titleLabel != null)
            {
                // 지역을 넘었으면 그 사실이 먼저다. 스테이지 번호는 그 안에서만
                // 뜻이 있고, 지역이 바뀌는 순간에는 지역이 더 큰 소식이다
                titleLabel.text = fight.ClearedRegion ? "지역 클리어" : "클리어";
                titleLabel.color = fight.ClearedRegion ? regionColor : stageColor;
            }

            if (detailLabel != null)
            {
                string place = "지역 " + BossCurve.RegionOf(stage)
                             + " · " + BossCurve.StageInRegion(stage) + "/" + BossCurve.RegionLength;

                // 보너스가 0이면 숫자를 안 적는다. "+0"은 보상이 아니라 버그로 읽힌다
                detailLabel.text = fight.ClearBonus > BigDouble.Zero
                    ? place + "   +" + NumberFormatter.Format(fight.ClearBonus)
                    : place;
            }
        }
    }
}
