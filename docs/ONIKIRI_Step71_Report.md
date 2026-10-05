# ONIKIRI 71단계 보고서 — Cloud: 다른 uid의 사이드카를 만났을 때 + allowBackup=false

- **브랜치** `feature/step71-sidecar-uid-mismatch` (develop 8c5d2b6에서 분기) · 커밋 1회 · push 승인 대기
- **기기** Galaxy Z Flip 3 (`R5CR81SQTXB`) · QA 익명 uid `4N5sjvV52BMAyTDuAlIGYAL5JJ02` — 운영 계정 무접촉
- **건드리지 않은 것** `firestore.rules`(변경·배포 없음) · 봉투 형식 1 · SaveData v23 · 63단계 세션/세대/PlayLock · `ApplyBoot` · `AccountLink` · 밸런스 · UI

## 0. 한 줄 요약

70단계의 `PERMISSION_DENIED`는 사이드카 uid 불일치 자체가 아니라 **한 실행 안에서 기기 id가 둘**이었던 것이 원인이었다(게이트·heartbeat는 `DeviceIdFor`, 첫 커밋은 시도마다 `NewDeviceId()` → 같은 세션 id에 다른 기기 id → 규칙 `ownerRenew` 거부). 새 사이드카를 만드는 문을 `CloudSaveSidecar.NewOwnedBy/CreateOwnedBy` 하나로 모으고(이 실행의 기기 id · 디스크 먼저 · 옛 사이드카 보존), 부팅이 다른 uid의 사이드카를 만나면 61단계 Recover 판정(`PlanFor`)을 입구 하나로 재사용한다. `allowBackup=false` + `dataExtractionRules`(기기 간 전송까지 차단). 실기: 첫 업로드 **부팅 2.5초 뒤 rev 1**, `PERMISSION_DENIED` 0건, 재실행 인수 확인 0회, 재설치는 빈 상태.

**수치** EditMode **1081/1081** · PlayMode **78/78**(MCP) · Emulator **73/73**(68 + 신규 5).

## 1. 변경 파일

| 파일 | 내용 |
|---|---|
| `Cloud/CloudSaveSidecar.cs` | `NewOwnedBy(uid)`(기기 id = `CloudSaveSession.DeviceIdFor`, 다른 uid 사이드카는 `PreRecoverPath`로 복사) · `CreateOwnedBy(uid)`(+디스크 저장, 실패 시 null) · `PreRecoverPath` |
| `Cloud/CloudSaveSync.cs` | `PrepareSidecar(uid)` — 첫 커밋이 사이드카를 **세션 Acquire 전에 디스크에** 확보, 실패면 커밋 안 함 |
| `Cloud/CloudSaveCoordinator.cs` | `ChooseBootSave`: 디스크에 다른 uid의 온전한 사이드카 + uid 확정 → `CloudSaveRecovery.EnterAtBoot` · 사슬 확정(`NoteLocalSaveCommitted`)이 `NewOwnedBy` 사용 |
| `Cloud/CloudSaveRecovery.cs` | `EnterAtBoot`(①·③~⑤ 판정, 새 사이드카, ③이면 urgent) · `BootEntries` · `BackupLocalSave` 공용화 · `KeepLocal`/`AdoptQuietly`가 `CreateOwnedBy`/`NewOwnedBy` 사용 |
| `Widget/Popups/CloudConflictPanel.cs` | `AdoptServerHead`가 `NewOwnedBy` 사용 |
| `Editor/OnikiriTestPanel.cs` | 71단계 절(디스크 상태 · 이 실행의 기기 id · 부팅 시뮬 ③④⑤·오프라인, 샌드박스) · 패널 사이드카 생성도 `CreateOwnedBy` |
| `Plugins/Android/AndroidManifest.xml` | `allowBackup="false"` · `dataExtractionRules` · `tools:replace` |
| `Plugins/Android/OnikiriBackupRules.androidlib/` (신규) | `res/xml/onikiri_data_extraction_rules.xml` — cloud-backup·device-transfer 모든 도메인 exclude |
| `Tests/EditMode/CloudForeignSidecarTests.cs` (신규, 11) | 아래 §5 |
| `Tests/PlayMode/CloudForeignSidecarPlayTests.cs` (신규, 3) | 아래 §5 |
| `tools/firestore-rules-tests/rules.spec.mjs` | 71단계 describe 5개 |

`fullBackupContent`는 원래 어디에도 없어 지울 것이 없었다. Gradle 템플릿 무수정.

