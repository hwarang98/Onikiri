# 슬레이어 키우기 600.9.4 정적 분석과 귀참키우기 적용안

분석 기준일: 2026-08-14  
분석 대상: `Slayer+Legend+_+Idle+RPG_600.9.4_APKPure.xapk`  
적용 대상: ONIKIRI(귀참키우기), Step 56 완료 시점

---

## 0. 결론부터

이 빌드에서 가장 가치 있는 것은 특정 스킬·가챠·미니게임 하나가 아니다.
**작은 자동전투 코어 위에 새 성장축, 전투 모드, 시즌 콘텐츠를 반복해서 얹을 수
있는 데이터/보상/해금/패키징 구조**다.

귀참키우기는 현재 다음 부분이 이미 강하다.

- 스테이지 전투와 보스 실패 피드백
- `BigDouble` 기반 장기 수치 곡선
- 성장축을 하나씩 무력화해 기여도를 재는 `StageSimulation`
- 오의 8종/4슬롯, 장비 2슬롯, 동료 3종, 요도 4+전설 2종의 상호작용
- v19 세이브 마이그레이션, 오프라인 보상, 퀘스트, 뽑기 천장
- Firebase 익명 로그인, 구글 연동, 도달층 리더보드
- Step 56 기준 EditMode 580/580 검증

반대로 슬레이어 키우기에서 먼저 가져와야 할 것은 아래 순서다.

1. **데이터 주도 카탈로그**: 하드코딩된 성장 표를 검증 가능한 외부 데이터로 전환
2. **공용 `ContentGate / RewardGroup / Currency / Schedule`**
3. **공용 전투 모드 템플릿**: 같은 전투를 규칙·입장권·웨이브·보상만 바꿔 재사용
4. **우편함 + 출석 + 콘텐츠 해금 예고**: 복귀/일일 루프의 최소 골격
5. **장비 인벤토리·제련**과 **프리셋**: 지금의 2슬롯 직선 성장을 선택으로 바꿈
6. **요괴 계약**: 펫·요도·영체를 별도 가챠 셋으로 늘리지 않고 하나의 세계관 축으로 통합
7. **시즌 이벤트 템플릿**: 영구 시스템이 안정된 뒤 하나만 만들고 반복 사용

길드, 광장, 복수의 시즌 전용 서브게임, 10종이 넘는 가챠, 수십 개의 패키지 팝업은
지금 가져오면 안 된다. 슬레이어 키우기의 **운영 결과물**보다 그 결과물을 가능하게 한
**뼈대**를 먼저 가져오는 것이 이번 분석의 핵심 판단이다.

---

## 1. 분석 범위와 증거 등급

### 1.1 수행한 정적 분석

- XAPK 무결성 확인, XAPK manifest와 10개 APK split 조사
- Unity player 설정, IL2CPP 바이너리, scripting assembly 목록 조사
- base/contents/emoticon/hero/map/monster/popup/skin/sound asset pack 인덱싱
- Unity asset을 타입별로 내보내 경로·이름·크기·개수 조사
- `Assets/Resources/gamedatatables/client`의 게임 테이블 399종 전수 인덱싱
- 팝업 prefab, 스킨, 몬스터, 맵, 사운드, 스킬 아이콘의 콘텐츠 폭 조사
- 귀참키우기 Step 49~56 보고서와 현행 Progression/Save/Cloud 코드를 비교
- Google Play의 공식 설명 및 현재 업데이트 설명과 정적 결과 교차 확인

### 1.2 증거 표기

| 등급 | 뜻 | 예시 |
|---|---|---|
| A | 파일/manifest/asset 이름에서 직접 확인 | 테이블 399종, popup prefab 193종 |
| B | 여러 독립 이름이 같은 구조를 가리키는 강한 추론 | 모드별 Base/Config/Wave/Reward 분리 |
| C | 공개 설명이나 커뮤니티 자료로 보강 | 10분 일일 플레이, 원소 스킬 조합 |
| D | 귀참키우기에 대한 설계 제안 | 요괴 계약, 봉인동굴, 시즌 템플릿 |

### 1.3 의도적으로 하지 않은 것

`global-metadata.dat`는 정상 IL2CPP magic 대신 `LIKEY` 계열 보호 헤더가 있고,
게임 테이블 본문도 평문 구조로 복원되지 않았다. 따라서 보호를 우회하거나 실제
확률·가격·드롭식·서버 프로토콜을 추출하지 않았다.

이 문서에서 정확하다고 말하는 것은 **테이블/asset의 존재와 구조 이름, 패키지 구성,
귀참키우기 소스의 현재 상태**다. 슬레이어 키우기의 실제 수치와 서버 동작은 추정하지
않는다. 저작권이 있는 그래픽·음원·텍스트도 귀참키우기에 복사하지 않는다.

---

## 2. 패키지 지문과 기술 구조 [A]

### 2.1 무결성

| 항목 | 값 |
|---|---|
| 원본 크기 | 430,711,713 bytes |
| SHA-256 | `3C522980B284AE645CF6150BF5C2AF325867995DB3A765C8FD5E149D382494B6` |
| package | `com.gear2.growslayer` |
| version | `600.9.4` / versionCode `695` |
| Android | min SDK 25 / target SDK 36 |
| Unity | asset 기준 `6000.3.10f1` |
| backend | ARM64 IL2CPP |

귀참키우기는 Unity `6000.5.2f1`이므로 엔진 세대는 가깝다. 다만 자산이나 코드를
옮긴다는 뜻이 아니라, Addressables/AssetBundle/PAD 같은 운영 구조를 같은 세대의
Unity 기능으로 설계할 수 있다는 뜻이다.

### 2.2 split APK

| split | 크기(byte) | 정적 역할 |
|---|---:|---|
| base | 95,354,498 | player, base data, 공용 UI/리소스 |
| arm64 | 214,587,970 | IL2CPP, Unity native library |
| contents | 8,049,136 | demon/spirit/beast/black orb |
| emoticon | 86,512 | 채팅 이모티콘 |
| hero | 7,000,552 | 영웅/캐릭터 시각 자산 |
| map | 10,535,398 | 맵/배경/atlas |
| monster | 21,807,598 | 몬스터/보스 animation |
| popup | 36,471,274 | 기능별 popup prefab |
| skin | 4,874,728 | 외형 파츠 |
| sound | 31,908,330 | BGM/SFX |

