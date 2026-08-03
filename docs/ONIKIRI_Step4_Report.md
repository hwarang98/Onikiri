# ONIKIRI — 4단계 작업 보고서

> 대상: `docs/ONIKIRI_MVP_handoff.md` §7 개발 순서 4단계 + 자동 공격·참격·히트스톱 + 색 그레이드
> 2026-08-04 · Unity 6 (6000.5.2f1) · Universal 2D (URP) · Portrait 고정 · Android
> 상태: **완료** (5단계 이후 미착수)
> 선행 보고서: `ONIKIRI_Step3_Report.md`

---

## 1. 요약

이번 세션의 핵심 목표는 **"때리는 느낌이 나는가"** 였습니다. 스폰만 되고 타격감이 없으면 실패로 간주하기로 했고, 70ms짜리 타격 창을 눈으로 확인하기 위해 프레임 그래버를 만들어 총 5회 녹화하며 조정했습니다.

작업 중 **버그 8건을 발견해 수정**했고, 그중 3건은 사용자가 스크린샷을 보고 지적한 것입니다.

| 커밋 | 내용 |
|---|---|
| `9d25281` | 4단계: 요괴 스폰 + 자동 공격 + 참격 VFX + 히트스톱 |
| `4e8d268` | 색 그레이드 + 전투 연출 정리 |

브랜치 `feature/step3-stage-setup`, 푸시 안 함. 테스트 **59개 통과**.

---

## 2. 선행 정리 3건

### 2.1 SortingOrders 명시적 정의

`Scripts/Core/SortingOrders.cs` 한 곳에 모았습니다.

| 상수 | 값 | 대상 |
|---|---|---|
| `SkyFill` | -300 | 카메라 전체를 덮는 하늘 |
| `BackgroundBase` | -200 | 패럴랙스 레이어 (레이어당 +1) |
| `EnemyBase` | 0 | 적 (`EnemySlots` 40칸으로 z-fight 방지) |
| `Player` | 50 | 사무라이 |
| `GroundCover` | 70 | 풀 (Gras) |
| `Vfx` | 100 | 참격 |
| `DamageNumber` | 200 | 6단계 예약 |

**적을 플레이어 뒤에 둔 이유**: 시선이 따라가야 할 대상은 사무라이이고, 적은 큐로 정렬돼 겹치지 않으므로 가려지는 일이 실제로 없습니다.

### 2.2 Gras를 캐릭터 앞으로

풀이 사무라이 다리를 가로지르면서 "씬 위에 붙여놓은" 느낌이 사라지고 **씬 안에 서 있는** 것으로 읽힙니다. 3단계 보고서 주의사항 #5 해소.

### 2.3 하늘 타일링 — 클리어 색 의존 제거

기존에는 배경(180px) 위 여백을 카메라 클리어 색이 `Sky.png`와 우연히 같아서 메우고 있었습니다. **하늘에 뭘 그리는 순간 깨지는 방식**이었습니다.

`Sky`를 밴드 앵커에서 떼어내 별도 `SkyFill` 오브젝트로 만들고, `BattleStageLayout`이 매 프레임 카메라 전체 크기로 타일링합니다. 원본이 균일 톤이라 반복 이음매는 보이지 않습니다. 3단계 보고서 주의사항 #3 해소.

---

## 3. 4단계 본체

### 3.1 오브젝트 풀링

`Core/ObjectPool<T>` — prewarm 후 활성/비활성 토글로 재사용. **런타임 Instantiate/Destroy 0회**:

```
pooled instances under Enemies = 8  (prewarm 8)
active = 4   poolGrowth = 0   slashGrowth = 0
```

prewarm을 넘어서면 실패 대신 증식하되 `GrowthCount`로 **드러냅니다**. 조용히 커지면 프레임 히칭의 원인을 못 찾기 때문입니다.

### 3.2 스폰 → 이동 → 사거리 진입

`EnemySpawner`가 웨이브 타이머가 아니라 **목표 생존 수(4마리)를 유지**하는 방식입니다. 세로 화면 가로폭이 6.75 units뿐이라 사양서가 3~5마리로 제한한 만큼, 타이머로 쏟아붓고 기대하는 것보다 안정적입니다.

큐 위치는 매 프레임 살아있는 적을 x 순으로 정렬해 재계산합니다. 덕분에 **앞의 적이 죽으면 뒤가 자동으로 당겨집니다.**

### 3.3 SpriteAnimator (Animator 대신)