## 2. 실제 원인

70단계 추정("첫 커밋이 기기 id를 메모리에서만 만든다")은 **절반만 맞았다.** 메모리 전용인 것은 맞지만, 거부의 직접 원인은 **같은 실행 안에서 기기 id 생성기가 둘**이라는 것이다.

| 자리 | 기기 id | 디스크 |
|---|---|---|
| 부팅 게이트 `TakeoverGate` → `DeviceIdFor(uid)` | 자기 사이드카 없음 → `pendingDeviceId` D1(실행마다 하나) | ✗ |
| heartbeat `CloudSaveSessionWatch` → `DeviceIdFor(uid)` | D1 | ✗ |
| 첫 커밋 `CommitAsync` → `NewFor(uid, NewDeviceId())` | **D2, 시도마다 새것** | ✗ |

1. 게이트가 세션 문서를 (S, D1)으로 잡는다.
2. 커밋이 같은 S에 D2로 `AcquireAsync` → 클라 판정 `StepFor`는 세션 id만 봐서 `Renew` → deviceId=D2로 쓴다.
3. 규칙 `ownerRenew`는 `deviceId` 불변, `takeoverCommit`은 sessionId 변경을 요구 → **둘 다 거부** → 30초 뒤 재시도, 영원히. 서버 쓰기 경로만 사이드카를 남기므로 사이드카도 끝내 안 남는다.
4. 다음 실행은 또 새 D1′ → 서버 owner D1과 달라 "같은 기기의 이전 실행" 갈래를 못 타고(180초 안이면) `AskTheHuman` → 거의 매 부팅 인수 확인.

heartbeat는 D1로 일관돼 실패 로그가 없었고, 70단계 logcat에도 `Acquire` 거부만 30초 간격으로 있다. **Emulator가 규칙 수준에서 두 거부를 재현했다**(§5). **규칙은 옳게 거부하고 있었다 — 무수정으로 풀렸다.**

같은 결함이 사이드카 생성 자리 다섯 곳(커밋 · 사슬 확정 · Recover ③④ · 충돌 패널)에 다 있었다. EditMode `OnlyTheSessionMintsDeviceIds`가 이제 `NewDeviceId()` 호출이 `DeviceIdFor` 한 곳뿐인지 소스에서 센다.

## 3. 경로 설계 — 61단계 순서 안의 위치

부팅 순서: `AwaitIdentity` → 세션 게이트(이제 디스크에 남을 그 기기 id) → `ChooseBootSave` → [입구] → `Finish` → `BootWith`(ApplyBoot 1회 · 저장 · 사슬 확정).

`ChooseBootSave`가 서버를 읽은 뒤, **자기 사이드카가 없고 디스크에 다른 uid의 온전한 사이드카가 있으며 uid가 확정됐으면** `EnterAtBoot`:

| 61단계 | 부팅 입구에서 |
|---|---|
| ① 후보 보존 | 세이브 → `.prerecover`, 옛 사이드카 → `onikiri_cloud_state.json.prerecover`. **지우지 않는다** |
| ② 새 uid 서버 읽기 | 부팅이 이미 읽은 결과를 그대로 쓴다(두 번 안 읽는다). 못 봤으면 `WaitForServer` |
| ③~⑤ | `CloudSaveRecoveryPolicy.PlanFor` 그대로. 어느 갈래든 **새 uid의 rev 0 사이드카를 먼저 디스크에** |
| ③ 서버 없음 | + urgent 첫 업로드(상태 Dirty는 부팅 판정 UploadLocal이 정한다) |
| ④ 같음 | 사슬은 부팅의 `ArmChain` → **로컬 저장 뒤** 확정(62.1.1 순서 유지 — 그래서 앱 내 Recover의 `AdoptQuietly` 즉시 확정을 부팅에서는 쓰지 않는다) |
| ⑤ 다름 | 부팅 `Finish`의 `NoteConflict` + Conflict(60단계 화면 재료) |
| ⑥ | 옛 uid 문서 무접촉(규칙상 권한도 없다 — Emulator 확인) |

자기 사이드카가 없는 `Decide`와 `PlanFor`는 네 갈래에서 같은 답이다(UploadLocal↔③ · InSync↔④ · Conflict↔⑤ · LocalOnly↔대기). 입구는 판정을 바꾸지 않고 **파일 효과만** 더한다 — 새 갈래 없음. 인증 타임아웃(uid 없음)이면 누구 것인지 모르므로 입구를 타지 않고, 옛 파일 그대로 다음 부팅/첫 커밋(`PrepareSidecar`가 같은 보존·생성)으로 넘어간다.

