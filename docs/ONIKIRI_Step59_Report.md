# ONIKIRI 59단계 — 크로스 저장 3단계 (부팅 선택 · 단일 Apply · 방치 보상 1회)

> 57단계가 판정표를, 58단계가 저장소·세션·규칙을 세웠다. **아직 아무도 부르지 않았다.**
> 이 스텝이 배선한다 — 그리고 배선의 알맹이는 클래스가 아니라 **순서**다.
>
> 예전: `GameSession.Start` → 즉시 Apply → (클라우드를 나중에 얹으면) **방치 보상 두 번**
> 지금: 읽기 → **고르기** → 고른 한 벌만 Apply 1회 → 보상 1회 → 즉시 저장

---

## 0. 한 줄 요약

부팅이 로컬과 클라우드 중 **하나를 먼저 고르고**, 고른 한 벌만 적용한다.
방치 보상을 지급하는 자리는 이제 코드 전체에 **하나**(`GameSession.ApplyBoot`)뿐이고,
클라우드를 채택한 부팅에서 **적용 1회 · 보상 1회 · 그 보상이 클라우드 기준**임을
PlayMode가 실측한다.

EditMode **870/870**(기존 856 + 신규 14), PlayMode **37/37**(기존 32 + 신규 5),
Emulator 규칙 **47/47 유지**. SaveData **v21 그대로**(새 필드 0), 씬·프리팹·밸런스·
귀문·리더보드 **무수정**, Firebase 운영 배포 **0**, 커밋·푸시 **없음**.

---

## 1. 변경 파일

| 파일 | 상태 | 역할 |
|---|---|---|
| `Scripts/Cloud/CloudSaveCoordinator.cs` | 신규 | 부팅 선택 · 상태기계 · 적용 전 로컬 백업 · 동기/비동기 두 경로 |
| `Scripts/Subsystems/GameSession.cs` | 수정 | `Start` 재구성 · `Apply`/`ApplyBoot` 분리 · 부팅 카운터 |
| `Tests/EditMode/CloudSaveBootTests.cs` | 신규 | 14개 (판정표 각 행 · 차단 · **지급 자리 단일화**) |
| `Tests/PlayMode/CloudSaveBootPlayTests.cs` | 신규 | 5개 (**실제 부팅에서 1회 실측**) |
| `Editor/OnikiriTestPanel.cs` | 확장 | "크로스 저장 (3단계 - 부팅 선택)" 절 |

**손대지 않은 것:** `SaveData`(v21) · `SaveSystem` · 씬 · 프리팹 · 인트로(`IntroFlow`/
`IntroPolicy`) · `CloudScores`(리더보드 큐 포함) · `AccountLink` · `firestore.rules` ·
밸런스 곡선 · 귀문(승급 5단계 폐쇄) · `CloudSaveStore`/`CloudSaveSession`(58단계 그대로).

---

## 2. 부팅 순서 [C-1]

```
로컬 JSON 읽기
  → sidecar 읽기 (uid가 다르면 없는 것으로 친다)
  → (서버를 볼 이유가 있으면, 최대 6초) 서버 확인
  → CloudSavePolicy 판정
  → 선택된 SaveData 한 벌 확정        ← 여기까지 아무것도 적용하지 않는다
  → GameSession.ApplyBoot 1회
  → 선택된 세이브 기준 방치 보상 1회
  → 즉시 로컬 저장
  → 게임 진입 (인트로 게이트가 IsLoaded를 본다)
```

**판정은 새로 짜지 않았다.** 57단계 `CloudSavePolicy.Decide`와 58단계
`CloudSaveStore.FetchAsync`를 부르기만 한다. `ChooseFrom`은 파일도 네트워크도
없는 순수 함수라, EditMode가 판정표의 각 줄을 그대로 지난다.

### ⚠️ 경로가 둘인 이유 — 한 프레임이 실제로 문제였다

처음에는 `Start`를 통째로 코루틴으로 바꿨다. 그러자 귀문 PlayMode 검사 **다섯**이
깨졌다("180초가 지나도 폐쇄되지 않았다", "적이 두 번도 안 때렸다"). 원인은 밸런스가
아니라 타이밍이다 — 그 검사들은 세이브 오염을 막으려고 **첫 프레임에
`GameSession` 컴포넌트를 파괴**하는데, 부팅이 한 프레임 뒤로 밀리면 그 순간
코루틴이 함께 죽어 **세이브가 통째로 안 얹힌다.**

