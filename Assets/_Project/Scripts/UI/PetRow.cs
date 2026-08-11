using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 펫 화면의 카드 한 장. 펫 하나의 상태·비용·버튼을 굴린다.
     *
     * EquipmentRow와 같은 계약이다 - 빌더가 만든 라벨·버튼을 참조로 받아
     * 시스템 상태에 맞게 다시 그린다. 문구를 빌더가 아니라 여기서 정하는
     * 이유도 같다: 상태(잠김/보유/출전/상한)에 따라 내용이 바뀌기 때문이다.
     *
     * ## 재화 분리가 버튼 색으로 보인다
     *
     * 버튼은 상태에 따라 해금(보석·청) 또는 레벨업(골드·자주)이다. 한 버튼이
     * 두 재화를 동시에 요구하는 일은 없다 - "해금 = 보석 / 레벨 = 골드"라는
     * 재화 계약이 화면 문법으로 그대로 선다.
     *
     * ## 출전 버튼이 없다
     *
     * 보유 동료 전원이 출전한다 - 선택할 것이 없으므로 버튼도 없다. 해금된
     * 카드는 이름 옆 "출전 중"과 금색 판으로 그 사실만 말한다.
     */
    public sealed class PetRow : MonoBehaviour
    {
        [SerializeField] private PetSystem system;
        [SerializeField] private int petIndex;

        [SerializeField] private Image icon;
        [SerializeField] private Image rowBackground;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private TMP_Text bonusLabel;
        [SerializeField] private TMP_Text levelLabel;

        [SerializeField] private Button actionButton;
        [SerializeField] private Image actionBackground;
        [SerializeField] private TMP_Text actionTitle;
        [SerializeField] private TMP_Text actionCost;

        [SerializeField] private Color affordableColor = Color.white;
        [SerializeField] private Color unaffordableColor = Color.gray;
        [SerializeField] private Color normalRowTint = Color.white;
        [SerializeField] private Color activeRowTint = Color.white;
        [SerializeField] private Color goldButtonTint = Color.white;
        [SerializeField] private Color gemButtonTint = Color.white;
        [SerializeField] private Color lockedIconTint = Color.black;

        private PlayerWallet wallet;
        private GemWallet gems;
        private StageProgress stage;

        private void Start()
        {
            if (system == null) system = PetSystem.Instance;
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;

            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            // 스테이지도 듣는다(41단계). 그전에는 st31 전에 이 화면에 들어올
            // 수 없어서 몰랐는데, 미리보기가 생기면서 열어둔 채 최전선이
            // st31에 닿는 경로가 생겼다 - 골드 이벤트를 기다리지 않고 그
            // 자리에서 버튼이 살아나야 한다. EquipmentRow가 먼저 하던 처리다
            stage = Object.FindFirstObjectByType<StageProgress>();
            if (stage != null) stage.Changed += Refresh;

            if (actionButton != null) actionButton.onClick.AddListener(OnAction);

            Refresh();
        }

        private void OnEnable()
        {
            if (system == null) system = PetSystem.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (stage != null) stage.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        private void OnAction()
        {
            if (system == null) return;

            var pet = system.GetPet(petIndex);
            if (pet == null) return;

            if (!pet.unlocked) system.TryUnlock(petIndex);
            else system.TryLevelUp(petIndex);
        }

        private void Refresh()
        {
            if (system == null) return;

            var pet = system.GetPet(petIndex);
            if (pet == null) return;

            // 화면(패널) 자체가 잠긴 미리보기인가 (41b). 세이브의 pet.unlocked와
            // 별개다 - 미리보기에서 "출전 중"이 뜨면 아직 오지 않은 동료가
            // 이미 싸우고 있다는 거짓말이 된다
            bool panelLocked = !system.IsUnlocked;
            bool fielded = pet.unlocked && !panelLocked;

            if (nameLabel != null)
                nameLabel.text = fielded ? pet.petName + " · 출전 중" : pet.petName;

            if (roleLabel != null)
            {
                roleLabel.text = pet.role;
                // 미리보기의 주인공은 얻는 것(역할·DPS)이다. 평소에는 위계상
                // 흐리지만, 잠긴 화면에서는 카드가 통째로 어두워지므로 얻는
                // 것만 밝게 남긴다
                roleLabel.color = panelLocked ? affordableColor : unaffordableColor;
            }

            // 잠긴 펫은 실루엣으로 보여준다. 값을 보여주면 이미 가진 것으로
            // 읽힌다는 규칙(EquipmentRow.DrawLocked)의 절충이다 - 모습은 예고,
            // 숫자는 잠금
            if (icon != null) icon.color = pet.unlocked ? Color.white : lockedIconTint;

            // 잠긴 화면은 카드 판부터 어둡다 - "아직 못 쓴다"가 버튼 하나가
            // 아니라 카드 전체에서 읽혀야 한다. 출전 금테는 실제로 출전 중일
            // 때만이다
            if (rowBackground != null)
                rowBackground.color = panelLocked ? Dimmed(normalRowTint)
                    : (fielded ? activeRowTint : normalRowTint);

            if (bonusLabel != null)
            {
                // 전후값은 버튼이 아니라 이 줄이 말한다(강화 행과 같은 문법).
                // P1이다 - 명궁·묵웅의 한 칸(+0.3%p 안팎)이 P0으로는 +0%로
                // 반올림돼 "눌러도 안 오르는 버튼"처럼 읽힌다
                if (!pet.unlocked) bonusLabel.text = "DPS +?";
                else if (fielded && !system.IsMaxed(petIndex))
                    bonusLabel.text = string.Format("DPS +{0:P1} → +{1:P1}",
                        system.BonusOf(petIndex), system.NextBonusOf(petIndex));
                else
                    bonusLabel.text = string.Format("DPS +{0:P1}", system.BonusOf(petIndex));

                bonusLabel.color = pet.unlocked ? affordableColor : unaffordableColor;
            }

            if (levelLabel != null)
                levelLabel.text = !pet.unlocked
                    ? "잠김"
                    : panelLocked
                        ? "해금 시 출전"
                        : system.IsMaxed(petIndex)
                            ? "Lv." + pet.level + " MASTER"
                            : "Lv." + pet.level + " / " + PetCurve.MaxLevel;

            RefreshActionButton(pet, panelLocked);
        }

        private void RefreshActionButton(PetSystem.Slot pet, bool panelLocked)
        {
            if (actionButton == null) return;

            if (!pet.unlocked)
            {
                int cost = system.UnlockGemCostOf(petIndex);
                bool canPay = gems != null && gems.CanAfford(cost) && system.IsUnlocked;

                SetActionVisual(gemButtonTint, panelLocked, canPay,
                                "해금", "보석 " + cost);
                actionButton.interactable = system.CanUnlock(petIndex);
                return;
            }

            if (system.IsMaxed(petIndex))
            {
                SetActionVisual(goldButtonTint, panelLocked, false, "MASTER", "");
                actionButton.interactable = false;
                return;
            }

            var levelCost = system.LevelCostOf(petIndex);
            bool canBuy = wallet != null && wallet.CanAfford(levelCost) && !panelLocked;

            SetActionVisual(goldButtonTint, panelLocked, canBuy,
                            "레벨업", "골드 " + NumberFormatter.Format(levelCost));
            actionButton.interactable = system.CanLevelUp(petIndex);
        }

        /**
         * @brief 버튼의 판과 글자를 한 번에 칠한다.
         *
         * interactable=false만으로는 안 죽는다 - SpriteSwap 버튼은 비활성
         * 스프라이트를 안 물리므로(UiSkin.ApplyButton 주석) 판이 평소처럼
         * 밝게 남고, 그것이 "잠김인데 활성처럼 보인다"의 정체였다. 잠긴
         * 화면에서는 판 자체를 눌러 죽인다.
         */
        private void SetActionVisual(Color tint, bool panelLocked, bool affordable,
                                     string title, string cost)
        {
            if (actionBackground != null)
                actionBackground.color = panelLocked ? Dimmed(tint) : tint;

            if (actionTitle != null)
            {
                actionTitle.text = title;
                actionTitle.color = affordable ? affordableColor : unaffordableColor;
            }
            if (actionCost != null)
            {
                actionCost.text = cost;
                actionCost.color = affordable ? affordableColor : unaffordableColor;
            }
        }

        /** 판 죽이기. 알파가 아니라 RGB를 누른다 - 알파는 뒤가 비친다(41의 배너 함정) */
        private static Color Dimmed(Color c)
        {
            return new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
        }
    }
}
