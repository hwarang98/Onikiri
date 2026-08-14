using System.Collections;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 플레이 화면의 가이드 퀘스트 카드 - **지금 무엇을 하면 되는가** 한 장.
     *
     * ## 전투 화면 오른쪽의 소형 반투명 패널이다 (슬레이어식)
     *
     * 하단 풀폭 바 세대는 전투 화면의 밑변을 통째로 차지했고, 강한 테두리와
     * 긴 금색 진행 바가 배경 위에서 혼자 소리를 질렀다. 슬레이어 키우기의
     * 우측 소형 카드처럼 줄인다 - 반투명 남색 바탕에 목표명·진행도·보상
     * 셋만 적고, 진행 바는 뺀다(진행도는 "4/100" 텍스트가 말한다).
     *
     * 카드는 **차례**를 말한다. 정해진 순서(GuideQuestCatalog)에서 지금
     * 칸 하나를 꺼내 보여준다.
     *
     * ## 자리
     *
     * 전투 화면 오른쪽, 퀘스트 책 아이콘(#16) 바로 아래 정렬:
     *
     *   - 아이콘과 세로로 이어져 "퀘스트에 관한 것"이 한 기둥으로 읽힌다.
     *     카드 탭 = 퀘스트 화면이라 신호와 입구도 같은 기둥이다
     *   - 화면 폭의 ~37%라 사무라이(왼쪽)와 보스 도전 버튼(위 가운데)을
     *     안 덮는다. 요괴는 지면을 걷지만 카드는 나무 높이에 떠 있다
     *   - 전투 화면 안이므로 어느 하단 탭을 열어도 계속 보인다
     *
     * ## 눌린다
     *
     * 카드 전체는 퀘스트 화면을 여는 문이고(HudScreenButton - 퀘스트 아이콘과
     * 같은 부품), 완료 가능 상태에서는 오른쪽 끝 받기 버튼이 그 자리에서
     * 수령한다(TryClaim - 퀘스트 화면의 받기와 같은 경로).
     *
     * ## 갱신은 이벤트로 한다. **그런데 이벤트마다 다시 그리지는 않는다**
     *
     * 진행 줄은 0.5초 폴링이다. 그럴 만한 이유가 있었다 - 카운터는 요괴를
     * 벨 때마다 오르므로 초당 여러 번이고, 그때마다 TMP 메시를 다시 만들면
     * 한 줄짜리 표시가 전투 프레임을 갉아먹는다.
     *
     * 여기서는 그 둘을 나눈다:
     *
     *   상태·칸이 바뀌었다   그 자리에서 다시 그린다. "완료 가능"이 0.5초
     *                        늦게 뜨면 그 순간의 안내로서는 이미 늦다
     *   숫자만 바뀌었다      0.25초 안의 것을 모아 한 번만 그린다
     *
     * 가르는 값(칸 번호와 상태)은 문자열을 만들지 않고 구하므로
     * (GuideQuestLine.Resolve), 이벤트가 초당 수십 번 와도 비용은 정수 비교
     * 몇 개다. 모아 그리는 것도 Update가 아니라 코루틴이라, 조용할 때는
     * **매 프레임 도는 코드가 한 줄도 없다.**
     */
    public sealed class GuideQuestCard : MonoBehaviour
    {
        [Tooltip("태그 줄. \"가이드\" / \"가이드 완료\" / \"보상 수령 완료\"")]
        [SerializeField] private TMP_Text actionLabel;

        [Tooltip("보상 숫자. 아랫줄의 보석 아이콘 오른쪽")]
        [SerializeField] private TMP_Text rewardLabel;

        [Tooltip("목표명. 가운데 줄")]
        [SerializeField] private TMP_Text detailLabel;

        [Tooltip("진행도(4/100). 아랫줄 오른쪽, 화살표 앞")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("보상 줄(아이콘 + 숫자 + 진행도)의 루트. 진행 중에만 보인다")]
        [SerializeField] private GameObject rewardRoot;

        /**
         * @brief 받기 버튼. **완료 가능 상태에서만 선다.**
         *
         * 수령 경로는 퀘스트 화면의 받기 버튼과 같다(QuestSystem.TryClaim) -
         * 카드가 새 지급 경로를 만드는 것이 아니라 그 버튼 하나를 전투
         * 화면으로 꺼내온 것이다. 그래서 보상량도 순서도 퀘스트 화면에서
         * 받는 것과 같다.
         */
        [Tooltip("받기 버튼의 루트. 완료 가능 상태에서만 켜진다")]
        [SerializeField] private GameObject claimRoot;
        [SerializeField] private Button claimButton;

        [Tooltip("진행 중 우측 끝의 화살표. 카드가 눌린다는 것을 형태로 말한다")]
        [SerializeField] private TMP_Text arrowLabel;

        /**
         * @brief 카드 전체의 알파. 보여줄 칸이 없으면 0이 된다.
         *
         * **오브젝트를 끄지 않는다.** 끄면 이 컴포넌트가 함께 멈춰 구독이
         * 끊기고, 다시 켜질 조건을 스스로 확인할 수 없게 된다 - 옛 퀘스트
         * 진행 줄이 실기 첫 캡처에서 통째로 사라졌던 함정 그대로다
         * (root.SetActive(false)가 자기 Update까지 껐다).
         */
        [SerializeField] private CanvasGroup group;

        [Header("색")]
        [SerializeField] private Color normalColor = new Color32(0xF6, 0xE5, 0xBF, 0xFF);
        [SerializeField] private Color dimColor = new Color32(0x8A, 0x7F, 0x9B, 0xFF);

        [Tooltip("완료 가능. 금색은 이 게임에서 완성의 색이다(UiSkin.Gold)")]
        [SerializeField] private Color readyColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        [Header("갱신")]
        [Tooltip("숫자만 바뀐 갱신을 모으는 시간(초). 상태 변화는 이것을 안 기다린다")]
        [SerializeField] private float coalesceSeconds = 0.25f;

        [Tooltip("보상을 받은 칸을 '수령 완료'로 붙잡아 두는 시간(초)")]
        [SerializeField] private float claimedHoldSeconds = 1.4f;

        private QuestSystem quests;
        private StageProgress stage;
        private CharacterLevel character;
        private UpgradeSystem upgrades;
        private SkillSystem skills;

        /** 지금 화면에 그려져 있는 것 */
        private GuideQuestView shown;

        private bool coalescing;

        /** "수령 완료"를 붙잡고 있는 동안. 그동안 들어오는 이벤트는 무시한다 */
        private bool holding;

        private void OnEnable()
        {
            shown = default(GuideQuestView);
            shown.Step = -1;

            // 빌더가 적어둔 예시 문구("5스테이지 도달 0 / 5")를 한 프레임도
            // 안 보여준다. 그 글자는 자리를 잡으려고 씬에 박아둔 것이지
            // 진행 상황이 아니고, 진짜 값이 오기 전의 한 프레임 동안 그것이
            // 화면에 있으면 그건 안내가 아니라 거짓말이다
            Show(false);

            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);

            StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            if (claimButton != null) claimButton.onClick.RemoveListener(OnClaimClicked);
            Unbind();
            StopAllCoroutines();
            coalescing = false;
            holding = false;
        }

        /**
         * @brief 시스템들이 씬에 설 때까지 기다렸다가 구독한다.
         *
         * 이 카드는 전투 화면에 켜진 채로 저장되므로 OnEnable이 첫 프레임에
         * 돈다. 그때 QuestSystem.Awake가 아직 안 돌았을 수 있고, 그러면
         * 이벤트 기반인 이 컴포넌트는 **영영 아무것도 못 듣는다** - 폴링이면
         * 다음 프레임에 저절로 낫는 종류의 실수가 여기서는 낫지 않는다.
         */
        private IEnumerator BindWhenReady()
        {
            while (QuestSystem.Instance == null) yield return null;

            quests = QuestSystem.Instance;
            quests.Changed += OnChanged;

            /**
             * @brief 퀘스트 말고도 넷을 더 듣는다.
             *
             * 가이드가 가리키는 것은 대부분 업적이고, 업적의 지표는 카운터가
             * 아니라 **지금 상태**다(QuestSystem.CurrentStateOf) - 스테이지·
             * 레벨·강화 총합·오의 칸 수. 그 넷은 QuestSystem을 거치지 않고
             * 바뀌므로, 퀘스트 이벤트만 들으면 "스테이지를 올렸는데 카드가
             * 다음 요괴를 벨 때까지 안 바뀐다"가 된다.
             *
             * 넷 다 자주 발생하지만(경험치는 처치마다 온다) 받아서 하는 일이
             * 정수 비교 몇 개라 비용이 없다 - 다시 그리는 것은 그다음 판단이다.
             */
            stage = StageProgress.Instance;
            if (stage != null) stage.Changed += OnChanged;

            character = CharacterLevel.Instance;
            if (character != null) character.Changed += OnChanged;

            upgrades = UpgradeSystem.Instance;
            if (upgrades != null) upgrades.Changed += OnChanged;

            skills = SkillSystem.Instance;
            if (skills != null) skills.Changed += OnChanged;

            Refresh();
        }

        private void Unbind()
        {
            if (quests != null) quests.Changed -= OnChanged;
            if (stage != null) stage.Changed -= OnChanged;
            if (character != null) character.Changed -= OnChanged;
            if (upgrades != null) upgrades.Changed -= OnChanged;
            if (skills != null) skills.Changed -= OnChanged;

            quests = null;
            stage = null;
            character = null;
            upgrades = null;
            skills = null;
        }

        /**
         * @brief 무엇이든 바뀌었다. **다시 그릴지는 여기서 가른다.**
         *
         * Resolve는 문자열을 만들지 않는다 - 표를 훑어 정수 두 개를 고를
         * 뿐이다. 그래서 이 함수는 초당 수십 번 불려도 되고, 비싼 쪽(Paint)은
         * 정말 달라졌을 때만 돈다.
         */
        private void OnChanged()
        {
            if (quests == null || holding) return;

            var next = GuideQuestLine.Resolve(quests);

            if (next.Step != shown.Step || next.State != shown.State)
            {
                Apply(next);
                return;
            }

            // 숫자만 움직였다. 사람이 읽는 속도로 충분하다
            if (coalescing) return;
            coalescing = true;
            StartCoroutine(CoalesceNumbers());
        }

        private IEnumerator CoalesceNumbers()
        {
            yield return new WaitForSecondsRealtime(coalesceSeconds);
            coalescing = false;
            Refresh();
        }

        private void Refresh()
        {
            if (quests == null || holding) return;
            Apply(GuideQuestLine.Resolve(quests));
        }

        /**
         * @brief 새 상태를 화면에 옮긴다. 수령 직후만 한 박자 쉰다.
         *
         * 완료 가능하던 칸이 진행선에서 빠졌다는 것은 **그 보상을 받았다**는
         * 뜻이다(가이드는 안 받은 칸만 고른다). 그 순간 카드가 곧바로 다음
         * 목표로 갈아타면 화면에서 일어난 일은 "글자가 바뀌었다"뿐이고,
         * 방금 받은 보상은 어디에도 안 남는다. 잠깐 "수령 완료"를 세워
         * 한 칸이 끝났다는 것을 말하고 넘어간다.
         */
        private void Apply(GuideQuestView next)
        {
            bool justClaimed = shown.State == GuideQuestState.Claimable
                               && shown.Step >= 0
                               && next.Step != shown.Step;

            if (justClaimed && claimedHoldSeconds > 0f)
            {
                StartCoroutine(HoldClaimed(shown.Step));
                return;
            }

            shown = next;
            Paint(next);
        }

        private IEnumerator HoldClaimed(int claimedStep)
        {
            var done = GuideQuestLine.Describe(quests, claimedStep);

            // 표에서 그 칸이 사라졌다면(퀘스트 id가 빠진 세이브 경로) 붙잡을
            // 것이 없다. 곧바로 다음 상태로 간다 - 빈 카드를 1.4초 세우는
            // 것보다 낫다
            //
            // Apply를 거치지 않고 직접 그린다. 거치면 shown이 아직 "완료
            // 가능"인 채라 같은 판정이 다시 서고, 그 자리에서 무한히 돈다
            if (done.Step < 0)
            {
                var fallback = GuideQuestLine.Resolve(quests);
                shown = fallback;
                Paint(fallback);
                yield break;
            }

            holding = true;

            done.State = GuideQuestState.Claimed;
            shown = done;
            Paint(done);

            // 실시간이다. 타격 정지(HitStop)가 timeScale을 0으로 눕히는
            // 순간이 실제로 있고, 그때 이 표시만 화면에 얼어붙으면 안 된다
            yield return new WaitForSecondsRealtime(claimedHoldSeconds);

            holding = false;

            // 붙잡고 있는 동안 또 무엇이 바뀌었을 수 있다. 지금 상태로 다시 푼다
            Refresh();
        }

        private void Paint(GuideQuestView view)
        {
            if (view.State == GuideQuestState.None)
            {
                Show(false);
                return;
            }

            Show(true);

            bool ready = view.State == GuideQuestState.Claimable;
            bool claimed = view.State == GuideQuestState.Claimed;

            /**
             * @brief 상태마다 카드가 말하는 것 (슬레이어식 소형 패널).
             *
             *   진행 중    태그 "가이드" / 목표명 / 보석+수 · 4/100 · 화살표
             *   완료 가능  태그 "가이드 완료"(금색) / 목표명 + 받기 버튼
             *   수령 완료  태그 "보상 수령 완료" / 목표명 (짧은 전이 상태)
             *
             * 목표명·진행도·보상 셋만 적는다. 행동 문구(GuideStep.Action)는
             * 이 판형에서 뺐다 - 세 줄 패널에 문장이 서면 안내가 아니라
             * 문단이 되고, 무엇을 할지는 목표명이 이미 말한다.
             *
             * 진행도가 목표명 옆이 아니라 아랫줄인 이유는 폭이다. 가장 긴
             * 목표("강화 총합 1200")에 "1200/1200"을 이으면 40% 폭 카드에서
             * 반드시 잘리고, 잘린 진행도는 없는 진행도보다 나쁘다. 아랫줄은
             * 보석 수 옆이 통째로 비어 있다.
             */
            if (actionLabel != null)
            {
                actionLabel.text = ready ? "가이드 완료"
                                 : claimed ? "보상 수령 완료"
                                 : "가이드";
                actionLabel.color = ready ? readyColor : dimColor;
            }

            if (detailLabel != null)
            {
                detailLabel.text = view.Title;
                detailLabel.color = claimed ? dimColor : normalColor;
            }

            // 보상 줄(아이콘 + 숫자 + 진행도)은 진행 중에만. 완료 상태에서
            // 강조되는 것은 받기 버튼 하나여야 한다
            bool showReward = !ready && !claimed;
            if (rewardRoot != null && rewardRoot.activeSelf != showReward)
                rewardRoot.SetActive(showReward);

            if (rewardLabel != null)
            {
                rewardLabel.text = view.Gems.ToString();
                rewardLabel.color = dimColor;
            }

            if (progressLabel != null)
            {
                // 빗금 양옆에 공백을 두지 않는다("4/100"). 퀘스트 화면의 표기
                // 그대로다
                progressLabel.text = GuideQuestLine.FormatCount(view.Progress)
                                     + "/" + GuideQuestLine.FormatCount(view.Target);
                progressLabel.color = dimColor;
            }

            if (claimRoot != null && claimRoot.activeSelf != ready)
                claimRoot.SetActive(ready);

            // 화살표(카드 탭 = 퀘스트 화면)도 진행 중에만
            if (arrowLabel != null)
                arrowLabel.alpha = ready || claimed ? 0f : 1f;
        }

        private void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;

            // 카드가 눌리는 것(전체 탭 = 퀘스트 화면, 받기 버튼 = 즉시 수령)이
            // 됐으므로 레이캐스트도 가시성을 따라간다. 안 보이는 카드가 지면
            // 띠의 터치를 삼키면 안 된다
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        /**
         * @brief 받기. 퀘스트 화면의 받기 버튼과 같은 경로다.
         *
         * TryClaim이 Raise를 부르므로 카드는 이벤트로 다음 상태(수령 완료
         * 잠깐 -> 다음 칸)로 넘어간다 - 여기서 화면을 직접 만지지 않는다.
         */
        private void OnClaimClicked()
        {
            if (quests == null || holding) return;
            if (shown.State != GuideQuestState.Claimable || shown.Step < 0) return;

            var steps = GuideQuestCatalog.Steps;
            if (shown.Step >= steps.Length) return;

            var step = steps[shown.Step];
            if (step.Index < 0) return;

            quests.TryClaim(step.Kind, step.Index);
        }

        // ---------------------------------------------------------------- 테스트 패널

        /** 지금 화면에 서 있는 상태. 테스트 패널이 읽는다 */
        public GuideQuestState CurrentState { get { return shown.State; } }

        /** 지금 가리키는 칸 (0부터, 없으면 -1) */
        public int CurrentStep { get { return shown.Step; } }
    }
}