이 분리는 단순 용량 절약보다 **업데이트 위험의 격리**에 가치가 있다. 새 이벤트 팝업을
넣는 작업이 몬스터·사운드 전체와 한 덩어리로 움직이지 않는다. 귀참키우기는 아직
`Packages/manifest.json`에 Addressables가 없으므로 당장 split을 흉내 내기보다,
먼저 자산 주소 규칙과 콘텐츠 소유권을 분리한 뒤 빌드 용량이 실제로 문제일 때
Addressables/PAD로 옮겨야 한다.

### 2.3 런타임 의존성

`ScriptingAssemblies.json`에는 232개 assembly가 있고 다음 정적 의존성이 확인된다.

- 게임: `Assembly-CSharp`, `GrowSlayer.DataTableClient`, `GrowSlayer.ProtoGenScripts`
- 패치/비동기: `IFix.Core`, UniTask, R3
- 콘텐츠: Unity Addressables, Cinemachine, URP 2D
- 서버: BestHTTP, WebSocket, SignalR 계열, protobuf 생성 코드
- 계정/운영: Firebase Auth, Analytics, Messaging, Crashlytics
- 유입 분석: Adjust, Airbridge
- 수익화: Unity IAP, Google Mobile Ads와 AppLovin/Mintegral/Pangle mediation
- 웹 UI: GPM WebView

assembly의 존재는 해당 기능이 이 빌드에 포함됐다는 증거이지, 모든 기능이 매 세션
활성화된다는 증거는 아니다. 그래도 `DataTableClient + ProtoGen + HTTP/WebSocket +
Addressables` 조합은 **클라이언트 하드코딩 게임이 아니라 운영형 클라이언트**라는
강한 신호다.

### 2.4 player 설정에서 읽히는 운영 성격

- `gc-max-time-slice=3`: 프레임 중 GC 시간을 작게 나눠 방치 화면 끊김을 낮추려는 설정
- Android asset delivery 사용
- 인터넷/네트워크, billing, notification, boot, wake lock, FCM, AD_ID 권한
- foreground data sync 권한
- 광고 attribution/ad services 권한

귀참키우기는 이 값을 그대로 복사할 필요가 없다. 배울 점은 **기능이 요구할 때만 권한과
SDK를 추가**하는 것이다. 특히 광고·푸시를 넣기 전에 개인정보 고지, 동의, 실패 시 게임
계속 진행이라는 정책이 먼저 필요하다.

---

## 3. 콘텐츠 양을 숫자로 본 구조 [A]

AssetRipper 내보내기 결과의 타입별 개수다. Unity 내부 중복/보조 객체가 포함될 수 있어
게임에서 보이는 아이템 수와 동일하다고 해석하면 안 된다.

| 타입 | 파일 수 | 내보낸 총량 |
|---|---:|---:|
| JSON | 49,637 | 332,416,533 B |
| PNG | 6,234 | 435,213,022 B |
| GLB | 844 | 8,355,420 B |
| bytes | 416 | 4,946,967 B |
| OGG | 190 | 32,512,738 B |
| TTF/OTF | 15 | 3,898,340 B |

핵심 인덱스는 다음과 같다.

- 게임 데이터 테이블: **399종**, 암호화된 payload 합계 1,074,480 B
- popup pack prefab: **193종**
- base ResourceManager UI prefab 경로: **203종**
- skin 경로: **1,421종**
- monster pack 경로: **910종**(그중 monster animation PNG 887종)
- map 경로: **275종**
- hero PNG 경로: **233종**
- skill icon 경로: **141종**
- item icon 경로: **399종**
- sound index: SFX 155 + BGM 36 = **191경로**, 실제 내보낸 audio 190개

중요한 해석은 “우리도 399개 아이템을 만들자”가 아니다. 콘텐츠의 폭보다 먼저
**새 항목을 추가해도 코드가 늘지 않는 구조**가 있었기 때문에 이 수량이 가능했다.

---

## 4. 399개 테이블이 드러내는 설계 문법 [A/B]

### 4.1 모든 콘텐츠가 같은 문법을 쓴다

반복해서 나타나는 접미사와 역할은 다음과 같다.

| 역할 | 관찰 이름 | 귀참키우기 대응 |
|---|---|---|
| 정체성/기본값 | `Base`, `Info` | `*Catalog`, 표시명, unlock, 기본 수치 |
| 전역 규칙 | `Config`, `Constant` | 입장 횟수, 제한 시간, 시즌 규칙 |
| 전투 구성 | `BattleInfo`, `WaveInfo`, `MonsterInfo` | `BattleModeSpec`, `WaveSpec` |
| 성장 | `Level`, `Grade`, `Upgrade`, `Awaken` | 비용/효과 curve |
| 확률 | `Prob`, `Gacha`, `RandomPercentage` | 공개 확률표, pity 정책 |
| 지급 | `Reward`, `RewardGroup`, `Box` | 공용 reward resolver |
| 해금 | `ContentsOpen`, `SlotOpen`, `Unlock` | 단일 `ContentGate` |
| 상점 | `Shop`, `Store`, `Product`, `Package` | offer + schedule + price + reward |
| 시즌 | `Season`, `Event`, `Pass` | 기간/버전/교환 종료 처리 |

귀참키우기에서 새 던전을 만들 때 `DungeonSystem` 하나에 입장, 적, 보상, 상점까지
몰아넣으면 두 번째 던전부터 복제가 시작된다. 위 문법을 빌리면 런타임 코드는 하나고
데이터만 달라진다.

### 4.2 코어 진행

확인된 대표 테이블:

