using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief [일괄 수령] (#5). 열려 있는 퀘스트 보상을 한 번에 전부 받는다.
     *
     * ## 왜 이 버튼이 생겼는가
     *
     * 반복 퀘스트의 티어는 카운터에서 유도되므로 오래 안 열어보면 그만큼
     * 쌓인다. 백 개가 쌓인 화면에서 한 번에 하나씩 받는 규칙은 보람이
     * 아니라 노동이 된다 - 백 번의 탭 동안 화면에 뜨는 것은 매번 같은
     * 숫자다.
     *
     * ## 보상은 그대로다
     *
     * 지급은 QuestSystem.ClaimAll이 하고, 그것은 TryClaim을 도는 것뿐이다.
     * 한 번에 받아서 더 주거나 덜 주는 일이 없고, 업적 보상이 받는 순간의
     * 스테이지로 환산되는 규칙도 그대로다. 배수 구매(#9)와 같은 판단이다.
     *
     * ## 몇 개를 받았는지는 말해준다
     *
     * 누른 뒤 잠깐 "24개 수령"으로 바뀐다. 백 개를 한 번에 받으면 화면에서
     * 일어나는 일이 재화 숫자가 커지는 것뿐이라, 무엇이 일어났는지 알려주는
     * 것이 하나도 없기 때문이다 - 한 번에 하나씩 받던 시절에는 그 피드백이
     * 버튼이 사라지는 것으로 나왔다.
     */
    public sealed class QuestClaimAllButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Tooltip("평소 문구. 수령 직후에는 잠깐 결과가 뜬다")]
        [SerializeField] private string idleLabel = "일괄 수령";

        [Tooltip("결과 문구를 붙들고 있는 시간(초). 실시간이다")]
        [SerializeField] private float resultSeconds = 1.6f;

        [Header("색")]
        [SerializeField] private Color readyText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color idleText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        private QuestSystem quests;

        /** 결과 문구가 사라지는 시각. 0이면 평소 상태 */
        private float resultUntil;

        private void Start()
        {
            quests = QuestSystem.Instance;
            if (quests != null) quests.Changed += Refresh;
            if (button != null) button.onClick.AddListener(OnClick);
            Refresh();
        }

        private void OnEnable()
        {
            if (quests == null) quests = QuestSystem.Instance;
            // 열 때마다 평소 문구로 돌아간다. 지난번 결과가 남아 있으면
            // 방금 받은 것으로 읽힌다
            resultUntil = 0f;
            Refresh();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.Changed -= Refresh;
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void Update()
        {
            if (resultUntil <= 0f) return;

            // unscaled다. 히트스톱이 timeScale을 0으로 붙드는 게임이라
            // 스케일 시간으로 재면 결과 문구가 전투 타이밍에 따라 늘어난다
            if (Time.unscaledTime < resultUntil) return;

            resultUntil = 0f;
            Refresh();
        }

        private void OnClick()
        {
            if (quests == null) return;

            int claimed = quests.ClaimAll();
            if (claimed <= 0) return;

            if (label != null) label.text = claimed + "개 수령";
            resultUntil = Time.unscaledTime + resultSeconds;

            // 여기서 Refresh를 부르지 않는다. ClaimAll이 이미 Changed를
            // 발생시켰고 그것이 Refresh를 돌렸다 - 그 시점에는 resultUntil이
            // 아직 0이라 평소 문구로 덮인다. 그래서 문구를 나중에 적는다
            if (button != null) button.interactable = false;
        }

        private void Refresh()
        {
            bool any = quests != null && quests.AnyClaimable;

            // 결과 문구가 떠 있는 동안에는 글자를 건드리지 않는다
            if (resultUntil <= 0f && label != null)
            {
                label.text = idleLabel;
                label.color = any ? readyText : idleText;
            }

            if (button != null) button.interactable = any && resultUntil <= 0f;
        }
    }
}
