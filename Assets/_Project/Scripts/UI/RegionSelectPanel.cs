using Onikiri.Battle;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 스테이지 재선택 화면 (37단계). 지역 목록 + 최전선 복귀.
     *
     * 규칙은 전부 StageProgress에 있다(SelectStage - 최전선 상한 클램프,
     * st11 게이트). 이 화면은 그 규칙을 보여주고 부를 뿐이다.
     *
     * ## 스테이지 단위가 아니라 지역 단위다
     *
     * 골드/초는 최전선이 항상 최적이라(StageReselectTests) 세부 스테이지
     * 선택에 실익이 없다 - 되돌아가는 이유는 수치가 아니라 "그 지역"이다
     * (배경, 그 지역의 몹). 목록이 4줄이면 스크롤 없이 밴드에 들어간다.
     *
     * ## 이동은 파밍 중에만
     *
     * 보스전 도중 스테이지가 바뀌면 BossFight의 배수·배치가 그 자리에서
     * 갈린다. 테스트 패널의 지역 점프와 같은 가드다.
     */
    public sealed class RegionSelectPanel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Row
        {
            public GameObject root;
            public TMP_Text nameLabel;
            public TMP_Text stateLabel;
            public Button button;
            public Image background;

            [Tooltip("이 지역의 첫 스테이지 (전역 번호)")]
            public int firstStage;

            [Tooltip("이 지역의 마지막 스테이지 (전역 번호)")]
            public int lastStage;
        }

        [SerializeField] private StageProgress progress;
        [SerializeField] private BossFight fight;
        [SerializeField] private EnemySpawner spawner;

        [SerializeField] private TMP_Text currentLabel;
        [SerializeField] private Button frontierButton;
        [SerializeField] private TMP_Text frontierLabel;

        [SerializeField] private Row[] rows;

        [Header("색")]
        [SerializeField] private Color normalTint = new Color(0.46f, 0.50f, 0.94f, 1f);
        [SerializeField] private Color currentTint = new Color(0.52f, 0.47f, 0.34f, 1f);
        [SerializeField] private Color textColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        private void Start()
        {
            if (progress == null) progress = StageProgress.Instance;
            if (progress != null) progress.Changed += Refresh;

            if (frontierButton != null) frontierButton.onClick.AddListener(OnFrontier);

            if (rows != null)
                foreach (var row in rows)
                {
                    if (row == null || row.button == null) continue;
                    var captured = row;
                    row.button.onClick.AddListener(() => OnRow(captured));
                }
        }

        private void OnDestroy()
        {
            if (progress != null) progress.Changed -= Refresh;
        }

        private void OnEnable()
        {
            // 꺼진 채 저장되는 판이라 Start보다 먼저 열릴 수 있다.
            // 열리는 순간의 화면이 옛 상태면 안 된다
            if (progress == null) progress = StageProgress.Instance;
            Refresh();
        }

        /** 파밍 중에만 이동한다. ClearField는 보상 없는 정리라 골드가 새지 않는다 */
        private void MoveTo(int stage)
        {
            if (progress == null) return;
            if (fight != null && fight.Current != BossFight.Phase.Farming) return;

            int before = progress.Stage;
            progress.SelectStage(stage);
            if (progress.Stage != before && spawner != null) spawner.ClearField();
        }

        private void OnFrontier()
        {
            if (progress != null) MoveTo(progress.MaxStageReached);
        }

        private void OnRow(Row row)
        {
            MoveTo(row.firstStage);
        }

        private void Refresh()
        {
            if (progress == null) return;

            int stage = progress.Stage;
            int frontier = progress.MaxStageReached;

            if (currentLabel != null)
                currentLabel.text = "현재 " + stage + " 스테이지"
                                    + (progress.IsAtFrontier ? "  (최전선)" : "");

            if (frontierLabel != null)
                frontierLabel.text = "최전선으로  (" + frontier + ")";
            if (frontierButton != null)
                frontierButton.interactable = !progress.IsAtFrontier;

            if (rows == null) return;

            foreach (var row in rows)
            {
                if (row == null) continue;

                bool cleared = frontier > row.lastStage;
                bool inside = frontier >= row.firstStage && frontier <= row.lastStage;
                bool here = stage >= row.firstStage && stage <= row.lastStage;

                if (row.stateLabel != null)
                    row.stateLabel.text = here
                        ? "현재 위치"
                        : cleared ? "클리어" : inside ? "진행 중" : "잠김";

                // 잠긴 지역은 눌리지 않는다. 진행 중 지역은 그 지역의 첫
                // 스테이지가 이미 클리어 구간이므로 눌린다(첫 스테이지 = 최전선인
                // 경우도 SelectStage가 제자리 이동으로 무시한다)
                if (row.button != null)
                    row.button.interactable = !here && row.firstStage <= frontier;

                if (row.background != null)
                    row.background.color = here ? currentTint : normalTint;

                if (row.nameLabel != null)
                    row.nameLabel.color = row.firstStage <= frontier ? textColor : dimColor;
            }
        }
    }
}
