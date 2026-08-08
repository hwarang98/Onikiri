using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 상단의 레벨 표시, 경험치 바, 레벨업 버튼.
     *
     * 레벨업 버튼은 **올릴 수 있을 때만 보인다.** 항상 띄워두고 비활성으로 두는
     * 방법도 있지만, 방치형에서 화면에 상시 존재하는 비활성 버튼은 곧 배경이 된다.
     * 없다가 나타나는 것이 눈에 걸리는 유일한 방법이다.
     *
     * 여러 레벨분이 쌓이면 버튼에 개수를 함께 적는다. 한 번에 하나씩 오르므로
     * (CharacterLevel.TryLevelUp) 몇 번 더 누를 수 있는지가 곧 남은 보상의 크기다.
     */
    public sealed class LevelHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel;

        [Tooltip("Image.type = Filled 여야 한다. fillAmount만 건드리므로 메시 재생성이 없다")]
        [SerializeField] private Image expFill;
        [SerializeField] private TMP_Text expLabel;

        [SerializeField] private GameObject levelUpRoot;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpLabel;

        [Tooltip("남은 스탯 포인트. 상단이 아니라 증폭 행 근처에 두는 라벨이지만, " +
                 "값의 출처가 CharacterLevel 하나뿐이라 여기서 함께 갱신한다")]
        [SerializeField] private TMP_Text pointsLabel;

        [SerializeField] private string levelPrefix = "레벨 ";

        private CharacterLevel character;

        private void Start()
        {
            character = CharacterLevel.Instance;
            if (character == null)
            {
                Debug.LogWarning("[Onikiri] LevelHud found no CharacterLevel.");
                return;
            }

            character.Changed += Refresh;
            if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);

            Refresh();
        }

        private void OnDestroy()
        {
            if (character != null) character.Changed -= Refresh;
            if (levelUpButton != null) levelUpButton.onClick.RemoveListener(OnLevelUpClicked);
        }

        private void OnLevelUpClicked()
        {
            if (character != null) character.TryLevelUp();
        }

        private void Refresh()
        {
            if (character == null) return;

            if (levelLabel != null) levelLabel.text = levelPrefix + character.Level;
            if (expFill != null) expFill.fillAmount = character.ExpFraction;

            if (expLabel != null)
                // 슬래시 양옆 공백을 빼고, **소수 첫째 자리도 뗀다.**
                //
                // 55pt는 아틀라스를 구운 크기라 글자를 줄일 수 없으므로(14단계)
                // 줄일 수 있는 것은 글자 수뿐이다. 소수 한 자리는 두 숫자에 걸쳐
                // 네 글자(약 108px)를 먹는데, 이 행에서 가장 값싼 픽셀이 그것이다 -
                // 미세한 진행은 채움 막대가 이미 연속으로 보여주고 있고, 숫자가
                // 맡은 몫은 자릿수, 즉 크기다. 1000 미만에서는 어차피 정수라
                // 초반 표시("0/48")는 그대로다.
                expLabel.text = NumberFormatter.Format(character.Exp, 0)
                                + "/" + NumberFormatter.Format(character.ExpRequired, 0);

            if (pointsLabel != null)
                pointsLabel.text = "남은 포인트 " + character.UnspentPoints;

            int pending = character.PendingLevelUps;
            if (levelUpRoot != null && levelUpRoot.activeSelf != pending > 0)
                levelUpRoot.SetActive(pending > 0);

            if (levelUpLabel != null && pending > 0)
                levelUpLabel.text = pending > 1 ? "레벨업 " + pending : "레벨업";
        }
    }
}