`StageInfoBaseData`, `StageInfoBase1000Data`, `StageInfoBase2000Data`,
`StageGoldFactorBaseData`, `UserLevelBaseData`, `MonsterInfoBaseData`,
`PlayerClassBaseData`, `SlayerClassBaseData`, `TrainingGradeBaseData`,
`TrainingGradeAdditionalData`, `CurrencyBaseData`, `ItemInfomationBaseData`,
`RewardBaseData`, `RewardGroupData`, `ContentsOpenBaseData`,
`ContentsOpenConditionData`.

`StageInfo`가 3개 범위로 분할돼 있고 각각 약 110~120 KB인 점은 긴 스테이지를 한
테이블로 계속 키우지 않는다는 신호다. 귀참키우기는 현재 `StageCurve` 수식이 잘
검증돼 있으므로 천 단위 행을 복제할 필요가 없다. 더 나은 적용은 다음이다.

- 기본 지수 성장식은 코드의 순수 함수로 유지
- 구간별 연출, 몬스터군, 보상 배수, 변칙만 `StageBand` 데이터로 외부화
- `StageSimulation`도 같은 provider를 읽어 런타임과 단일 출처 유지
- 긴 표는 region/band 단위로 shard하고 validator가 빈 구간·겹침을 검사

### 4.3 장비·직업·유물

대표 테이블:

`EquipmentBase/Info/GachaLevel/GachaReward/Refine/RefineConfig`,
`AccessoryBase`, `AwakeningWeapon`, `AwakeningAccessory`, `ArtifactBase`,
`SoulWeaponBase`, `SoulWeaponImprint`, `SoulWeaponSoulGemInfo/Level`,
`SlayerClassGachaProb/Reward`, `AwakeningSlayerClass`.

여기서 배울 핵심은 다음 세 층을 분리하는 것이다.

1. **보유 효과**: 장착하지 않아도 수집/강화가 계정 성장에 기여
2. **장착 효과**: 현재 빌드가 선택하는 주효과
3. **돌파/제련 효과**: 중복과 장기 재료의 sink

귀참키우기는 현재 무기/방어구 2슬롯이 곧 장비이며 인벤토리가 없다. 바로 유물,
액세서리, 소울웨폰을 더하지 말고 먼저 **장비 인벤토리 + 제련 + 도감 보유 효과**만
완성하는 것이 맞다.

### 4.4 스킬

대표 테이블:

`SkillBase`, `SkillLevel`, `SkillTree`, `SkillStone`, `SkillMaking`,
`SkillGrindingConfig/Grade/Options/PassiveOptions/Reward/SlotOpen`,
`SkillMasteryNode/Collection/AttackSkillFactor/SkillEnforceBattle`,
`SeasonSkillBase/Bingo/Pass/Quest/Package`.

스킬은 단순히 개수를 늘리지 않고 다음 생명주기를 가진다.

`획득 → 레벨 → 장착/조합 → 전용 재료 → 옵션/연마 → 마스터리 → 시즌 변형`

귀참키우기는 이미 8종, 진행 해금/가챠 해금, 4장착, 자동시전, 요도 상성을 갖는다.
다음 단계는 스킬 수 증가보다 **선택의 이유**를 늘리는 쪽이어야 한다.

- 즉시 채택: 오의 프리셋 3개, 모드별 자동 선택, 전투 태그(`다타/관통/화면/출혈`)
- 그 다음: 오의 마스터리 노드 1장, 모든 오의에 공통 재료 사용
- 보류: 스톤·연마·제작을 각각 별도 시스템으로 추가
- 금지: 스킬마다 전용 재화를 만들어 자원 목록을 폭증시키기

### 4.5 동료 계열

대표 테이블:

- Spirit: Base/Upgrade/Awakening/EquipSlotUnlock/Inventory/LevelSync/Forest/GachaProb
- Beast: Base/Level/Awaken/Scout/Shop/Summon/Appearance
- Friend: Class/Passive/PassiveDmg/GradeFactor/OptionValue/BattleInfo

슬레이어 키우기는 정령, 야수, 동료가 서로 다른 획득·성장·전투 역할을 담당한다.
귀참키우기에서 이를 세 메뉴와 세 가챠로 복제하면 현재 콘텐츠 크기에 비해 과하다.

**통합안: 요괴 계약**

| 원본 역할 | 귀참키우기에서의 한 시스템 |
|---|---|
| Spirit의 장착/동기화 | 계약 요괴 3슬롯, 계정 레벨 동기화 |
| Beast의 정찰/소환 | 지역 보스 처치 후 계약/정찰 |
| Friend의 패시브/전투 | 계약별 패시브와 전용 시험 |
| Spirit Forest | 요도원정/영체 수련 |

현행 청랑·명궁·묵웅은 계약 요괴의 첫 3종으로 그대로 승격한다. 요도의 봉인된 혼은
같은 시스템의 **상성 재료/영체 스킬**이 되고, 별도 정령 가챠는 만들지 않는다.

### 4.6 장기 성장축

확인된 축:

- Black Orb: Base/Grade/Level/Gacha/Awaken/Option/Resonate
- Demon: Level/Skill/Immortal/Mastery/Altar/Sanctuary/SecretGate
- Dragon Emblem: Config/Prob/Upgrade/Battle
- Immortal Tree: MainNode/SubNode/Factor/Upgrade/Currency
- Knowledge, Wish, Star(Node/Nebula/Constellation)

이들은 모두 “새 탭 하나 = 새 곱연산 하나”가 되기 쉽다. 귀참키우기는 이미 전직,
요도 티어/혼격, 상성, 영체, 전설 요도, 스탯, 오의, 장비, 펫을 갖는다. 추가할 수 있는
영구 성장축은 당분간 **하나**뿐이다.

추천은 `귀혈맥` 또는 `요도 각인`이라는 계정 공용 노드판이다.

- 재화는 기존 요도 파편 overflow와 연결
- 공격/체력만이 아니라 프리셋 슬롯, 원정 시간, 오프라인 보상 상한 같은 utility 포함
- 노드의 전체 파워 기여 상한을 `StageSimulation` 정책으로 고정
- 전직/요도/오의의 역할을 침범하는 노드는 금지

### 4.7 전투·비전투 모드