## 4. 같은 경로가 열리는 경우

| 경우 | 덮이는가 |
|---|---|
| 자동 백업 복원(재설치·새 폰) | ✓ 입구 + 기기 id 통일. 그리고 allowBackup=false로 원천 차단(실기 ②) |
| 기기 간 직접 전송(Android 12+) | ✓ 코드로 덮임 + `dataExtractionRules` device-transfer 전부 exclude(targetSdk 36에서 allowBackup=false만으로는 못 막는다) |
| Recover 중단(uid 교체 후 사이드카 저장 전 종료) | ✓ 다음 부팅이 불일치로 입구 |
| 앱 실행 중 Recover(`AccountLink`) | ✓ ③④ 갈래가 `CreateOwnedBy`/`NewOwnedBy` — 옛 사이드카 보존 + 이 실행의 기기 id |
| **uid 불일치가 아닌데 자기 사이드카가 없는 부팅**(첫 실행 커밋 전에 캐시 인증으로 uid가 이미 있음 → 게이트가 돎) | ✓ 같은 결함이었다. `PrepareSidecar`가 게이트의 기기 id로 디스크에 먼저 쓴다 |
| 충돌 화면 선택 · 부팅 사슬 확정이 사이드카를 새로 세울 때 | ✓ 둘 다 `NewOwnedBy` |
| 로그아웃 → 새 익명 | 해당 없음(SignOut 경로가 코드에 없다). 생기면 같은 입구·같은 생성 문을 탄다 |
| 인증 타임아웃 부팅 | 입구 없음(의도). 옛 사이드카는 그대로, 첫 커밋 준비에서 보존·교체 |

## 5. 수치

| 묶음 | 결과 | 비고 |
|---|---|---|
| EditMode | **1081 / 1081** | MCP · 신규 `CloudForeignSidecarTests` 11 |
| PlayMode | **78 / 78** | MCP · 신규 `CloudForeignSidecarPlayTests` 3 |
| Emulator | **73 / 73** | `npm test`(Unity 동봉 OpenJDK) · 기존 68 유지 + 신규 5 |

**EditMode 신규**: 입구 1회·③·새 사이드카 디스크·기기 id=게이트·옛 사이드카 `.prerecover` 원문 일치·세이브 `.prerecover`·urgent·옛 uid 미조회 / 다음 실행이 같은 기기 id·입구 0회 / ④ 사슬은 저장 뒤·기기 id 유지 / ⑤ 충돌 재료·자동 쓰기 없음 / 오프라인도 사이드카 남김 / 자기 사이드카면 입구 0 / uid 없으면 옛 파일 그대로 / **사이드카 저장 실패 → urgent 없음·`PrepareSidecar` null(=업로드 차단, 62.1.2 무회귀)·재시도는 같은 기기 id** / 커밋은 `PrepareSidecar`가 `AcquireAsync`보다 앞(소스 순서) / 커밋 준비 두 번 = 같은 기기 id / `NewDeviceId()` 호출 자리 1곳.

**PlayMode 신규**: 실제 씬 부팅에서 ApplyBoot 1회 · 방치 보상 1회 · 입구 1회 · Dirty / 새 사이드카 기기 id = `DeviceIdFor` · 옛 사이드카 보존 / **다음 실행 게이트가 같은 폰(이전 실행 heartbeat 5초 전, 미release)에게 묻지 않음 — `AwaitingConfirm` 0회 · 팝업 미표시 · Acquired**.

**Emulator 신규**: [70 재현] 같은 세션이 기기 id를 바꿔 갱신 → 거부 · 게이트와 같은 기기 id로 갱신 → rev 1 업로드 성공 · 두 번째 부팅(새 세션·같은 기기 id·살아 있는 이전 세션) 이어받기 성공 · [70 재현] 기기 id가 바뀌면 거부 · 옛 uid 문서 읽기/쓰기 거부 + 흐름 뒤 무변화.

## 6. 실기 재현

빌드: `AndroidDeployBuilder.BuildDevelopmentApkBatch`(에디터 안 실행) · 88.4MB · 208초. APK 검증(`aapt2`): `allowBackup=false`, `dataExtractionRules=@xml/onikiri_data_extraction_rules`(리소스 포함). 설치 후 `dumpsys package` pkgFlags에 `ALLOW_BACKUP` 없음. 병합 경고·충돌 없음 — 현재 이 속성을 선언한 라이브러리는 없다(`tools:replace`는 보험).