56단계 인트로도 같은 것을 가정하고 있다(`IntroFlow.Awake` 주석: "세이브
로드가 같은 프레임에 돈다").

그래서 갈래를 둘로 뒀다:

| 조건 | 경로 | 비용 |
|---|---|---|
| 서버를 볼 이유가 없다 (기본) | `ChooseLocally` — **같은 프레임에 끝난다** | 0 프레임 (59단계 이전과 동일) |
| 서버를 봐야 한다 | `ChooseBootSave` 코루틴 | 응답까지, 최대 6초 |

두 경로가 같은 뒷정리(`Finish`)를 지난다 — 백업·sidecar 채택·상태·로그가 갈리지 않는다.

---

## 3. Apply 경로 단일화 [C-2] ★

```
Apply(SaveData)          시스템 복원만. 검사·프리셋이 여러 번 부른다
ApplyBoot(SaveData, 이유) 복원 + **방치 보상**. 부팅·디스크 재적용만 부른다
```

59단계 전에는 `Apply` 끝에서 보상을 지급했다. 그 자리에 두면 **이 함수를 부르는
모든 경로가 지급 경로**가 된다 — 프리셋 검사가 세이브를 갈아끼울 때마다,
그리고 클라우드를 나중에 덮을 때마다.

증명 방식 셋:

1. **소스 검사**(EditMode `OnlyOnePlaceGrantsTheOfflineReward`): `GameSession.cs`에서
   `GrantOfflineReward(` 등장 횟수가 **정확히 2**(정의 1 + 호출 1). 세 번째 자리가
   생기면 그날 실패한다.
2. **두 번째 부팅 거부**: `ApplyBoot`은 `bootApplied` 플래그로 두 번째 호출을
   거부하고 경고를 남긴다.
3. **실측 카운터**: `BootApplyCount` · `OfflineRewardGrants`가 노출돼 PlayMode와
   테스트 패널이 직접 읽는다.

---

## 4. 방치 보상 정확히 1회 — 실측 ★★

PlayMode `CloudSaveBootPlayTests`가 만드는 상황:

```
로컬   3시간 전 종료 · 초당 100 · 12층
sidecar 서버 rev 5까지 동기화, 그 뒤 논 적 없음
서버   rev 6 · 171층 · Lv.48 · 초당 1000     ← 다른 기기가 앞서 갔다
```

실제 로그(테스트 실행 중 콘솔):

```
[CloudSave] 클라우드 적용 전 로컬 백업: ...\onikiri_save.json.precloud.1
[CloudSave] 부팅 선택: 클라우드 채택 (rev 6, 171층 · Lv.48)
[Onikiri] Boot apply #1 - 클라우드 채택 (rev 6, 171층 · Lv.48) (방치 보상 1회)
```

| 검사 | 결과 |
|---|---|
| `BootApplyCount == 1` | 통과 |
| `OfflineRewardGrants == 1` | 통과 |
| 지급액이 **클라우드 초당 수입** 기준 (로컬 기준의 10배) | 통과 |
| 씬의 `StageProgress.MaxStageReached == 171` | 통과 |
| 덮이기 전 로컬 백업이 남았고 그 내용이 **로컬**(12층) | 통과 |
| 부팅 직후 디스크의 정본이 채택된 한 벌 | 통과 |

**금액 하나가 순서 전체를 증언한다.** 로컬 기준으로 먼저 한 번 나갔다면 값이
1/10이거나, 두 번 나갔다면 합계가 더 크다.

---

## 5. 판정표(설계 §6.1) — 각 행이 실제로 가는 곳

| 상황 | 결정 | 상태 | 적용되는 것 |
|---|---|---|---|
| 서버 확인 못 함(오프라인·미로그인·시간 초과) | `LocalOnly` | `LocalOnly` | 로컬 |
| 서버에 문서 없음 | `UploadLocal` | `Dirty` | 로컬 (업로드는 4단계) |
| 상태 지문 동일 | `InSync` | `InSync` | 로컬 (+ sidecar가 서버 revision 채택) |
| 서버 rev↑ + 로컬 무변화 | `DownloadCloud` | `InSync` | **클라우드** (+ 적용 전 로컬 백업) |
| 서버 rev = base + 로컬 변경 | `UploadLocal` | `Dirty` | 로컬 |
| 서버 rev↑ + 로컬도 변경 | `Conflict` | `Conflict` | **로컬** — 자동 쓰기 없음 |
| sidecar 없음 + 두 기록이 다름 | `Conflict` | `Conflict` | 로컬 |
| 클라우드 saveVersion > 앱 | `Blocked/FutureSaveVersion` | `Blocked` | 로컬 |
| 서버 rev < base (롤백) | `Blocked/ServerRollback` | `Blocked` | 로컬 |
| 서버 문서 손상(해시 불일치 등) | `Blocked/CorruptPayload` | `Blocked` | 로컬 |
| 내려받기인데 payload를 못 읽음 | `Blocked/CorruptPayload` | `Blocked` | 로컬 |

- **차단도 게임에 들어간다.** 적용·업로드만 금지이고, 로컬로 계속 논다 —
  "진행이 사라졌다"와 "게임이 안 켜진다"는 사람에게 전혀 다른 문제다(56단계 규칙).
- **시계로 정하지 않는다**: 로컬이 더 최근에 저장됐어도 서버 revision이 앞서면
  클라우드가 정본이다(`TheNewerClockNeverWinsOverTheRevisionChain`).
- 옛 버전의 클라우드 payload는 적용 전에 `SaveData.Migrate`를 지난다 — 로컬 파일이
  지나는 것과 같은 사슬이라 두 경로가 다른 답을 내지 않는다.

---

## 6. 상태기계 (이 스텝 범위)

```
Bootstrapping → LocalOnly | InSync | Dirty | Conflict | Blocked
```

`Uploading`과 충돌 **해결**은 4단계다. 이 스텝에서 충돌이 나면 상태만 남기고
로컬로 진입하며, **자동으로 어느 쪽도 쓰지 않는다**(EditMode
`TwoBranchesEnterLocallyAndWriteNothing`이 못 박는다).

클라우드를 채택할 때만 덮이기 직전의 로컬을 한 벌 남긴다
(`onikiri_save.json.precloud.1`). 순환 보관 3벌은 4단계다 — 지금 필요한 것은
"클라우드 채택이 되돌릴 수 없는 동작이 되지 않게 하는 것"이다.

---

## 7. 시간 제한과 오프라인

- 제한 시간 **6초**(`CloudSavePolicy.BootServerCheckSeconds`, 57단계에서 정한 값).
  근거는 값 자체가 아니라 **유한하다**는 것이다. 넘으면 로컬로 들어가고,
  그 뒤 서버가 돌아와도 전투 중에 핫스왑하지 않는다(4단계 충돌 화면의 일).
- **부팅 게이트는 여전히 세이브 준비 완료 하나뿐이다.** `IntroPolicy.CanEnter`에
  Firebase 인자를 넣지 않았고 `IntroFlow`도 무수정이다 — 56단계 계약 그대로이고,
  `IntroPolicyTests`가 그 구조를 계속 지킨다.
- 오프라인·미로그인·게스트에서 부팅은 **동기 경로**로 끝난다(0프레임). 그 상태가
  지금의 기본값이라, 이 스텝은 실기에서 부팅 시간을 한 프레임도 늘리지 않는다.

---

## 8-b. (후속) 규칙 운영 배포 — 2026-08-18, 사용자 승인

59단계 보고 뒤 Firebase MCP가 연결되어, 사용자 승인 하에 **규칙을 운영
프로젝트(onikiri-9cc18)에 배포했다.** 절차와 증거:

1. 배포 전 확인: 공식 검증기 통과("No errors detected") · 운영에는 4-B만 있음 ·
   git diff **190줄 추가 / 0줄 삭제**(`scores` 무변) · 운영 컬렉션은 `scores` 하나
2. `firebase deploy --only firestore` (MCP 경유) → **success**
3. 배포 후 되읽기: 서버의 규칙 = 저장소의 `firestore.rules` **그대로**
   (scores 4-B 포함 전문 일치). 인덱스도 함께 배포됐지만 파일 무변이라 no-op

클라이언트는 여전히 아무도 서버를 부르지 않으므로(`ServerCheckEnabled` 꺼짐,
`CommitAsync` 미호출) **사용자 눈에 보이는 변화는 0**이다. 달라진 것은 하나 —
이제 `ServerCheckEnabled`를 켜면 부팅 왕복이 거부가 아니라 실제 응답을 받는다.
켜는 시점은 4단계(쓰기 경로)와 함께 정한다.

## 8. 서버 검증 경로 [S3-5] — 스텝 당시의 판단 (권고: Emulator 유지)

`CloudSaveCoordinator.ServerCheckEnabled`를 **기본 꺼짐**으로 두었다. 근거 넷:

1. **아직 아무도 서버에 쓰지 않는다.** 이 스텝은 읽기만이고, 실제 쓰기 경로는
   4단계(자동 동기화)에서 붙는다. 쓰는 코드가 없는데 규칙을 여는 것은 이르다.
2. **규칙이 배포되지 않은 지금 켜면 부팅마다 실패하는 왕복이 하나씩 나간다.**
   현재 배포된 규칙의 catch-all이 `playerSaves`를 닫고 있어 반드시 거부되고,
   그 로그가 쌓이면 진짜 실패와 구분되지 않는다.
3. **규칙 배포는 되돌리기 어려운 외부 상태 변경**이고, 대상은 라이브 프로젝트
   (onikiri-9cc18)다. `scores` 4-B 무변경은 Emulator 회귀 4개로 이미 확인했지만,
   배포 시점은 사람이 정할 일이다.
4. 실기 왕복이 정말 필요한 시점은 **6단계(두 기기 실측 + App Check)**로 이미
   배정돼 있다.

권고 시점: **4단계에서 쓰기 경로가 붙을 때 규칙 배포 + `ServerCheckEnabled` 켜기**
(사용자 승인 후). 켜는 일은 값 하나이고, 테스트 패널 3단계 절에 토글이 있다.

---

## 9. 테스트

| 묶음 | 수 | 내용 |
|---|---|---|
| EditMode `CloudSaveBootTests` | 14 | 판정표 각 행 · 차단 넷 · 시계 무시 · fallback · **지급 자리 단일화** |
| PlayMode `CloudSaveBootPlayTests` | 5 | 적용 1회 · 보상 1회 · 클라우드 기준 지급 · 적용 전 백업 · 부팅 직후 디스크 |
| EditMode 전량 | **870/870** | 기존 856 + 14 |
| PlayMode 전량 | **37/37** | 기존 32 + 5 (귀문 검사 포함 전부 통과) |
| Emulator 규칙 | **47/47** | 이 스텝에서 `firestore.rules` 무수정 |

실사용 세이브는 실행 전후로 **바이트 동일**을 확인했고, 부산물(`precloud`·sidecar)도
남지 않았다.

---

## 10. 막힌 점 · 남은 것

- **실서버 왕복은 이 스텝에서 한 번도 하지 않았다.** 의도한 범위이고(8절),
  그래서 "규칙 배포 후 실제로 어떤 지연이 나는가"는 아직 모른다. 6초라는 상한만
  정해져 있다.
- **PlayMode 회귀 하나를 밟았다가 고쳤다**(2절). 부팅을 코루틴으로 미루는 것이
  귀문 검사 다섯을 깨뜨렸고, 원인은 "첫 프레임에 GameSession을 파괴하는" 그
  검사들의 방어 코드였다. 동기 경로를 되살려 해결했지만, **비동기 경로에서는
  같은 취약점이 그대로 있다** — 서버 확인을 켜는 단계에서 그 검사들이 세이브를
  어떻게 지킬지 다시 봐야 한다(지금은 `ServerCheckEnabled`가 꺼져 있어 안 밟는다).
- 테스트가 uid·서버 응답을 주입하는 seam 둘(`UseFetchForTests`·`UseIdentityForTests`)이
  런타임 코드에 있다. 4단계에서 코디네이터가 세션·업로드까지 들면 이 자리를
  한 번 정리하는 편이 좋다.

## 11. 다음 — 4단계

자동 동기화(120초 debounce · urgent sync · pause release) + **충돌 선택 UI**
(두 브랜치 비교 · 선택 · 백업 · 씬 재로드) + 로컬 3벌/클라우드 1벌 백업 +
전투 중 hot swap 금지. 그 스텝이 `Uploading` 전이와 `CloudSaveStore.CommitAsync`를
처음으로 실제 호출한다.