확인된 주요 콘텐츠:

- Cave, Adventure, Campaign, Mine
- Ranking Battle, Season Ranking, Time Attack
- World Boss, Guild Quest/Battle/Beast
- Spirit Forest, Skin Battle, Theme Dungeon
- Plaza/Fountain/Tarot/Quest
- Dragon Valley: hunt/search/cook/trade/archaeology/airship/dispatch/ether robot/scientist

이 중 귀참키우기에 실제로 필요한 것은 테마가 아니라 세 가지 **규칙 원형**이다.

| 원형 | 귀참 이름 | 규칙 | 주 보상 |
|---|---|---|---|
| 제한 횟수 성장 던전 | 봉인동굴 | 고정 웨이브, 1일 N회, 최고층 sweep | 장비 제련석 |
| 시간/점수 도전 | 백귀야행 | 제한 시간 내 처치/보스 연전 | 주간 보석·칭호 |
| 비동기 파견 | 요도원정 | 계약 요괴 배치, 시간 경과, 지역 상성 | 혼/파편/외형 조각 |

세 모드는 아래 하나의 스키마와 runner를 공유해야 한다.

```text
BattleModeSpec
  id, modeType, unlockGateId, scheduleId
  entryCost, dailyLimit, sweepRule
  battleRuleId, waveGroupId, rewardGroupId
  scoreRuleId, leaderboardId, failureAdviceId
```

### 4.8 유지·복귀·수익화

확인된 테이블:

- Daily/Repeat/Achievement quest
- 진행도별 NewUserAttendance, regular/event attendance
- Welcome, NewUserGrowthMail
- EventDailyMission, EventPassReward, PromotionPass
- Product/ProductReward/PastProduct, Store, EventShop, SeasonShop
- Equipment/Class/Spirit/BlackOrb gacha, random reward box
- Invitation, coupon, ad reward popup
- step-up/special/season/theme dungeon package

이 구조에서 가져올 것과 버릴 것을 구분해야 한다.

**가져올 것**

- 우편함: 보상 지급 실패, 점검 보상, 신규/복귀 지급의 단일 통로
- 출석: 7일 고정판 하나, 놓친 날을 벌주는 연속 출석은 피함
- 공용 event mission/pass/shop 데이터 구조
- offer의 시작/종료/구매제한/보상/대체보상 분리
- 광고는 강제 노출 없이 선택형 보상만

**가져오지 않을 것**

- 여러 신규 유저 출석판을 동시에 띄우는 구조
- 성장축마다 별도 패키지 popup
- 첫 화면 연쇄 할인 팝업
- 한 상품을 사야 다음 상품이 보이는 step-up을 초기 핵심 루프로 사용
- 확률형 상품 여러 종을 같은 시점에 개방

### 4.9 미니게임과 시즌

테이블상 Balloon, Card Matching, Defense, Fishing, Flappy Bird, Fruit, Mole, Race,
Rhythm, Shooting, Stacking과 길드 Quiz/Sword Sparring이 있다. retry/reward/config가
공통 문법을 쓰는 것이 중요한 관찰이다.

귀참키우기는 미니게임 개수를 따라갈 필요가 없다. 첫 시즌은 기존 전투를 변형한
`백귀야행`으로 만들고, 별도 조작 장르의 미니게임은 retention 데이터가 실제로
요구할 때 하나만 만든다. 장르가 다른 게임은 QA·튜토리얼·모바일 입력·밸런스 비용이
각각 새 게임 하나만큼 든다.

---

## 5. popup과 UX가 보여주는 운영 패턴 [A/B]

193개 popup 이름에서 확인되는 흐름은 기능의 수보다 중요하다.

- `ContentsOpen`: 새 기능이 열린 순간을 독립 사건으로 처리
- `Reward`, `Result`, `RankReward`: 결과와 수령을 분리
- `Info`, `Help`, `Probability`: 규칙/확률 설명을 별도 화면으로 제공
- `Preset`, `Equip`, `SlotOpen`: 보유와 장착, 해금을 분리
- `Pass`, `Shop`, `Package`: 같은 이벤트 안에서도 진행과 구매를 분리
- `Chat`, `UserInfo`, `Report` 계열: 사회 기능의 moderation 면을 함께 준비
- `SkillReview/Write/Share/UsageStatistics`: 빌드를 커뮤니티 콘텐츠로 전환
- `PowerSaving`, `BattleEffectSkip`: 장시간 켜두는 방치형의 기기 비용을 관리

귀참키우기 UX 우선순위는 다음이다.

1. 다음 해금 타임라인: “무엇이 언제 열리는지”를 잠금 화면에서 먼저 보여줌
2. claim-all과 red dot: **보상이 실제 수령 가능할 때만** 표시
3. 오의/장비/동료 프리셋: 모드가 늘기 전에 반드시 추가
4. 실패 화면의 바로가기: 공격 부족/생존 부족/상성 부족을 현재 `BossFailureAdvice`와 연결
5. sweep: 이미 깬 일일 콘텐츠의 반복 조작 제거
6. 절전 모드: 화면/이펙트/FPS를 낮추되 실제 계산 주기는 유지
7. 확률/천장/중복 변환을 뽑기 화면에서 한 번에 확인

일일 세션 목표는 공식 설명의 “하루 10분”을 그대로 복제하는 것이 아니라 다음처럼
구체적으로 정의한다.

```text
복귀 보상 확인(20초)
→ 오늘의 추천 3개 확인(20초)
→ 봉인동굴 sweep/도전(2분)
→ 백귀야행 또는 보스 갱신(3분)
→ 장비/오의/요괴 한 번 정리(2분)
→ 일일 보상 일괄 수령(20초)
→ 자동 진행으로 복귀
```

핵심 행동이 5~8분에 끝나고 더 하고 싶은 사람만 최전선/랭킹/빌드 조정을 한다.

---

## 6. 귀참키우기 현행과의 갭 분석

### 6.1 현재 기준선

