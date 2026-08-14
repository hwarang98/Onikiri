using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 도달층을 Firestore에 쓰고 다시 읽어오는 **스파이크**. 리더보드의 토대.
     *
     * 여기서 증명하려는 것은 딱 하나다 - **안드로이드 실기에서 익명 로그인이 되고,
     * 도달층 한 숫자가 서버에 올라갔다가 그대로 돌아온다.** 그것이 되면 리더보드는
     * 이 왕복 위에 얹는 일이 되고, 안 되면(Play 서비스 버전·SHA·규칙·네트워크)
     * 리더보드 UI를 아무리 잘 지어도 아무 데도 닿지 않는다.
     *
     * ## 게임에 대한 계약: **영향 0**
     *
     * 이 클래스는 게임 상태를 **읽기만 한다**(StageProgress.MaxStageReached).
     * 세이브도 밸런스도 전투 루프도 건드리지 않고, 실패해도 게임은 그대로 돈다.
     * 전 경로가 try/catch이고 예외는 여기서 죽는다 - 방치형은 사용자가 화면을
     * 켜둔 채 몇 시간을 두는 게임이라, 네트워크 하나로 앱이 내려가면 그 시간이
     * 통째로 날아간다. "되면 로그, 안 되면 조용히 넘어감"이 규칙이다.
     *
     * ## 왜 도달층인가
     *
     * MaxStageReached는 52단계에서 이미 리더보드 점수의 성질을 갖췄다 - 결정론
     * (같은 세이브 -> 같은 값), 단조(내려가지 않음), 유일 경로(보스 처치).
     * 서버가 나중에 재검증할 수 있는 값이라는 뜻이다. StageProgress 주석 참고.
     *
     * ## 스파이크가 **아직** 아닌 것 (다음 스텝)
     *
     *   - 자동 제출이 없다. 수동 버튼 하나로만 돈다 (CloudSpikeOverlay)
     *   - "높을 때만 갱신"이 없다. 낮은 값도 그냥 덮어쓴다
     *   - 서버 재검증이 없다. 지금은 클라이언트가 보낸 숫자를 그대로 믿는다
     *   - 이름이 상수 "Rang"이다. **출시 전 교체** - 닉네임 입력이 서야 한다
     *   - 보안 규칙이 테스트 모드(전면 개방, 2026-09-30 만료)다. **출시 전 잠금**
     */
    public static class CloudScores
    {
        /** logcat에서 이 한 단어로 거른다: `adb logcat -s Unity | grep CloudScores` */
        private const string Tag = "[CloudScores]";

        /** 문서 경로는 scores/{uid}. 컬렉션 이름은 리더보드 쿼리도 같이 쓸 이름이다 */
        public const string Collection = "scores";

        /** 문서의 필드 이름. 쿼리·정렬·보안 규칙이 같은 문자열을 봐야 한다 */
        public const string StageField = "maxStage";
        public const string NameField = "name";
        public const string UpdatedField = "updatedAt";
        public const string UidField = "uid";

        // 응답 없이 버튼이 영원히 도는 것을 막는다. 오프라인이면 write는 로컬 큐에
        // 남고(Firestore 기본 동작) 앱이 다시 온라인이 될 때 알아서 올라간다 -
        // 그러니 여기서 기다리기를 포기하는 것은 실패가 아니라 보고의 문제다
        private const int InitTimeoutMs = 20000;
        private const int AuthTimeoutMs = 20000;
        private const int IoTimeoutMs = 15000;

        private static Task<bool> initTask;
        private static FirebaseAuth auth;
        private static FirebaseFirestore db;

        /** 의존성 확인까지 끝나 Firebase를 쓸 수 있는 상태인가 */
        public static bool IsReady { get; private set; }

        /** 익명 로그인으로 받은 uid. 로그인 전에는 null */
        public static string Uid { get; private set; }

        /**
         * @brief 이 클래스가 만든 Firebase 세션. **계정 연동(55단계)이 같은 것을 써야 한다.**
         *
         * 노출하는 이유는 AccountLink가 FirebaseAuth.GetAuth를 따로 부르면
         * 안 되기 때문이 아니다(같은 객체가 온다) - 초기화 순서를 두 곳이
         * 각자 관리하게 되는 것이 문제다. 여기 한 곳만이 CheckAndFixDependencies를
         * 통과했는지 알고, 그것을 통과하지 않은 상태에서 auth를 만지면 앱이 내려간다.
         */
        public static FirebaseAuth Auth { get { return auth; } }

        /**
         * @brief uid가 바뀌었다 (계정 복구). 제출기가 기억을 지우는 신호.
         *
         * 익명 계정만 있던 시절에는 uid가 앱 수명 동안 안 바뀌었다. 복구가
         * 생기면서 **한 실행 안에서 문서가 갈리는 순간**이 처음 생겼고, 그때
         * "같은 값을 두 번 안 보낸다"는 제출기의 기억이 새 문서에 대해서는
         * 거짓이 된다(옛 문서에 보냈던 값이다).
         */
        public static event Action UidChanged;

        /** 마지막으로 서버에서 읽어온 도달층. 아직 읽은 적이 없으면 -1 */
        public static int LastReadStage { get; private set; } = -1;

        /**
         * @brief 마지막으로 읽은 내 문서의 이름. 아직 읽은 적이 없으면 빈 문자열.
         *
         * 계정 복구(55단계)가 쓴다 - 데이터를 지운 기기에는 이름이 없는데,
         * 돌아온 문서에는 그 사람이 정한 이름이 적혀 있다. **이름도 기록의
         * 일부다**: 랭킹표에 "랑무사"로 서 있는데 자기 화면에서는 "이름없는
         * 무사"라고 읽히면, 복구가 절반만 된 것으로 보인다.
         */
        public static string LastReadName { get; private set; } = string.Empty;

        /** 화면(테스트 패널·기기 오버레이)에 그대로 띄우는 한 줄 상태 */
        public static string Status { get; private set; } = "대기";

        /** 스파이크가 도는 중인가. 버튼 연타를 막는 데 쓴다 */
        public static bool IsBusy { get; private set; }

        // ---------------------------------------------------------------- 진입점

        /**
         * @brief 초기화 -> 로그인 -> write -> read 를 한 번에. 버튼 하나가 부르는 것.
         *
         * async void가 아니라 Task를 버리는 모양인 이유는 호출자(에디터 GUI,
         * 기기 OnGUI)가 동기 컨텍스트이기 때문이다. 예외는 RunSpikeAsync 안에서
         * 전부 잡히므로 버려지는 Task에 남는 것이 없다.
         */
        public static void RunSpike()
        {
            if (IsBusy)
            {
                Debug.Log(Tag + " 이미 실행 중입니다.");
                return;
            }
            var _ = RunSpikeAsync();
        }

        public static async Task RunSpikeAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // 게임 상태는 await 전에 읽는다. await 뒤는 몇 초 뒤의 세계라
                // 그 사이에 보스가 잡혀 도달층이 올라갔을 수 있고, 그러면
                // "무엇을 썼는가"와 "무엇을 읽었는가"가 어긋나 왕복 증명이 흐려진다
                int reach = CurrentReach();
                Debug.Log(Tag + " 스파이크 시작. 도달층 = " + reach);

                if (!await InitializeAsync()) return;
                if (!await SignInAnonymouslyAsync()) return;
                if (!await SubmitReachAsync(reach)) return;

                int readBack = await FetchReachAsync();
                if (readBack < 0) return;

                if (readBack == reach)
                {
                    Set("왕복 성공: " + reach + "층 (uid " + Short(Uid) + ")");
                    Debug.Log(Tag + " ★ 왕복 성공 - write " + reach + " -> read " + readBack);
                }
                else
                {
                    // 실패가 아니라 관측이다. 같은 uid로 다른 기기가 더 높은 값을
                    // 썼거나, 서버가 아직 앞선 write를 반영 중일 수 있다
                    Set("왕복 값 불일치: write " + reach + " / read " + readBack);
                    Debug.LogWarning(Tag + " 왕복은 됐지만 값이 다릅니다. write "
                                     + reach + " -> read " + readBack);
                }
            }
            catch (Exception e)
            {
                Fail("스파이크 예외: " + e.Message, e);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /** 지금 시점의 도달층. 씬에 StageProgress가 없으면(테스트·부팅 직후) 1 */
        public static int CurrentReach()
        {
            var progress = StageProgress.Instance;
            if (progress == null)
            {
                Debug.LogWarning(Tag + " StageProgress가 없습니다. 도달층 1로 진행합니다.");
                return 1;
            }
            return progress.MaxStageReached;
        }

        // ---------------------------------------------------------------- [1] 초기화

        /**
         * @brief 의존성 확인. **결과가 Available일 때만** 그 뒤가 존재한다.
         *
         * 안드로이드에서 이것이 실패하는 가장 흔한 이유는 기기의 Google Play
         * 서비스가 낡은 것이다. 그때 Firebase는 예외를 던지는 게 아니라 상태를
         * 돌려주므로, 여기서 걸러내지 않으면 그 다음 줄의
         * FirebaseFirestore.DefaultInstance가 초기화 안 된 네이티브를 만나
         * 앱이 통째로 내려간다.
         *
         * 성공한 결과는 캐시한다(초기화는 1회). 실패는 캐시하지 않는다 - 비행기
         * 모드를 끄고 버튼을 다시 누르는 것이 곧 재시도여야 한다.
         */
        public static Task<bool> InitializeAsync()
        {
            if (initTask == null) initTask = InitializeCoreAsync();
            return initTask;
        }

        private static async Task<bool> InitializeCoreAsync()
        {
            try
            {
                Set("Firebase 의존성 확인 중...");

                var check = FirebaseApp.CheckAndFixDependenciesAsync();
                if (!await Completes(check, InitTimeoutMs, "의존성 확인"))
                {
                    initTask = null;
                    return false;
                }

                DependencyStatus status = check.Result;
                if (status != DependencyStatus.Available)
                {
                    // 게임은 계속 돈다. 여기서 멈추는 것은 Firebase뿐이다
                    Fail("Firebase 사용 불가: " + status
                         + " (Google Play 서비스 버전을 확인하세요)", null);
                    initTask = null;
                    return false;
                }

                var app = FirebaseApp.DefaultInstance;
                auth = FirebaseAuth.GetAuth(app);
                db = FirebaseFirestore.GetInstance(app);

                IsReady = true;
                Set("Firebase 준비됨 (" + app.Options.ProjectId + ")");
                Debug.Log(Tag + " 초기화 완료. projectId = " + app.Options.ProjectId);
                return true;
            }
            catch (Exception e)
            {
                Fail("초기화 예외: " + e.Message, e);
                initTask = null;
                return false;
            }
        }

        // ---------------------------------------------------------------- [2] 익명 로그인

        /**
         * @brief 익명 로그인. 이미 로그인돼 있으면 그 세션을 그대로 쓴다.
         *
         * 재로그인을 하지 않는 이유는 익명 계정이 **매번 새 uid를 만들기**
         * 때문이다. 버튼을 두 번 누를 때마다 uid가 갈리면 scores 컬렉션에
         * 주인 없는 문서가 쌓이고, 도달층은 기기 하나에 한 줄이라는 성질이 깨진다.
         */
        public static async Task<bool> SignInAnonymouslyAsync()
        {
            try
            {
                if (!IsReady && !await InitializeAsync()) return false;

                var current = auth.CurrentUser;
                if (current != null)
                {
                    Uid = current.UserId;
                    Set("로그인 재사용: " + Short(Uid));
                    Debug.Log(Tag + " 기존 로그인 재사용. uid = " + Uid);
                    return true;
                }

                Set("익명 로그인 중...");

                var signIn = auth.SignInAnonymouslyAsync();
                if (!await Completes(signIn, AuthTimeoutMs, "익명 로그인")) return false;

                Uid = signIn.Result.User.UserId;
                Set("로그인됨: " + Short(Uid));
                Debug.Log(Tag + " 익명 로그인 성공. uid = " + Uid);
                return true;
            }
            catch (Exception e)
            {
                Fail("로그인 예외: " + e.Message, e);
                return false;
            }
        }

        /**
         * @brief 연동·복구로 정해진 사용자를 이 클래스의 현재 사용자로 삼는다.
         *
         * 55단계가 부르는 유일한 쓰기 진입점이다. 하는 일 셋:
         *
         *   uid 교체        이후의 모든 문서 경로가 새 uid를 본다
         *   서버 값 잊기    LastReadStage는 **옛 문서**의 값이라, 안 지우면
         *                   화면이 남의 기록을 내 것으로 적는다
         *   신호            제출기가 "같은 값 두 번 금지" 기억을 지운다
         *
         * 연동(Link) 성공에서도 부른다. 그때는 uid가 그대로라 아무것도 안
         * 바뀌지만, 부르는 쪽이 두 갈래를 다르게 다루지 않아도 되는 편이 낫다.
         */
        public static void AdoptUser(FirebaseUser user)
        {
            if (user == null) return;

            bool changed = Uid != user.UserId;

            Uid = user.UserId;
            if (changed)
            {
                LastReadStage = -1;
                LastReadName = string.Empty;
            }

            Debug.Log(Tag + " 사용자 채택. uid = " + Uid
                      + " / 익명 = " + user.IsAnonymous
                      + " / 교체됨 = " + changed);

            if (!changed) return;

            var handler = UidChanged;
            if (handler != null) handler();
        }

        /**
         * @brief **다른** uid의 도달층을 읽는다. 병합에서만 쓴다.
         *
         * FetchReachAsync와 갈라 둔 이유는 이쪽이 LastReadStage를 건드리면
         * 안 되기 때문이다 - 그 값은 "내 문서의 서버 값"이라는 뜻이고, 곧
         * 버려질 문서의 값을 거기 적으면 화면이 그것을 내 기록으로 적는다.
         *
         * 문서가 없으면 0이다(-1이 아니다). 없는 것은 실패가 아니라 **0층**
         * 이고, 병합은 그 값을 그대로 최댓값 후보로 쓸 수 있어야 한다.
         */
        public static async Task<int> FetchStageOfAsync(string uid)
        {
            try
            {
                if (string.IsNullOrEmpty(uid)) return 0;
                if (!IsReady && !await InitializeAsync()) return 0;

                var read = db.Collection(Collection).Document(uid).GetSnapshotAsync(Source.Server);
                if (!await Completes(read, IoTimeoutMs, "병합 전 조회")) return 0;

                var snapshot = read.Result;
                if (!snapshot.Exists) return 0;

                long stage = snapshot.TryGetValue(StageField, out long value) ? value : 0L;
                Debug.Log(Tag + " 병합 전 조회. " + Collection + "/" + uid + " = " + stage);
                return (int)stage;
            }
            catch (Exception e)
            {
                Fail("병합 전 조회 예외: " + e.Message, e);
                return 0;
            }
        }

        // ---------------------------------------------------------------- [3] write

        /**
         * @brief 도달층을 scores/{uid}에 병합 기록한다.
         *
         * merge인 이유는 이 문서가 앞으로 이 스파이크만의 것이 아니기 때문이다 -
         * 리더보드가 서면 서버가 검증 표식을 같은 문서에 붙이고, 통째로 덮어쓰는
         * 쓰기는 그것을 매번 지운다.
         *
         * 시각은 클라이언트 시계가 아니라 **서버 타임스탬프**다. 기기 시계는
         * 사용자가 돌릴 수 있고, 방치형은 시계를 돌려 이득을 보는 시도가 실제로
         * 오는 장르다. 순위 동점 처리의 기준도 결국 이 값이 된다.
         *
         * ⚠️ 스파이크라 조건 없이 쓴다. "지금 값이 서버 값보다 높을 때만"은
         * 다음 스텝(리더보드)에서 서버 규칙과 함께 온다.
         */
        public static async Task<bool> SubmitReachAsync(int maxStage)
        {
            try
            {
                if (string.IsNullOrEmpty(Uid) && !await SignInAnonymouslyAsync()) return false;

                var doc = db.Collection(Collection).Document(Uid);
                var fields = new Dictionary<string, object>
                {
                    { UidField, Uid },
                    { NameField, PlayerProfile.Name },
                    { StageField, maxStage },
                    { UpdatedField, FieldValue.ServerTimestamp },
                };

                Set("도달층 " + maxStage + " 기록 중...");

                var write = doc.SetAsync(fields, SetOptions.MergeAll);
                if (!await Completes(write, IoTimeoutMs, "write"))
                {
                    // 실패로 단정하지 않는다. Firestore는 오프라인 write를 로컬에
                    // 큐잉하고 다음 접속에서 올린다 - Task가 안 끝난 것은 "서버가
                    // 아직 확인해주지 않았다"이지 "버려졌다"가 아니다
                    Set("write 응답 없음 - 오프라인 큐에 남았습니다 (복귀 시 자동 전송)");
                    return false;
                }

                Set("write 완료: " + maxStage + "층");
                Debug.Log(Tag + " write 성공. " + Collection + "/" + Uid
                          + " maxStage = " + maxStage);
                return true;
            }
            catch (Exception e)
            {
                Fail("write 예외: " + e.Message, e);
                return false;
            }
        }

        // ---------------------------------------------------------------- [4] read

        /**
         * @brief 같은 문서를 다시 읽는다. **왕복의 증명은 이 줄이다.**
         *
         * 캐시에서 읽으면 증명이 되지 않는다 - 방금 내가 쓴 값이 로컬 캐시에
         * 그대로 있어서, 서버에 한 글자도 안 갔어도 똑같이 돌아온다. 그래서
         * Source.Server로 명시해서 읽고, 스냅샷이 캐시에서 왔는지도 함께 찍는다.
         *
         * @return 서버의 도달층. 실패하면 -1
         */
        public static async Task<int> FetchReachAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Uid) && !await SignInAnonymouslyAsync()) return -1;

                var doc = db.Collection(Collection).Document(Uid);

                Set("서버에서 다시 읽는 중...");

                var read = doc.GetSnapshotAsync(Source.Server);
                if (!await Completes(read, IoTimeoutMs, "read"))
                {
                    Set("read 응답 없음 - 오프라인이거나 서버가 느립니다");
                    return -1;
                }

                DocumentSnapshot snapshot = read.Result;
                if (!snapshot.Exists)
                {
                    Fail("문서가 없습니다: " + Collection + "/" + Uid, null);
                    return -1;
                }

                long stage = snapshot.TryGetValue(StageField, out long value) ? value : -1L;
                string when = snapshot.TryGetValue(UpdatedField, out Timestamp stamp)
                    ? stamp.ToDateTime().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    // 서버 타임스탬프는 서버가 확정하기 전까지 비어 있을 수 있다.
                    // 값이 아니라 상태이므로 실패로 다루지 않는다
                    : "(아직 미확정)";

                LastReadStage = (int)stage;
                LastReadName = snapshot.TryGetValue(NameField, out string storedName)
                    ? storedName : string.Empty;

                Debug.Log(Tag + " read 성공. maxStage = " + stage
                          + " / updatedAt = " + when
                          + " / 캐시에서 옴 = " + snapshot.Metadata.IsFromCache);
                Set("read 완료: " + stage + "층  (" + when + ")");
                return LastReadStage;
            }
            catch (Exception e)
            {
                Fail("read 예외: " + e.Message, e);
                return -1;
            }
        }

        // ---------------------------------------------------------------- 리더보드 (54단계)

        /**
         * @brief 랭킹표 한 줄. 서버에서 온 그대로이고 게임 상태가 아니다.
         *
         * class가 아니라 struct인 이유는 이 값이 목록 하나를 그리는 동안만
         * 살기 때문이다 - 백 줄이 매 조회마다 힙에 남을 이유가 없다.
         */
        public struct LeaderboardEntry
        {
            public string Uid;
            public string Name;
            public int MaxStage;

            /** 서버가 찍은 시각. 아직 확정 전이면 null */
            public DateTime? UpdatedAt;

            /** 이 줄이 나인가. 랭킹표가 내 줄을 강조하는 데 쓴다 */
            public bool IsMe;
        }

        /**
         * @brief 지금 도달층이 서버 값보다 높을 때만 쓴다. **후퇴를 서버에서 막는 첫 겹.**
         *
         * 읽기 한 번이 앞에 붙는 것이 비용이지만, 이 읽기가 없으면 재설치한
         * 기기(도달층 1)가 접속하는 순간 자기 기록을 지운다. 규칙 4-B도 같은
         * 검사를 하므로 실제 방어는 두 겹이다 - 여기는 "안 보낸다", 규칙은
         * "보내도 안 받는다".
         *
         * ⚠️ 값의 **진위**는 여전히 검증되지 않는다. 개조 클라가 높은 값을
         * 주장하면 그대로 올라간다. 그 구멍은 Cloud Functions 재검증(다음
         * 스텝, Blaze 필요)이 막는다.
         *
         * @return 실제로 서버에 쓴 경우에만 true (안 써도 되는 경우는 false)
         */
        public static async Task<bool> SubmitIfHigherAsync(int localStage)
        {
            try
            {
                if (!LeaderboardPolicy.IsSubmittable(localStage))
                {
                    Debug.LogWarning(Tag + " 제출 범위 밖이라 보내지 않습니다: " + localStage);
                    return false;
                }

                if (string.IsNullOrEmpty(Uid) && !await SignInAnonymouslyAsync()) return false;

                var doc = db.Collection(Collection).Document(Uid);

                var read = doc.GetSnapshotAsync(Source.Server);
                if (!await Completes(read, IoTimeoutMs, "제출 전 조회"))
                {
                    // 서버 값을 모르면 쓰지 않는다. 여기서 낙관적으로 쓰면
                    // 오프라인 큐에 낮은 값이 실려 나중에 기록을 덮을 수 있다
                    Set("서버 값을 못 읽어 제출을 미룹니다");
                    return false;
                }

                var snapshot = read.Result;
                bool hasServer = snapshot.Exists
                                 && snapshot.TryGetValue(StageField, out long _);
                int serverStage = hasServer ? (int)snapshot.GetValue<long>(StageField) : 0;

                // 이름도 함께 본다. 도달층이 안 올라도 이름이 다르면 보낸다 -
                // 그러지 않으면 개명이 랭킹표에 영영 안 닿는다(LeaderboardPolicy)
                string serverName = hasServer
                                    && snapshot.TryGetValue(NameField, out string stored)
                    ? stored : string.Empty;
                bool nameIsStale = serverName != PlayerProfile.Name;

                if (!LeaderboardPolicy.ShouldSubmit(localStage, serverStage, hasServer, nameIsStale))
                {
                    Set("제출 생략: 로컬 " + localStage + " <= 서버 " + serverStage);
                    Debug.Log(Tag + " 제출 생략(후퇴 방지). 로컬 " + localStage
                              + " / 서버 " + serverStage);
                    return false;
                }

                if (nameIsStale && localStage == serverStage)
                    Debug.Log(Tag + " 이름이 바뀌어 같은 도달층으로 다시 보냅니다: "
                              + serverName + " -> " + PlayerProfile.Name);

                LastReadStage = serverStage;
                return await SubmitReachAsync(localStage);
            }
            catch (Exception e)
            {
                Fail("조건부 제출 예외: " + e.Message, e);
                return false;
            }
        }

        /**
         * @brief 상위 N위. 도달층 내림차순, 동점은 **먼저 도달한 사람이 위**.
         *
         * 동점 처리를 updatedAt 오름차순으로 두는 것이 이 쿼리의 유일한 판단이다.
         * 도달층은 정수라 상위권에서 동점이 흔하고, tie-break가 없으면 순서가
         * Firestore의 내부 순서(문서 ID)로 정해져 새로고침마다 뒤바뀐다 -
         * "내가 왜 내려갔지"가 설명 불가능해진다.
         *
         * ⚠️ 두 필드 정렬이라 **복합 인덱스가 필요하다**(firestore.indexes.json).
         * 인덱스가 없으면 FAILED_PRECONDITION이 오고, 그때는 목록 대신
         * 실패 상태를 보여준다 - 조용히 단일 정렬로 물러나면 순서가 흔들리는
         * 것이 정상처럼 보인다.
         *
         * @return 실패하면 null (빈 배열은 "아무도 없다"라는 다른 뜻이다)
         */
        public static async Task<LeaderboardEntry[]> FetchTopAsync(int limit)
        {
            try
            {
                if (!IsReady && !await InitializeAsync()) return null;
                if (limit <= 0) return new LeaderboardEntry[0];

                var query = db.Collection(Collection)
                    .OrderByDescending(StageField)
                    .OrderBy(UpdatedField)
                    .Limit(limit);

                var task = query.GetSnapshotAsync(Source.Server);
                if (!await Completes(task, IoTimeoutMs, "랭킹 조회")) return null;

                var documents = task.Result.Documents;
                var list = new List<LeaderboardEntry>(limit);

                foreach (var document in documents)
                {
                    if (!document.TryGetValue(StageField, out long stage)) continue;

                    list.Add(new LeaderboardEntry
                    {
                        Uid = document.Id,
                        // 이름이 없는 문서(스파이크 시절·규칙 통과한 빈 값)도
                        // 목록에서 지우지 않는다. 순위는 도달층이 정하고,
                        // 이름은 표시 문제다
                        Name = document.TryGetValue(NameField, out string name) && !string.IsNullOrEmpty(name)
                            ? name : PlayerProfile.DefaultName,
                        MaxStage = (int)stage,
                        UpdatedAt = document.TryGetValue(UpdatedField, out Timestamp stamp)
                            ? stamp.ToDateTime() : (DateTime?)null,
                        IsMe = document.Id == Uid,
                    });
                }

                Set("랭킹 " + list.Count + "줄");
                Debug.Log(Tag + " 랭킹 조회 성공. " + list.Count + "줄");
                return list.ToArray();
            }
            catch (Exception e)
            {
                Fail("랭킹 조회 예외: " + e.Message, e);
                return null;
            }
        }

        /**
         * @brief 내 순위 = 나보다 높은 도달층의 문서 수 + 1.
         *
         * 집계 쿼리(count)를 쓴다. 문서를 실제로 받아오지 않으므로 만 명이
         * 있어도 읽기 과금이 문서 수에 비례하지 않는다 - 목록으로 세면 상위
         * 만 줄을 통째로 내려받아야 하고 그건 요금과 대역폭 양쪽에서 틀린 답이다.
         *
         * 동점자는 나와 같은 순위가 된다("공동 n위"). 동점을 updatedAt으로
         * 갈라 정확한 등수를 내려면 부등호가 둘 필요한데(층이 높거나, 같으면서
         * 먼저 도달) 그건 쿼리 두 번이다. 상위 N 목록 안에서는 tie-break가
         * 이미 보이므로, 목록 밖의 사람에게 "공동 순위"는 충분히 정확하다.
         *
         * @return 순위(1부터). 실패하면 -1
         */
        public static async Task<int> FetchRankAsync(int myStage)
        {
            try
            {
                if (!IsReady && !await InitializeAsync()) return -1;

                var higher = db.Collection(Collection)
                    .WhereGreaterThan(StageField, myStage)
                    .Count;

                var task = higher.GetSnapshotAsync(AggregateSource.Server);
                if (!await Completes(task, IoTimeoutMs, "내 순위 집계")) return -1;

                long? count = task.Result.Count;
                if (count == null) return -1;

                int rank = (int)count.Value + 1;
                Debug.Log(Tag + " 내 순위 " + rank + "위 (도달층 " + myStage + ")");
                return rank;
            }
            catch (Exception e)
            {
                Fail("순위 집계 예외: " + e.Message, e);
                return -1;
            }
        }

        // ---------------------------------------------------------------- [5] 오프라인

        /**
         * @brief Firestore의 네트워크를 껐다 켠다. **오프라인 안전을 재현하는 스위치.**
         *
         * 방치형에서 오프라인은 예외가 아니라 일상이다(지하철·비행기·데이터 절약).
         * 그런데 그 상태를 실기에서 만들려면 기기의 wifi를 꺼야 하고, 무선 디버깅으로
         * 붙어 있으면 그 순간 관측 수단까지 함께 끊긴다 - 확인하려는 대상 때문에
         * 확인이 불가능해지는 구조다.
         *
         * DisableNetworkAsync는 Firestore가 실제 오프라인에서 하는 일을 그대로
         * 한다: write는 로컬 큐에 쌓이고, Source.Server 읽기는 Unavailable로
         * 떨어진다. 다시 켜면 큐가 서버로 올라간다. 그래서 이 스위치로 본 것은
         * 흉내가 아니라 **같은 코드 경로**다.
         *
         * 디버그 경로다 - 게임 코드에서 부르는 곳은 없다.
         */
        public static async Task<bool> SetNetworkEnabledAsync(bool enabled)
        {
            try
            {
                if (!IsReady && !await InitializeAsync()) return false;

                var task = enabled ? db.EnableNetworkAsync() : db.DisableNetworkAsync();
                if (!await Completes(task, IoTimeoutMs, enabled ? "네트워크 켜기" : "네트워크 끄기"))
                    return false;

                Set(enabled ? "네트워크 켜짐" : "네트워크 꺼짐 (오프라인 흉내)");
                Debug.Log(Tag + " Firestore 네트워크 " + (enabled ? "ON" : "OFF"));
                return true;
            }
            catch (Exception e)
            {
                Fail("네트워크 전환 예외: " + e.Message, e);
                return false;
            }
        }

        // ---------------------------------------------------------------- 공통

        /**
         * @brief Task가 제한 시간 안에 끝났고 성공했는가.
         *
         * 방치형에서 무한정 기다리는 await는 그 자체가 버그다. 지하철에서
         * 데이터가 끊기면 Firestore의 Task는 **영영 완료되지 않는다**(로컬
         * 큐에 남는다). 그것을 그대로 기다리면 버튼이 영원히 도는 것처럼 보이고,
         * 그 뒤에 뭐가 붙어 있으면 그것도 함께 멈춘다.
         *
         * 버리는 Task에는 예외 관측자를 달아둔다. 안 달면 나중에 GC가
         * UnobservedTaskException을 올려 콘솔이 시체 로그로 덮인다.
         */
        private static async Task<bool> Completes(Task task, int timeoutMs, string label)
        {
            var finished = await Task.WhenAny(task, Task.Delay(timeoutMs));

            if (finished != task)
            {
                Observe(task);
                Debug.LogWarning(Tag + " " + label + " 응답 없음 ("
                                 + (timeoutMs / 1000) + "초). 오프라인일 수 있습니다.");
                return false;
            }

            if (task.IsCanceled)
            {
                Fail(label + " 취소됨", null);
                return false;
            }

            if (task.IsFaulted)
            {
                // Firebase는 예외를 AggregateException으로 싸서 준다. 그대로 찍으면
                // logcat에서 진짜 원인(권한 없음·네트워크)이 껍질에 묻힌다
                var inner = Flatten(task.Exception);
                Fail(label + " 실패: " + inner, task.Exception);
                return false;
            }

            return true;
        }

        private static void Observe(Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                    Debug.LogWarning(Tag + " (늦게 도착한 실패) " + Flatten(t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static string Flatten(AggregateException aggregate)
        {
            if (aggregate == null) return "알 수 없는 오류";

            var flat = aggregate.Flatten();
            return flat.InnerExceptions.Count > 0
                ? flat.InnerExceptions[0].GetType().Name + ": " + flat.InnerExceptions[0].Message
                : flat.Message;
        }

        private static void Set(string status)
        {
            Status = status;
        }

        private static void Fail(string status, Exception e)
        {
            Status = status;
            // LogError가 아니라 LogWarning이다. Firebase가 안 되는 것은 이 게임에서
            // 오류가 아니라 상태다 - 곁다리 기능 하나가 쉬는 것뿐이고, 에러로 찍으면
            // Crashlytics·콘솔에서 진짜 문제와 같은 무게로 섞인다
            Debug.LogWarning(Tag + " " + status + (e != null ? "\n" + e : string.Empty));
        }

        /** uid 전체는 28자라 화면 한 줄을 다 먹는다. 로그에는 전체가 남는다 */
        private static string Short(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return "(없음)";
            return uid.Length <= 10 ? uid : uid.Substring(0, 10) + "...";
        }
    }
}
