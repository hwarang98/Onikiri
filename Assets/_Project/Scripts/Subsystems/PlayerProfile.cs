using System;
using UnityEngine;

namespace Onikiri.Progression
{
    /**
     * @brief 플레이어가 스스로 정하는 이름. 리더보드에 걸리는 유일한 개인 정보다.
     *
     * MonoBehaviour가 아닌 이유는 이 값이 씬의 어떤 것에도 딸려 있지 않기 때문이다.
     * 세이브에서 와서 세이브로 가고, 읽는 곳이 리더보드 하나다 - 씬에 컴포넌트를
     * 세우면 그 배선이 빠지는 날 이름이 조용히 기본값으로 돌아간다(49단계
     * StageProgress 미배선과 같은 종류의 사고).
     *
     * ## 왜 검증이 여기 있나
     *
     * 이름은 **남의 화면에 뜨는 유일한 내 문자열**이다. 길이를 안 자르면 랭킹표의
     * 한 줄이 옆 칸을 밀고, 빈 이름은 순위표에 유령 줄을 만든다. 그리고 이 검사는
     * 서버 규칙에도 같은 값으로 적혀 있어야 한다(firestore.rules의 name 길이 상한) -
     * 클라가 자르는 것은 편의고, 강제는 서버가 한다.
     */
    public static class PlayerProfile
    {
        /**
         * @brief 이름 길이 상한. **firestore.rules와 같은 값이어야 한다.**
         *
         * 12자인 이유는 랭킹 한 줄의 이름 칸(캡션 크기)이 그쯤에서 찬다.
         * 한글 12자가 기준이고, 영문은 같은 칸에 더 들어가지만 상한을 글자
         * 종류별로 가르면 "왜 내 이름만 잘리나"가 설명 불가능해진다.
         */
        public const int MaxLength = 12;

        /**
         * @brief 이름을 아직 안 정한 사람의 표시 이름.
         *
         * 빈 문자열로 두지 않는다. 랭킹표에서 이름 칸이 비면 그 줄은 순위와
         * 숫자만 남아 "불러오다 만 줄"로 읽힌다.
         */
        public const string DefaultName = "이름없는 무사";

        private static string current = string.Empty;

        /** 이름이 바뀌었다. 랭킹 화면과 제출이 같은 값을 보게 하는 신호 */
        public static event Action Changed;

        /** 화면과 서버에 나가는 이름. 안 정했으면 기본 이름 */
        public static string Name
        {
            get { return string.IsNullOrEmpty(current) ? DefaultName : current; }
        }

        /** 스스로 정한 이름이 있는가. 랭킹 첫 진입에서 입력을 띄울지 가른다 */
        public static bool HasChosenName { get { return !string.IsNullOrEmpty(current); } }

        /**
         * @brief 이름을 정한다. **변경 횟수를 막지 않는다.**
         *
         * 잠글 이유가 지금은 없다 - 값비싼 것(도달층)은 이름이 아니라 문서에
         * 붙어 있고, 이름을 바꿔도 순위는 그대로다. 오히려 오타 하나를 영구히
         * 박제하는 쪽이 실제 사용자에게 나쁘다. 변경 제한이 필요해지는 것은
         * 이름이 자산이 될 때(시즌 보상·거래)이고 그건 이 게임에 아직 없다.
         *
         * @return 실제로 받아들여졌으면 true
         */
        public static bool SetName(string raw)
        {
            string clean = Sanitize(raw);
            if (clean.Length == 0) return false;
            if (clean == current) return true;

            current = clean;
            var handler = Changed;
            if (handler != null) handler();
            return true;
        }

        /** 세이브 복원용. 신호를 내지 않는다 - 복원은 변경이 아니다 */
        public static void Restore(string saved)
        {
            current = Sanitize(saved);
        }

        /** 세이브에 적을 값. 안 정했으면 빈 문자열(기본 이름을 굳히지 않는다) */
        public static string Collect()
        {
            return current;
        }

        /**
         * @brief 입력을 저장 가능한 이름으로 다듬는다.
         *
         * 하는 일 셋: 앞뒤 공백 제거, 줄바꿈·제어문자 제거, 길이 자르기.
         *
         * 제어문자를 지우는 것이 길이보다 중요하다. 줄바꿈이 낀 이름은 랭킹표의
         * 한 줄을 두 줄로 만들어 그 아래 전부를 밀어내고, 그건 남의 화면에서
         * 일어난다. 욕설 필터는 지금 없다 - 목록 기반 필터는 우회가 쉽고
         * 오탐이 잦아서, 신고·차단이 설 때 서버 쪽에서 함께 오는 편이 맞다.
         */
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var builder = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                // 탭·줄바꿈도 여기서 걸린다(char.IsControl이 참이다)
                if (char.IsControl(c)) continue;
                builder.Append(c);
            }

            string trimmed = builder.ToString().Trim();
            if (trimmed.Length > MaxLength) trimmed = trimmed.Substring(0, MaxLength);

            // 자른 끝이 공백일 수 있다("가나다라마바사아자차카타 " -> 앞 12자)
            return trimmed.Trim();
        }

        /** 테스트·세이브 삭제용 초기화 */
        public static void Clear()
        {
            current = string.Empty;
        }
    }
}