| 영역 | 귀참키우기 현재 |
|---|---|
| 스테이지 | 10잡몹 후 보스, HP ×1.55, 골드 ×1.72 기반 순수 곡선 |
| 오의 | 8종, 진행 6/가챠 2, 기본 3→최대 4슬롯, 자동시전 |
| 장비 | 무기/방어구 2슬롯, 등급+단련, 인벤토리 없음 |
| 전직 | 로닌 + 6티어 |
| 동료 | 청랑/명궁/묵웅 3종, 보유 전원 전투 참여 |
| 요도 | 지역 보스 요도 4종 + 전설 2종, 혼/티어/혼격/상성/영체/파편 |
| 퀘스트 | 일일 5 + 반복 3 + 업적 14 |
| 가챠 | 요도와 오의, 별도 pity/누적/일일 무료 상태 저장 |
| 장기 밸런스 | st1~50 밴드 + st51~500 도달층 계약, 축별 무력화 시뮬레이션 |
| 클라우드 | Firebase 인증/구글 연동/리더보드, 서버 진위 재검증과 클라우드 세이브는 미완 |
| 저장 | v19, ID 배열 기반 마이그레이션, 손상 원본 `.broken` 백업 |
| 운영 | Remote Config, Addressables, 우편함, 출석, event schedule 없음 |

### 6.2 의사결정 표

| 시스템 | 상대 격차 | 가치 | 판단 | 선행 조건 |
|---|---:|---:|---|---|
| 데이터 카탈로그 | 큼 | 매우 큼 | 즉시 채택 | validator, stable ID |
| ContentGate | 큼 | 매우 큼 | 즉시 채택 | 단일 진행 snapshot |
| RewardGroup | 큼 | 매우 큼 | 즉시 채택 | reward ledger |
| Currency catalog | 중간 | 큼 | 즉시 정리 | 기존 gold/gem/yodo 자산 매핑 |
| 우편함 | 큼 | 큼 | 조기 채택 | 클라우드 정체성/중복 수령 키 |
| 출석 | 큼 | 중간 | 조기 채택 | 서버 시각 또는 보수적 오프라인 정책 |
| 봉인동굴 | 큼 | 매우 큼 | 첫 신규 모드 | mode runner, reward group |
| 장비 인벤토리/제련 | 큼 | 큼 | 두 번째 성장 확장 | 저장 v20+, source/sink 검증 |
| 프리셋 | 큼 | 매우 큼 | 모드보다 먼저 | 오의/장비/요괴 loadout DTO |
| 오의 마스터리 | 중간 | 큼 | 2차 채택 | 기존 50%/tail 계약 재측정 |
| 요괴 계약 | 중간 | 매우 큼 | 통합 채택 | 펫/요도 역할 마이그레이션 |
| 시즌 이벤트 | 큼 | 큼 | 뼈대 후 채택 | schedule, inbox, shop, telemetry |
| 광고/IAP | 큼 | 사업 의존 | 별도 단계 | 동의/복구/영수증 검증/실패 정책 |
| World Boss | 큼 | 중간 | 후순위 | 서버 검증, 비동기 snapshot |
| 길드/광장 | 매우 큼 | 현재 낮음 | 보류 | moderation, 서버 비용, 충분한 DAU |
| 다수 미니게임 | 매우 큼 | 불명 | 보류 | retention 근거 |
| 복수 가챠 | 큼 | 위험 | 비채택 | 현재 2배너도 충분 |
| asset pack split | 중간 | 후반 큼 | 준비만 | 실제 용량/패치 지표 |

### 6.3 귀참키우기가 더 잘하고 있는 부분

슬레이어 키우기의 구조를 보고도 다음은 바꾸지 않는 편이 낫다.

- `StageCurve`를 수천 행의 평문 표로 대체하지 않는다.
- `StageSimulation`의 결정론과 축별 neutralization을 유지한다.
- “가챠는 풀의 폭을 열고 슬롯은 진행이 연다”는 현행 경계를 유지한다.
- 스킬 5종의 초당 기여를 맞춰 취향 선택을 만든 설계를 유지한다.
- 모든 신규 성장축은 f2p/lead 도달층과 tail share로 먼저 검증한다.
- Firebase 실패가 전투/세이브 로드를 막지 않는 오프라인 우선 정책을 유지한다.
- 높은 버전 세이브 거부, 낮은 버전 명시 migration, 손상 원본 보관을 유지한다.

슬레이어 키우기의 규모가 더 크다고 해서 귀참키우기의 검증 가능한 순수 곡선보다
무조건 좋은 것은 아니다. 귀참키우기의 강점을 데이터 파이프라인이 **먹는 구조**가
아니라, 데이터 파이프라인이 그 강점을 **공급하는 구조**여야 한다.

---

## 7. 귀참키우기용 권장 데이터 구조

### 7.1 런타임 경계

```text
Authoring CSV/JSON
  → Editor Import + Schema Validation
  → immutable generated catalog
  → IProgressionData 인터페이스
  ├─ Runtime systems
  ├─ StageSimulation
  └─ Test fixtures

Remote numeric overlay (선택)
  → signature/schema/version/allow-list 검사
  → immutable session snapshot
```

`ScriptableObject`를 런타임 시스템과 시뮬레이션이 직접 제각각 읽게 하지 않는다.
Editor에서 검증 후 순수 immutable 데이터로 바꾸고, 런타임과 시뮬레이션이 같은
인터페이스를 읽게 해야 지금의 테스트 강도를 잃지 않는다.

### 7.2 최소 테이블

| 테이블 | 핵심 필드 |
|---|---|
| `ContentGate` | id, type, stage/level/evolution/quest 조건, preview text |
| `Currency` | id, cap, expiry, display, overflow target |
| `RewardGroup` | id, entries, amount formula, select/random policy |
| `StageBand` | range, region, monster group, modifiers, reward factor |
| `BattleMode` | runner type, gate, entry, wave, reward, sweep, schedule |
| `WaveGroup` | wave, spawn group, boss, time limit, modifiers |
| `Equipment` | stable id, slot, rarity, owned/equipped/refine effects |
| `Skill` | 기존 spec + tags, mastery group, acquisition source |
| `Companion` | contract source, role, passive, expedition tags |
| `ShopOffer` | schedule, price, reward, limit, prerequisite, receipt policy |
| `EventSchedule` | server start/end, grace end, timezone, content ids |
| `Localization` | key, ko/en, font coverage test |

