using System;
using Onikiri.Cloud;
using Onikiri.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Onikiri.UI
{
    /**
     * @brief 도달층 랭킹표. 54단계의 얼굴.
     *
     * 이 화면이 하는 일은 셋이다 - 상위 N위를 보여주고, 내가 몇 등인지 알려주고,
     * 이름을 정하게 한다. 그 셋이 한 화면에 있는 이유는 전부 "남들 사이에서의
     * 나"에 대한 것이라서다.
     *
     * ## 열 때만 조회한다
     *
     * 폴링이 없다. 랭킹은 초 단위로 볼 값이 아니고(도달층은 분·시간 단위로
     * 오른다), 폴링은 Firestore 읽기 과금을 사람 수 x 시간으로 곱한다.
     * 새로고침 버튼이 그 자리를 대신한다 - 사람이 원할 때만 돈다.
     *
     * ## 상태가 넷이다
     *
     *   로딩    조회 중. 목록은 지난 결과를 그대로 두고 상태줄만 바뀐다
     *   목록    정상
     *   빈      서버에 아무도 없다 (실패와 **다른 문구**여야 한다)
     *   실패    네트워크·규칙·인덱스. 재시도 버튼이 함께 뜬다
     *
     * 빈 상태와 실패 상태를 같은 문구로 묶으면 "아무도 없음"이 고장으로
     * 읽히고, 반대로 고장이 "1등이 될 기회"로 읽힌다.
     */
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            public GameObject root;
            public TMP_Text rankLabel;
            public TMP_Text nameLabel;
            public TMP_Text stageLabel;
            public Image background;
        }

        [SerializeField] private Row[] rows = new Row[0];

        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text myRankLabel;
        [SerializeField] private Button refreshButton;

        [Header("계정 (55단계)")]
        [SerializeField] private TMP_Text accountLabel;
        [SerializeField] private Button accountButton;
        [SerializeField] private TMP_Text accountButtonLabel;

        [Header("이름")]
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Button nameEditButton;
        [SerializeField] private GameObject nameEditGroup;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button nameConfirmButton;

        [SerializeField] private StageProgress progress;

        /** 내 줄을 다른 줄과 가르는 색. 강조색 셋 규칙 안에서 고른다 */
        [SerializeField] private Color myRowColor = new Color32(0x3A, 0x2E, 0x3E, 0xFF);
        [SerializeField] private Color rowColor = new Color32(0x2A, 0x25, 0x3C, 0xFF);

        /** 조회가 도는 중인가. 새로고침 연타를 막는다 */
        private bool loading;

        /**
         * @brief 목록에서 읽은 **내 문서의** 도달층. 없으면 0.
         *
         * ⚠️ **55단계가 깨뜨린 전제가 여기 있었다.** 그 전까지는 "로컬 도달층
         * >= 서버 도달층"이 늘 참이었다 - 기기 하나에 계정 하나였고 도달층은
         * 내려가지 않으니, 내 순위를 로컬 값으로 재는 것이 옳았다.
         *
         * 복구는 그 전제를 깬다. 지운 기기의 로컬은 1층인데 돌아온 문서는
         * 3층이라, 로컬로 재면 **같은 화면이 자기모순에 빠진다** - 목록은 내
         * 줄을 2위(3층)로 강조하는데 아래 한 줄은 "내 순위 3위 (1층)"이라고
         * 적는다(실기에서 그렇게 떴다).
         */
        private int myRecordStage;

        private void Awake()
        {
            if (progress == null) progress = StageProgress.Instance;

            if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
            if (nameEditButton != null) nameEditButton.onClick.AddListener(BeginNameEdit);
            if (nameConfirmButton != null) nameConfirmButton.onClick.AddListener(ConfirmName);

            // 키보드의 "완료"로도 확정된다. **실기에서 이것이 없으면 첫
            // 사용자가 막힌다** - 소프트 키보드가 확인 버튼을 덮어버리고,
            // 키보드를 닫으려고 뒤로가기를 누르면 TMP가 입력을 취소한다
            // (ESC = 편집 취소). 즉 "치고 → 닫고 → 확인"의 가운데 단계에서
            // 방금 친 글자가 사라진다
            if (nameInput != null) nameInput.onSubmit.AddListener(OnNameSubmitted);

            if (accountButton != null) accountButton.onClick.AddListener(LinkGoogle);
        }

        private void OnEnable()
        {
            RefreshName();

            AccountLink.Changed += RefreshAccount;
            RefreshAccount();

            // 이름을 아직 안 정했으면 입력을 열어둔 채로 시작한다. 랭킹표에
            // 자기 이름이 "이름없는 무사"로 서 있는 것을 본 다음에 고치는 것이
            // 순서상 자연스럽지만, 그러려면 그 줄이 목록 안에 보여야 하고
            // 대부분의 사람은 100위 밖이다
            if (nameEditGroup != null) nameEditGroup.SetActive(!PlayerProfile.HasChosenName);

            Refresh();
        }

        private void OnDisable()
        {
            AccountLink.Changed -= RefreshAccount;
        }

        // ---------------------------------------------------------------- 계정 (55단계)

        /**
         * @brief 계정 줄을 다시 그린다. **정체성이 이 화면에 있는 이유**가 여기 있다.
         *
         * 계정 연동을 설정 화면이 아니라 랭킹 화면에 둔 것은 자리 부족 때문이
         * 아니다. 연동이 실제로 하는 일이 **"이 순위표에 선 내가 누구인가"**를
         * 정하는 것이고, 복구의 결과(내 순위가 1층에서 원래 기록으로 돌아옴)가
         * 눈에 보이는 곳이 바로 이 화면이다. 설정에 두면 성공 문구 한 줄만
         * 보고 나와야 한다.
         */
        private void RefreshAccount()
        {
            if (this == null) return;

            var provider = AuthProviders.Google;

            if (accountLabel != null)
            {
                switch (AccountLink.State)
                {
                    case AccountState.Linked:
                        string who = AccountLink.LinkedLabel;
                        accountLabel.text = string.IsNullOrEmpty(who)
                            ? provider.DisplayName + " 연동됨"
                            : provider.DisplayName + " · " + who;
                        break;

                    case AccountState.Guest:
                        // "게스트"만 적고 끝내지 않는다. 연동을 안 하면 무엇을
                        // 잃는지가 이 줄에서 읽혀야 사람이 버튼을 누를 이유를 안다
                        // 폭을 재고 줄인 문장이다. 캡션 33pt · 라벨 폭 660px에
                        // "기록이"까지 넣으면 627px로 아슬아슬하고, 긴 기기
                        // (좁은 가로)에서 먼저 잘린다
                        accountLabel.text = "게스트 · 앱을 지우면 사라집니다";
                        break;

                    default:
                        // 확인 중과 게스트는 다른 문장이다. 부팅 직후에 "게스트"를
                        // 띄우면 이미 연동한 사람이 연동이 풀린 줄 안다
                        accountLabel.text = "계정 확인 중...";
                        break;
                }
            }

            if (accountButton != null)
            {
                // 이 기기에서 못 쓰는 로그인은 **버튼 자체를 감춘다**. 눌러도
                // 아무 일이 없는 회색 버튼은 20단계 함정 버튼과 같은 거짓말이다
                bool usable = provider.IsAvailable && AccountLink.State == AccountState.Guest;
                accountButton.gameObject.SetActive(usable);
                accountButton.interactable = usable && !AccountLink.IsBusy;
            }

            if (accountButtonLabel != null)
                // "구글로 로그인"이 아니라 "구글 로그인"이다. 캡션 33pt에서
                // 한글 한 자가 33px이라 조사 하나가 버튼 폭을 넘긴다
                // (54단계에서 무료 줄 문구가 잘린 것과 같은 자리)
                accountButtonLabel.text = provider.DisplayName + " 로그인";

            // 연동 중의 진행 문구도 같은 상태줄에 적는다. 상태가 두 줄로
            // 갈리면 어느 것이 지금 일어나는 일인지 읽는 사람이 정해야 한다
            if (!string.IsNullOrEmpty(AccountLink.Status)) SetStatus(AccountLink.Status);
        }

        /**
         * @brief "구글로 로그인". 끝나면 **목록까지 다시 부른다.**
         *
         * 복구가 성공하면 uid가 바뀌어 있다 - 내 줄(IsMe)도 내 순위도 전부
         * 다른 값이 된다. 그것을 다시 안 부르면 화면은 방금 버린 계정의
         * 순위를 계속 보여주고, 이 스텝의 결과가 눈에 안 보인다.
         */
        private void LinkGoogle()
        {
            var _ = LinkGoogleAsync();
        }

        private async System.Threading.Tasks.Task LinkGoogleAsync()
        {
            if (AccountLink.IsBusy) return;

            if (accountButton != null) accountButton.interactable = false;

            bool changed = await AccountLink.LinkAsync(AuthProviders.Google);

            if (this == null || !isActiveAndEnabled) return;

            RefreshAccount();

            if (!changed) return;

            // 복구가 이름까지 되살렸을 수 있다(AccountLink). PlayerProfile.Restore는
            // **신호를 안 내므로**(복원은 변경이 아니다) 여기서 직접 다시 그린다
            RefreshName();
            if (nameEditGroup != null && PlayerProfile.HasChosenName)
                nameEditGroup.SetActive(false);

            Refresh();
        }

        // ---------------------------------------------------------------- 조회

        public void Refresh()
        {
            if (loading) return;
            var _ = RefreshAsync();
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            loading = true;
            SetStatus("불러오는 중...");
            if (refreshButton != null) refreshButton.interactable = false;

            try
            {
                var entries = await CloudScores.FetchTopAsync(rows.Length);

                // 파괴된 뒤에 응답이 오는 경로가 실재한다(패널을 닫고 씬을
                // 나가는 동안 15초 타임아웃이 돈다). 유니티 객체는 파괴돼도
                // 참조가 null이 아니라서 == null로 물어봐야 한다
                if (this == null || !isActiveAndEnabled) return;

                if (entries == null)
                {
                    SetStatus("불러올 수 없습니다 - 연결을 확인하고 다시 시도하세요");
                    return;
                }

                Fill(entries);
                SetStatus(entries.Length == 0
                    ? "아직 아무도 오르지 않았습니다"
                    : string.Empty);

                await RefreshMyRankAsync();
            }
            finally
            {
                loading = false;
                if (this != null && refreshButton != null) refreshButton.interactable = true;
            }
        }

        private async System.Threading.Tasks.Task RefreshMyRankAsync()
        {
            if (myRankLabel == null) return;

            // 셋 중 가장 높은 값이 내 기록이다 - 로컬, 목록에서 본 내 줄,
            // 마지막으로 읽은 내 문서. 복구 직후에는 로컬이 가장 낮다.
            //
            // 병합과 **같은 함수**를 쓴다. 묻는 것이 같은 질문이기 때문이다 -
            // "여러 출처가 말하는 내 도달층 중 무엇이 내 기록인가". 여기만
            // 따로 max를 적으면 언젠가 두 곳이 갈린다
            int mine = AccountLinkPolicy.MergedStage(
                progress != null ? progress.MaxStageReached : 0,
                myRecordStage,
                CloudScores.LastReadStage);

            if (mine <= 0) return;

            myRankLabel.text = "내 순위  집계 중...";

            int rank = await CloudScores.FetchRankAsync(mine);
            if (this == null || myRankLabel == null) return;

            // 실패를 "-위"로 적지 않는다. 숫자 자리에 뭔가 적혀 있으면 그것이
            // 순위로 읽히고, 0위·-1위는 존재하지 않는 순위다
            //
            // "최고"를 붙이는 것은 61단계의 인정이다 - 복구에서 랭킹은 max
            // 병합이고 세이브는 한 벌 선택이라, 이 값이 세이브의 현재 진행보다
            // 높을 수 있다. 그 상태는 버그가 아니라 "랭킹 = 이 계정의 최고
            // 기록 / 세이브 = 지금 이어가는 진행"이고, 그 구분이 여기 적힌다
            myRankLabel.text = rank > 0
                ? string.Format("내 순위  {0:N0}위   (최고 {1}층)", rank, mine)
                : string.Format("내 순위  -   (최고 {0}층)", mine);
        }

        private void Fill(CloudScores.LeaderboardEntry[] entries)
        {
            // 매 조회마다 다시 센다. 안 지우면 계정을 갈아탄 뒤에도 옛 계정의
            // 기록이 "내 기록"으로 남는다
            myRecordStage = 0;
            foreach (var entry in entries)
                if (entry.IsMe && entry.MaxStage > myRecordStage) myRecordStage = entry.MaxStage;

            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.root == null) continue;

                bool used = i < entries.Length;
                row.root.SetActive(used);
                if (!used) continue;

                var entry = entries[i];

                if (row.rankLabel != null) row.rankLabel.text = (i + 1).ToString();
                if (row.nameLabel != null) row.nameLabel.text = entry.Name;
                if (row.stageLabel != null) row.stageLabel.text = entry.MaxStage.ToString("N0") + "층";
                if (row.background != null)
                    row.background.color = entry.IsMe ? myRowColor : rowColor;
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel == null) return;
            statusLabel.text = text;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // ---------------------------------------------------------------- 이름

        private void RefreshName()
        {
            if (nameLabel != null) nameLabel.text = PlayerProfile.Name;
        }

        private void BeginNameEdit()
        {
            if (nameEditGroup == null) return;

            bool opening = !nameEditGroup.activeSelf;
            nameEditGroup.SetActive(opening);

            if (opening && nameInput != null)
            {
                // 기본 이름은 채우지 않는다. "이름없는 무사"가 입력칸에 들어
                // 있으면 지우는 것이 첫 동작이 된다
                nameInput.text = PlayerProfile.HasChosenName ? PlayerProfile.Name : string.Empty;
                nameInput.characterLimit = PlayerProfile.MaxLength;
                nameInput.ActivateInputField();
            }
        }

        /**
         * @brief 입력을 확정한다. 제출은 **제출기가** 디바운스를 거쳐 보낸다.
         *
         * 여기서 곧바로 write를 날리지 않는 이유는, 그러면 이름을 세 번 고치는
         * 사람이 쓰기를 세 번 만들기 때문이다. `PlayerProfile.Changed`를 듣는
         * `LeaderboardSubmitter`가 20초 창으로 묶어 한 번만 보낸다 - 도달층이
         * 안 올라도 이름이 다르면 나간다.
         */
        /** 키보드의 완료 키. 인자로 오는 문자열은 무시한다 - 출처는 늘 입력칸 하나다 */
        private void OnNameSubmitted(string _)
        {
            ConfirmName();
        }

        private void ConfirmName()
        {
            if (nameInput == null) return;

            if (!PlayerProfile.SetName(nameInput.text))
            {
                SetStatus("이름을 한 글자 이상 입력하세요");
                return;
            }

            RefreshName();
            if (nameEditGroup != null) nameEditGroup.SetActive(false);
            SetStatus("이름이 바뀌었습니다 - 잠시 뒤 랭킹에 반영됩니다");
        }
    }
}
