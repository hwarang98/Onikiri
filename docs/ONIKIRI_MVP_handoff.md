# ONIKIRI (귀참 키우기) — MVP 개발 핸드오프 사양서

> Claude Code에 그대로 전달하는 프로젝트 브리프
> 2026-08-02 · Unity 6 (6000.5.2f1) · Universal 2D (URP) · **세로 고정(Portrait)**

---

## 1. 프로젝트 개요

**ONIKIRI(鬼斬) / 한국명 "귀참 키우기"** — 2D 픽셀 사이드뷰 **방치형 키우기(idle RPG)**.
요괴에게 모든 걸 잃은 로닌이 몰려오는 요괴를 **발도(居合) 참격**으로 자동 처치하며 무한 성장한다.

- 플랫폼: 모바일(Android/iOS) F2P 우선 → 이후 스팀 프리미엄
- 엔진: Unity 6 (6000.5.2f1), Universal 2D 템플릿(URP)
- **화면: 세로 고정 Portrait, 기준 해상도 1080×1920**
- 아트: 2D 픽셀(32~48px 캐릭터), 먹빛·적·벚꽃 팔레트

---

## 2. 화면 사양 (세로 고정) ⭐

### Player Settings 필수 설정
- Resolution and Presentation → **Default Orientation: Portrait**
- Auto Rotation 비활성 (Portrait만 허용, Upside Down 해제 권장)
- 기준 해상도 1080×1920 (9:16), 대응 범위 9:16 ~ 9:21 (최신 폰 롱스크린)

### 화면 레이아웃 (위→아래)

| 영역 | 비율 | 내용 |
|---|---|---|
| 상단 바 | ~10% | 재화(금·보석), 스테이지/챕터 정보, 설정 버튼 |
| 전투 영역 | ~45% | 배경(패럴랙스) + 좌측 사무라이 / 우측에서 오는 요괴 + 참격 VFX + 데미지 숫자 |
| 성장 패널 | ~35% | 스크롤 업그레이드 리스트 (방치형의 메인 화면) |
| 하단 탭바 | ~10% | 성장 / 장비 / 스킬 / 전직 / 상점 |

### 카메라 / 픽셀 설정
- Orthographic 카메라 + **Pixel Perfect Camera** 컴포넌트
- Reference Resolution: 가로 기준(예: 270×480 또는 360×640) — PPU와 함께 조정
- Sprite Import: Filter Mode = **Point (no filter)**, Compression = None
- Canvas: Screen Space - Overlay, **CanvasScaler = Scale With Screen Size, Reference 1080×1920, Match = 0.5 (또는 width 우선 1)**

### 주의사항 (실제로 걸리는 것들)
- **배경 에셋은 가로(16:9) 기준** → 세로 전투 영역(약 1080×860)에 맞게 크롭/스케일 필요
- 세로는 가로 폭이 좁음 → 화면에 동시에 보이는 적은 **3~5마리** 수준으로 설계
- 적은 우측 화면 밖에서 스폰 → 좌측 플레이어 쪽으로 이동 → 사거리 진입 시 전투
- 안전 영역(Safe Area) 처리: 노치/홈 인디케이터 대응 (상단 바·하단 탭바에 SafeArea 스크립트)

---

## 3. MVP 범위 (1단계 버티컬 슬라이스)

**목표: "10분 해보고 또 하고 싶은가?"를 검증한다.** 콘텐츠 양산 금지, 루프만 완성.

### 포함 (이것만)
1. **자동 전투**: 사무라이가 사거리 내 요괴를 자동 공격 (공격속도 쿨다운)
2. **발도 참격 연출**: 타격 시 참격 VFX + 히트스톱(짧은 정지) + 화면 흔들림 약간
3. **데미지 숫자**: 타격마다 숫자 팝업 (오브젝트 풀링, Thaleah 폰트)
4. **요괴 스폰**: 웨이브로 우측에서 등장, 체력/피격/사망 처리
5. **재화**: 처치 시 골드 획득
6. **성장 축 1개**: 골드로 "공격력 강화" 업그레이드 (비용 지수 증가)
7. **세이브/로드**: 골드·업그레이드 레벨·마지막 접속 시각 저장
8. **방치(오프라인) 보상**: 재접속 시 경과 시간만큼 골드 지급 + 팝업

### 제외 (MVP 이후)
전직/캐릭터 교체, 장비, 스킬, 가챠, 광고, 배틀패스, 다중 스테이지, 보스, BGM 다양화

---

## 4. 기술 요구사항

### BigNumber (최우선, 구조부터)
- 방치형은 데미지·재화가 `long` 범위를 초과 → **처음부터 큰 수 타입 사용**
- 권장: `BreakInfinity.cs`(무료, MIT) 또는 자체 `BigDouble`(가수부+지수부) 구현
- 표기: 1.5K, 3.2M, 7.8B, 1.2aa … 포맷터 유틸 필수
- ⚠️ 나중에 바꾸면 전체를 갈아엎어야 하므로 **1일차에 결정**

