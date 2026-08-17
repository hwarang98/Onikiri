using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 퀘스트 목록의 한 줄. 목표 · 진행바 · 보상 · 받기 버튼 · 완료 배지.
     *
     * ## 진행바를 두는 이유
     *
     * 숫자만 적어도 진행은 읽힌다("42 / 60"). 그런데 목록을 훑을 때 필요한 것은
     * 정확한 값이 아니라 **어느 것이 곧 끝나는가**이고, 그것은 다섯 줄의 숫자를
     * 비교해야 나온다. 막대는 훑는 순간에 답한다.
     *
     * 숫자도 함께 적는다. "골드 2,000 획득"처럼 목표가 큰 것은 막대만으로
     * 남은 양을 가늠할 수 없다.
     *
     * ## 받기 버튼은 조건을 만족할 때만 눌린다
     *
     * 회색으로 두고 눌리지 않게 한다. 감추지 않는 이유는 LockedTab과 같다 -
     * **자리는 있고 아직 못 누른다**가 사실이고, 감추면 그 줄에서 무엇을 할 수
     * 있는지가 사라진다.
     */
    public sealed class QuestRow : MonoBehaviour
    {
        [SerializeField] private QuestKind kind;
        [SerializeField] private int index;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text rewardLabel;

        [Tooltip("진행바의 채워지는 부분. 가로 앵커를 진행률로 민다")]
        [SerializeField] private RectTransform progressFill;

        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimLabel;

        [Tooltip("이미 받은 줄에 뜨는 완료 배지")]
        [SerializeField] private GameObject doneBadge;

        [Header("색")]
        [SerializeField] private Color readyText = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimText = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color readyFill = new Color32(0x8B, 0xD4, 0x50, 0xFF);
        [SerializeField] private Color busyFill = new Color32(0x62, 0x6A, 0xC8, 0xFF);

        private QuestSystem quests;

        private void Start()
        {
            quests = QuestSystem.Instance;
            if (quests != null) quests.Changed += Refresh;
            if (claimButton != null) claimButton.onClick.AddListener(Claim);

            Refresh();
        }

        /**
         * @brief 켜질 때마다 다시 그린다.
         *
         * 이 줄은 퀘스트 판이 꺼진 채로 씬에 저장되므로 Start가 **처음 열릴 때**
         * 돈다. 그 사이 진행이 바뀌어 있으면 빌더가 적어둔 초기 문구가 한 프레임
         * 보인다 - LockedTab·StatPointButton과 같은 처리다.
         */
        private void OnEnable()
        {
            if (quests == null) quests = QuestSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (quests != null) quests.Changed -= Refresh;
            if (claimButton != null) claimButton.onClick.RemoveListener(Claim);
        }

        private void Claim()
        {
            if (quests == null) return;
            quests.TryClaim(kind, index);
        }

        private void Refresh()
        {
            var specs = QuestCatalog.Of(kind);
            if (index < 0 || index >= specs.Length) return;
            var spec = specs[index];

            if (quests == null)
            {
                if (title != null) title.text = spec.Title;
                return;
            }

            if (DrawIfLocked(spec)) return;

            double progress = quests.ProgressOf(kind, index);
            int claimable = quests.ClaimableCount(kind, index);
            bool claimed = quests.IsClaimed(kind, index);

            if (title != null)
            {
                // 반복은 지금 몇 단계째인지 함께 적는다. 같은 문구가 영원히
                // 반복되면 "받았는데 그대로다"로 읽힌다
                title.text = kind == QuestKind.Repeat
                    ? spec.Title + "  <size=80%>(" + (quests.RepeatTier(index) + 1) + "단계)</size>"
                    : spec.Title;

                title.color = claimed ? dimText : readyText;
            }

            if (progressLabel != null)
            {
                progressLabel.text = FormatCount(progress) + " / " + FormatCount(spec.Target);
                progressLabel.color = claimable > 0 ? readyText : dimText;
            }

            if (progressFill != null)
            {
                float fraction = spec.Target > 0d
                    ? Mathf.Clamp01((float)(progress / spec.Target))
                    : 0f;

                // 앵커를 밀어 채운다. sizeDelta를 쓰면 행 폭이 바뀔 때 어긋난다
                progressFill.anchorMin = new Vector2(0f, 0f);
                progressFill.anchorMax = new Vector2(fraction, 1f);
                progressFill.offsetMin = Vector2.zero;
                progressFill.offsetMax = Vector2.zero;

                var image = progressFill.GetComponent<Image>();
                if (image != null) image.color = claimable > 0 ? readyFill : busyFill;
            }

            if (rewardLabel != null) rewardLabel.text = RewardText(spec);

            if (claimButton != null)
            {
                claimButton.interactable = claimable > 0;
                claimButton.gameObject.SetActive(!claimed);
            }

            if (claimLabel != null)
            {
                // 여러 티어가 쌓였으면 몇 개인지 적는다. 반복만 2 이상이 된다.
                // 배수는 작게 - "받기"가 동사이고 배수는 곁가지인데, 같은 크기로
                // 두면 "x20"이 버튼을 밀어내 진행 숫자와 겹친다
                claimLabel.text = claimable > 1
                    ? "받기 <size=70%>x" + claimable + "</size>"
                    : "받기";
                claimLabel.color = claimable > 0 ? readyText : dimText;
            }

            if (doneBadge != null && doneBadge.activeSelf != claimed)
                doneBadge.SetActive(claimed);
        }

        /**
         * @brief 아직 기능이 없는 지표의 일일 칸을 잠금 표시로 대체한다 (39단계).
         *
         * "오의 20회 시전"은 오의가 열리는 Lv.10 전에는 **구조적으로 못 깨는
         * 칸**이다. 신규 플레이어의 일일 목록에 영원히 0/20인 줄이 서 있으면
         * "일일은 다 못 채우는 것"으로 학습된다 - LockedTab이 잠긴 기능을 밝은
         * 빈 화면으로 두지 않는 것과 같은 이유로, 여기도 조건을 정직하게 적는다.
         *
         * 행을 통째로 숨기지 않는 이유도 LockedTab과 같다 - "앞으로 무엇이
         * 열리는가"가 계속할 이유의 절반이고, 자리가 사라지면 오의를 열었을 때
         * 일일 보상이 **늘어난 것**을 알 방법이 없다.
         *
         * 문구는 대장간의 "미개방"을 재사용한다 - 이미 아틀라스에 있는 글자다.
         */
        private bool DrawIfLocked(QuestSpec spec)
        {
            if (kind != QuestKind.Daily || spec.Metric != QuestMetric.SkillCasts) return false;

            var character = CharacterLevel.Instance;
            int needed = SkillCatalog.PanelUnlockLevel;
            if (character == null || character.Level >= needed) return false;

            if (title != null) { title.text = "오의 미개방"; title.color = dimText; }
            if (progressLabel != null) { progressLabel.text = "Lv." + needed; progressLabel.color = dimText; }
            if (rewardLabel != null) rewardLabel.text = RewardText(spec);

            if (progressFill != null)
            {
                progressFill.anchorMin = new Vector2(0f, 0f);
                progressFill.anchorMax = new Vector2(0f, 1f);
                progressFill.offsetMin = Vector2.zero;
                progressFill.offsetMax = Vector2.zero;
            }

            if (claimButton != null) claimButton.gameObject.SetActive(false);
            if (doneBadge != null) doneBadge.SetActive(false);
            return true;
        }

        /**
         * @brief 보상 **수량**. 재화 이름은 이제 아이콘이 말한다 (#11).
         *
         * 그전에는 "보석 20 <size=75%>·골드 ·EXP</size>"였다. 재화 이름을
         * 글자로 적으면 세 가지가 따라온다 - 매번 읽어야 하고, 곁가지를
         * 75%로 줄여 넣는 편법이 필요하고, 그렇게 해도 380px을 먹어 진행바를
         * 밀어낸다.
         *
         * 지금 이 함수가 돌려주는 것은 숫자 하나다. 아이콘은 빌드 시점에
         * 행에 박힌다(QuestPanelBuilder.BuildRewardGroup) - 어느 재화를
         * 주는가는 퀘스트마다 고정이라 런타임에 바뀔 일이 없기 때문이다.
         */
        private static string RewardText(QuestSpec spec)
        {
            return spec.Gems.ToString();
        }

        /**
         * @brief 진행 숫자.
         *
         * 골드는 자릿수가 커서 축약하고(NumberFormatter), 처치 수처럼 작은 값은
         * 그대로 적는다. 한 가지로 통일하지 않는 이유는 "60마리"가 "60"으로
         * 보여야 하고 "2000골드"는 "2.0K"로 보여야 읽히기 때문이다.
         */
        private static string FormatCount(double value)
        {
            if (value >= 10000d) return NumberFormatter.Format(BigDouble.FromDouble(value));
            return Mathf.FloorToInt((float)value).ToString();
        }
    }
}