전투에는 상태 머신이 불편한 프레임 단위 제어가 필요했습니다:
- 공격 애니메이션의 **임팩트 시점** 통보
- 사망 애니메이션 **마지막 프레임에 풀 반환**
- 풀링된 오브젝트의 **재사용 시 깨끗한 재시작**

스케일 타임으로 돌려서 **히트스톱이 애니메이션까지 함께 얼립니다.** 이 정지가 타격감의 대부분입니다.

### 3.4 전투 파라미터

| 항목 | 값 |
|---|---|
| 사거리 | 2.0 units |
| 공격 속도 | 1.15/초 |
| 데미지 | 5 |
| 임팩트 시점 | 공격 애니메이션 **4/7** 지점 |
| 히트스톱 | 0.07초 |
| 큐 간격 / 선두 정지선 | 1.0 / x=0.05 |

---

## 4. 타격감 조정 — 녹화 기반

70ms 창은 수동 스크린샷으로 잡을 수 없어 `ImpactRecorder`(개발용)를 만들었습니다. 140프레임을 떠서 **12~15프레임이 히트스톱 중**임을 확인하고, 프레임 단위로 보며 세 가지를 고쳤습니다.

### 4.1 참격이 녹색이었다

팩 기본값 `color1`이 형광 녹색. 사양서 §1의 **먹빛·적·벚꽃 팔레트와 정면충돌**. → `color2`(적색) → 최종적으로 흰색(§5.2).

### 4.2 임팩트 타이밍이 어긋나 있었다

사무라이 아트에 **이미 흰 검격 궤적이 프레임 5~6에 그려져 있었습니다.** 그런데 제 임팩트는 45% 지점(프레임 3)에 터지고 있었습니다 — 궤적과 참격이 **다른 순간에** 일어나 따로 놀았습니다.

임팩트를 **4/7 지점**으로 옮겨, 검격 궤적 · 참격 VFX · 피격 플래시 · 프리즈가 **한 프레임에** 떨어지도록 했습니다.

### 4.3 히트스톱이 가장 약한 프레임에서 얼어붙었다

참격 9프레임 중 앞 5개는 가느다란 예비 동작, 뒤 4개가 굵은 호입니다. 히트스톱은 **표시 중인 프레임에서 멈추므로**, 정지 화면이 가장 흐릿한 프레임을 붙잡고 있었습니다. → **6~9번만 사용**해 가장 강한 프레임에서 시작하도록 변경.

---

## 5. 색 그레이드 (요청 반영)

### 5.1 깊이별 틴트

`BattleStageBuilder`의 상수 + `TintFor()`로 분리했습니다.

| 깊이 | 레이어 | 틴트 |
|---|---|---|
| 원경 | Sky, Clouds, Fuji | `#6E68A0` |
| 중경 | Mountain_Back/Middle/Front, BackgroundTrees, Trees | `#8B82B5` |
| 근경 | Shrine_Single, Ground, Gras | `#A89ECB` |
| 카메라 클리어 | — | `#2A2740` |

**캐릭터/적은 무틴트(`#FFFFFF`)** — 이 분리가 그레이드의 핵심입니다. 황혼 톤으로 내려가면서 벚꽃이 주인공과 경쟁하지 않게 됐습니다.

### 5.2 참격 흰색화

**팩에 흰색 변형이 없었습니다** (green / red / purple / orange / blue 5종).

적색 시트를 무채화해 `Assets/_Project/Art/VFX/Slash_White.png`를 생성했습니다. 처음엔 **휘도(luminance)로 변환했다가 실패**했습니다 — 순적색의 휘도는 0.3에 불과해 이펙트의 밝은 부분이 탁한 회색이 됐습니다. **max 채널** 방식으로 바꾸니 채도 높은 영역이 흰색으로 갑니다.

유채색 시트 5종은 손대지 않았으므로 **흰 → 적 → 금** 무기 등급 상승에 그대로 쓸 수 있습니다.

---

## 6. 적 스케일 재검토

Transform 스케일 0.74배는 픽셀 격자를 깨뜨리므로, **애초에 작은 아트로 교체**했습니다.

idle 프레임 기준 실측 (주인공 34px):

| 타입 | 크기 | 판정 |
|---|---|---|
| Inimig(1) 등롱 | 47×40 | +13px |
| Inimig(2) | 56×41 | +22px |
| Inimig(3) | 20×22 | OK |
| **Inimig(4) 히토다마** | **32×20** | **채택** |
| Inimig(6) | 22×33 | OK |
| Inimig(7) | 29×32 | OK |
| Inimig(8) | 20×17 | OK |

