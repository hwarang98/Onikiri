using System;
using System.Security.Cryptography;
using System.Text;
using Onikiri.Progression;
using UnityEngine;

namespace Onikiri.Cloud
{
    /**
     * @brief 세이브 한 벌을 **문자열 하나와 지문 둘**로 바꾼다. Firebase가 없다.
     *
     * 두 지문이 서로 다른 질문에 답한다:
     *
     *   payloadSha256   내려받은 바이트가 온전한가 (전송·저장 무결성)
     *   stateSha256     **게임 상태가 실제로 달라졌는가** (충돌 판정)
     *
     * 하나로 합칠 수 없다. payload 해시는 30초짜리 자동 저장마다 달라진다 -
     * `lastQuitUtcTicks`가 매번 바뀌기 때문이다. 그것을 변경으로 세면 두 기기가
     * 번갈아 켜져 있기만 해도 충돌 화면이 뜨고, 사람은 아무것도 안 했는데 기록을
     * 고르라는 요구를 받는다. 반대로 상태 지문만 두면 전송 중 망가진 payload를
     * 걸러낼 수단이 사라진다.
     */
    public static class CloudSaveFingerprint
    {
        /**
         * @brief payload 상한 (바이트).
         *
         * 현재 세이브는 4KB 남짓이다. 그런데도 상한을 못 박는 이유는 Firestore의
         * 1MiB에 기대는 순간 **상한이 우리 것이 아니게 되기** 때문이다. 배열
         * 하나가 잘못 자라 문서 한도를 밟으면 그날부터 그 계정은 클라우드 저장이
         * 통째로 안 되고, 원인은 세이브가 아니라 Firestore 에러로 보인다.
         * 50배 여유는 형식이 몇 번 늘어도 남는다.
         */
        public const int MaxPayloadBytes = 200 * 1024;

        /**
         * @brief 상태 지문에서 **0으로 정규화되는** 필드들.
         *
         * 셋 다 "언제 껐는가"에서 나오는 값이고 진행이 아니다. 골드·경험치·
         * 처치 수는 여기 없다 - 그것은 실제 진행이라, 빼면 두 기기가 다른
         * 재화를 들고도 같은 지문을 갖게 된다.
         */
        public static readonly string[] VolatileFields =
        {
            "lastQuitUtcTicks",
            "goldPerSecond",
            "expPerSecond"
        };

        /**
         * @brief 세이브를 payload 문자열로 만든다. **원본을 건드리지 않는다.**
         *
         * 들여쓰기를 끄는 이유는 크기다(약 40% 차이). 로컬 파일은 사람이 읽을
         * 일이 있어 pretty로 두지만(SaveSystem), 서버로 가는 것은 아무도 눈으로
         * 안 읽는다 - 읽어야 할 때는 다운로드해서 로컬 파일로 떨군다.
         */
        public static string Serialize(SaveData data)
        {
            if (data == null) return null;

            try
            {
                return JsonUtility.ToJson(data, false);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Onikiri] Cloud payload serialization failed: " + exception.Message);
                return null;
            }
        }

        /**
         * @brief payload를 세이브로 되돌린다. **마이그레이션은 하지 않는다.**
         *
         * 버전을 올리는 일은 적용을 결정한 뒤(3단계 Coordinator)의 몫이다.
         * 여기서 올려 버리면 "서버에 있던 것"과 "적용될 것"이 달라져, 해시
         * 대조가 자기 자신과 어긋난다.
         *
         * @return 못 읽으면 null. 예외를 밖으로 던지지 않는다
         */
        public static SaveData Deserialize(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return null;

            try
            {
                return JsonUtility.FromJson<SaveData>(payload);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Onikiri] Cloud payload was unreadable: " + exception.Message);
                return null;
            }
        }

        /** JSON 왕복으로 만드는 깊은 사본. 정규화가 원본을 못 건드리게 하는 수단이다 */
        public static SaveData Clone(SaveData data)
        {
            string json = Serialize(data);
            if (json == null) return null;
            return Deserialize(json);
        }

        /**
         * @brief 시간성 필드를 0으로 민 사본. 상태 지문의 입력이다.
         *
         * 사본을 만드는 것이 요점이다 - 원본을 밀면 다음 로컬 저장이 방치
         * 보상의 기준 시각을 잃는다.
         */
        public static SaveData Normalize(SaveData data)
        {
            var copy = Clone(data);
            if (copy == null) return null;

            copy.lastQuitUtcTicks = 0L;
            copy.goldPerSecond = 0d;
            copy.expPerSecond = 0d;
            return copy;
        }

        public static string StateHashOf(SaveData data)
        {
            var normalized = Normalize(data);
            if (normalized == null) return null;
            return HashOf(Serialize(normalized));
        }

        /** 내려받은 봉투의 상태 지문을 직접 다시 계산한다 (서버 값과 대조용) */
        public static string StateHashOfPayload(string payload)
        {
            return StateHashOf(Deserialize(payload));
        }

        /** SHA-256 소문자 16진. 서버에도 같은 형식으로 적힌다 */
        public static string HashOf(string text)
        {
            if (text == null) return null;

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        /**
         * @brief 지문 두 개가 같은가.
         *
         * 대소문자를 무시한다 - 우리는 소문자로만 쓰지만, 다른 도구가 만든
         * 대문자 지문 때문에 멀쩡한 세이브가 손상으로 판정되는 것이 그 반대의
         * 위험보다 크다(SHA-256에서 대소문자는 정보가 아니다).
         */
        public static bool Matches(string payload, string expectedSha)
        {
            if (payload == null || string.IsNullOrEmpty(expectedSha)) return false;
            return string.Equals(HashOf(payload), expectedSha, StringComparison.OrdinalIgnoreCase);
        }

        /** SHA-256 16진 표기의 길이 */
        public const int HashLength = 64;

        /**
         * @brief 이 문자열이 지문의 **형식**인가. 값이 맞는지는 묻지 않는다.
         *
         * sidecar 불변식이 이것을 쓴다 - "동기화한 적이 있다"고 적혀 있는데
         * 지문 자리에 아무 문자열이나 들어 있으면, 그 sidecar는 다음 부팅에서
         * 거짓을 말한다. 형식만으로도 그 거짓의 대부분이 걸린다.
         */
        public static bool IsHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.Length != HashLength) return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }

            return true;
        }

        public static int ByteCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Encoding.UTF8.GetByteCount(text);
        }

        public static bool IsWithinLimit(string payload)
        {
            int bytes = ByteCount(payload);
            return bytes > 0 && bytes <= MaxPayloadBytes;
        }
    }
}
