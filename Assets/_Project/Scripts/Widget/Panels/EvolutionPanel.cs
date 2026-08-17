using Onikiri.Core;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 전직 페이지의 실체 (33단계). 잠금 안내와 진화 카드를 오간다.
     *
     * 12단계부터 "전직 Lv.30"으로 잠겨 있던 자리다. 페이지는 성장 패널의 셋째
     * 탭이고(GrowthPanelTabs), 이 컴포넌트가 페이지 루트에 앉아 두 상태를
     * 가른다:
     *
     *   잠김 (Lv < 30)   잠금 안내만 보인다. 문구는 LockedTab이 그린다 -
     *                    해금 문구 규칙이 이미 거기 있고, 두 번 적을 이유가 없다
     *   열림             진화 카드. 현재/다음 티어의 스프라이트·이름·배수,
     *                    비용(보석 먼저 - 관문이 보석이다), 진화 버튼
     *
     * ## 전/후 미리보기 규칙은 장비 카드와 같다
     *
     * 화살표의 왼쪽(현재 배수)은 현재 칸에 이미 있으므로 다음 칸에는 화살표와
     * 오른쪽만 적는다. 배수는 두 자리다 - 티어 한 칸이 +10% 이상이라 장비처럼
     * 세 자리까지 갈 이유가 없다.
     */
    public sealed class EvolutionPanel : MonoBehaviour
    {
        [SerializeField] private EvolutionSystem system;

        [Tooltip("잠금 안내 행. LockedTab이 문구를 그린다")]
        [SerializeField] private GameObject lockedRoot;

        [Tooltip("진화 카드. 해금되면 이것만 보인다")]
        [SerializeField] private GameObject contentRoot;

        [Header("현재 티어")]
        [SerializeField] private Image currentIcon;
        [SerializeField] private TMP_Text currentName;
        [SerializeField] private TMP_Text currentStat;

        [Header("다음 티어")]
        [SerializeField] private Image nextIcon;
        [SerializeField] private TMP_Text nextName;
        [SerializeField] private TMP_Text nextStat;

        [Header("진화 버튼 (보석 + 골드)")]
        [SerializeField] private Button evolveButton;
        [SerializeField] private TMP_Text evolveTitle;
        [SerializeField] private TMP_Text evolveCost;

        /**
         * @brief 귀문 상태를 읽는 창구 (4단계 §5). 없어도 화면은 돈다.
         *
         * 이 참조가 비면 버튼은 3단계까지의 문구("귀문 st50 돌파")로 떨어진다 -
         * 요구 게이트는 카탈로그만으로 알 수 있고, `BossFight`가 더해주는 것은
         * **지금 그 문이 어떤 상태인가**뿐이다. 그래서 없을 때의 화면이
         * 거짓말이 되지 않는다.
         */
        [Tooltip("귀문 상태(도전 가능/도전 중)를 읽는다. 비면 요구 게이트만 적는다")]
        [SerializeField] private Onikiri.Battle.BossFight fight;

        [Tooltip("티어별 초상. 인덱스 = 티어(0=로닌). 빌더가 idle 첫 프레임을 적는다")]
        [SerializeField] private Sprite[] tierPortraits;

        /**
         * @brief 티어별 초상의 발선 (셀 아래에서의 비율). 빌더가 픽셀을 실측한다.
         *
         * 스프라이트 피벗을 쓰지 않는 이유는 그 값이 **팩 전체의 최솟값**이기
         * 때문이다(슬라이서가 쓰러지는 DEATH 프레임까지 훑는다). 초상은 idle
         * 첫 프레임 하나이고 그 프레임의 발은 팩 최솟값보다 위에 있다 - 실제로
         * 검객(팩 2px, idle 13px)이 다른 초상보다 떠 보였다. 정렬의 기준은
         * 메타데이터가 아니라 그려진 픽셀이어야 한다.
         */
        [SerializeField] private float[] tierPortraitFeet;

        /** 티어별 초상의 그려진 가로 중심 (셀 왼쪽에서의 비율). 빌더가 실측한다 */
        [SerializeField] private float[] tierPortraitCenter;

        /**
         * @brief 티어별 마감 넛지 (**원본 아트 픽셀** 단위, 39단계).
         *
         * 발선·가로 중심 실측이 큰 어긋남은 잡지만, 팩마다 그림자·칼끝·장식이
         * 잉크 경계에 섞여 실측 기준선이 눈의 기준선과 1~2px 어긋난다. 그 마지막
         * 픽셀은 계측이 아니라 눈으로 잡는 값이라 빌더의 팩별 표에서 온다
         * (UpgradePanelBuilder.PortraitNudges).
         */
        [SerializeField] private Vector2[] tierPortraitNudge;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color masteredColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /**
         * @brief 초상의 **원본 픽셀 배율**과 발 기준선.
         *
         * 초상은 캐릭터 시트의 idle 첫 프레임 그대로다. 두 가지를 고정한다:
         *
         *   크기   원본 아트 1픽셀 = 3.4 캔버스 단위. 박스-맞춤 배율을 쓰면
         *          셀 크기가 팩마다 달라서(96x64 / 106x84 / 128x108) 같은
         *          키(34px)의 캐릭터가 팩에 따라 10%씩 다르게 나온다 - 인간
         *          경지는 전부 같은 크기여야 하고, 데몬이 커 보이는 것은
         *          배율이 아니라 **아트 자체가 1.5배(52px)라서**여야 한다
         *   발선   모든 초상의 발이 박스 바닥의 같은 선에 닿는다. 크기가
         *          달라도 발은 한 줄 - 데몬은 머리만 위로 솟는다
         *
         * 정렬이 없으면 전후의 두 초상이 다른 높이에 떠서 도약이 아니라
         * 오배치로 읽힌다 - 실제로 그렇게 나왔다.
         */
        private const float PortraitPixelScale = 3.4f;
        private const float PortraitFeetInset = 10f;

        private CharacterLevel character;
        private PlayerWallet wallet;
        private GemWallet gems;

        private Vector2 currentIconBase;
        private Vector2 nextIconBase;
        private bool iconBasesCaptured;

        private void Start()
        {
            character = CharacterLevel.Instance;
            wallet = PlayerWallet.Instance;
            gems = GemWallet.Instance;

            if (evolveButton != null) evolveButton.onClick.AddListener(OnEvolve);

            if (system != null) system.Changed += Refresh;
            if (character != null) character.Changed += Refresh;
            if (wallet != null) wallet.GoldChanged += OnGoldChanged;
            if (gems != null) gems.GemsChanged += OnGemsChanged;

            // 귀문이 열리고 닫히는 것은 이 표의 마지막 줄이 말하는 사실이다.
            // 안 들으면 문이 열려도 "st50 돌파"인 채로 남는다 (4단계 §5)
            if (fight != null) fight.Changed += Refresh;

            Refresh();
        }

        /** 페이지가 꺼진 채로 저장된다. 켜질 때 다시 그린다 - EquipmentRow와 같다 */
        private void OnEnable()
        {
            if (wallet == null) wallet = PlayerWallet.Instance;
            if (gems == null) gems = GemWallet.Instance;
            Refresh();
        }

        private void OnDestroy()
        {
            if (evolveButton != null) evolveButton.onClick.RemoveListener(OnEvolve);

            if (system != null) system.Changed -= Refresh;
            if (character != null) character.Changed -= Refresh;
            if (wallet != null) wallet.GoldChanged -= OnGoldChanged;
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (fight != null) fight.Changed -= Refresh;
        }

        private void OnGoldChanged(BigDouble gold) { Refresh(); }
        private void OnGemsChanged(long balance) { Refresh(); }

        /**
         * @brief 경지 버튼. **이제 아무것도 사지 않는다.**
         *
         * 3단계 전까지 이 자리에서 `TryEvolve`가 보석과 골드를 받았다. 승급이
         * 귀문 돌파로만 오르게 되면서 살 것이 없어졌고, 버튼은 **다음 문이
         * 어디인지 알려주는 안내**가 됐다.
         *
         * 버튼 자체를 지우지 않은 이유는 화면에서 그 자리가 "다음에 무엇을
         * 해야 하는가"를 말하는 유일한 곳이기 때문이다 - 비활성으로 두면
         * 경지 탭이 읽을 것 없는 표가 된다.
         */
        private void OnEvolve()
        {
            // 눌러도 아무 일도 일어나지 않는다. 진행은 귀문이 연다
        }

        private void Refresh()
        {
            if (system == null) return;

            bool unlocked = system.IsUnlocked;

            // 잠긴 동안은 안내(LockedTab)가 화면의 전부다. 카드를 함께 보여주면
            // 값이 이미 갖고 있는 것으로 읽힌다 - EquipmentRow.DrawLocked과 같은 규칙
            if (lockedRoot != null && lockedRoot.activeSelf != !unlocked) lockedRoot.SetActive(!unlocked);
            if (contentRoot != null && contentRoot.activeSelf != unlocked) contentRoot.SetActive(unlocked);

            if (!unlocked) return;

            int tier = system.Tier;
            bool last = system.IsMaxTier;

            CaptureIconBases();

            ApplyPortrait(currentIcon, PortraitOf(tier), currentIconBase,
                          FeetOf(tier), CenterOf(tier), NudgeOf(tier));
            if (currentName != null)
            {
                currentName.text = system.TierName;
                currentName.color = last ? masteredColor : affordableColor;
            }
            if (currentStat != null)
            {
                currentStat.text = "공격 " + Multiplier(system.AttackMultiplier)
                                 + "\n체력 " + Multiplier(system.HealthMultiplier);
                currentStat.color = unaffordableColor;
            }

            if (last)
            {
                // 최종 진화. 다음 칸에 아무것도 없는 것보다 "끝까지 왔다"가
                // 화면에 남는 편이 낫다 - MASTER 규칙과 같다
                ApplyPortrait(nextIcon, PortraitOf(tier), nextIconBase,
                              FeetOf(tier), CenterOf(tier), NudgeOf(tier));
                if (nextName != null) { nextName.text = "최종 경지"; nextName.color = masteredColor; }
                if (nextStat != null) nextStat.text = string.Empty;

                SetButton("최종 경지", string.Empty, false, masteredColor);
                return;
            }

            ApplyPortrait(nextIcon, PortraitOf(tier + 1), nextIconBase,
                          FeetOf(tier + 1), CenterOf(tier + 1), NudgeOf(tier + 1));
            if (nextName != null)
            {
                nextName.text = system.NextTierName;
                nextName.color = affordableColor;
            }
            if (nextStat != null)
            {
                nextStat.text = "공격 → " + Multiplier(system.NextAttackMultiplier)
                              + "\n체력 → " + Multiplier(system.NextHealthMultiplier);
                nextStat.color = unaffordableColor;
            }

            // **값이 없다.** 승급은 귀문 돌파로 무료로 온다(승인 A-1) - 보석·골드
            // 표기를 지운 자리에 "어디서 열리는가"와 **"지금 어떤 상태인가"**를 적는다.
            //
            // 이름은 "진화"가 아니라 **"경지"**다 - 티어의 화면 용어를 경지로
            // 정한 네이밍 변경(사용자 지시)을 따른다
            int nextGate = PromotionTrialCatalog.NextGateStage(tier);
            bool open = TrialIsOpen;

            SetButton("경지", GateStatusText(nextGate), false,
                      open ? masteredColor : unaffordableColor);
        }

        /**
         * @brief 다음 문의 **상태 한 줄** (4단계 §5).
         *
         * ```
         * 도전 중     귀문 도전 중
         * 도전 가능   귀문 열림 · 지금 도전
         * 그 외       귀문 st50 돌파        (아직 그 층에 못 갔다)
         * 없음        (빈 줄)               최종 경지 앞에서만 나온다
         * ```
         *
         * "돌파 완료"와 "잠김"은 이 함수가 아니라 **바깥의 두 분기**가 이미
         * 말한다(`IsMaxTier` -> "최종 경지", `IsUnlocked` -> 잠금 안내). 네 상태를
         * 한 함수에 몰면 그 둘이 두 곳에서 결정되고, 그때부터 화면이 갈린다.
         */
        private string GateStatusText(int nextGate)
        {
            if (fight != null && fight.Current == Onikiri.Battle.BossFight.Phase.Trial)
                return PromotionTrialCatalog.GateInProgress;

            if (TrialIsOpen) return PromotionTrialCatalog.GateOpenNow;

            return nextGate > 0
                ? PromotionTrialCatalog.GateRequirementPrefix + nextGate
                  + PromotionTrialCatalog.GateRequirementSuffix
                : string.Empty;
        }

        /** 지금 문이 열려 있는가(대기 상태). 참조가 없으면 모른다 = 아니다 */
        private bool TrialIsOpen
        {
            get { return fight != null && fight.CanChallengeTrial; }
        }

        private void SetButton(string titleText, string costText, bool interactable, Color color)
        {
            if (evolveTitle != null) { evolveTitle.text = titleText; evolveTitle.color = color; }
            if (evolveCost != null) { evolveCost.text = costText; evolveCost.color = color; }
            if (evolveButton != null) evolveButton.interactable = interactable;
        }

        private Sprite PortraitOf(int tier)
        {
            if (tierPortraits == null || tierPortraits.Length == 0) return null;
            int index = Mathf.Clamp(tier, 0, tierPortraits.Length - 1);
            return tierPortraits[index];
        }

        /** 빌더가 놓은 초상 위치. 보정이 늘 base + 보정이어야 누적되지 않는다 */
        private void CaptureIconBases()
        {
            if (iconBasesCaptured) return;
            if (currentIcon != null) currentIconBase = currentIcon.rectTransform.anchoredPosition;
            if (nextIcon != null) nextIconBase = nextIcon.rectTransform.anchoredPosition;
            iconBasesCaptured = true;
        }

        /** 이 티어 초상의 실측 발선. 빌더가 안 쟀으면 스프라이트 피벗으로 내려간다 */
        private float FeetOf(int tier)
        {
            if (tierPortraitFeet == null || tierPortraitFeet.Length == 0) return -1f;
            int index = Mathf.Clamp(tier, 0, tierPortraitFeet.Length - 1);
            return tierPortraitFeet[index];
        }

        /** 이 티어 초상의 실측 가로 중심. 빌더가 안 쟀으면 셀 중앙으로 내려간다 */
        private float CenterOf(int tier)
        {
            if (tierPortraitCenter == null || tierPortraitCenter.Length == 0) return -1f;
            int index = Mathf.Clamp(tier, 0, tierPortraitCenter.Length - 1);
            return tierPortraitCenter[index];
        }

        /** 이 티어 초상의 마감 넛지 (아트 픽셀). 빌더가 안 적었으면 0이다 */
        private Vector2 NudgeOf(int tier)
        {
            if (tierPortraitNudge == null || tierPortraitNudge.Length == 0) return Vector2.zero;
            int index = Mathf.Clamp(tier, 0, tierPortraitNudge.Length - 1);
            return tierPortraitNudge[index];
        }

        /**
         * @brief 초상을 고정 픽셀 배율로 키우고 발을 공통 바닥선에, 몸통을
         * 세로 중심선에 정렬한다.
         *
         * 발선·가로 중심은 빌더가 초상 프레임의 **그려진 픽셀에서 실측한 값**이다.
         * 스프라이트 피벗은 폴백일 뿐이다 - 그 값은 팩 전체의 최솟값이라 idle
         * 초상의 실제 발보다 낮고, 실제로 검객 초상이 떠 보였다.
         *
         * 현재/다음 초상이 **완전히 같은 이 루틴 하나**를 지난다. 한쪽만 다른
         * 배치를 쓰면 그 차이가 그대로 "오른쪽이 떠 있다"가 된다.
         */
        private void ApplyPortrait(Image image, Sprite sprite, Vector2 basePosition,
                                   float feetNorm, float centerNorm, Vector2 nudgeArtPixels)
        {
            if (image == null) return;
            image.sprite = sprite;
            if (sprite == null) return;

            var rect = image.rectTransform.rect;
            float cellW = sprite.rect.width;
            float cellH = sprite.rect.height;
            if (cellW <= 0f || cellH <= 0f) return;

            if (feetNorm < 0f) feetNorm = sprite.pivot.y / cellH;
            if (centerNorm < 0f) centerNorm = 0.5f;

            // preserveAspect가 셀을 박스에 맞춘 크기. 이 맞춤 배율은 셀 크기에
            // 따라 팩마다 다르므로, 원본 픽셀 배율이 일정해지도록 로컬 스케일로
            // 나눠서 상쇄한다 - 같은 키의 캐릭터는 같은 크기로 나온다
            float fit = Mathf.Min(rect.width / cellW, rect.height / cellH);
            float shownW = cellW * fit;
            float shownH = cellH * fit;
            float zoom = PortraitPixelScale / fit;

            image.rectTransform.localScale = Vector3.one * zoom;

            // **preserveAspect는 남는 공간을 rectTransform 피벗으로 나눈다 -
            // 가운데가 아니다.** 피벗이 (0.5, 1)이므로 줄어든 세로는 전부 아래로
            // 남고 셀은 박스 **위 모서리에 붙는다.** 33단계에 "가운데 그려진다"고
            // 잘못 가정해서, 정사각 셀(로닌 96x96)만 맞고 가로로 긴 셀 넷이
            // (박스높이 - 표시높이)/2 x 확대만큼 떠 있었다 - 실측 44~69px이
            // 이 식과 정확히 일치했다(39단계). 위에서 발까지는
            //   (1 - 발선) x 셀 표시 높이 x 확대
            float feetFromTop = zoom * (1f - feetNorm) * shownH;
            float targetFromTop = rect.height - PortraitFeetInset;

            // 그려진 몸통이 셀 안에서 치우쳐 있으면 그만큼 반대로 민다
            float dx = -(centerNorm - 0.5f) * shownW * zoom;

            // 마감 넛지 (39단계). 아트 픽셀 단위이므로 초상 배율을 곱해야
            // 화면에서 "1픽셀"이 실제로 그 픽셀만큼 움직인다
            var nudge = nudgeArtPixels * PortraitPixelScale;

            image.rectTransform.anchoredPosition =
                basePosition + new Vector2(dx, feetFromTop - targetFromTop) + nudge;
        }

        /** 배수는 두 자리다. 티어 한 칸이 +10% 이상이라 장비의 세 자리가 필요 없다 */
        private static string Multiplier(double value)
        {
            return "×" + value.ToString("F2");
        }
    }
}