### 7.3 validator에서 반드시 막을 것

- 중복/빈 stable ID
- 존재하지 않는 reward/currency/gate 참조
- 음수 가격·보상, 0분모, NaN/Infinity
- 확률합 오류와 공개 확률 불일치
- pity보다 먼저 확정 보상이 나갈 수 없는 구성 오류
- stage band 공백/중첩
- 시작보다 빠른 종료, 교환 grace가 이벤트 종료보다 빠른 일정
- 순환 prerequisite
- max 이후 중복 변환 경로 누락
- localization key/폰트 glyph 누락
- save에 존재하는 ID를 삭제하면서 alias/migration이 없는 경우

### 7.4 Remote Config 경계

Step 56 보고서에도 Remote Config가 다음 범위로 남아 있다. 도입할 때는 모든 데이터를
원격에서 바꾸지 말고 다음 숫자만 allow-list로 시작한다.

- 이벤트 시작/종료와 노출 여부
- 모드 입장 횟수, 보상 배수의 좁은 범위
- 광고 선택 보상 배수
- 상점 offer 노출/가격 ID(실제 가격은 store product가 원본)
- 긴급 kill switch

스킬 공식, 세이브 스키마, 클래스 이름, 보상 타입, prefab 주소를 런타임 원격값으로
바꾸지 않는다. config snapshot의 schema version, payload hash, 적용 시각을 로그와
세이브에 남겨 재현 가능하게 한다.

---

## 8. 경제 설계 적용안

### 8.1 재화 수를 먼저 제한한다

| 재화 | source | sink | 규칙 |
|---|---|---|---|
| 골드 | 자동전투/오프라인 | 기본 스탯/단련 | 무한 성장, `BigDouble` |
| 보석 | 퀘스트/업적/시즌 | 현행 뽑기 | 유료와 무료 잔액 정책 명시 |
| 요도 혼 | 지역 보스 | 요도 티어/영체 | 요도 전용, 거래 금지 |
| 요도 파편 | 중복/overflow | 귀혈맥/각인 | 장기 통합 재료 |
| 제련석 | 봉인동굴 | 장비 제련 | 모드 하나와 성장 하나의 폐루프 |
| 시즌 인장 | 시즌 미션 | 시즌 상점 | 종료 후 자동 변환/유예 기간 |

초기 화면에 동시에 보이는 영구 재화는 골드/보석/혼/파편/제련석 **5개 이내**를
권장한다. 새 재화는 “새 선택을 만드는가”를 통과해야 한다. 단순히 기존 재화를 다른
색으로 한 번 더 요구하면 추가하지 않는다.

### 8.2 보상 ledger

모든 지급은 아래 식별자를 가진 한 경로로 통과시킨다.

```text
grantId = sourceType/sourceId/seasonId/userId/sequence
RewardLedger.TryGrant(grantId, RewardGroup, context)
```

- 같은 우편/출석/랭킹 보상을 재시도해도 한 번만 지급
- 지급 중 앱이 종료돼도 복구 가능
- 지급 전후 currency balance와 config version 기록
- 서버 보상은 서버 grantId를 원본으로 사용
- 로컬 보상도 save transaction 안에서 claim flag와 balance를 같이 기록

### 8.3 가챠

현행 요도/오의 두 배너면 충분하다.

- pity는 배너별 유지, 세이브/클라우드에서 절대 합치지 않음
- 확률·천장까지 남은 수·최대 후 중복 변환을 한 화면에 표시
- 무료 뽑기 날짜는 서버 시각 도입 전까지 조작 가능하다는 현행 경고 유지
- 최고 돌파 후 중복은 공용 요도 파편으로만 변환
- 신규 배너 대신 기존 풀 확장 또는 선택권/교환소 우선
- 오의 슬롯 판매 금지와 진행 해금 원칙 유지

### 8.4 `StageSimulation`에 추가할 계약

각 새 시스템마다 다음 두 정책을 추가한다.

- `SkipNewAxis`: 시스템을 아예 없는 세계
- `NeutralizeNewAxis`: 해금/UX는 있지만 파워 기여가 1인 세계

검사해야 할 값:

- st1~50 기존 도달 시간 비트 불변
- st51~500 f2p/lead 도달층 변화
- 새 축 단독 기여율과 전체 tail share
- 일일 재화 source/sink와 목표 완성 일수
- 7/30/90일 누적 power gap
- 최대치 도달 뒤 overflow가 막히지 않는가
- 미접속 1/8/24시간 보상과 접속 플레이의 비율

---

## 9. 구현 로드맵

### Gate 0 — 출시 기반 부채

게임 콘텐츠를 늘리기 전 병행해서 닫아야 할 현행 부채다.

- 클라우드 세이브와 계정 복구의 실제 진행 데이터 범위 정의
- Cloud Functions 기반 리더보드 진위 재검증
- App Check Play Integrity
- release/app-signing SHA-1과 구글 로그인 실기 확인
- Apple 로그인/iOS bring-up
- 서버 시각 또는 일일 보상에 대한 보수적 offline 정책

이 작업은 슬레이어 키우기 분석에서 새로 나온 것이 아니라 Step 54~56에 이미 적힌
출시 전 부채다. 새 출석/이벤트/상점 보상은 이 경계 없이 쌓으면 부정 수령과 복구
문제가 동시에 커진다.

### Milestone A — 운영 뼈대

산출물:

- `IProgressionData`와 generated catalog
- `ContentGate`, `Currency`, `RewardGroup`, `RewardLedger`
- schema validator와 cross-reference EditMode tests
- 기존 hardcoded catalog를 한 번에 없애지 않고 adapter로 연결
- config snapshot/version/hash 로그

완료 조건:

- 기존 580개 테스트와 도달층 수치 불변
- 런타임/시뮬레이션이 동일 catalog snapshot 사용
- 잘못된 표 10종 이상을 validator test가 명시적으로 거부
- save의 삭제/rename ID가 migration 없이는 build를 막음

