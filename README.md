# Monster Kindergarten

슬라임을 생성하고 같은 등급끼리 합성해 성장시키는 Unity 6 기반 Android
방치형 클리커 게임입니다. 클릭과 자동 생산으로 포인트를 모아 슬라임의
생산량과 공용 스폰 시스템을 업그레이드합니다.

## 게임

- 최고 해금 등급과 업그레이드 상태에 따라 1–6등급 슬라임이 일정 시간마다 자연
  생성됩니다.
- 같은 등급 두 마리를 드래그해 합치면 다음 등급으로 성장하며, 최대 20등급까지
  있습니다.
- 1–10등급은 지상, 11–20등급은 하늘 스테이지에서 활동합니다. 10등급 둘을 합쳐
  처음 11등급을 만들면 하늘 스테이지와 이동 기능이 해금됩니다.
- 최고 등급 3부터 장식장이 열립니다. 메인 스테이지의 슬라임을 전시하거나 다시
  꺼낼 수 있고, 넣은 일반 슬라임은 도감에 영구 등록됩니다. 전시 중인 슬라임은
  포인트를 생산하지 않고 수용량도 차지하지 않습니다.
- 슬라임 터치와 자동 생산으로 포인트를 얻어, 등급별 생산량과 공용 스폰 간격·최대
  수용량·상위 슬라임 등장 확률을 강화합니다.
- 접속하지 않은 시간만큼 오프라인 자동 생산 보상을 받습니다.
- 신규 플레이어와 새로 열리는 기능마다 튜토리얼이 안내합니다.

오프라인 보상은 오프라인 플레이를 의미하지 않습니다. Android에서 게임을
시작하려면 Google Play Games 및 Firebase 로그인이 필요합니다.

수치와 규칙을 포함한 확정 기획은
[게임 기획 및 Phase 구현 스펙](Documentation/MonsterKindergarten_GAME_DESIGN_IMPLEMENTATION_SPEC.md)에
있습니다.

## 개발 환경

- Unity `6000.3.21f1`
- Android / Universal Render Pipeline 2D
- Unity Input System `1.20.0`
- Google Play Games Plugin for Unity
- Firebase Authentication / Firestore `13.7.0`
- UniTask / DOTween / Lean Pool
- TextMesh Pro / UIEffect

## 실행

1. Unity Hub에서 Unity `6000.3.21f1`과 Android Build Support를 설치합니다.
2. 저장소를 Unity 프로젝트로 엽니다.
3. `Assets/01.Scenes/LoginScene.unity`에서 Play Mode를 실행합니다.

Editor에서는 `LocalPlayer` 계정과 로컬 저장소를 사용합니다. Google Play Games
로그인, Firebase 저장·복구, 터치 입력, 서명과 기기 성능은 Android 빌드에서 별도로
검증해야 합니다.

## 빌드 프로필

| 용도 | 프로필 | 앱 버전 | Version Code |
| --- | --- | --- | ---: |
| Play Console 배포 | `Android_Release` | `0.1.08` | `9` |
| 개발 및 기기 확인 | `Android™` | `0.1.08` | `9` |

Android 애플리케이션 ID는 `com.skku_say.Monster_Kindergarten`입니다. Release는
Development Build가 꺼진 AAB, 개발 프로필은 켜진 APK이며 둘 다 `LoginScene` 다음에
`GameScene`을 포함합니다. 개발 프로필도 릴리스와 같은 커스텀 키스토어로 서명합니다.
Google Play Games 로그인이 디버그 키스토어 빌드를 거부하기 때문입니다.

프로젝트 전역 버전이 아니라 실제 빌드에 쓰는 프로필 값을 확인해야 합니다.

산출물은 `Builds/Release/<버전>/`에 두며 Git에서 제외됩니다. `build-info.txt`
핸드오프 기록은 `0.1.06`까지 있고, `0.1.07`과 `0.1.08`은 산출물만 있습니다.

## 저장 구조

- 재화, 슬라임 상태, 업그레이드를 독립된 도메인과 저장소로 관리합니다.
- Android에서는 PlayerPrefs에 즉시 저장하고 Firebase에 지연 반영하는
  `HybridRepository<T>`를 사용합니다.
- 활성 슬라임은 개체별 `InstanceId`, 등급, 특별 여부와 위치를 저장하며 화면
  좌표는 저장하지 않습니다. 도감은 등록 상태와 등급별 누적 통계를 함께 남깁니다.
- 도메인마다 `SchemaVersion`을 기록하고, 현재 앱이 지원하는 것보다 높은 저장
  데이터는 구버전이 덮어쓰지 못하도록 로드를 중단합니다.