### ① 70단계와 같은 상태 → 부팅

폰은 70단계 끝 상태 그대로였다: 사이드카 `ownerUid=fvuPhk…`(옛 uid, rev 7) · 지금 uid `4N5sjv…`(서버 문서 없음). 파일은 설치 전에 PC로 백업(`scratchpad/s71/before/`). `install -r`(데이터 유지).

- **첫 부팅(과도기 1회)**: 인수 확인이 떴다(`docs/media/step71_legacy_session_prompt.png`). 원인 = 바로 직전까지 **70단계 빌드가 폰에서 돌고 있었고**(내가 설치 때 force-stop), 그 실행이 메모리 기기 id로 heartbeat를 감던 세션 문서가 살아 있었다. 그 id는 어디에도 저장된 적이 없으므로 새 빌드가 같은 기기로 알아볼 방법이 없다 — 옛 빌드가 남긴 잔재이고 새 코드의 경로가 아니다. 출시에서는 이 잔재가 생길 옛 빌드가 없다. 앱을 멈추고 세션 만료(180초)를 기다린 뒤 다시 측정했다.
- **부팅 ①(본 측정)**:

```
02:55:03.933 기다린 사이드카가 지금 uid(4N5sjv…)의 것이 아닙니다. 자동 적용하지 않고 복구 입구로 갑니다.
02:55:04.084 ★ 다른 uid의 사이드카 - 부팅 복구 판정 = KeepLocalAsFirstRevision (서버 Missing / 로컬 8aca74f8 / 서버지문 -)
02:55:04.087 다른 uid의 사이드카를 보존했습니다: …/files/onikiri_cloud_state.json.prerecover
02:55:04.090 이 uid의 새 사이드카를 남겼습니다 (rev 0, 기기 495ba2…).
02:55:04.091 urgent 동기화 예약: 다른 uid 사이드카 - 새 계정의 첫 업로드
02:55:04.095 [Onikiri] Boot apply #1 - 로컬이 정본 - 업로드 대기 (업로드는 4단계) (방치 보상 1회)
02:55:06.598 urgent 동기화 완료 (rev 1)
02:57:29.475 주기 동기화 완료 (rev 2)
```

사이드카(170초 뒤): `ownerUid=4N5sjv…` · `baseRevision 2` · `deviceId 495ba2c3…`. **`PERMISSION_DENIED` 0건 · 인수 확인 0회.**

- **곧바로 재실행(이전 실행 미release, heartbeat 수십 초 전 — 70단계에서 인수 확인이 뜨던 조건)**: 인수 확인 없이 진입, `같은 기록 - 로컬 사용 (서버 rev 2)` → `서버 사슬 확정: rev 2 (로컬 저장 완료 후)`. 기기 id `495ba2…` 그대로. `PERMISSION_DENIED` 0 · 인수 확인 0.

### ② 앱 삭제 → 재설치

`adb uninstall` → `adb install`. **첫 실행 전** `…/files` 디렉터리 자체가 없음 → 첫 실행 뒤 `onikiri_save.json`(새 게임) 하나뿐, 사이드카·`.prerecover` 없음. 부팅 `서버 확인 없음 - 로컬 사용` · `Boot apply #1 (방치 보상 0회)` · 인트로 첫 화면(`docs/media/step71_reinstall_fresh.png`). **되살아난 것 없음.**

**구글 로그인 Recover는 이번에 실기로 돌리지 않았다**(사용자 결정). QA uid에 구글을 미리 연동해야 하고, 폰의 구글 계정이 운영 uid에 연동된 계정이면 Recover가 운영 계정으로 가기 때문이다. 61단계에서 Recover 실기는 이미 통과했고, 이번 변경이 그 경로에 준 영향(③④가 `CreateOwnedBy`/`NewOwnedBy`)은 EditMode `CloudSaveRecoveryTests`(무회귀)가 덮는다.

⚠️ 재설치로 QA 익명 uid `4N5sjv…`의 로그인은 사라졌다(서버 문서 rev 2와 세션 문서는 남음, 지우지 않음). 지금 폰은 새 익명 uid로 첫 화면에 있다. 70단계 상태의 세이브·사이드카 원본은 `scratchpad/s71/before/`에 있다.

## 7. 남은 것