### Milestone B — 복귀/일일 루프

산출물:

- 우편함, 7일 출석, 오늘의 추천 3개
- claim-all과 idempotent grant
- 다음 콘텐츠 해금 타임라인
- 절전 모드 1차(FPS/effect/UI 갱신 제한)

완료 조건:

- 오프라인/중복 탭/강제 종료 후 중복 지급 0
- 5~8분 안에 핵심 일일 행동 완료
- red dot이 단순 미열람이 아니라 실제 claim 가능 상태와 일치

### Milestone C — 첫 공용 전투 모드

`봉인동굴` 하나만 만든다.

- 기존 combat actor/spawn/boss를 `BattleModeRunner`가 재사용
- 1일 입장, 최고층, sweep, 제련석 reward group
- 보스 실패 조언과 전투 snapshot 연결
- mode data만 바꾼 가짜 두 번째 모드 test fixture로 재사용성을 증명

완료 조건:

- 두 번째 fixture를 추가하는 데 새 runtime system class가 필요하지 않음
- sweep 보상은 실제 클리어 최고층을 넘지 않음
- 일일 리셋/시간 조작/오프라인 실패가 게임 본체를 막지 않음

### Milestone D — 장비를 선택으로

- 무기/방어구 inventory DTO
- 획득, 장착, 강화, 제련, 잠금, 일괄 분해
- 보유 효과와 장착 효과 분리
- 프리셋 3개(오의+장비+요괴 참조)
- 기존 `equipmentIds/Grades/Levels`를 v20+에서 손실 없이 이관

첫 버전은 슬롯을 늘리지 않는다. **같은 2슬롯 안에서 교체 선택**을 먼저 만든다.

### Milestone E — 요괴 계약

- 청랑/명궁/묵웅을 계약 요괴로 이관
- 레벨 동기화와 역할 태그
- 지역 보스/요도 혼을 계약 source로 연결
- 요도원정: 파견 시간, 상성, 보상, 재배치
- 별도 companion gacha 없이 콘텐츠 획득+선택권부터 시작

### Milestone F — 시즌 템플릿

- schedule, mission, pass, exchange shop, inbox 종료 보상
- 첫 시즌은 기존 전투 규칙을 변형한 백귀야행
- free lane을 완결된 경로로 만들고 paid lane은 편의/외형 중심
- 이벤트 종료 뒤 남은 재화의 grace/자동 변환
- 두 번째 시즌은 데이터/아트 교체만으로 열리는지 검증

### 후순위

1. 비동기 월드 보스
2. 오의 사용 통계/추천 프리셋
3. 외형 파츠/도감
4. 길드
5. 광장/실시간 채팅

길드와 채팅은 UI 문제가 아니라 moderation, 신고/차단, 서버 비용, 개인정보, 운영
인력 문제다. DAU와 운영 준비가 증명되기 전에 착수하지 않는다.

---

## 10. popup/기능을 귀참 세계관으로 번역한 목록

| 슬레이어 키우기에서 읽힌 기능 | 귀참키우기 번역 | 판단 |
|---|---|---|
| Cave | 봉인동굴 | 채택 |
| Mine | 요도 광맥/혼맥 | 봉인동굴과 통합 |
| Spirit Forest | 요도원정 | 채택 |
| Time Attack | 백귀야행 | 채택 |
| World Boss | 귀문 대요괴 | 후순위 |
| Skill Mastery | 오의 마스터리 | 2차 채택 |
| Soul Weapon | 요도 각인 | 현행 요도에 통합 |
| Black Orb | 귀혈맥 | 장기 노드 하나로 축소 |
| Demon/Immortal | 전직/귀혈맥 | 별도 축 금지 |
| Dragon Emblem/Star/Wish | 귀혈맥 utility node | 통합 |
| Spirit/Beast/Friend | 요괴 계약 | 하나로 통합 |
| Plaza | 신사 마당 | 사회 기능 시점까지 보류 |
| Guild Beast/Quest | 문파 토벌 | 길드와 함께 후순위 |
| Season Skill | 월식 오의 | 시즌 안에서만 sidegrade |
| Skin Battle/Shop | 의상 도감 | 수익화/외형 단계 |
| Skill Review/Usage | 추천 오의/사용률 | 서버 데이터 뒤 채택 |
| Ad Reward | 선택형 부적 축복 | 강제 광고 금지 |
| Power Saving | 수행 모드 | 조기 채택 |

---

## 11. 절대 복제하지 않을 것

1. **기능 수**: 399테이블/193팝업은 여러 해의 운영 결과다.
2. **재화 수**: 새 탭마다 재화를 만들면 선택이 아니라 암기가 된다.
3. **가챠 수**: 무기/직업/정령/야수/구슬을 그대로 분리하지 않는다.
4. **팝업 수익화**: promotion package 이름이 많은 것은 채택할 설계 원칙이 아니다.
5. **시즌별 코드 복제**: `DragonValleySeason3/4`처럼 버전 이름을 타입에 박는 방식은
   장기 운영의 흔적이지만, 귀참은 schema/version 데이터로 푼다.
6. **공식/드롭률 추정**: 보호된 table body에서 읽지 못한 값을 지어내지 않는다.
7. **아트/음원/UI/문구 복사**: 구조적 패턴만 참고한다.
8. **클라이언트 신뢰**: 수익/랭킹/시즌 보상을 클라이언트 수치만 믿고 지급하지 않는다.

---

## 12. 테이블 계열 전수 요약

399종을 기능군으로 묶은 색인이다. 한 테이블이 여러 기능을 잇기 때문에 기능군 합계는
399와 일치하지 않을 수 있다.