- 읽기는 "읽음 / 없음 / **실패**"를 구분합니다. 읽기 실패를 기본값으로 바꾸면 그
  세션이 신규 계정으로 시작하고 첫 저장이 읽지 못한 원본을 덮어쓰기 때문입니다.
- 해석할 수 없는 저장을 만나면 게임에 들어가지 않고 안내와 함께 로그인 화면으로
  돌아갑니다. 손상된 경우에는 그 화면에서 진행도를 초기화해 복구할 수 있습니다.
- 메인 튜토리얼 중에는 진행 저장을 잠그고, 완료 시 세 도메인을 함께 저장합니다.

저장 규칙의 세부는 [CLAUDE.md](CLAUDE.md)에 있습니다.

## 옵션과 진행도 관리

- 배경음·효과음 음량은 기기별로 저장하며 재실행 시 복원합니다.
- 진행도 초기화는 확인창을 거쳐 현재 계정의 재화·슬라임·업그레이드와 이 기기의
  튜토리얼 기록을 삭제하고 로그인 화면으로 돌아갑니다. 인증 계정과 음량 설정은
  남습니다.
- 계정 삭제는 같은 정리를 마친 뒤 Firebase 인증 계정을 삭제합니다. Google 계정과
  Play 게임 프로필은 삭제하지 않습니다.
- 초기화나 계정 삭제가 중단되면 다음 로그인에서 게임 진입 전에 마무리합니다.
- 다른 기기의 로컬 저장까지 무효화하지는 않습니다. 그 기기로 접속하면 이전
  진행도가 클라우드에 다시 저장될 수 있습니다.

## 주요 폴더

- `Assets/01.Scenes`: 로그인 및 게임 장면
- `Assets/02.Scripts/Core`: 애플리케이션 및 Firebase 초기화
- `Assets/02.Scripts/Ingame`: 클릭, 스폰, 합성, 스테이지 및 게임 진행 로직
- `Assets/02.Scripts/Outgame/Feature`: 도메인, 저장소, 매니저 및 밸런스 데이터
- `Assets/02.Scripts/UI`: 게임 및 업그레이드 UI
- `Assets/03.Prefabs`: 슬라임과 UI 프리팹
- `Assets/04.Images`: 슬라임, 로그인 화면 및 스테이지 배경
- `Assets/10.ScriptableObjects`: 튜토리얼 데이터
- `Assets/11.Sounds`: 스테이지 BGM과 효과음

## 문서

- [프로젝트 작업 지침](CLAUDE.md)
- [게임 기획 및 Phase 구현 스펙](Documentation/MonsterKindergarten_GAME_DESIGN_IMPLEMENTATION_SPEC.md)
- [Google Play Games 및 Firebase 연동 기록](Documentation/GOOGLE_PLAY_FIREBASE_INTEGRATION.md)
- [C# 코딩 컨벤션](Documentation/CODING_CONVENTION.md)
- [코드 리뷰 및 검증 기록](Documentation/CODE_REVIEW_2026-08-20.md)
- [PlayerPrefs 저장 비용 측정 기록](Documentation/PLAYERPREFS_SAVE_PROFILING.md)

## 현재 상태

Phase 1·1.5 자연 스폰과 개체 저장, Phase 2 장식장, Phase 2-B 관찰 UX, Phase 3 일반
슬라임 도감까지 구현했습니다. 특별 슬라임과 가챠는 Phase 4 이후 범위입니다.

Android 내부 테스트에서 Google Play Games 로그인, 재로그인, Firebase 클라우드 저장,
앱 데이터 삭제 후 복구, 오프라인 보상 수령을 확인했습니다.

저장 로드 실패 처리는 Unity Editor에서 손상·상위 버전·결손·복구 경로를, 개발 APK
기기 테스트에서 Firestore 필드 결손과 튜토리얼 중 보상 보류를 확인했습니다.

아직 검증하지 않은 항목은 다음과 같습니다. 실행 테스트는 사용자가 수행하며 기존
결과를 새 빌드의 검증 완료로 확대하지 않습니다.

- 다해상도 Play Mode와 기기에서의 UpgradeUI / Safe Area, 드래그 합성 대상 표시
- `Builds/Release/0.1.08/`의 AAB는 2026-08-29 산출물이라 저장 로드 실패 처리가
  들어 있지 않습니다. 해당 내용을 포함한 릴리스 AAB와 Play Console 업로드는
  아직 없습니다

다음 개발 단계는 Phase 4 가챠권입니다.
