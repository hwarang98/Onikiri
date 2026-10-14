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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /**
         * @brief 다음 sidecar 저장 **한 번만** 실패시킨다 (62.1.2 검사용).
         *
         * "사슬 확정 예약은 sidecar 파일이 남은 뒤에만 푼다"는 계약은 실패를
         * 만들 수 있어야 잴 수 있다. 한 번 쓰면 스스로 꺼진다.
         * **출시 빌드에는 이 필드가 없다.**
         */
        public static bool FailNextSaveForTests;
#endif

        public static bool Save(CloudSaveLocalState state)
        {
            if (state == null) return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FailNextSaveForTests)
            {
                FailNextSaveForTests = false;
                Debug.LogWarning("[Onikiri] [진단] sidecar 저장 실패 모의.");
                return false;
            }
#endif

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

        /**
         * @brief 다른 uid의 사이드카를 옮겨 두는 자리 (71단계).
         *
         * 세이브의 `.prerecover`(CloudSaveRecovery)와 짝이다. 옛 uid의 사슬은
         * 이 기기에서 다시 쓰일 일이 없지만, 복구가 어그러졌을 때 "이 기기가
         * 어느 계정의 몇 번째 revision까지 알았는가"를 말해 줄 유일한 증거다.
         */
        public static string PreRecoverPath
        {
            get { return Path + ".prerecover"; }
        }

        /**
         * @brief 이 uid의 새 사이드카(revision 0). **아직 디스크에 쓰지 않는다.**
         *
         * ## 기기 id는 이 실행의 것이다 (71단계)
         *
         * 70단계 실기가 연 결함이 여기 있었다. 새 사이드카를 만드는 자리가 다섯
         * 군데였고, 다섯 다 `NewDeviceId()`로 **새 id를 즉석에서** 만들었다. 그런데
         * 같은 실행의 세션 게이트와 heartbeat는 `CloudSaveSession.DeviceIdFor`의
         * 값을 쓴다. 한 실행 안에서 기기 id가 둘이 되자, 첫 커밋이 같은 세션
         * id에 다른 기기 id를 적으려 했고 규칙(`ownerRenew`: deviceId 불변)이
         * 거부했다 - 30초마다 `PERMISSION_DENIED`, 업로드 0회.
         *
         * 그래서 id를 만드는 문을 하나로 둔다. 디스크에 아직 없는 동안 그 값은
         * `DeviceIdFor`가 들고 있고, 사이드카가 남는 순간부터는 디스크가 들고 있다.
         *
         * 디스크의 사이드카가 **다른 uid의 것**이면 먼저 `PreRecoverPath`로 옮겨
         * 둔다. 지우지 않는다(61단계 `.prerecover` 규칙).
         *
         * @return uid가 비었으면 null
         */
        public static CloudSaveLocalState NewOwnedBy(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;

            PreserveForeign(uid);
            return CloudSaveLocalState.NewFor(uid, CloudSaveSession.DeviceIdFor(uid));
        }

        /**
         * @brief `NewOwnedBy` + 디스크 저장. **디스크에 남지 못하면 null이다.**
         *
         * null을 받은 쪽은 서버로 아무것도 보내지 않는다(62.1.2 계약 - 사이드카가
         * 디스크에 없으면 서버 쓰기 금지). 메모리에서만 만든 사이드카로 세션을
         * 잡으면 다음 실행은 그 기기 id를 모르고, 자기 자신에게 인수 확인을 띄운다.
         */
        public static CloudSaveLocalState CreateOwnedBy(string uid)
        {
            CloudSaveLocalState state = NewOwnedBy(uid);
            if (state == null || !Save(state)) return null;

            Debug.Log("[CloudSave] 이 uid의 새 사이드카를 남겼습니다 (rev 0, 기기 "
                      + state.deviceId.Substring(0, 6) + "…).");
            return state;
        }

        /** 디스크의 사이드카가 다른 uid의 것이면 옆에 옮겨 둔다. 실패는 삼킨다 */
        private static void PreserveForeign(string uid)
        {
            CloudSaveLocalState existing = Load();
            if (existing == null || existing.ownerUid == uid) return;

            try
            {
                File.Copy(Path, PreRecoverPath, true);
                Debug.Log("[CloudSave] 다른 uid의 사이드카를 보존했습니다: " + PreRecoverPath);
            }
            catch (Exception exception)
            {
                // 백업은 최선의 노력이다 - SaveSystem의 손상 백업과 같은 규칙
                Debug.LogWarning("[CloudSave] 옛 사이드카 보존 실패(계속 진행): " + exception.Message);
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

        /**
         * @brief 설치마다 한 번 만들어 sidecar에 눌러 둔다.
         *
         * **새 사이드카를 만들 때 이것을 직접 부르지 않는다** - `NewOwnedBy`를
         * 쓴다. 직접 부르면 같은 실행의 세션과 다른 기기 id가 생긴다(71단계).
         * 부르는 자리는 `CloudSaveSession.DeviceIdFor` 하나다.
         */
        public static string NewDeviceId()
        {
            return CloudSaveIds.New();
        }
    }
}