1. **구글 Recover 실기(②의 나머지)** — QA 전용 구글 계정이 필요하다(운영 연동 계정 금지). 순서: QA uid에 연동 → 삭제·재설치 → 구글 로그인 → AlreadyInUse → Recover.
2. **기기 간 직접 전송 실기** — 두 번째 Android 12+ 폰이 필요해 미측정. 규칙 파일이 APK에 들어간 것까지만 확인.
3. 과도기 1회 인수 확인(§6①) — 70단계 빌드를 설치했던 기기만 해당한다(QA 폰뿐, 출시 전). 조치 없음.
4. 70단계 메모의 나머지(`AssetPackManager` ClassNotFound · "레벨업 n" 넘침 · 인수 팝업 여백)는 이번 범위 밖.
5. **출시 전 교체 항목 없음.** 이번 조치는 임시 우회가 아니다.

---

# 71.1단계 — 옛 버전 클라우드 세이브가 "손상"으로 막히던 결함 + 복구 직후 자기 회수

- **브랜치** `feature/step71-1-legacy-state-hash` (71단계 `0b48b4d` 위) · 커밋 1회 · push 승인 대기
- **발단** 71단계 실기 뒤 사용자가 Flip에서 구글 로그인 → 운영 계정으로 Recover → **랭킹 44층인데 세이브는 새 게임**
- **수치** EditMode **1092/1092**(신규 `CloudLegacyStateHashTests` 11) · PlayMode **78/78** · Emulator **73/73**(규칙 무수정)

## 8.0 한 줄 요약

서버 세이브는 멀쩡했다(v21 · rev 1243 · payload 해시 일치). 검증이 payload를 **v23 클래스로 읽고 다시 직렬화**해 상태 지문을 냈고, v22·v23의 새 필드가 붙어(2570 → 2607자) 올린 쪽 지문과 영영 안 맞아 `StateHashMismatch → Blocked`. **66단계(v22)부터 업데이트를 받은 모든 사용자의 옛 클라우드 세이브가 같은 이유로 막히는 출시 차단 결함**이었다. 지문을 payload **원문**에서 계산하도록 바꿨고, 실기에서 운영 계정 43층이 충돌 화면 → 클라우드 선택 → 복원 → rev 1244 업로드까지 돌았다.

## 8.1 증거 (운영 문서 읽기 전용)

| | 값 |
|---|---|
| `playerSaves/A9aMg…` | saveVersion **21** · rev 1243 · 43층 · Lv.65 · 보석 8717 · 2026-09-10 업로드 |
| payload 해시 재계산 | `5fdc2362…` = 서버 값 ✓ (payload 온전) |
| 서버의 상태 지문 | `80d2fdb2…` |
| 예전 검증(재직렬화) | `eca4f8a8…` ✗ → Blocked |
| **원문에서 시간성 3필드만 0으로** | `80d2fdb2…` ✓ 정확히 일치 |

실기 로그(03:42, 71단계 빌드): `서버 정본을 쓸 수 없습니다: StateHashMismatch` → `복구 세이브 판정 = Blocked` → `병합 - … 복구된 문서 44 -> 44`(랭킹은 별개 문서라 정상 복구). Blocked는 업로드도 막으므로 **운영 세이브는 덮이지 않았다**(03:42 이후 동기화 0건).

## 8.2 고친 것

| 파일 | 내용 |
|---|---|
| `Cloud/CloudSaveFingerprint.cs` | `StateHashOfPayload` = **원문**에서 `lastQuitUtcTicks`→`0`, `goldPerSecond`/`expPerSecond`→`0.0`(JsonUtility가 쓰던 글자 그대로) 후 해시. `StateHashOf(data)`도 이것을 지나 **정의가 하나**. 같은 키 둘·숫자 아님 → null(실패로 읽는다), 키 없음 → 옛 버전으로 정상 |
| `Cloud/CloudSaveEnvelope.cs` | `Validate`의 상태 지문 대조가 `StateHashOfPayload(payload)` |
| `Cloud/CloudSaveSession.cs` | 자리를 쥔 uid(`seatUid`) 기록 · `HoldsSeatFor(uid)` · `BeginNewRun`/Reset이 비움 · 검사 seam 인자 |
| `Cloud/CloudSaveSessionWatch.cs` | uid를 읽은 뒤 `HoldsSeatFor(uid)`가 아니면 폴링 안 함 |
| `Tests/EditMode/CloudLegacyStateHashTests.cs` + `Fixtures/cloud_payload_v21_rev1243.json` | 신규 11. 고정값은 **운영 payload 원문**(해시 `5fdc2362…` 바이트 일치) — 실제 v21 클라가 계산한 두 지문을 못 박는다 |

