using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 강화 한 줄의 버튼.
     *
     * 이름/레벨, 현재 효과, 다음 비용을 보여주고 누르면 구매한다.
     *
     * 갱신은 이벤트로만 한다. 골드는 처치할 때마다 바뀌므로 매 프레임 폴링하면 값이
     * 그대로인 프레임에도 BigDouble 포맷과 TMP 메시 재생성을 계속 돌리게 된다.
     */
    public sealed class UpgradeButton : MonoBehaviour
    {
        [SerializeField] private UpgradeSystem system;
        [SerializeField] private int trackIndex;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("이번 구매로 스탯이 얼마에서 얼마가 되는지. 이것이 없으면 비용만 " +
                 "보이고 그 대가로 무엇을 얻는지는 보이지 않는다")]
        [SerializeField] private TMP_Text valueLabel;

        [SerializeField] private TMP_Text costLabel;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);

        [Tooltip("골드가 모자랄 때의 비용 글자색. 버튼을 숨기지 않고 색만 죽여서 " +
                 "다음 목표가 얼마인지 계속 보이게 한다")]
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        /**
         * @brief 상한에 닿은 축의 색.
         *
         * **회색이 아니라 금색이다.** 17단계에서 바꿨다.
         *
         * 공격속도는 아트가 정한 상한이라(7프레임/14fps = 3.88회/초) 앞으로도
         * 안 열린다. 그것을 죽은 회색으로 두면 화면에 "고장난 버튼"이 영구히
         * 남는다 - 플레이어는 자기가 뭘 잘못했는지 모른 채 그 줄을 계속 본다.
         *
         * 같은 사실을 **완성**으로 표현하면 읽는 방향이 뒤집힌다. 더 못 사는
         * 것이 아니라 다 산 것이고, 금색은 이 게임에서 이미 보상의 색이다.
         *
         * 캡을 올리려고 아트에 투자하지 않는다 - 프레임을 늘리면 곧 또 막힌다.
         */
        [Header("완성")]
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        [Tooltip("완성된 축의 행 배경 틴트. 금빛으로 살짝 물들여 목록에서 구분된다")]
        [SerializeField] private Color masteredRowTint = new Color(0.62f, 0.55f, 0.42f, 1f);

        [Tooltip("완성 표시 문구. 'MAX'는 막혔다는 뜻이고 이쪽은 다 채웠다는 뜻이다")]
        [SerializeField] private string masteredLabel = "MASTER";

        [Tooltip("행 배경. 완성되면 금빛으로 물든다")]
        [SerializeField] private UnityEngine.UI.Image rowBackground;

        /**
         * @brief 이 스테이지 전에는 잠긴다. 0이면 잠금 없음.
         *
         * 21단계에 골드 획득 축을 위해 생겼다. 그 축은 회수에 2분이 걸리는데
         * 온보딩(1~5)이 3분이라, 거기서는 지표가 사라고 말하지만 실제로는
         * 손해다 - **함정 버튼**이었다.
         *
         * 숨기지 않고 잠근다. 숨기면 목록의 길이가 어느 날 갑자기 늘어나 무엇이
         * 새로 생겼는지 알 수 없고, 잠긴 줄은 "앞으로 열릴 것"을 미리 보여준다 -
         * 방치형에서 그것이 계속할 이유의 절반이다(LockedTab과 같은 규칙).
         */
        [Header("해금")]
        [SerializeField] private int unlockStage;

        [Tooltip("잠겨 있을 때 비용 칸에 적을 문구. {0}이 해금 스테이지로 바뀐다")]
        [SerializeField] private string lockedLabel = "{0}스테이지";

        [SerializeField] private Color lockedColor = new Color32(0x5A, 0x51, 0x6B, 0xFF);

        [Tooltip("잠긴 행의 배경 틴트")]
        [SerializeField] private Color lockedRowTint = new Color(0.34f, 0.36f, 0.55f, 1f);

        /**
         * @brief 심화 게이트 (43단계). 치명타 확률이 MASTER(100%)여야 열린다.
         *
         * 스테이지 게이트와 별개의 문이다 - 조건이 여정의 위치가 아니라
         * **다른 축의 완성**이다. 잠긴 비용 칸에는 스테이지 대신 조건
         * ("치명타 100%")이 선다.
         */
        [SerializeField] private bool deepGate;

        [Tooltip("심화 게이트가 잠겼을 때 비용 칸에 적을 문구")]
        [SerializeField] private string deepLockedLabel = "치명타 Lv.1000";

        /** 지금 잠겨 있는가 */
        private bool IsLocked
        {
            get
            {
                if (deepGate && !DeepUnlocked) return true;

                if (unlockStage <= 0) return false;
                var progress = StageProgress.Instance;
                // 진행 정보가 없으면 잠그지 않는다. 전투 전용 테스트 씬은
                // StageProgress 없이 강화만 세우는데, 거기서 전부 잠기면
                // 21단계 이전에 쓰던 검사가 이유 없이 깨진다.
                // 현재 스테이지가 아니라 최전선이다(37단계 재선택) - 되돌아가도
                // 이미 열린 축이 다시 잠기지 않는다
                return progress != null && progress.MaxStageReached < unlockStage;
            }
        }

        /**
         * @brief 치명타 확률이 상한(100%)에 서 있는가.
         *
         * 레벨이 아니라 **값의 상한**으로 판정한다(IsMaxed || IsValueCapped) -
         * 상한이 움직인 업데이트에서 레벨만으로 판정하면 두 곳이 갈린다.
         * 트랙이 없는 씬(전투 전용 테스트)에서는 잠그지 않는다 - 스테이지
         * 게이트와 같은 규칙이다.
         */
        private bool DeepUnlocked
        {
            get
            {
                if (system == null) return true;
                var crit = system.GetTrack(UpgradeSystem.CritRateId);
                if (crit == null) return true;
                return crit.IsMaxed || crit.IsValueCapped;
            }
        }

        /** 완성 전 행 색. 처음 한 번만 기억한다 */
        private Color? normalRowTint;

        /** 잠금/완성 전 이름 색 */
        private Color? normalNameColor;

        private PlayerWallet wallet;

        private void Start()
        {
            wallet = PlayerWallet.Instance;

            if (button != null) button.onClick.AddListener(OnClick);
            if (system != null) system.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;

            // 해금은 스테이지가 오를 때 일어난다. 골드/강화 이벤트만 듣고 있으면
            // 잠긴 줄이 해금 스테이지에 닿아도 다음 구매가 있을 때까지 잠긴 채로
            // 남는다 - 화면에서는 "해금이 안 됐다"로 보인다
            progress = StageProgress.Instance;
            if (progress != null) progress.Changed += Refresh;

            // 배수가 바뀌면 이 줄의 가격과 증가폭이 통째로 달라진다 (#9)
            UpgradeBatchSelector.Changed += Refresh;

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            if (system != null) system.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (progress != null) progress.Changed -= Refresh;
            UpgradeBatchSelector.Changed -= Refresh;
        }

        private StageProgress progress;

        private void OnGoldChanged(BigDouble gold)
        {
            Refresh();
        }

        /**
         * @brief 이번에 살 칸 수. 배수 줄이 고른 값을 이 줄에 적용한 결과다 (#9).
         *
         * "최대"(0)는 **이 축의** 최대다. 잔액과 이 축의 곡선이 함께 정하므로
         * 축마다 다른 수가 나오고, 그것이 맞다 - 싼 축은 많이, 비싼 축은 적게.
         *
         * 배수가 정해준 수보다 잔액이 모자라면 살 수 있는 데까지로 줄인다.
         * 화면에 뜬 가격과 실제로 나가는 골드가 같아야 하기 때문이다 -
         * ×100인데 열두 칸만 살 수 있으면 이 줄은 열두 칸의 가격을 보여준다.
         */
        private int PlannedLevels(UpgradeTrack track)
        {
            int wanted = UpgradeBatchSelector.Current;
            int affordable = track.AffordableLevels(wallet, wanted);

            // 한 칸도 못 사면 1로 둔다. 0칸의 가격("0")을 보여주면 공짜로
            // 읽히고, 이 줄이 원래 하던 일(다음 한 칸이 얼마인가)도 사라진다
            return affordable > 0 ? affordable : 1;
        }

        private void OnClick()
        {
            if (system == null) return;

            var track = system.GetTrack(trackIndex);
            if (track == null) return;

            int count = UpgradeBatchSelector.Current;

            // 한 칸이면 예전 경로 그대로다. 배수 줄이 없는 씬(전투 전용 테스트)도
            // 여기로 떨어진다
            if (count == 1) { system.TryPurchase(trackIndex); return; }

            // "최대"는 잔액이 감당하는 데까지. 여기서 수를 확정해 넘기지 않고
            // 0을 그대로 넘기면 시스템 쪽이 다시 잔액을 읽어야 하고, 그 사이
            // 골드가 들어오면(방치 수익은 계속 돈다) 화면에 뜬 수보다 더 산다
            if (count <= 0) count = track.AffordableLevels(wallet, 0);
            if (count <= 0) return;

            system.TryPurchaseMany(trackIndex, count);
        }

        private void Refresh()
        {
            if (system == null) return;

            var track = system.GetTrack(trackIndex);
            if (track == null) return;

            // 잠긴 줄은 레벨도 값도 보여주지 않는다. 아직 시작하지 않은 축이라
            // "Lv.1"이 떠 있으면 이미 갖고 있는 것으로 읽힌다
            if (IsLocked)
            {
                if (nameLabel != null)
                {
                    nameLabel.text = track.DisplayName;
                    nameLabel.color = lockedColor;
                }
                if (valueLabel != null) valueLabel.text = string.Empty;
                if (costLabel != null)
                {
                    // 심화 게이트의 조건은 스테이지가 아니다 - 문구가 갈린다
                    costLabel.text = deepGate && !DeepUnlocked
                        ? deepLockedLabel
                        : string.Format(lockedLabel, unlockStage);
                    costLabel.color = lockedColor;
                }
                if (rowBackground != null)
                {
                    if (normalRowTint == null) normalRowTint = rowBackground.color;
                    rowBackground.color = lockedRowTint;
                }
                if (button != null) button.interactable = false;
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = track.DisplayName + "  Lv." + track.Level;

                // 잠금이 풀리면 이름 색도 돌아와야 한다. 아래에서 완성(금색)일
                // 때만 색을 건드리므로, 여기서 되돌리지 않으면 해금된 줄이
                // 잠금색으로 남는다
                if (normalNameColor == null) normalNameColor = affordableColor;
                nameLabel.color = normalNameColor.Value;
            }

            // 상한에 막혀 있으면 구매도 막혀 있어야 한다. 예전 세이브가 상한 위의
            // 레벨을 들고 올 수 있으므로(레벨은 유지하고 효과만 막는다) IsMaxed 하나로는
            // 부족하다 - Lv.44 / 상한 Lv.32 인 트랙은 IsMaxed가 참이지만, 규칙이
            // 바뀌어 상한만 올라간 경우에는 거짓이면서 값은 여전히 막혀 있을 수 있다
            bool capped = track.IsMaxed || track.IsValueCapped;

            // 이번 구매가 몇 칸인가 (#9). 배수가 1이면 예전과 똑같은 한 칸이다
            int planned = capped ? 0 : PlannedLevels(track);

            if (valueLabel != null)
            {
                // Format이 아니라 FormatStat이다. Format은 1000 미만을 정수로 읽어서
                // 이 버튼이 보여줘야 할 변화를 정확히 그 구간에서 지워버린다
                // (5 -> 5.6이 "5 -> 6", 1.15 -> 1.27이 "1 -> 1").
                //
                // 소수 둘째 자리까지 두는 이유도 같다. 첫째 자리로는 공격속도의
                // 1.15 -> 1.27이 둘 다 1.2로 뭉개진다
                //
                // 배수 구매면 **그 칸 수만큼 간 값**을 보여준다. 다음 한 칸의 값을
                // 보여주면서 백 칸의 가격을 받으면 화면이 거짓말을 한다
                valueLabel.text = capped
                    ? track.Format(track.Value)
                    : track.Format(track.Value) + " → " + track.Format(track.ValueAtLevel(track.Level + planned))
                      + (planned > 1 ? "   +" + planned : string.Empty);
            }

            // 배수 구매의 가격은 그 칸들의 **합**이다. 한 칸 가격만 보여주면
            // 누르는 순간 예상보다 훨씬 많은 골드가 나간다
            var price = planned > 1 ? track.CostOfNextLevels(planned) : track.Cost;
            bool affordable = wallet != null && wallet.CanAfford(price);

            if (costLabel != null)
            {
                // 상한에 닿으면 비용 대신 완성 표시를 세운다. 값만 멈추고 비용이
                // 계속 보이면 골드가 모자라서 못 사는 것인지 더 살 것이 없는
                // 것인지 구분되지 않는다. 공격속도는 아트가 정한 상한이 있어서
                // (AttackSpeedCurve) 실제로 여기 도달한다
                // 비용은 언제나 정수 골드다(E-3 수정, UpgradeCost) - 43단계의
                // 소수 표기는 규칙과 함께 사라졌다. Format의 정수 축약이 맞다
                costLabel.text = capped ? masteredLabel : NumberFormatter.Format(price);
                costLabel.color = capped ? masteredColor
                                : affordable ? affordableColor : unaffordableColor;
            }

            // 이름도 함께 금색으로. 비용 칸만 바뀌면 목록을 훑을 때 눈에 안 걸린다
            if (nameLabel != null && capped) nameLabel.color = masteredColor;

            if (rowBackground != null)
            {
                if (normalRowTint == null) normalRowTint = rowBackground.color;
                rowBackground.color = capped ? masteredRowTint : normalRowTint.Value;
            }

            if (button != null) button.interactable = !capped && affordable;
        }
    }
}