### 세이브
- MVP는 `PlayerPrefs` + JSON 직렬화로 충분 (추후 파일/암호화로 승격)
- **저장 항목**: 골드, 업그레이드 레벨, `lastQuitTimeUtc` (DateTime.UtcNow.Ticks)
- 오프라인 보상: `(현재시각 - lastQuitTime)` 초 × 초당골드 × 방치효율(예: 0.5), 상한 캡(예: 8시간)
- `OnApplicationPause` / `OnApplicationQuit` 양쪽에서 저장 (모바일은 Pause가 실제 종료)

### 성능 (모바일)
- 데미지 숫자·요괴·VFX는 **오브젝트 풀링** 필수 (Instantiate/Destroy 금지)
- 스프라이트 아틀라스로 드로우콜 절감
- Application.targetFrameRate = 60 (배터리 고려 시 30 옵션)

---

## 5. 스크립트 구조 (제안)

```
Assets/
├─ _Project/
│  ├─ Scripts/
│  │  ├─ Core/
│  │  │  ├─ BigNumber/        (BreakInfinity 또는 자체 구현)
│  │  │  ├─ NumberFormatter.cs
│  │  │  ├─ GameManager.cs     (전역 상태, 초기화 순서)
│  │  │  └─ ObjectPool.cs
│  │  ├─ Save/
│  │  │  ├─ SaveData.cs        (직렬화 모델)
│  │  │  ├─ SaveSystem.cs
│  │  │  └─ OfflineReward.cs   (방치 보상 계산)
│  │  ├─ Battle/
│  │  │  ├─ PlayerCombat.cs    (자동 공격, 공격력 참조)
│  │  │  ├─ Enemy.cs           (체력, 피격, 사망, 보상)
│  │  │  ├─ EnemySpawner.cs    (웨이브 스폰)
│  │  │  ├─ SlashVFX.cs        (참격 이펙트 재생)
│  │  │  └─ HitStop.cs         (타임스케일 순간 정지)
│  │  ├─ Progression/
│  │  │  ├─ PlayerStats.cs     (공격력 등 스탯 산출)
│  │  │  └─ UpgradeSystem.cs   (업그레이드 레벨/비용/효과)
│  │  └─ UI/
│  │     ├─ DamageNumber.cs    (풀링 대상)
│  │     ├─ HUDCurrency.cs     (상단 재화 표시)
│  │     ├─ UpgradePanel.cs    (스크롤 리스트)
│  │     ├─ OfflineRewardPopup.cs
│  │     └─ SafeAreaFitter.cs  (노치 대응)
│  ├─ Prefabs/
│  ├─ Scenes/  (Main.unity)
│  └─ Data/    (ScriptableObject: EnemyData, UpgradeData)
└─ (임포트한 서드파티 에셋들은 최상위 유지)
```

**데이터는 ScriptableObject로**: 요괴 스탯, 업그레이드 곡선을 코드가 아닌 에셋으로 → 밸런싱이 쉬워짐.

---

## 6. 보유 에셋 (임포트 완료)

경로: `C:\Users\ASUS\Desktop\UnityAssets`

| 용도 | 에셋 |
|---|---|
| 주인공 | Mattz Art — Samurai 2D Pixel Art (32×32, idle/attack 애니) |
| 적(요괴) | pozac — Feudal Japan Enemies (8종 + 다크 사무라이 보스, 애니 포함) |
| 배경 | Tiny Pixel Japan Parallax Background |
| 참격 VFX | Frostwindz — Pixel Art Slashes (15종) |
| UI | Kenney — Pixel UI Pack (CC0) |
| 효과음 | RPG Essentials Sound Effects (무료) |
| 폰트 | Free Pixel Font - Thaleah (데미지 숫자용) |

⚠️ 팩마다 픽셀 스케일이 다름 → **PPU(Pixels Per Unit)와 피벗을 통일**해서 화면상 키를 맞출 것.
⚠️ 라이선스: CC-BY 항목은 크레딧 화면에 출처 표기 필요. Frostwindz는 상업 라이선스 확인 필요.

---

## 7. 개발 순서 (권장)

1. 프로젝트 세팅: Portrait 고정, Pixel Perfect Camera, Canvas Scaler, 폴더 구조
2. **BigNumber 도입 + NumberFormatter** (가장 먼저!)
3. 배경 배치(세로 크롭) + 사무라이 배치 + idle 애니
4. 요괴 스폰 → 이동 → 사거리 진입
5. 자동 공격 + 피격/사망 + 골드 획득
6. 참격 VFX + 히트스톱 + 데미지 숫자(풀링)
7. 상단 재화 HUD + 업그레이드 패널(1종) + 비용 곡선
8. 세이브/로드 + 오프라인 보상 팝업
9. **플레이 테스트**: 10분 돌려보고 재미 판단 → 이후 성장 축 확장

---

## 8. 다음 단계 (MVP 검증 이후)

전직(캐릭터 통째 교체 + 각성 연출) → 장비 강화 → 스킬(액티브/패시브) → 다중 스테이지·보스 → 수익화(배틀패스·가챠·리워드 광고) → 스팀 프리미엄 버전(광고 제거)

전체 기획은 Notion "키우기" 페이지 참조.