**Inimig(4) 히토다마(도깨비불)**를 잡몹으로 채택. 주인공보다 살짝 작고, 부유하는 요괴라 방치형 잡몹에 적합합니다. 등롱(초칭오바케)은 **상위 등급용으로 보존**.

클립은 팩 태그가 무명이라 눈으로 식별했습니다: `Tag`=부유(16f), `Tag_1`=사망(6f). 피격 클립이 없어 색 플래시로 대체되며, 이 크기에서는 충분히 읽힙니다.

---

## 7. 배치

사무라이 **x = -2.0 → -1.2** (오층탑 겹침 해소). 선두 정지선도 `frontLineX = 0.05`로 함께 옮겨 칼과 요괴 사이 간격을 유지했습니다.

---

## 8. 발견해 고친 버그 8건

### 8.1 Reapply 임포트 설정이 슬라이싱을 전부 파괴 (심각)

1단계부터 있던 지뢰를 이번에 밟았습니다. `Onikiri/Art/Reapply Pixel Art Import Settings`가 `spriteImportMode`를 무조건 `Single`로 되돌려 **사무라이 23시트 171스프라이트가 전부 삭제**되고 씬 참조가 끊겼습니다.

→ 이미 `Multiple`이면 보존. 슬라이싱된 시트의 피벗도 SpriteRect에 있으므로 함께 보존.

### 8.2 빈 그리드 셀이 프레임으로 잡힘

참격 시트는 5×2 격자지만 **9칸만 그려져 있습니다.** 마지막 빈 프레임이 이펙트 끝에 히칭으로 보였습니다. 처음엔 `sprite.triangles`로 감지하려 했으나 FullRect 메시는 절대 비지 않아 실패 → PNG를 직접 디코드해 투명 셀을 건너뛰도록 변경.

### 8.3 씬 로드 전 만든 참조가 조용히 null로 기록됨

`OpenScene` 이전에 만든 프리팹 참조가 리임포트로 무효화되고, **무효 오브젝트를 `SerializedProperty`에 대입하면 에러 없이 null이 기록됩니다.** 적이 아예 안 나왔습니다.

→ 씬 로드 **후** 경로에서 다시 로드. 추가로 **배선 검증 단계**(`VerifyWiring`)를 넣어 같은 실패가 조용히 지나가지 못하게 했습니다. "빌더가 에러 없이 돌았다"는 것만으로는 아무것도 증명하지 못하기 때문입니다.

### 8.4 적 피벗 상수가 성립 불가능한 전제였음

캔버스 하단에서 20px로 고정했는데, **요괴마다 캔버스 내 아트 위치가 다릅니다** (등롱 20px, 히토다마 34px). 상수 하나로는 불가능.

→ 상수 제거. 피벗은 캔버스 하단으로 통일하고, `EnemyDefinition.artBottomOffset`을 스프라이트 `bounds.min.y`에서 **자동 측정**. 측정값 1.0625 units = 정확히 34/32.

### 8.5 참격이 요괴 발밑 지면에 떨어짐

적 `transform` 기준으로 위치를 잡았는데 피벗이 캔버스 하단이라 실제 그림보다 한참 아래였습니다. → `Enemy.HitPoint`(렌더러 bounds 중심) 기준으로 변경.

### 8.6 Sky 사본이 리빌드마다 누적

배경 구성이 두 빌더에 나뉘어 있었습니다. 결합 빌더가 Sky를 재부모화하면 스테이지 빌더는 그걸 모르고 새로 만들어, **씬에 Sky가 4개**(3개는 무틴트) 쌓였습니다.

### 8.7 foreach 도중 SetParent로 레이어 하나가 건너뛰어짐

`foreach (Transform layer in background.transform)` 안에서 `SetParent`를 호출해 인덱스가 밀렸고, `Clouds`가 정렬 순서 배정을 받지 못했습니다 (-109 잔존 vs 나머지 -200대).

→ 8.6·8.7 모두 **배경 구성을 `BattleStageBuilder` 한 곳으로 일원화**해 해소.

### 8.8 녹화기가 무한 대기

`WaitForEndOfFrame`은 에디터가 포커스를 잃으면 Game 뷰가 리페인트되지 않아 **영원히 반환되지 않습니다.** → 카메라를 RenderTexture에 직접 `Render()`하도록 변경.

---

## 9. 생성·변경된 파일