**v23 문서 무회귀**: 지금 버전에서는 새 정의가 예전 정의(`Serialize(Normalize(d))`)와 같은 값을 낸다 — 0·큰 정수·지수 표기가 나올 큰 수·소수, 그리고 실제 v21을 v23으로 올린 세이브까지 대조. 이미 서버에 있는 v23 문서의 지문은 그대로 유효하다.

**판정은 바뀌지 않았다**: v21 서버 vs v23 로컬은 같은 게임이라도 지문이 다르므로 InSync가 아니라 Conflict/Download로 간다(사람이 고르거나 사슬이 설명하는 쪽). 클라우드를 고르면 `SaveData.Migrate`로 v23이 되어 적용되고, 다음 커밋이 v23으로 올린다.

**복구 직후 자기 회수(결함 2)**: 실기 `다른 기기가 세션을 인수했습니다 (세대 1 -> 20)` — 1은 버린 익명 uid의 세대, 20은 복구한 계정의 세대. 아무도 인수하지 않았는데 감시가 옛 uid의 자리로 새 uid의 세션 문서를 판정해 스스로를 회수했다. 이제 감시는 **그 uid의 자리를 쥔 때만** 판정한다(63단계 Busy 계약 = `HasHeldSeat` 유지). 새 uid의 자리는 첫 커밋의 Acquire가 잡는다.

## 8.3 실기 (Z Flip 3 · 운영 계정 · 사용자 승인 "2번으로")

```
04:52:05 기존 로그인 재사용. uid = A9aMgx…
04:52:07 부팅 선택: 두 곳이 갈라짐 - 로컬로 진입, 자동 쓰기 없음       ← 예전: StateHashMismatch → Blocked
   설정: "기록 선택 필요" → 충돌 화면: 현재 기기 1층·Lv.1·보석 0 / 클라우드 43층·Lv.65·보석 8717 (9월 10일 22:36)
04:53:17 클라우드 적용 전 로컬 백업 (3벌 순환): …precloud.1
04:53:17 충돌 해결: 클라우드 기록 사용 (rev 1243)
04:53:17 Boot apply #1 - 로컬이 정본 - 업로드 대기 (방치 보상 1회)
04:54:07 주기 동기화 완료 (rev 1244)
```

- 화면: Lv.65 · 보석 8,717 · 공격력 Lv.1742 · 방치 8시간(상한) 보상 (`docs/media/step71_1_restored_43f.png`, 충돌 화면 `step71_1_conflict_v21_cloud.png`)
- 서버: `playerSaves` **rev 1244 · saveVersion 23** · 43층 · 기기 `e6d020…`(Flip) / `playerSaveBackups` = rev 1243 · v21 그대로
- `PERMISSION_DENIED` 0 · `Mismatch` 0 · 인수 확인 0 · 자기 회수 0
- 재부팅 판정이 `UploadLocal`인 것은 정상: 사슬은 rev 1243(v21 지문)에 섰고 로컬은 v23으로 올라간 같은 진행이라 지문이 다르다 → 서버는 안 움직였고 로컬만 "변했다" → v23으로 한 번 올림. 충돌 반복 없음.

## 8.4 남은 것 · 주의

1. **운영 계정이 이제 v23이다.** v22 이전 빌드가 깔린 기기(Note 20 등)에서는 미래 버전 차단으로 그 계정 세이브가 막힌다 — 그 기기는 새 빌드로 올려야 한다. 운영 계정 세션 자리도 Flip(세대 21+)에 있어, 다른 기기를 켜면 인수 확인이 한 번 뜬다(정상).
2. **충돌 화면 글자 겹침** — "클라우드 기록" 제목과 "최고 43층" 요약이 겹친다(스크린샷). UI 잔챙이 묶음으로.
3. 복원 직후 방치 보상이 9월 10일부터의 8시간 상한으로 나갔다 — 59단계 설계(채택한 세이브 기준 1회)대로다. 장기 미접속 계정 복원에서 의도한 값인지는 밸런스 판단으로 남긴다.
4. 고정값 파일에 운영 계정의 진행·이름("랑무사")이 들어 있다(개인 저장소). 빼야 하면 합성 v21 고정값으로 바꿀 수 있지만, 그러면 "실제 v21 클라이언트가 계산한 지문"이라는 증거력을 잃는다.
5. 구글 Recover의 **충돌 없는 갈래**(③④)는 실기 미측정 — 이번 실기는 ⑤였다.
