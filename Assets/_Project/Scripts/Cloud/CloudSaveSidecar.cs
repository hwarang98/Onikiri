using System;
using System.IO;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief sidecar 파일 읽기/쓰기. **SaveSystem과 같은 규칙, 다른 파일.**
     *
     * 세이브와 한 파일에 담지 않는 이유는 CloudSaveLocalState 머리 주석에 있다.
     * 쓰기가 임시 파일 후 바꿔치기인 것은 SaveSystem과 같은 이유다 - 모바일은
     * 쓰는 도중에 프로세스가 사라질 수 있고, 반쪽짜리 sidecar는 "서버에 대해
     * 거짓을 아는 기기"를 만든다. 모르는 것보다 나쁘다.
     *
     * 어떤 실패도 밖으로 던지지 않는다. 이 파일이 없거나 깨져도 게임은 그대로
     * 돌아야 한다 - 클라우드는 곁다리이고, 곁다리가 본체를 멈추면 그 순간
     * 방치형 게임의 오프라인 계약이 깨진다.
     */
    public static class CloudSaveSidecar
    {
        public const string FileName = "onikiri_cloud_state.json";

        public static string Path
        {
            // 세이브와 **같은 뿌리**에서 출발한다(61단계 S5-0). 테스트가 루트를
            // 갈아 끼우면 sidecar도 따라온다 - 세이브만 격리되고 sidecar가
            // 실사용 것을 보면, 검사가 "revision 41까지 동기화한 기기" 행세를 한다
            get { return System.IO.Path.Combine(Progression.SaveSystem.Root, FileName); }
        }

        public static bool Exists
        {
            get { return File.Exists(Path); }
        }

        /**
         * @brief sidecar를 읽는다. 없거나 못 읽거나 **미래 형식**이면 null.
         *
         * 미래 형식에서 null인 것이 중요하다. 다운그레이드된 앱이 모르는
         * 형식의 revision을 그대로 믿으면, 사슬 중간에서 출발하는 쓰기가
         * 나간다. 모른다고 말하는 쪽이 안전하다 - 그 결과는 충돌 화면이고,
         * 그때 사람이 고른다.
         */
        public static CloudSaveLocalState Load()
        {
            if (!File.Exists(Path)) return null;

            try
            {
                string json = File.ReadAllText(Path);
                var state = JsonUtility.FromJson<CloudSaveLocalState>(json);

                if (state == null)
                {
                    Debug.LogWarning("[Onikiri] Cloud sidecar was empty; treating it as absent.");
                    return null;
                }

                if (state.formatVersion > CloudSaveLocalState.CurrentFormatVersion)
                {
                    Debug.LogWarning("[Onikiri] Cloud sidecar format v" + state.formatVersion
                                     + " is newer than " + CloudSaveLocalState.CurrentFormatVersion
                                     + "; treating it as absent.");
                    return null;
                }

                // **반쯤 맞는 sidecar가 없는 것보다 나쁘다.** JsonUtility는 잘린
                // JSON에서도 객체를 만들어 주므로(없는 필드는 기본값), 여기서
                // 불변식을 보지 않으면 "revision 41까지 동기화했다"고만 적힌
                // 껍데기가 그대로 믿긴다 - CloudSaveLocalState.IsWellFormed 주석
                if (!state.IsWellFormed())
                {
                    Debug.LogWarning("[Onikiri] Cloud sidecar failed its invariants; treating it as absent.");
                    return null;
                }

                return state;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Onikiri] Cloud sidecar unreadable: " + exception.Message);
                return null;
            }
        }

        /**
         * @brief sidecar를 쓴다. **불변식을 어긴 것은 디스크에 안 남긴다.**
         *
         * 못 쓰게 막는 것이 조용히 쓰는 것보다 낫다 - 다음 부팅이 그것을
         * 어차피 버릴 것이고(Load), 그 사이의 파일은 진단을 방해할 뿐이다.
         *
         * ## 반환값이 2단계의 계약이다
         *
         * **pending을 디스크에 남기는 데 실패했으면 서버 트랜잭션을 시작하지
         * 않는다**(CloudSavePolicy.MayStartServerWrite). 이유는 응답 유실
         * 복구가 이 파일 하나에 걸려 있기 때문이다 - 서버는 커밋했는데 그
         * 커밋의 mutation id가 로컬 어디에도 없으면, 다음 실행은 같은 쓰기를
         * 다시 올린다. revision이 하나 더 오르고 직전 백업이 한 칸 밀린다.
         *
         * 저장이 실패한 상태의 정답은 **로컬 dirty 유지**다. 게임은 그대로
         * 돌고(오프라인 계약), 다음 디바운스에서 다시 시도한다 - 클라우드
         * 쓰기는 그때까지 나가지 않는다.
         *
         * @return 원자 교체까지 끝났으면 true. null·불변식 실패·IO 예외는 false
         */
        public static bool Save(CloudSaveLocalState state)
        {
            if (state == null) return false;

            if (!state.IsWellFormed())
            {
                Debug.LogWarning("[Onikiri] Refused to write a malformed cloud sidecar.");
                return false;
            }

            try
            {
                state.formatVersion = CloudSaveLocalState.CurrentFormatVersion;

                string json = JsonUtility.ToJson(state, true);
                string temp = Path + ".tmp";

                File.WriteAllText(temp, json);

                if (File.Exists(Path)) File.Replace(temp, Path, null);
                else File.Move(temp, Path);

                return true;
            }
            catch (Exception exception)
            {
                // 실패해도 게임은 계속된다. 다음 동기화가 다시 적는다 - 그때까지는
                // "서버를 모르는 기기"이고, 그 상태에서 서버로 쓰지 않는 것이
                // 이 false의 뜻이다
                Debug.LogWarning("[Onikiri] Cloud sidecar save failed: " + exception.Message);
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Onikiri] Could not delete the cloud sidecar: " + exception.Message);
            }
        }

        /**
         * @brief 쓰기 하나를 가리키는 id. **재시도에서도 같은 값을 다시 쓴다.**
         *
         * 재시도마다 새로 만들면 응답 유실 복구가 성립하지 않는다 - 서버에
         * 적힌 id와 지금 들고 있는 id가 영원히 다르기 때문이다.
         */
        public static string NewMutationId()
        {
            return CloudSaveIds.New();
        }

        /** 앱 실행마다 새로 만든다. 소프트 단일 작성 세션의 열쇠 */
        public static string NewSessionId()
        {
            return CloudSaveIds.New();
        }

        /** 설치마다 한 번 만들어 sidecar에 눌러 둔다 */
        public static string NewDeviceId()
        {
            return CloudSaveIds.New();
        }
    }
}
