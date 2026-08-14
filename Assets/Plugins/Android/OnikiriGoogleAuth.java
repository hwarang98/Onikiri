package com.studio202.onikiri.auth;

import android.app.Activity;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

/**
 * 구글 계정에서 ID 토큰 한 장을 받아 유니티로 넘긴다. 그것 말고는 아무것도 안 한다.
 *
 * <h2>왜 플러그인을 직접 쓰는가</h2>
 *
 * 유니티에서 구글 로그인의 관례적 답은 googlesamples/google-signin-unity 였다.
 * 그 래퍼는 2021년 이후 커밋이 없고(아카이브됨), 감싸고 있는 안드로이드 API
 * (GoogleSignInClient)는 구글이 **명시적으로 폐기**하고 Credential Manager로
 * 옮기라고 안내하는 것이다. 출시할 앱의 로그인 경로를, 폐기된 API를 감싼
 * 방치된 래퍼 위에 올릴 수는 없다.
 *
 * 그래서 이 파일이 그 래퍼 자리를 대신한다. 하는 일이 적어서 가능하다 -
 * 계정 선택창을 띄우고, ID 토큰 문자열 하나를 돌려주는 것이 전부다. 연동·
 * 복구·병합은 전부 C# 쪽(AccountLink)에 있고 여기로 내려오지 않는다.
 *
 * <h2>GetSignInWithGoogleOption 이지 GetGoogleIdOption 이 아니다</h2>
 *
 * 후자는 "이미 이 기기에 있는 계정"을 바텀시트로 제안하는 흐름이라 조건이
 * 맞지 않으면 NoCredentialException 으로 조용히 끝난다. 우리 자리는 사람이
 * **"구글로 로그인" 버튼을 명시적으로 누른** 뒤이므로, 언제나 계정 선택창이
 * 뜨는 쪽이 맞다. 눌렀는데 아무 일도 안 일어나는 것은 20단계 함정 버튼과
 * 같은 종류의 거짓말이다.
 */
public final class OnikiriGoogleAuth {

    /**
     * 결과를 받는 쪽. C#이 AndroidJavaProxy로 구현한다.
     *
     * <p>⚠️ 이 두 메서드는 **유니티 스레드가 아닌 곳에서** 불린다. C# 구현이
     * 유니티 API를 만지면 안 된다(GoogleIdTokenBridge 주석 참고).
     */
    public interface Listener {
        void onToken(String idToken);

        /** code 는 C#의 AuthFailure로 번역되는 짧은 문자열이다 */
        void onError(String code, String message);
    }

    /** C# 쪽 AuthFailure와 짝이다. 갈리면 취소가 오류로 읽힌다 */
    private static final String CODE_CANCELLED = "cancelled";
    private static final String CODE_NO_CREDENTIAL = "no_credential";
    private static final String CODE_OTHER = "other";

    private OnikiriGoogleAuth() {
    }

    /**
     * 계정 선택창을 띄우고 ID 토큰을 받아온다.
     *
     * @param activity       UnityPlayer.currentActivity
     * @param serverClientId Firebase 콘솔이 만든 **웹 클라이언트 ID**(client_type 3).
     *                       안드로이드 클라이언트 ID가 아니다 - 그것을 넣으면
     *                       토큰의 audience가 어긋나 Firebase가 거절한다.
     */
    public static void requestIdToken(final Activity activity,
                                      final String serverClientId,
                                      final Listener listener) {
        if (activity == null || listener == null) return;

        // 로그인 창을 띄우는 호출이라 UI 스레드여야 한다. 유니티의 스크립트
        // 스레드는 UI 스레드가 아니므로, 여기서 넘기지 않으면 기기에 따라
        // 창이 안 뜨거나 그 자리에서 예외가 난다
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    GetSignInWithGoogleOption option =
                            new GetSignInWithGoogleOption.Builder(serverClientId).build();

                    GetCredentialRequest request = new GetCredentialRequest.Builder()
                            .addCredentialOption(option)
                            .build();

                    // 콜백 전용 스레드 하나. 유니티 스레드에 태우면 로그인 창이
                    // 떠 있는 동안 게임이 멈춘다 - 방치형에서 그 시간은 그대로
                    // 손해이고, 애초에 이 기능은 곁다리다
                    Executor executor = Executors.newSingleThreadExecutor();

                    CredentialManager.create(activity).getCredentialAsync(
                            activity,
                            request,
                            null,
                            executor,
                            new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                                @Override
                                public void onResult(GetCredentialResponse response) {
                                    handle(response, listener);
                                }

                                @Override
                                public void onError(GetCredentialException e) {
                                    listener.onError(classify(e), describe(e));
                                }
                            });
                } catch (Throwable t) {
                    // 여기서 새는 예외는 유니티까지 못 올라가고 앱을 내린다.
                    // 로그인은 곁다리라 절대 게임을 죽이면 안 된다
                    listener.onError(CODE_OTHER, describe(t));
                }
            }
        });
    }

    private static void handle(GetCredentialResponse response, Listener listener) {
        try {
            Credential credential = response.getCredential();

            if (credential instanceof CustomCredential
                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
                    .equals(credential.getType())) {

                GoogleIdTokenCredential google = GoogleIdTokenCredential
                        .createFrom(((CustomCredential) credential).getData());

                String token = google.getIdToken();
                if (token == null || token.length() == 0) {
                    listener.onError(CODE_OTHER, "빈 ID 토큰");
                    return;
                }

                listener.onToken(token);
                return;
            }

            // 구글이 아닌 자격 증명(패스키·비밀번호)이 왔다. 요청에 구글
            // 옵션만 넣었으므로 정상 경로에서는 오지 않는다
            listener.onError(CODE_OTHER, "예상 밖 자격 증명: "
                    + (credential != null ? credential.getType() : "null"));
        } catch (Throwable t) {
            listener.onError(CODE_OTHER, describe(t));
        }
    }

    /**
     * 예외를 C#이 아는 짧은 코드로 좁힌다.
     *
     * <p>취소를 따로 가르는 것이 이 함수의 존재 이유다. 사람이 계정 선택창을
     * 닫는 것은 이 기능에서 **가장 흔한 종료 경로**이고, 그것이 "오류"로
     * 화면에 뜨면 안 하겠다고 한 사람에게 고장을 알리는 꼴이 된다.
     */
    private static String classify(GetCredentialException e) {
        if (e instanceof GetCredentialCancellationException) return CODE_CANCELLED;
        if (e instanceof NoCredentialException) return CODE_NO_CREDENTIAL;
        return CODE_OTHER;
    }

    private static String describe(Throwable t) {
        if (t == null) return "알 수 없는 오류";
        String message = t.getMessage();
        return t.getClass().getSimpleName() + (message != null ? ": " + message : "");
    }
}
