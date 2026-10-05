using System;
using System.Collections.Generic;
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
     * 그리고 다음 스텝의 현금·광고.
     *
     * ## 화면이 말하는 것 (69단계 개편)
     *
     *   배너 행     큰 아이콘 · 제목 · [확률] [광고] · 1회 / 10회 · 소환 레벨 바
     *   준비 중     광고 · 보석 팩. **가격은 적고 버튼은 죽인다**
     *
     * 46~68단계의 배너는 본문에 확률표를 펼치고 아래에 무료 줄을 따로 뒀다.
     * 69단계에 그것을 행 하나로 접었다 - 무료는 1회 버튼이 "무료 1회"로,
     * 온보딩 10연은 10회 버튼이 "선물 10연"으로 바뀌어 말하고, 확률표는 제목
     * 줄의 "확률" 버튼 뒤로 갔다(GachaRatePopup). 두 배너가 스크롤 없이 한
     * 화면에 선다.
     */
    public sealed class ShopPanel : MonoBehaviour
    {
        /**
         * @brief 배너 행 하나의 조각들. 빌더가 짓고 배선한다.
         *
         * 요도·오의가 **같은 모양**이고 그리는 규칙만 다르다 - 시스템이 다른
         * 타입이라(GachaSystem / SkillGachaSystem) 그리는 함수는 둘이다.
         */
        [Serializable]
        public sealed class BannerView
        {
            public Button singleButton;
            public Image singleBackground;
            public TMP_Text singleTitle;
            public TMP_Text singleCost;
            public TMP_Text singleNote;
            public Image singleGem;

            public Button tenButton;
            public Image tenBackground;
            public TMP_Text tenTitle;
            public TMP_Text tenCost;
            public TMP_Text tenNote;
            public Image tenGem;

            public Button rateButton;

            /** 바 줄. 잠김·구매 대기·소진이면 꺼지고 stateLabel이 대신 선다 */
            public GameObject barRoot;
            public TMP_Text levelLabel;
            public RectTransform barFill;
            public Image barFillImage;
            public TMP_Text barText;
            public TMP_Text stateLabel;
        }

        [SerializeField] private GachaSystem system;
        [SerializeField] private YodoSystem yodo;
        [SerializeField] private GemWallet gems;
        [SerializeField] private SkillGachaSystem skillSystem;
        [SerializeField] private SkillSystem skills;

        [SerializeField] private BannerView yodoBanner = new BannerView();
        [SerializeField] private BannerView skillBanner = new BannerView();

        [Header("결과 · 확률")]
        [SerializeField] private GachaResultPopup popup;
        [SerializeField] private GachaRatePopup ratePopup;

        [Header("색")]
        [SerializeField] private Color affordableColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color unaffordableColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);
        [SerializeField] private Color goldColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);
        [SerializeField] private Color buttonTint = new Color(0.42f, 0.56f, 1.00f, 1f);
        [SerializeField] private Color freeTint = new Color(0.55f, 1.00f, 0.62f, 1f);
        [SerializeField] private Color barTint = new Color32(0xFF, 0xB0, 0x3B, 0xFF);

        [Header("레벨업 연출 (69단계)")]
        [SerializeField] private float levelUpFillSeconds = 0.25f;
        [SerializeField] private float levelUpFlashSeconds = 0.15f;
        [SerializeField] private float levelUpPunchSeconds = 0.22f;
        [SerializeField] private float levelUpPunch = 1.45f;

        // ---------------------------------------------------------------- 문구

        public const string YodoTitle = "요도 뽑기";
        public const string YodoSubtitle = "(혼·전설 요도)";
        public const string SkillTitle = "오의 뽑기";
        public const string SkillSubtitle = "(스킬 해금)";

        public const string SingleTitle = "1회 소환";
        public static string TenTitle { get { return GachaCurve.TenPullCount + "회 소환"; } }
        public const string FreeSingleTitle = "무료 1회";
        public const string FreeSingleNote = "오늘 한 번";
        public static string IntroTenTitle { get { return "선물 " + SkillGachaCurve.IntroPullCount + "연"; } }
        public const string IntroTenNote = "무료";
        public const string RateButtonText = "확률";
        public const string AdButtonText = "광고";

        /**
         * @brief 다 팔린 배너가 적는 말.
         *
         * "품절"이 아니라 이유를 적는다. 재고가 없는 상태는 **플레이어가
         * 다 이룬 상태**이고(오의 둘을 열었고 장착이 전부 상한), 그 사실을
         * 말하지 않으면 화면이 고장난 것처럼 읽힌다.
         */
        public const string SoldOutText = "해금 완료 · 장착 오의 전부 상한";

        /** 배너는 섰는데 보석 구매가 아직이다. 무료로는 돌아간다 (69단계: 692px 줄에 맞춰 겹공백을 뺐다) */
        public static string SkillBuyLaterText
        {
            get { return "보석 구매는 " + SkillGachaCurve.PullUnlockStage + "스테이지부터 · 무료는 지금부터"; }
        }

        public static string LockedText(int stage) { return stage + "스테이지부터"; }

        /** 바 왼쪽 "소환 Lv.n". 바 안 숫자(BarText)와 같은 곡선에서 나온다 */
        public static string LevelLabel(int level) { return "소환 Lv." + level; }

        /** 바 안 "xp / next" */
        public static string BarText(long xp, long next) { return xp + " / " + next; }

        /** 바의 채움 비율 = 이번 레벨의 경험치 / 다음 레벨까지. **출처는 곡선 하나** */
        public static float BarRatio(long summonXp)
        {
            int level = SummonLevelCurve.LevelFor(summonXp);
            long next = SummonLevelCurve.XpToNext(level);
            return next > 0L ? Mathf.Clamp01((float)SummonLevelCurve.XpIntoLevel(summonXp) / next) : 0f;
        }

        /**
         * @brief 69단계에 화면에 새로 서는 문구 전부. 아틀라스 검사가 읽는다
         * (Step69Strings_AreInTheBakedCharset) - 없는 글자는 □로 뜬다.
         */
        public static string[] Step69Strings
        {
            get
            {
                return new[]
                {
                    YodoTitle, YodoSubtitle, SkillTitle, SkillSubtitle,
                    SingleTitle, TenTitle, FreeSingleTitle, FreeSingleNote,
                    IntroTenTitle, IntroTenNote, RateButtonText, AdButtonText,
                    SkillBuyLaterText, SoldOutText, LockedText(41),
                    LevelLabel(888), BarText(8888, 8888),
                    GachaRatePopup.YodoTitle, GachaRatePopup.SkillTitle, GachaRatePopup.FootnoteFor(888),
                    GachaResultPopup.RevealingText,
                    GachaRatePopup.PercentText(0.7163),
                    GachaResultPopup.DowngradePrefix + "상위 혼 → 혼 정수 → 파편 80",
                    GachaResultPopup.DowngradePrefix + "오의 개안 → 오의 해금 → XP +240",
                    "혼 +1", "★3", "획득", "돌파 3", "개안", "외 3", "확인", "소환"
                };
            }
        }

        // ---------------------------------------------------------------- 레벨업 연출

        /**
         * @brief 배너 바 하나의 레벨업 연출. **큐에 쌓아 순서대로** (69단계 결정 4).
         *
         * 결과 판이 떠 있는 동안에는 기다린다 - 판이 화면을 덮어 바가 안
         * 보이기 때문이다. 판이 닫히면(GachaResultPopup.Closed) 한 레벨씩:
         * 바가 끝까지 찬다 → 흰빛 → "소환 Lv.n" 펀치 → 0부터 다시. 10연에서 두 번
         * 오르거나 결과 판 위에서 재뽑기로 더 오른 것도 같은 큐다.
         */
        private sealed class BarAnimator
        {
            private enum Phase { Idle, Fill, Flash, Punch, Refill }

            private readonly Queue<int> pending = new Queue<int>();
            private Phase phase = Phase.Idle;
            private float t;
            private float from;
            private int level;

            /** 지금까지 끝까지 재생한 레벨업 수. 검사가 읽는다 */
            public int Played { get; private set; }

            public bool Busy { get { return phase != Phase.Idle || pending.Count > 0; } }

            public void Enqueue(int newLevel) { pending.Enqueue(newLevel); }

            public void Clear() { pending.Clear(); phase = Phase.Idle; }

            /** @return 이번 프레임에 바를 그렸는가 (그렸으면 Refresh가 덮어쓰지 않는다) */
            public bool Step(ShopPanel shop, BannerView view, long summonXp, float dt)
            {
                if (phase == Phase.Idle)
                {
                    if (pending.Count == 0) return false;
                    level = pending.Dequeue();
                    phase = Phase.Fill;
                    t = 0f;
                    from = view.barFill != null ? view.barFill.anchorMax.x : 0f;
                    if (from >= 1f) from = 0f;
                }

                t += dt;

                switch (phase)
                {
                    case Phase.Fill:
                    {
                        float k = shop.levelUpFillSeconds > 0f ? Mathf.Clamp01(t / shop.levelUpFillSeconds) : 1f;
                        SetFill(view, Mathf.Lerp(from, 1f, k));
                        SetLabel(shop, view, level - 1, 1f);
                        SetFull(view, level - 1);
                        if (k >= 1f) { phase = Phase.Flash; t = 0f; }
                        break;
                    }

                    case Phase.Flash:
                    {
                        float k = shop.levelUpFlashSeconds > 0f ? Mathf.Clamp01(t / shop.levelUpFlashSeconds) : 1f;
                        SetFill(view, 1f);
                        SetFull(view, level - 1);
                        if (view.barFillImage != null) view.barFillImage.color = Color.Lerp(Color.white, shop.barTint, k);
                        if (k >= 1f) { phase = Phase.Punch; t = 0f; }
                        break;
                    }

                    case Phase.Punch:
                    {
                        float k = shop.levelUpPunchSeconds > 0f ? Mathf.Clamp01(t / shop.levelUpPunchSeconds) : 1f;
                        SetFill(view, 0f);
                        SetLabel(shop, view, level, Mathf.Lerp(shop.levelUpPunch, 1f, k));
                        if (view.barText != null) view.barText.text = BarText(0, SummonLevelCurve.XpToNext(Mathf.Max(1, level)));
                        if (k >= 1f) { phase = Phase.Refill; t = 0f; from = 0f; }
                        break;
                    }

                    case Phase.Refill:
                    {
                        // 큐에 더 있으면 다음 바퀴가 이어서 끝까지 채운다. 마지막이면
                        // 지금 레벨의 실제 비율까지
                        float target = pending.Count > 0 ? 0f : BarRatio(summonXp);
                        if (pending.Count == 0 && view.barText != null)
                            view.barText.text = BarText(SummonLevelCurve.XpIntoLevel(summonXp),
                                                        SummonLevelCurve.XpToNext(SummonLevelCurve.LevelFor(summonXp)));
                        float k = shop.levelUpFillSeconds > 0f ? Mathf.Clamp01(t / shop.levelUpFillSeconds) : 1f;
                        SetFill(view, Mathf.Lerp(0f, target, k));
                        if (k >= 1f)
                        {
                            Played++;
                            phase = Phase.Idle;
                            if (view.levelLabel != null) view.levelLabel.transform.localScale = Vector3.one;
                            return pending.Count > 0;
                        }
                        break;
                    }
                }
                return true;
            }

            private static void SetFill(BannerView view, float ratio)
            {
                if (view.barFill == null) return;
                var max = view.barFill.anchorMax;
                max.x = ratio;
                view.barFill.anchorMax = max;
            }

            /**
             * 펀치 배율을 라벨 칸 폭 안으로 묶는다. 라벨은 왼쪽 위 기준으로 커져서
             * 1.45배면 "소환 Lv.7"(149px)이 216px이 돼 칸(200px)을 넘고 바를 덮었다
             */
            public static float PunchScale(TMPro.TMP_Text label, float desired)
            {
                if (desired <= 1f || label == null) return desired;
                float width = label.preferredWidth;
                float room = ((RectTransform)label.transform).rect.width;
                if (width <= 0f || room <= 0f) return desired;
                return Mathf.Clamp(room / width, 1f, desired);
            }

            /** 채움 · 번쩍 동안 바 숫자는 옛 레벨이 꽉 찬 값이다 - 라벨(옛 레벨)과 같은 순간을 말한다 */
            private static void SetFull(BannerView view, int oldLevel)
            {
                if (view.barText == null) return;
                long next = SummonLevelCurve.XpToNext(Mathf.Max(1, oldLevel));
                view.barText.text = BarText(next, next);
            }

            private static void SetLabel(ShopPanel shop, BannerView view, int shownLevel, float scale)
            {
                if (view.levelLabel == null) return;
                view.levelLabel.text = LevelLabel(Mathf.Max(1, shownLevel));
                view.levelLabel.color = shop.goldColor;
                view.levelLabel.transform.localScale = Vector3.one * PunchScale(view.levelLabel, scale);
                // 커지는 동안은 바 위에 그린다 - 바 뒤로 들어가면 "Lv.7"이 잘린다(69단계 캡처)
                if (scale > 1f) view.levelLabel.transform.SetAsLastSibling();
            }
        }

        private readonly BarAnimator yodoBar = new BarAnimator();
        private readonly BarAnimator skillBar = new BarAnimator();

        /** 요도 배너 바가 끝까지 재생한 레벨업 수 (검사용) */
        public int YodoLevelUpsPlayed { get { return yodoBar.Played; } }

        /** 오의 배너 바가 끝까지 재생한 레벨업 수 (검사용) */
        public int SkillLevelUpsPlayed { get { return skillBar.Played; } }

        public bool LevelUpAnimating { get { return yodoBar.Busy || skillBar.Busy; } }

        /** 결과 판이 지금 어느 배너의 것인가 - 재뽑기가 이 배너로 간다 */
        private bool lastWasSkill;

        // ---------------------------------------------------------------- 생명주기

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
                system.SummonLevelUp += yodoBar.Enqueue;
            }
            if (skillSystem != null)
            {
                skillSystem.Changed += Refresh;
                skillSystem.Pulled += ShowSkillResults;
                skillSystem.SummonLevelUp += skillBar.Enqueue;
            }
            if (gems != null) gems.GemsChanged += OnGemsChanged;
            if (popup != null) popup.RepullRequested += Repull;

            Wire(yodoBanner, YodoSingle, YodoTen, () => OpenRates(false));
            Wire(skillBanner, SkillSingle, SkillTen, () => OpenRates(true));

            Refresh();
        }

        private void OnDestroy()
        {
            if (system != null)
            {
                system.Changed -= Refresh;
                system.Pulled -= ShowResults;
                system.SummonLevelUp -= yodoBar.Enqueue;
            }
            if (skillSystem != null)
            {
                skillSystem.Changed -= Refresh;
                skillSystem.Pulled -= ShowSkillResults;
                skillSystem.SummonLevelUp -= skillBar.Enqueue;
            }
            if (gems != null) gems.GemsChanged -= OnGemsChanged;
            if (popup != null) popup.RepullRequested -= Repull;

            Unwire(yodoBanner);
            Unwire(skillBanner);
        }

        private static void Wire(BannerView view, UnityEngine.Events.UnityAction single,
                                 UnityEngine.Events.UnityAction ten, UnityEngine.Events.UnityAction rates)
        {
            if (view.singleButton != null) view.singleButton.onClick.AddListener(single);
            if (view.tenButton != null) view.tenButton.onClick.AddListener(ten);
            if (view.rateButton != null) view.rateButton.onClick.AddListener(rates);
        }

        private static void Unwire(BannerView view)
        {
            if (view.singleButton != null) view.singleButton.onClick.RemoveAllListeners();
            if (view.tenButton != null) view.tenButton.onClick.RemoveAllListeners();
            if (view.rateButton != null) view.rateButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            // 레벨업 연출은 결과 판이 닫힌 뒤에만 돈다 - 판이 바를 가린다
            bool popupOpen = popup != null && popup.IsOpen;
            if (!popupOpen)
            {
                float dt = Time.unscaledDeltaTime;
                bool drewYodo = system != null && yodoBar.Step(this, yodoBanner, system.SummonXp, dt);
                bool drewSkill = skillSystem != null && skillBar.Step(this, skillBanner, skillSystem.SummonXp, dt);
                if (!drewYodo && !yodoBar.Busy && yodoAnimatedLastFrame) RefreshYodo();
                if (!drewSkill && !skillBar.Busy && skillAnimatedLastFrame) RefreshSkill();
                yodoAnimatedLastFrame = drewYodo || yodoBar.Busy;
                skillAnimatedLastFrame = drewSkill || skillBar.Busy;
            }

            // 무료 뽑기는 날이 바뀌면 열린다 - 시계가 필요한 것은 그것뿐이다
            clock += Time.unscaledDeltaTime;
            if (clock < ClockInterval) return;
            clock = 0f;
            Refresh();
        }

        private bool yodoAnimatedLastFrame;
        private bool skillAnimatedLastFrame;

        private void OnGemsChanged(long value) { Refresh(); }

        // ---------------------------------------------------------------- 동작

        /** 요도 1회 버튼. 오늘의 무료가 남았으면 그것이다 - 버튼에 적힌 말과 같다 */
        private void YodoSingle()
        {
            if (system == null) return;
            if (system.HasFreePull) system.TryFreePull();
            else system.TryPull(1);
        }

        private void YodoTen()
        {
            if (system != null) system.TryPull(GachaCurve.TenPullCount);
        }

        /**
         * @brief 오의 1회 버튼. 온보딩 10연이 남아 있는 동안은 일일 무료를 권하지 않는다.
         *
         * **온보딩이 일일 무료보다 먼저다**(50단계 RefreshSkillFree 순서). 처음
         * 상점을 연 플레이어에게 오늘의 한 번을 먼저 권하면 열 번짜리 선물이
         * 그 한 번에 가려진다.
         */
        private void SkillSingle()
        {
            if (skillSystem == null) return;
            if (!skillSystem.CanClaimIntro && skillSystem.HasFreePull) skillSystem.TryFreePull();
            else skillSystem.TryPull(1);
        }

        private void SkillTen()
        {
            if (skillSystem == null) return;
            if (skillSystem.CanClaimIntro) skillSystem.ClaimIntro();
            else skillSystem.TryPull(SkillGachaCurve.TenPullCount);
        }

        /** 결과 판의 재뽑기(10회). 판을 연 배너로 간다 - 상점 버튼과 같은 경로 */
        private void Repull()
        {
            if (lastWasSkill)
            {
                if (skillSystem != null) skillSystem.TryPull(SkillGachaCurve.TenPullCount);
            }
            else if (system != null) system.TryPull(GachaCurve.TenPullCount);
        }

        private void OpenRates(bool skill)
        {
            if (ratePopup == null) return;
            // 표 = 그 배너의 지금 레벨 표 - 다음 뽑기가 굴리는 표가 그것이다
            int level = skill ? (skillSystem != null ? skillSystem.SummonLevel : 1)
                              : (system != null ? system.SummonLevel : 1);
            ratePopup.Open(skill, level);
        }

        /**
         * @brief 오의 뽑기 결과. **같은 판을 쓴다** - GachaResultPopup 주석 참고.
         */
        private void ShowSkillResults(List<SkillGachaSystem.PullResult> list)
        {
            lastWasSkill = true;
            if (popup != null)
                popup.Show(list, skills, skillSystem != null ? skillSystem.LastBatchLevelUp : 0);
            RefreshRepull();
        }

        private void ShowResults(List<GachaSystem.PullResult> list)
        {
            // 목록은 GachaSystem이 돌려 쓰는 것이다 - 여기서 보관하지 않고
            // 그 자리에서 그린다(GachaSystem.results 주석)
            lastWasSkill = false;
            if (popup != null) popup.Show(list, yodo, system != null ? system.LastBatchLevelUp : 0);
            RefreshRepull();
        }

        // ---------------------------------------------------------------- 표시

        private void Refresh()
        {
            RefreshYodo();
            RefreshSkill();
            RefreshRepull();
        }

        private void RefreshRepull()
        {
            if (popup == null) return;

            if (lastWasSkill)
                popup.SetRepull(skillSystem != null && skillSystem.CanPull(SkillGachaCurve.TenPullCount),
                                skillSystem != null ? skillSystem.CostFor(SkillGachaCurve.TenPullCount)
                                                    : SkillGachaCurve.TenPullCostGems);
            else
                popup.SetRepull(system != null && system.CanPull(GachaCurve.TenPullCount),
                                system != null ? system.CostFor(GachaCurve.TenPullCount)
                                               : GachaCurve.TenPullCostGems);
        }

        private void RefreshYodo()
        {
            var view = yodoBanner;
            bool unlocked = system != null && system.IsUnlocked;
            bool free = unlocked && system.HasFreePull;

            if (free) DrawFree(view.singleButton, view.singleBackground, view.singleTitle, view.singleCost,
                               view.singleNote, view.singleGem, FreeSingleTitle, FreeSingleNote);
            else DrawPaid(view.singleButton, view.singleBackground, view.singleTitle, view.singleCost,
                          view.singleNote, view.singleGem, SingleTitle,
                          system != null ? system.CostFor(1) : GachaCurve.PullCostGems,
                          unlocked && system != null && system.CanPull(1));

            DrawPaid(view.tenButton, view.tenBackground, view.tenTitle, view.tenCost, view.tenNote, view.tenGem,
                     TenTitle, system != null ? system.CostFor(GachaCurve.TenPullCount) : GachaCurve.TenPullCostGems,
                     unlocked && system != null && system.CanPull(GachaCurve.TenPullCount));

            if (yodoBar.Busy) return;

            if (!unlocked) DrawState(view, LockedText(GachaCurve.UnlockStage));
            else DrawBar(view, system.SummonXp);
        }

        /**
         * @brief 오의 배너. **재고가 이 배너의 두 번째 게이트다.**
         *
         * 해금(st14) 위에 보석 구매(st41)와 재고가 얹힌다. 잠긴 것·아직 못 사는
         * 것·다 팔린 것을 **다른 문장으로** 적는다 - 조건이 다르기 때문이다.
         */
        private void RefreshSkill()
        {
            var view = skillBanner;
            bool unlocked = skillSystem != null && skillSystem.IsUnlocked;
            bool stock = skillSystem != null && skillSystem.HasStock;
            bool intro = unlocked && skillSystem.CanClaimIntro;
            bool free = unlocked && !intro && skillSystem.HasFreePull;

            if (free) DrawFree(view.singleButton, view.singleBackground, view.singleTitle, view.singleCost,
                               view.singleNote, view.singleGem, FreeSingleTitle, FreeSingleNote);
            else DrawPaid(view.singleButton, view.singleBackground, view.singleTitle, view.singleCost,
                          view.singleNote, view.singleGem, SingleTitle,
                          skillSystem != null ? skillSystem.CostFor(1) : SkillGachaCurve.PullCostGems,
                          skillSystem != null && skillSystem.CanPull(1));

            if (intro) DrawFree(view.tenButton, view.tenBackground, view.tenTitle, view.tenCost,
                                view.tenNote, view.tenGem, IntroTenTitle, IntroTenNote);
            else DrawPaid(view.tenButton, view.tenBackground, view.tenTitle, view.tenCost, view.tenNote,
                          view.tenGem, TenTitle,
                          skillSystem != null ? skillSystem.CostFor(SkillGachaCurve.TenPullCount)
                                              : SkillGachaCurve.TenPullCostGems,
                          skillSystem != null && skillSystem.CanPull(SkillGachaCurve.TenPullCount));

            if (skillBar.Busy) return;

            if (!unlocked) DrawState(view, LockedText(SkillGachaCurve.UnlockStage));
            else if (!skillSystem.CanBuy) DrawState(view, SkillBuyLaterText);
            else if (!stock) DrawState(view, SoldOutText);
            else DrawBar(view, skillSystem.SummonXp);
        }

        private void DrawBar(BannerView view, long summonXp)
        {
            if (view.barRoot != null) view.barRoot.SetActive(true);
            if (view.stateLabel != null) view.stateLabel.gameObject.SetActive(false);

            int level = SummonLevelCurve.LevelFor(summonXp);
            long next = SummonLevelCurve.XpToNext(level);

            if (view.levelLabel != null)
            {
                view.levelLabel.text = LevelLabel(level);
                view.levelLabel.transform.localScale = Vector3.one;
                // 다음 레벨이 10연 하나 안이면 금빛이다(68단계 규칙 그대로)
                view.levelLabel.color = NearLevelUp(summonXp) ? goldColor : affordableColor;
            }

            if (view.barText != null) view.barText.text = BarText(SummonLevelCurve.XpIntoLevel(summonXp), next);

            if (view.barFill != null)
            {
                var max = view.barFill.anchorMax;
                max.x = BarRatio(summonXp);
                view.barFill.anchorMax = max;
            }
            if (view.barFillImage != null) view.barFillImage.color = barTint;
        }

        private void DrawState(BannerView view, string text)
        {
            if (view.barRoot != null) view.barRoot.SetActive(false);
            if (view.stateLabel == null) return;

            view.stateLabel.gameObject.SetActive(true);
            view.stateLabel.text = text;
            view.stateLabel.color = unaffordableColor;
        }

        /**
         * @brief 다음 레벨이 10연 하나 안에 있는가. 그러면 "소환 Lv.n"이 금빛이다.
         */
        private static bool NearLevelUp(long summonXp)
        {
            int level = SummonLevelCurve.LevelFor(summonXp);
            long left = SummonLevelCurve.XpToNext(level) - SummonLevelCurve.XpIntoLevel(summonXp);
            return left <= GachaCurve.TenPullCount;
        }

        /** 보석으로 사는 버튼. 못 사면 판을 죽인다 - RGB만 곱한다(41단계에서 물린 자리) */
        private void DrawPaid(Button button, Image background, TMP_Text title, TMP_Text cost, TMP_Text note,
                              Image gem, string titleText, int price, bool affordable)
        {
            if (button == null) return;
            button.interactable = affordable;

            if (title != null) { title.text = titleText; title.color = affordable ? affordableColor : unaffordableColor; }
            if (cost != null)
            {
                cost.gameObject.SetActive(true);
                cost.text = price.ToString();
                cost.color = affordable ? affordableColor : unaffordableColor;
            }
            if (gem != null) gem.gameObject.SetActive(true);
            if (note != null) note.gameObject.SetActive(false);

            if (background != null)
            {
                var tint = buttonTint;
                if (!affordable) { tint.r *= 0.55f; tint.g *= 0.55f; tint.b *= 0.55f; tint.a = 1f; }
                background.color = tint;
            }
        }

        /** 무료 버튼. 보석 아이콘과 숫자 대신 짧은 말 한 줄 - 판은 초록이다 */
        private void DrawFree(Button button, Image background, TMP_Text title, TMP_Text cost, TMP_Text note,
                              Image gem, string titleText, string noteText)
        {
            if (button == null) return;
            button.interactable = true;

            if (title != null) { title.text = titleText; title.color = affordableColor; }
            if (cost != null) cost.gameObject.SetActive(false);
            if (gem != null) gem.gameObject.SetActive(false);
            if (note != null)
            {
                note.gameObject.SetActive(true);
                note.text = noteText;
                note.color = goldColor;
            }
            if (background != null) background.color = freeTint;
        }

        // ---------------------------------------------------------------- 테스트 패널

        /** 레벨업 연출을 경험치 변화 없이 한 번 재생한다. 연출을 눈으로 보는 경로 */
        public void DebugPlayLevelUp(bool skill)
        {
            if (skill) { if (skillSystem != null) skillBar.Enqueue(skillSystem.SummonLevel); }
            else if (system != null) yodoBar.Enqueue(system.SummonLevel);
        }
    }
}