| 기능군 | 대표 테이블과 관찰 |
|---|---|
| 코어/스테이지 | StageInfo 3 shard, StageGoldFactor, UserLevel, Monster, Currency, RewardGroup, ContentsOpen |
| 장비/직업 | Equipment Base/Info/Gacha/Refine, Accessory, Awakening, Artifact, SlayerClass gacha, SoulWeapon/Imprint/SoulGem |
| 오의 | Skill base/level/tree/stone/making, Grinding config/options/reward/slot, Mastery node/collection/battle, SeasonSkill |
| 정령 | Base/Upgrade/Awakening/Forest/Inventory/LevelSync/SlotUnlock/Gacha |
| 야수 | Base/Level/Awaken/Scout/Shop/Summon/Appearance/Config |
| 동료 | Class/Passive/Dmg/Grade/Option/Battle |
| 장기 성장 | BlackOrb, Demon, DragonEmblem, ImmortalTree의 base/config/level/option/upgrade 계열, Knowledge/Wish/Star |
| 기본 모드 | Adventure/Wave, Campaign/Episode/Scenario/Dialogue, Cave, Mine/Wave |
| 경쟁 | RankingBattle, SeasonRanking, TimeAttack, WorldBoss |
| 길드 | Level/Member/Assist/Beast, Quest battle/reward/effect, Shop, Quiz/Sparring |
| 광장 | Config, Quest/NPC, Fountain, Tarot, Megaphone, Emoticon |
| Dragon Valley | 공용 + Season3 + Season4의 search/hunt/cook/trade/rank/tier/archaeology/ether/dispatch |
| 유지 | Daily/Repeat/Achievement, MiniQuest, Attendance, NewUser/Welcome/GrowthMail, Invitation |
| 이벤트 | Event base/buff/daily mission/reward/shop/skin shop/pass/step-up/bingo/blue marble/RPS/find/minigame |
| 수익화 | Product/Reward/PastProduct, Store, gacha probability/reward, random box, package, coupon |
| 미니게임 | Balloon/Card/Defense/Fishing/Flappy/Fruit/Mole/Race/Rhythm/Shooting/Stacking + retry/reward |
| 외형 | Body/Costume/Hat/Hair/Color/Back/Lens/Face/FaceDeco/Cloak/Earring/Weapon/Map/Beast/Friend |

크기가 큰 payload는 콘텐츠 폭의 보조 지표다.

| 테이블 | bytes | 해석 제한 |
|---|---:|---|
| MineInfoBase | 120,448 | 광산 콘텐츠 행이 많다는 신호 |
| StageInfoBase2000 | 119,968 | 고단계 shard |
| StageInfoBase1000 | 119,136 | 중단계 shard |
| SpiritUpgrade | 113,952 | 정령 성장 범위가 큼 |
| StageInfoBase | 110,496 | 초기 stage shard |
| CaveInfo | 79,568 | 동굴 단계/규칙 폭이 큼 |
| UserLevel | 78,000 | 레벨 progression 폭이 큼 |
| EquipmentInfo | 19,104 | 장비 개체/등급 데이터 |
| DialogueBase | 18,256 | 대사 콘텐츠 |
| CampaignScenario | 14,064 | 캠페인 시나리오 |
| KnowledgeInfo | 12,448 | 지식 성장 노드/행 |
| MineWaveInfo | 11,840 | 광산 wave 데이터 |
| EventShop | 9,808 | 이벤트 offer 폭 |
| SoulWeaponSoulGemLevel | 9,504 | 소울젬 레벨 데이터 |
| SpiritForest | 9,168 | 정령숲 콘텐츠 |
| FriendBattleInfo | 8,560 | 동료 전투 구성 |

파일 크기로 정확한 행 수, 확률, 가격, 공식은 알 수 없다.

---

## 13. 근거 위치

### 귀참키우기 로컬 근거

- `Assets/_Project/Scripts/Progression/StageCurve.cs`
- `Assets/_Project/Scripts/Progression/StageSimulation.cs`
- `Assets/_Project/Scripts/Progression/SkillCatalog.cs`
- `Assets/_Project/Scripts/Progression/SkillCurve.cs`
- `Assets/_Project/Scripts/Progression/EquipmentCatalog.cs`
- `Assets/_Project/Scripts/Progression/PetCatalog.cs`
- `Assets/_Project/Scripts/Progression/YodoCatalog.cs`
- `Assets/_Project/Scripts/Progression/LegendaryYodoCatalog.cs`
- `Assets/_Project/Scripts/Progression/QuestCatalog.cs`
- `Assets/_Project/Scripts/Progression/SaveData.cs`
- `Assets/_Project/Scripts/Cloud/CloudScores.cs`
- `docs/ONIKIRI_Step49_Report.md` ~ `docs/ONIKIRI_Step56_Report.md`

### 공개 교차검증

- [Google Play — Slayer Legend 공식 앱 설명](https://play.google.com/store/apps/details?id=com.gear2.growslayer)
- [Apple App Store — Slayer Legend](https://apps.apple.com/us/app/slayer-legend/id1635712706)
- [AssetRipper 공식 저장소](https://github.com/AssetRipper/AssetRipper)
- [Cpp2IL 공식 저장소](https://github.com/SamboyCoding/Cpp2IL)

Google Play 공식 페이지는 AFK 보상, 하루 10분, 장비 강화, 마법 재능, 원소 스킬 조합을
명시하고 있으며 2026-07-07 업데이트 설명에는 신규 familiar, 광장 summon, region
확장이 적혀 있다. 이는 패키지에서 확인된 동료/광장/장기 region 테이블과 일치한다.

---

## 14. 최종 적용 원칙

슬레이어 키우기를 벤치마크한 결과, 귀참키우기의 다음 성공 조건은 “콘텐츠를 많이
추가했다”가 아니다.

> 새 성장축이나 모드를 하나 추가했을 때 runtime class, save 예외, 보상 지급 코드,
> popup 흐름이 각각 새로 생기지 않고, 검증된 공용 문법 안에 데이터 한 벌로 들어가는가.

그 문법을 먼저 세우면 봉인동굴, 백귀야행, 요도원정, 장비 제련, 요괴 계약, 시즌
이벤트가 서로 다른 프로젝트가 아니라 같은 게임의 확장이 된다. 반대로 문법 없이
기능부터 복제하면 399개 테이블이 아니라 399개의 예외를 갖게 된다.
