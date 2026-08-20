using System;

namespace Onikiri.Cloud
{
    /**
     * @brief 세션·기기·쓰기를 가리키는 id. **형식이 하나여야 검사도 하나다.**
     *
     * 셋 다 `Guid.NewGuid().ToString("N")` = 소문자 16진 32자다. 종류마다 다른
     * 형식을 쓰면 "이 문자열이 id인가"를 묻는 자리마다 다른 검사가 서고, 그러면
     * 검사 중 하나가 빠진 것을 아무도 모른다.
     *
     * 형식을 **검사할 수 있게** 만든 것이 요점이다. 임의 문자열을 허용하면
     * `sessionId = "editor-session"` 같은 값이 서버 문서에 그대로 올라가고,
     * 2단계의 세션 일치 규칙이 그 값을 진짜 세션으로 받아들인다.
     *
     * uid는 여기 속하지 않는다 - Firebase가 만드는 값이고 형식이 우리 것이 아니다.
     */
    public static class CloudSaveIds
    {
        /** Guid "N" 표기의 길이 */
        public const int Length = 32;

        public static string New()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (id.Length != Length) return false;

            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }

            return true;
        }
    }
}
