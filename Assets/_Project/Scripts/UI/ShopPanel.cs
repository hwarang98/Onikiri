using System;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 상점 화면. **이 게임에 처음 생기는 상점이다.**
     *
     * ## 왜 지금까지 없었는가, 그리고 왜 이제 생기는가
     *
     * 31단계가 보석을 만들면서 화면에 "곧 상점에서 사용"이라고 적어 뒀다.
     * 그 뒤 소비처는 장비 등급업·동료 해금·전직·요도 촉매로 하나씩 생겼는데
     * 전부 **자기 화면 안**에 있었다 - 보석은 쓰는 곳이 넷인데 상점이라는
     * 이름의 자리는 어디에도 없었고, 사용자가 그것을 지적했다.
     *
     * 이 화면이 그 자리다. 그리고 여기 서는 것은 **자기 화면이 없는 상품**
     * 뿐이다 - 뽑기(어느 시스템에도 속하지 않는다), 일일 무료(리텐션),
     * 그리고 다음 스텝의 현금·광고. 장비 등급업을 여기로 옮기지 않는 이유는
     * 그 버튼이 "무엇을 사는가"보다 "무엇이 세지는가"에 붙어 있어야 하기
     * 때문이다 - 대장간에서 산 등급이 상점 목록의 한 줄이 되면 강화 동선이
     * 두 화면으로 갈린다.
     *
     * ## 화면이 말하는 것
     *
     *   뽑기 배너   확률표(공개) · 천장까지 남은 횟수 · 단연/10연
     *   일일 무료   오늘 남았는가 · 아니면 다음 04:00까지
     *   준비 중     광고 · 보석 팩. **가격은 적고 버튼은 죽인다**
     *
     * 마지막 줄이 이 화면의 정직함이다. 41단계의 잠긴 탭 규칙("눌리는데
     * 빈 화면이 나오는 것보다 안 눌리는 편이 정직하다")을 상품에 적용한
     * 것이고, 20단계의 "쌓이는 것이 보이고 쓸 곳이 온다는 것을 알면 죽은
     * 재화가 아니라 예고가 된다"와 같은 처방이다.
     */
    public sealed class ShopPanel : MonoBehaviour
    {
        [SerializeField] private GachaSystem system;
        [SerializeField] private YodoSystem yodo;
        [SerializeField] private GemWallet gems;

        [Header("뽑기 배너")]
        [SerializeField] private Button singleButton;
        [SerializeField] private TMP_Text singleCost;
        [SerializeField] private Image singleBackground;
        [SerializeField] private Button tenButton;
        [SerializeField] private TMP_Text tenCost;
        [SerializeField] private Image tenBackground;

        [Tooltip("천장까지 남은 횟수 + 누적")]
        [SerializeField] private TMP_Text pityLabel;

        [Header("일일 무료")]
        [SerializeField] private Button freeButton;
        [SerializeField] private TMP_Text freeTitle;
        [SerializeField] private TMP_Text freeCost;
        [SerializeField] private Image freeBackground;
        [SerializeField] private TMP_Text freeStateLabel;

        /**
         * @brief 오의 뽑기 배너 (50단계). **요도 배너와 같은 조각, 다른 시스템.**
         *
         * 필드를 따로 두고 그리는 함수를 공유하지 않은 이유는 두 시스템이
         * 다른 타입이기 때문이다(GachaSystem / SkillGachaSystem). 인터페이스를
         * 하나 세워 묶을 수도 있지만, 그러면 "두 배너가 같은 물건"이라는
         * 말을 코드가 하게 된다 - 천장도 무료 쿨도 재고도 갈려 있는 것이
         * 이 스텝의 설계이므로(SkillGachaSystem 머리 주석) 화면도 그것을
         * 두 벌로 적는 편이 정직하다.
         */
        [Header("오의 뽑기 배너")]
        [SerializeField] private SkillGachaSystem skillSystem;
        [SerializeField] private SkillSystem skills;
        [SerializeField] private Button skillSingleButton;
        [SerializeField] private TMP_Text skillSingleCost;
        [SerializeField] private Image skillSingleBackground;
        [SerializeField] private Button skillTenButton;
        [SerializeField] private TMP_Text skillTenCost;
        [SerializeField] private Image skillTenBackground;

        [Tooltip("천장까지 남은 횟수 + 누적. 재고가 없으면 그 사실을 적는다")]
        [SerializeField] private TMP_Text skillPityLabel;

        [Header("오의 일일 무료")]
        [SerializeField] private Button skillFreeButton;
        [SerializeField] private TMP_Text skillFreeTitle;
        [SerializeField] private TMP_Text skillFreeCost;
        [SerializeField] private Image skillFreeBackground;
        [SerializeField] private TMP_Text skillFreeStateLabel;

        [Header("결과")]
        [SerializeField] private GachaResultPopup popup;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color buttonTint = new Color(0.42f, 0.56f, 1.00f, 1f);
        [SerializeField] private Color freeTint = new Color(0.55f, 1.00f, 0.62f, 1f);

        /**
         * @brief 남은 시간 표시를 다시 그리는 주기 (초).
         *
         * 매 프레임 DateTime.UtcNow를 읽고 문자열을 만들면 방치 화면에서
         * GC가 계속 돈다. 초 단위 표시라 1초면 충분하다 - DailyResetTimer가
         * 같은 이유로 같은 값을 쓴다.
         */
        private const float ClockInterval = 1f;

        private float clock;

        private void OnEnable()
        {
            if (system == null) system = GachaSystem.Instance;
            if (yodo == null) yodo = YodoSystem.Instance;
            if (gems == null) gems = GemWallet.Instance;
            if (skillSystem == null) skillSystem = SkillGachaSystem.Instance;
            if (skills == null) skills = SkillSystem.Instance;

            Refresh();
        }

        private void Start()
        {
            if (system == null) system = GachaSystem.Instance;
            if (gems == null) gems = GemWallet.Instance;
            if (skillSystem == null) skillSystem = SkillGachaSystem.Instance;
            if (skills == null) skills = SkillSystem.Instance;

            if (system != null)
            {
                system.Changed += Refresh;
                system.Pulled += ShowResults;
            }
            if (skillSystem != null)
            {
                skillSystem.Changed += Refresh;
                skillSystem.Pulled += ShowSkillResults;
            }
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            if (singleButton != null) singleButton.onClick.AddListener(PullOnce);
            if (tenButton != null) tenButton.onClick.AddListener(PullTen);
            if (freeButton != null) freeButton.onClick.AddListener(PullFree);

            if (skillSingleButton != null) skillSingleButton.onClick.AddListener(SkillPullOnce);
            if (skillTenButton != null) skillTenButton.onClick.AddListener(SkillPullTen);
            if (skillFreeButton != null) skillFreeButton.onClick.AddListener(SkillPullFree);

            Refresh();
        }

        private void OnDestroy()
        {
            if (system != null)
            {
                system.Changed -= Refresh;
                system.Pulled -= ShowResults;
            }
            if (skillSystem != null)
            {
                skillSystem.Changed -= Refresh;
                skillSystem.Pulled -= ShowSkillResults;
            }
            if (gems != null) gems.GemsChanged -= OnGemsChanged;

            if (singleButton != null) singleButton.onClick.RemoveListener(PullOnce);
            if (tenButton != null) tenButton.onClick.RemoveListener(PullTen);
            if (freeButton != null) freeButton.onClick.RemoveListener(PullFree);

            if (skillSingleButton != null) skillSingleButton.onClick.RemoveListener(SkillPullOnce);
            if (skillTenButton != null) skillTenButton.onClick.RemoveListener(SkillPullTen);
            if (skillFreeButton != null) skillFreeButton.onClick.RemoveListener(SkillPullFree);
        }

        private void Update()
        {
            // 무료 뽑기의 남은 시간만 시계가 필요하다. 나머지는 이벤트로 온다
            clock += Time.unscaledDeltaTime;
            if (clock < ClockInterval) return;
            clock = 0f;
            RefreshFree();
            RefreshSkillFree();
        }

        private void OnGemsChanged(long value) { Refresh(); }

        // ---------------------------------------------------------------- 동작

        private void PullOnce()
        {
            if (system != null) system.TryPull(1);
        }

        private void PullTen()
        {
            if (system != null) system.TryPull(GachaCurve.TenPullCount);
        }

        private void PullFree()
        {
            if (system != null) system.TryFreePull();
        }

        private void SkillPullOnce()
        {
            if (skillSystem != null) skillSystem.TryPull(1);
        }

        private void SkillPullTen()
        {
            if (skillSystem != null) skillSystem.TryPull(SkillGachaCurve.TenPullCount);
        }

        private void SkillPullFree()
        {
            if (skillSystem != null) skillSystem.TryFreePull();
        }

        /**
         * @brief 오의 뽑기 결과. **같은 판을 쓴다** - GachaResultPopup 주석 참고.
         *
         * 두 배너가 같은 팝업을 나눠 쓰므로 한 번에 하나만 뜬다. 그것이
         * 맞는 이유는 뽑기가 동시에 두 번 돌 수 없기 때문이고(버튼이 하나씩
         * 눌린다), 판이 스스로 안 닫히므로 확인을 누르기 전에는 다음 뽑기가
         * 그 위를 덮지 않는다.
         */
        private void ShowSkillResults(System.Collections.Generic.List<SkillGachaSystem.PullResult> list)
        {
            if (popup != null) popup.Show(list, skills);
        }

        private void ShowResults(System.Collections.Generic.List<GachaSystem.PullResult> list)
        {
            // 목록은 GachaSystem이 돌려 쓰는 것이다 - 여기서 보관하지 않고
            // 그 자리에서 그린다(GachaSystem.results 주석)
            if (popup != null) popup.Show(list, yodo);
        }

        // ---------------------------------------------------------------- 표시

        private void Refresh()
        {
            bool unlocked = system != null && system.IsUnlocked;

            DrawPullButton(singleButton, singleBackground, singleCost, 1, unlocked);
            DrawPullButton(tenButton, tenBackground, tenCost, GachaCurve.TenPullCount, unlocked);

            if (pityLabel != null)
            {
                if (!unlocked)
                {
                    pityLabel.text = GachaCurve.UnlockStage + "스테이지부터";
                    pityLabel.color = unaffordableColor;
                }
                else
                {
                    // 문구의 출처는 곡선이다 - 빌더가 초기값을 적고 여기가
                    // 매번 다시 적는데, 두 곳에 따로 쓰면 씬을 연 순간과 첫
                    // 갱신 사이에 문장이 바뀐다(GachaCurve.PityText 주석)
                    int left = system.PullsUntilPity;
                    pityLabel.text = GachaCurve.PityText(left, system.TotalPulls);

                    // 천장이 코앞이면 금색이다. 다른 줄이 전부 "지금 얼마인가"인데
                    // 이 줄만 **다음에 무엇이 오는가**이고, 마지막 몇 회에서
                    // 그것이 지금의 사실이 된다
                    pityLabel.color = left <= GachaCurve.TenPullCount ? goldColor : unaffordableColor;
                }
            }

            RefreshFree();
            RefreshSkill();
        }

        /**
         * @brief 오의 배너. **재고가 이 배너의 두 번째 게이트다.**
         *
         * 해금(st41) 위에 재고가 얹힌다 - 팔 것이 없으면 버튼이 죽고 줄이
         * 그 사실을 적는다. 41단계의 "눌리는데 아무 일도 안 일어나는 것보다
         * 안 눌리는 편이 정직하다"이고, 잠긴 것과 다 팔린 것을 **다른
         * 문장으로** 적는 이유는 조건이 다르기 때문이다: 앞의 것은 가면
         * 열리고 뒤의 것은 다시 안 열린다.
         */
        private void RefreshSkill()
        {
            bool unlocked = skillSystem != null && skillSystem.IsUnlocked;
            bool stock = skillSystem != null && skillSystem.HasStock;

            DrawSkillButton(skillSingleButton, skillSingleBackground, skillSingleCost, 1);
            DrawSkillButton(skillTenButton, skillTenBackground, skillTenCost,
                            SkillGachaCurve.TenPullCount);

            if (skillPityLabel != null)
            {
                if (!unlocked)
                {
                    skillPityLabel.text = SkillGachaCurve.UnlockStage + "스테이지부터";
                    skillPityLabel.color = unaffordableColor;
                }
                else if (!stock)
                {
                    skillPityLabel.text = SoldOutText;
                    skillPityLabel.color = unaffordableColor;
                }
                else
                {
                    int left = skillSystem.PullsUntilPity;
                    skillPityLabel.text = GachaCurve.PityText(left, skillSystem.TotalPulls);
                    skillPityLabel.color = left <= SkillGachaCurve.TenPullCount
                        ? goldColor : unaffordableColor;
                }
            }

            RefreshSkillFree();
        }

        /**
         * @brief 다 팔린 배너가 적는 말.
         *
         * "품절"이 아니라 이유를 적는다. 재고가 없는 상태는 **플레이어가
         * 다 이룬 상태**이고(오의 둘을 열었고 장착이 전부 상한), 그 사실을
         * 말하지 않으면 화면이 고장난 것처럼 읽힌다 - 44단계가 넘침을
         * 문구로 지킨 것과 같은 자리다.
         */
        private const string SoldOutText = "해금 완료 · 장착 오의 전부 상한";

        private void DrawSkillButton(Button button, Image background, TMP_Text cost, int count)
        {
            if (button == null) return;

            int price = skillSystem != null ? skillSystem.CostFor(count) : 0;
            bool affordable = skillSystem != null && skillSystem.CanPull(count);

            button.interactable = affordable;

            if (cost != null)
            {
                cost.text = "보석 " + price;
                cost.color = affordable ? affordableColor : unaffordableColor;
            }

            if (background != null)
            {
                var tint = buttonTint;
                if (!affordable)
                {
                    tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f;
                    tint.a = 1f;
                }
                background.color = tint;
            }
        }

        private void RefreshSkillFree()
        {
            if (skillSystem == null) return;

            var now = DateTime.UtcNow;
            bool ready = skillSystem.HasFreePullAt(now);
            bool unlocked = skillSystem.IsUnlocked;
            bool stock = skillSystem.HasStock;

            if (skillFreeButton != null) skillFreeButton.interactable = ready;

            if (skillFreeTitle != null)
                skillFreeTitle.color = ready ? affordableColor : unaffordableColor;

            if (skillFreeCost != null)
            {
                if (!unlocked) skillFreeCost.text = SkillGachaCurve.UnlockStage + "스테이지부터";
                else if (!stock) skillFreeCost.text = "완료";
                else if (ready) skillFreeCost.text = "무료";
                else skillFreeCost.text = Clock(skillSystem.UntilFreePull(now));
                skillFreeCost.color = ready ? affordableColor : unaffordableColor;
            }

            if (skillFreeStateLabel != null)
            {
                skillFreeStateLabel.text = !unlocked
                    ? SkillGachaCurve.UnlockStage + "스테이지 도달 시 해금"
                    : !stock ? SoldOutText
                    : ready ? "오늘의 무료 뽑기가 남아 있다"
                            : "새벽 4시에 다시 열린다";
                skillFreeStateLabel.color = ready ? goldColor : unaffordableColor;
            }

            if (skillFreeBackground != null)
            {
                var tint = freeTint;
                if (!ready)
                {
                    tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f;
                    tint.a = 1f;
                }
                skillFreeBackground.color = tint;
            }
        }

        private void DrawPullButton(Button button, Image background, TMP_Text cost,
                                    int count, bool unlocked)
        {
            if (button == null) return;

            int price = system != null ? system.CostFor(count) : 0;
            bool affordable = unlocked && system != null && system.CanPull(count);

            button.interactable = affordable;

            if (cost != null)
            {
                cost.text = "보석 " + price;
                cost.color = affordable ? affordableColor : unaffordableColor;
            }

            if (background != null)
            {
                // 잠기거나 못 사면 판을 죽인다. RGB만 곱한다 - 스칼라 곱은
                // 알파까지 눌러 판을 반투명으로 만든다(41단계에서 물린 자리)
                var tint = buttonTint;
                if (!affordable)
                {
                    tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f;
                    tint.a = 1f;
                }
                background.color = tint;
            }
        }

        private void RefreshFree()
        {
            if (system == null) return;

            var now = DateTime.UtcNow;
            bool ready = system.HasFreePullAt(now);
            bool unlocked = system.IsUnlocked;

            if (freeButton != null) freeButton.interactable = ready;

            if (freeTitle != null)
                freeTitle.color = ready ? affordableColor : unaffordableColor;

            if (freeCost != null)
            {
                if (!unlocked) freeCost.text = GachaCurve.UnlockStage + "스테이지부터";
                else if (ready) freeCost.text = "무료";
                else freeCost.text = Clock(system.UntilFreePull(now));
                freeCost.color = ready ? affordableColor : unaffordableColor;
            }

            if (freeStateLabel != null)
            {
                // 잠긴 동안에는 "새벽 4시에 다시 열린다"가 거짓말이다 - 오늘 쓴
                // 적이 없고, 내일 새벽에도 안 열린다. 조건을 그대로 적는다
                // (헤더 배너와 같은 문장 - 한 화면이 같은 조건을 두 문장으로
                // 말하면 두 규칙이 된다)
                freeStateLabel.text = !unlocked
                    ? GachaCurve.UnlockStage + "스테이지 도달 시 해금"
                    : ready ? "오늘의 무료 뽑기가 남아 있다"
                            : "새벽 4시에 다시 열린다";
                freeStateLabel.color = ready ? goldColor : unaffordableColor;
            }

            if (freeBackground != null)
            {
                var tint = freeTint;
                if (!ready)
                {
                    tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f;
                    tint.a = 1f;
                }
                freeBackground.color = tint;
            }
        }

        /** 남은 시간 hh:mm:ss. DailyResetTimer와 같은 형식이다 */
        private static string Clock(TimeSpan left)
        {
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            return string.Format("{0:00}:{1:00}:{2:00}",
                (int)left.TotalHours, left.Minutes, left.Seconds);
        }
    }
}
