# ONIKIRI 74단계 보고서 — 출시 준비 점검 ①: `AssetPackManager` ClassNotFound

- **브랜치** `feature/step73-conflict-overlap`에 이어 적음 · **코드 변경 없음**(조사와 판정만)
- **닫는 항목** 70단계 §4-2 "`ClassNotFoundException: …assetpacks.AssetPackManager` — 출시 준비 스텝에서 원인 확인"

## 0. 한 줄 요약

**고칠 것이 없다.** Unity 플레이어 런타임이 부팅 때 Play Core의 `AssetPackManager`를
조회하는데, 우리 APK에는 그 라이브러리가 없다 — `androidSplitApplicationBinary: 0`이라
Unity가 넣지 않기 때문이다. 조회가 실패하면 Unity는 "PAD를 안 쓰는 빌드"로 판단하고 그냥
진행하고, 로그에 그 한 줄이 남는다. 없애려면 **쓰지도 않는 Play Core를 APK에 넣어야 하고**,
용량만 늘 뿐 얻는 것이 없다.

## 1. 근거

### 1.1 프로젝트에는 PAD가 없다

| 확인한 것 | 결과 |
|---|---|
| `ProjectSettings.asset` | `androidSplitApplicationBinary: 0` — Split Application Binary 꺼짐 |
| `mainTemplate.gradle` 의존성 | `com.google.android.play:asset-delivery` **없음**. Firebase·GMS·androidx.credentials뿐 |
| `Assets/Plugins/Android/` · `ProjectSettings/` · `Packages/` 전수 검색 | `assetpacks` · `AssetPackManager` · `asset-delivery` · `play:asset` **0건** |
| `Packages/manifest.json` | PAD·Addressables 계열 패키지 **없음** |

### 1.2 찾는 쪽은 Unity 런타임이다

`6000.5.2f1/Editor/Data/PlaybackEngines/AndroidPlayer/Variations/il2cpp/Release/Classes/classes.jar`:

```
com/unity3d/player/IAssetPackManagerConfirmationDialogCallback.class
com/unity3d/player/IAssetPackManagerDownloadStatusCallback.class
com/unity3d/player/IAssetPackManagerStatusQueryCallback.class
com/unity3d/player/UnityCoreAssetPacksStatusCallbacks.class
```

Unity 플레이어가 PAD 연동 경로를 **항상 들고 있고**, 부팅 때 Play Core 클래스가 실제로
있는지 확인한다. 우리처럼 PAD를 안 쓰는 빌드에서는 그 확인이 실패하고, 그것이 logcat의
한 줄이다. **70단계의 추정("Unity가 찾지만 빌드에 없다")이 맞았고**, 빠진 것은 "그래서
문제가 아니다"는 판정이었다.

## 2. 그래서 무엇을 하지 않았는가

| 안 | 왜 안 하는가 |
|---|---|
| `com.google.android.play:asset-delivery` 의존성 추가 | 쓰지 않는 라이브러리다. APK가 커지고, Play Core는 자체 수명주기·ProGuard 규칙을 끌고 온다 |
| Split Application Binary 켜기 | 에셋을 쪼갤 이유가 없다. 72단계 개발 빌드가 88.4MB로, AAB 150MB 한도에서 멀다 |
| 로그 필터로 가리기 | 증상을 지우는 일이다. 이 줄은 "PAD 없음"이라는 **사실의 기록**이고, 나중에 PAD를 켜면 사라진다 |

## 3. 출시 때 다시 볼 조건

AAB로 올리면서 **에셋 팩을 쓰기로 하면** 이야기가 달라진다 — 그때는 Split Application
Binary를 켜고 Unity가 Play Core를 넣으며, 이 줄은 저절로 사라진다. 그 전까지는 이 로그가
정상이다.

## 4. 남은 출시 준비 항목 (이 스텝에서 건드리지 않음)

62·63단계 계열 넷은 그대로다 — 모두 Firebase·Play 콘솔 조작이라 코드로 닿지 않는다:

1. production 서명 인증서 없음 (등록된 SHA-256이 이 기계의 debug keystore)
2. `PLAY_RECOGNIZED` 요구 해제 상태 — 출시 전 되돌려야 한다
3. Editor Debug Provider 미등록
4. Firestore App Check enforcement 미활성

> **순서 주의**: 4를 켜는 순간 지금 debug keystore로 도는 QA 빌드가 전부 막힌다. 실기 QA가
> 남아 있는 동안에는 켜지 않는다.
