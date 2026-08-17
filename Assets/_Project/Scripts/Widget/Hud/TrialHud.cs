using Onikiri.Battle;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 귀문(승급전)의 **최소 HUD.** 전투를 가리지 않는 작은 반투명 띠다.
     *
     * ## 왜 별도 컴포넌트인가
     *
     * `BossHud`는 "제한 시간 30초 · 보스 하나 · 클리어 보너스"를 그리는 화면이고,
     * 귀문은 그 셋이 전부 다르다(폐쇄 150초 · 적 셋 · 보상 없음). 같은 컴포넌트에
     * 두 세계를 넣으면 모든 줄이 `if (trial)`로 갈리고, 그 분기가 늘어날수록
     * 어느 쪽도 안 읽히는 화면이 된다.
     *
     * 대신 **둘이 동시에 뜨지 않는다**는 것은 계약이다 - `BossHud`는
     * `Phase.Trial`을 자기 상태 목록에 넣지 않았고, 이쪽은 그 상태에서만 뜬다.
     *
     * ## 폰 화면을 가리지 않는다
     *
     * 중앙 대형 패널을 만들지 않는다. 상단에 얇은 띠 하나로 문 이름·진행·시계·
     * 격노를 한 줄에 넣고, 결과(승리·실패)만 잠깐 가운데에 뜬다. 3연전은 세
     * 번의 전투라 화면이 계속 덮여 있으면 그 셋을 볼 수가 없다.
     *
     * ## 문구의 출처
     *
     * 소프트캡 안내는 `PromotionTrialCatalog.SoftCapNotice` 하나다. 여기서 다시
     * 적으면 두 벌이 되고, 그 둘이 갈리는 날 화면과 계약이 다른 말을 한다.
     */
    public sealed class TrialHud : MonoBehaviour
    {
        [SerializeField] private BossFight fight;
        [SerializeField] private EvolutionSystem evolution;

        [Header("상단 띠 - 전투 중에만")]
        [Tooltip("반투명 띠 하나. 전투를 가리지 않는 높이여야 한다")]
        [SerializeField] private GameObject bannerRoot;

        [Tooltip("문 이름. '일문 · 귀문'")]
        [SerializeField] private TMP_Text gateLabel;

        [Tooltip("'1/3' - 몇 번째 적인가")]
        [SerializeField] private TMP_Text foeLabel;

        [Tooltip("폐쇄까지 남은 시간")]
        [SerializeField] private TMP_Text clockLabel;

        [Tooltip("격노 상태. 90초 전에는 숨는다")]
        [SerializeField] private GameObject enrageRoot;
        [SerializeField] private TMP_Text enrageLabel;

        /**
         * @brief 지금 상대의 체력 (4단계 §2). **띠 안에 사는 얇은 바 하나.**
         *
         * 새 패널을 만들지 않고 기존 반투명 띠를 늘린다 - 3연전은 최대 150초를
         * 쓰는 전투라 화면이 덮이면 그 셋을 볼 수가 없고, 그것이 이 HUD가
         * 처음부터 지켜온 규칙이다.
         *
         * **전투 중에만 뜬다.** 전환 구간에는 상대가 없어서 비율이 0인데,
         * 그때 바를 세워 두면 "0이 됐다"로 읽혀 이긴 것처럼 보인다.
         */
        [Header("적 체력 - FightingFoe1~3에서만")]
        [SerializeField] private GameObject healthRoot;

        [Tooltip("채워지는 쪽. BossHud와 같은 방식으로 anchorMax.x를 민다")]
        [SerializeField] private Image healthFill;

        [Tooltip("짧은 숫자 표기. 백분율 한 줄")]
        [SerializeField] private TMP_Text healthLabel;

        [Header("진입 안내 - Entering 동안만")]
        [SerializeField] private GameObject noticeRoot;
        [SerializeField] private TMP_Text noticeLabel;

        [Header("전환 - 2초")]
        [SerializeField] private GameObject transitionRoot;
        [SerializeField] private TMP_Text transitionLabel;

        [Header("결과")]
        [SerializeField] private GameObject resultRoot;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultDetail;

        [Header("색")]
        [SerializeField] private Color victoryColor = new Color32(0xFF, 0xD5, 0x6B, 0xFF);
        [SerializeField] private Color failureColor = new Color32(0xE8, 0x7B, 0x7B, 0xFF);

        /** 마지막으로 찍은 남은 초. 정수가 바뀔 때만 다시 그린다 */
        private int shownSeconds = -1;

        /** 마지막으로 찍은 격노 단계 */
        private int shownEnrage = -1;

        /** 마지막으로 찍은 체력 백분율. 정수가 바뀔 때만 다시 그린다 */
        private int shownHealthPercent = -1;

        private void Start()
        {
            if (fight != null) fight.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (fight != null) fight.Changed -= Refresh;
        }

        /**
         * @brief 문 이름은 카탈로그가 낸다 (4단계 §3).
         *
         * 여기 있던 표를 `PromotionTrialCatalog.GateName`으로 올렸다 - 가이드
         * 카드도 같은 이름을 적게 되면서 두 벌이 될 뻔했다.
         */
        private static string GateName(int gate)
        {
            return PromotionTrialCatalog.GateName(gate);
        }

        private void Refresh()
        {
            if (fight == null) return;

            bool inTrial = fight.Current == BossFight.Phase.Trial;
            var state = fight.Trial;

            bool fighting = inTrial && (state == BossFight.TrialState.FightingFoe1
                                     || state == BossFight.TrialState.FightingFoe2
                                     || state == BossFight.TrialState.FightingFoe3);
            bool transition = inTrial && (state == BossFight.TrialState.Transition1
                                       || state == BossFight.TrialState.Transition2);
            bool result = inTrial && (state == BossFight.TrialState.Victory
                                   || state == BossFight.TrialState.Failure
                                   || state == BossFight.TrialState.Closed);

            Show(noticeRoot, inTrial && state == BossFight.TrialState.Entering);
            Show(bannerRoot, fighting || transition);
            Show(transitionRoot, transition);
            Show(resultRoot, result);

            // 체력 바는 **싸우는 동안만**. 전환·결과·폐쇄·비활성에서 전부 꺼진다
            Show(healthRoot, fighting);

            // 다음 적이 서면 새 비율부터 다시 그린다. 안 비우면 앞 적의
            // 마지막 백분율과 같은 값일 때 첫 갱신이 통째로 생략된다
            if (!fighting) shownHealthPercent = -1;

            if (!inTrial) { shownSeconds = -1; shownEnrage = -1; shownHealthPercent = -1; return; }

            if (gateLabel != null) gateLabel.text = GateName(fight.TrialGate) + " · 귀문";

            // ---- 진입 안내. 규칙을 **한 번만** 말한다
            if (state == BossFight.TrialState.Entering && noticeLabel != null)
                noticeLabel.text = GateName(fight.TrialGate) + "\n"
                                 + PromotionTrialCatalog.SoftCapNotice;

            if (fighting && foeLabel != null)
                foeLabel.text = fight.TrialFoeNumber + "/" + PromotionTrialCatalog.FoeCount;

            if (transition && transitionLabel != null)
                transitionLabel.text = PromotionTrialCatalog.TransitionLabel;

            if (result) DrawResult(state);
        }

        /**
         * @brief 결과 넉 줄 (4단계 §4). **짧게.** 전체 화면 팝업을 만들지 않는다.
         *
         * ```
         * 승리   돌파
         *        일문 돌파 · <새 경지>
         *        공격 x1.00 -> x1.21   체력 x1.00 -> x1.21
         *        외형이 바뀌었다
         *
         * 실패   귀문 실패
         *        시간이 다 됐다 (또는 쓰러졌다)
         *        잃은 것은 없다 · 무료 재도전 가능
         * ```
         *
         * 배수는 **바뀐 결과**를 적는다. 새 값만 적으면 "x1.21"이 큰지 작은지
         * 알 수 없고, 이 화면이 존재하는 이유가 정확히 "무엇이 좋아졌는가"다.
         */
        private void DrawResult(BossFight.TrialState state)
        {
            if (resultTitle == null) return;

            if (state == BossFight.TrialState.Victory)
            {
                resultTitle.text = PromotionTrialCatalog.VictoryTitle;
                resultTitle.color = victoryColor;

                // 티어는 이미 올라 있다(BossFight.EndTrial이 스탯 반영까지
                // 끝냈다). 여기서는 읽기만 하고, 직전 값은 곡선에서 되짚는다
                if (resultDetail != null && evolution != null)
                {
                    int tier = evolution.Tier;
                    int before = Mathf.Max(0, tier - 1);

                    resultDetail.text =
                        GateName(fight.TrialGate) + PromotionTrialCatalog.BreakthroughJoin
                        + evolution.TierName + "\n"
                        + PromotionTrialCatalog.AttackPrefix
                        + EvolutionCurve.AttackMultiplierAt(before).ToString("F2")
                        + PromotionTrialCatalog.Arrow
                        + evolution.AttackMultiplier.ToString("F2")
                        + "   " + PromotionTrialCatalog.HealthPrefix
                        + EvolutionCurve.HealthMultiplierAt(before).ToString("F2")
                        + PromotionTrialCatalog.Arrow
                        + evolution.HealthMultiplier.ToString("F2") + "\n"
                        + PromotionTrialCatalog.AppearanceChanged;
                }
                return;
            }

            // 제목은 **하나**다. "쓰러졌다"와 "닫혔다"를 제목에서 가르면 같은
            // 사건(실패)이 두 이름을 갖고, 무엇이 일어났는지는 아랫줄이 말한다
            resultTitle.text = PromotionTrialCatalog.FailureTitle;
            resultTitle.color = failureColor;

            // 실패는 아무것도 뺏지 않는다. 그 사실을 화면이 말해야 재도전이
            // 망설여지지 않는다 - 무료·무제한이라는 계약의 화면 쪽 절반이다
            if (resultDetail != null)
                resultDetail.text = (state == BossFight.TrialState.Closed
                                        ? PromotionTrialCatalog.ClosedReason
                                        : PromotionTrialCatalog.DeathReason) + "\n"
                                  + PromotionTrialCatalog.FailureConsolation;
        }

        /**
         * @brief 시계와 격노는 **매 프레임** 돈다. `Changed`는 상태 전환에만 오므로.
         *
         * 갱신을 정수 경계에서만 하는 이유는 `BossHud`와 같다 - 1초 단위 표시라
         * 프레임마다 다시 그리면 그중 59/60은 같은 글자를 다시 만드는 일이다.
         */
        private void Update()
        {
            if (fight == null || fight.Current != BossFight.Phase.Trial) return;

            int left = Mathf.CeilToInt(fight.TrialSecondsLeft);
            if (left != shownSeconds && clockLabel != null)
            {
                shownSeconds = left;
                clockLabel.text = left + "초";
            }

            int steps = fight.TrialEnrageSteps;
            if (steps != shownEnrage)
            {
                shownEnrage = steps;
                Show(enrageRoot, steps > 0);

                if (steps > 0 && enrageLabel != null)
                    enrageLabel.text = PromotionTrialCatalog.EnragePrefix + steps
                                     + PromotionTrialCatalog.EnrageSuffix;
            }

            DrawHealth();
        }

        /**
         * @brief 적 체력 바. **바는 매 프레임, 글자는 1% 경계에서만.**
         *
         * 바는 anchorMax를 미는 것뿐이라 메시를 다시 만들지 않는다 - 그래서
         * 매 프레임 밀어도 되고, 오히려 그래야 깎이는 것이 눈에 보인다.
         * 백분율 글자는 TMP 메시라 정수가 바뀔 때만 쓴다(시계·격노와 같은 규칙).
         */
        private void DrawHealth()
        {
            if (healthRoot == null || !healthRoot.activeSelf) return;

            float fraction = fight.TrialFoeHealthFraction;

            if (healthFill != null)
            {
                var rect = (RectTransform)healthFill.transform;
                rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), rect.anchorMax.y);
            }

            // 올림이다. 살아 있는 적이 "0%"로 적히면 안 된다 - 그 글자는
            // 죽었다는 뜻으로 읽히고, 그 순간 플레이어는 화면이 멈췄다고 본다
            int percent = fraction > 0f ? Mathf.Max(1, Mathf.CeilToInt(fraction * 100f)) : 0;
            if (percent != shownHealthPercent && healthLabel != null)
            {
                shownHealthPercent = percent;
                healthLabel.text = percent + "%";
            }
        }

        private static void Show(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
