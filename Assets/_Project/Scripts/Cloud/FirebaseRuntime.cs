using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief Firebase 초기화·Firestore 핸들의 **공용 입구**. 최소 범위다.
     *
     * `CloudScores`가 이미 의존성 확인과 익명 로그인을 들고 있고, 그 코드는
     * 실기에서 증명된 경로다(53~55단계). 크로스 저장이 자기 초기화를 새로 만들면
     * **한 앱에 Firebase 부팅 경로가 둘**이 되고, 그 순간 어느 쪽이 먼저 도는지에
     * 따라 uid가 갈리거나 이중 초기화가 난다.
     *
     * 그래서 여기서 하는 일은 셋뿐이다: 그 경로를 **부르고**, Firestore 핸들을
     * 하나로 들고 있고, 네트워크 예외를 이 스텝이 읽는 말로 옮긴다.
     * `CloudScores`와 `AccountLink`의 동작은 한 줄도 바뀌지 않는다 -
     * 리더보드의 오프라인 큐 정책도 그대로다(그쪽은 큐를 쓰고, 세이브 정본은
     * 절대 안 쓴다).
     */
    public static class FirebaseRuntime
    {
        private const string Tag = "[CloudSave]";

        /** 세이브 왕복의 제한 시간. 지하철에서 Firestore Task는 영영 안 끝난다 */
        public const int RequestTimeoutMs = 10000;

        private static FirebaseFirestore db;

        public static bool IsReady { get; private set; }

        /** 지금 로그인된 사용자. CloudScores가 정하는 값이다 */
        public static string Uid
        {
            get { return CloudScores.Uid; }
        }

        public static FirebaseFirestore Db
        {
            get { return db; }
        }

        /**
         * @brief 초기화 + 로그인까지 끝났는가. **CloudScores의 캐시를 그대로 쓴다.**
         *
         * 초기화는 1회 캐시되고(성공만), 익명 로그인은 기존 세션을 재사용한다 -
         * 둘 다 CloudScores의 기존 계약이고 여기서 바꾸지 않는다.
         */
        public static async Task<bool> EnsureReadyAsync()
        {
            try
            {
                if (!await CloudScores.InitializeAsync()) return false;
                if (string.IsNullOrEmpty(CloudScores.Uid)
                    && !await CloudScores.SignInAnonymouslyAsync()) return false;

                if (db == null) db = FirebaseFirestore.GetInstance(FirebaseApp.DefaultInstance);

                IsReady = db != null && !string.IsNullOrEmpty(CloudScores.Uid);
                return IsReady;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(Tag + " Firebase 준비 실패: " + exception.Message);
                return false;
            }
        }

        /**
         * @brief Task가 제한 시간 안에 끝났는가. `CloudScores.Completes`와 같은 이유다.
         *
         * 무한정 기다리는 await는 방치형에서 그 자체가 버그다. 오프라인이면
         * Firestore의 Task가 완료되지 않는 경로가 있고, 그것을 그대로 기다리면
         * 저장 상태 표시가 영원히 "동기화 중"에 머문다.
         */
        public static async Task<bool> Completes(Task task, int timeoutMs, string label)
        {
            var finished = await Task.WhenAny(task, Task.Delay(timeoutMs));

            if (finished != task)
            {
                Observe(task);
                Debug.LogWarning(Tag + " " + label + " 응답 없음 (" + (timeoutMs / 1000) + "초).");
                return false;
            }

            return !task.IsFaulted && !task.IsCanceled;
        }

        /** 버리는 Task에 예외 관측자를 단다. 안 달면 GC가 뒤늦게 콘솔을 덮는다 */
        public static void Observe(Task task)
        {
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                    Debug.LogWarning(Tag + " (늦게 도착한 실패) " + Flatten(t.Exception));
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /**
         * @brief 이 실패가 **네트워크**인가.
         *
         * 오프라인은 실패가 아니라 정상 경로다(57단계 판정표의 LocalOnly). 그것을
         * 다른 실패와 섞으면 지하철에서 "저장 실패" 경고가 뜨고, 사람은 진행이
         * 사라진 줄 안다 - 실제로는 로컬에 다 있다.
         */
        public static bool LooksOffline(Exception exception)
        {
            if (exception == null) return false;

            var aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    if (LooksOffline(inner)) return true;
                return false;
            }

            var firestore = exception as FirestoreException;
            if (firestore == null) return false;

            return firestore.ErrorCode == FirestoreError.Unavailable
                   || firestore.ErrorCode == FirestoreError.DeadlineExceeded;
        }

        /**
         * @brief 이 실패가 **규칙의 거부**인가.
         *
         * 여기 오는 것은 클라와 규칙이 갈렸다는 뜻이다 - 클라가 이미 세션·사슬을
         * 검사하고 보내므로, 그것을 지나고도 거부됐다면 두 검사 중 하나가 틀렸다.
         * 조용히 재시도하면 안 되는 종류다(같은 쓰기가 계속 거부된다).
         */
        public static bool LooksDenied(Exception exception)
        {
            if (exception == null) return false;

            var aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    if (LooksDenied(inner)) return true;
                return false;
            }

            var firestore = exception as FirestoreException;
            if (firestore == null) return false;

            return firestore.ErrorCode == FirestoreError.PermissionDenied;
        }

        public static string Flatten(AggregateException aggregate)
        {
            if (aggregate == null) return "알 수 없는 오류";

            var flattened = aggregate.Flatten();
            if (flattened.InnerExceptions.Count == 0) return flattened.Message;

            return flattened.InnerExceptions[0].GetType().Name + ": "
                   + flattened.InnerExceptions[0].Message;
        }

        /** Firestore Timestamp -> UTC ticks. 판정에는 안 쓰고 화면·진단에만 쓴다 */
        public static long TicksOf(object timestampValue)
        {
            if (timestampValue is Timestamp)
            {
                DateTime utc = ((Timestamp)timestampValue).ToDateTime();
                return utc.Ticks;
            }

            return 0L;
        }
    }
}