```
Assets/_Project/
├─ Scripts/Core/
│  ├─ SortingOrders.cs        정렬 순서 단일 정의
│  ├─ ObjectPool.cs           prewarm 재사용 풀
│  └─ SpriteAnimator.cs       프레임 단위 스프라이트 재생
├─ Scripts/Battle/
│  ├─ HitStop.cs              unscaled 복구, 중첩은 연장
│  ├─ EnemyDefinition.cs      ScriptableObject (프레임/스탯/artBottomOffset)
│  ├─ Enemy.cs                접근/교전/피격/사망
│  ├─ EnemySpawner.cs         생존 수 유지 + 큐 정렬
│  ├─ SlashVfx.cs             풀링 참격
│  ├─ PlayerCombat.cs         자동 공격 + 임팩트
│  └─ ImpactRecorder.cs       개발용 프레임 그래버
├─ Editor/
│  ├─ BattleContentBuilder.cs 적/참격 프리팹 + 씬 배선 + 검증
│  ├─ BattleStageBuilder.cs   배경·틴트·하늘·사무라이 (일원화)
│  └─ CharacterSpriteSlicer.cs 빈 셀 스킵, 아트 중심 피벗 측정
├─ Art/VFX/Slash_White.png    생성 자산 (max 채널 무채화)
├─ Data/Enemy_Hitodama.asset
├─ Prefabs/{Enemy, SlashVfx}.prefab
└─ Tests/EditMode/ObjectPoolTests.cs   8개
```

메뉴: `Onikiri/Scene/Build Combat Content`, `Onikiri/Art/Slice Slash VFX (64px)`

---

## 10. 테스트

**59개 통과.** 이번 추가분은 `ObjectPoolTests` 8개:

| 테스트 | 검증 |
|---|---|
| `Prewarm_CreatesInstancesUpFrontAndLeavesThemInactive` | 사전 생성 + 비활성 시작 |
| `Get_ActivatesWithoutAllocatingWhilePrewarmLasts` | prewarm 내 무증식 |
| `Get_BeyondPrewarm_GrowsAndReportsIt` | 증식이 **드러나는지** |
| `Release_ReturnsTheSameInstanceOnNextGet` | 재사용 |
| `DoubleRelease_DoesNotHandOutTheSameInstanceTwice` | 이중 반환 방어 |
| **`SpawnerSizedPool_NeverGrowsUnderItsIntendedLoad`** | **4마리 유지 × 200웨이브에서 증식 0** |

마지막 항목이 "Instantiate/Destroy 금지" 요구사항을 못 박는 테스트입니다. prewarm이 부족하면 폰에서 프레임 히칭으로 나타나기 전에 테스트가 먼저 깨집니다.

---

## 11. 남은 주의사항

| # | 내용 | 영향 |
|---|---|---|
| 1 | **적이 공격하지 않음** — 사거리에 서서 맞기만 함. 플레이어 피격/체력은 사양서 MVP 범위 밖 | 설계상 의도 |
| 2 | **적 1종만 사용** — `EnemyDefinition`이 SO라 추가는 클립 3개 지정이면 끝. 다만 나머지 6종은 태그가 무명이라 **눈으로 클립 식별이 한 번씩 필요** | 5단계+ |
| 3 | **Thaleah 폰트 / RPG Essentials SFX 미확보** — Package Manager 로그인 필요, 제가 할 수 없음 | 6단계 |
| 4 | **Safe Area 미처리** — `renderOutsideSafeArea=true` 상태 | 7단계 |
| 5 | `ImpactRecorder`는 개발용 디버그 스크립트. 빼도 되고, 타격감 조정에 계속 써도 됨 | 선택 |
| 6 | **패럴랙스 스크롤 없음** — 배치만 | 연출 확장 시 |
| 7 | 라이선스: CC-BY 크레딧, **Frostwindz 상업 라이선스 확인 필요** | 출시 전 |

`Assets/Screenshots/`는 `.gitignore`에 있어 커밋되지 않습니다. 리포에 남기려면 무시 항목에서 빼면 됩니다.

---

## 12. 다음 단계

사양서 §7의 5단계 — 자동 공격은 이미 동작하므로 **피격/사망 처리 + 골드 획득**이 남습니다. 골드는 1~2단계에서 만든 `BigDouble`을 그대로 쓰면 되고, `Enemy.Died` 이벤트에 보상 지급을 붙이는 지점이 이미 열려 있습니다.

그 다음 6단계(데미지 숫자)는 `SortingOrders.DamageNumber`(200)와 `ObjectPool`이 준비돼 있으나, **Thaleah 폰트가 있어야** 시작할 수 있습니다.
