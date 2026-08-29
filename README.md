# Monster Kindergarten

슬라임을 생성하고 같은 등급끼리 합성해 성장시키는 Unity 6 기반 Android
방치형 클리커 게임입니다. 클릭과 자동 생산으로 포인트를 모아 슬라임의
생산량과 공용 스폰 시스템을 업그레이드합니다.

## 게임 흐름

- 최고 해금 등급과 업그레이드 상태에 따라 1~6등급 슬라임이 일정 시간마다
  자연 생성됩니다.
- 같은 등급의 슬라임 두 마리를 드래그해 합치면 다음 등급으로 성장합니다.
- 슬라임 터치와 자동 생산으로 포인트를 획득합니다.
- 등급별 터치·자동 생산량과 공용 스폰 간격·최대 수용량·상위 슬라임 등장
  확률을 강화합니다.
- 1~10등급은 지상, 11~20등급은 하늘 스테이지에서 활동합니다.
- 10등급 두 마리를 합쳐 처음 11등급을 만들면 하늘 스테이지와 이동 기능이
  해금됩니다.
- 최고 등급 3부터 장식장이 열리며, 메인 스테이지의 슬라임을 전시하거나
  다시 꺼낼 수 있습니다.
- 장식장에 넣은 일반 슬라임은 도감에 영구 등록되며 등급별 기록을 확인할 수
  있습니다.

## 주요 기능

- 최대 20등급의 슬라임 생성, 합성 및 등급별 애니메이션
- 최고 해금 등급에 따른 자연 스폰 상한과 Weight 기반 등장 확률
- 최대 50마리 수용량과 Lv.50 상위 슬라임 등장 확률 업그레이드
- 캐러셀 방식의 시스템 업그레이드 UI와 현재 스폰 확률 팝업
- 지상·하늘 스테이지 전환과 마지막으로 본 스테이지 저장
- GameScene 안의 세 번째 공간인 장식장과 전용 배경·전환 연출
- 연속 입고 선택 모드, 같은 종류 중복 보관 제한과 메인 필드 꺼내기
- 장식장 슬라임 정보, 한 문장 소개, 카메라 포커스와 관찰 모드
- 일반 슬라임 20종 도감, 상세 정보, 모션 미리보기와 등급별 누적 통계
- 활성 스테이지에서만 터치 입력과 슬라임 효과음 재생
- 지상·하늘 슬라임을 합산한 자동 생산과 최대 수용량 (장식장 제외)
- 스테이지별 랜덤 무한 스크롤 배경과 전용 BGM
- 접속하지 않은 시간에 따른 오프라인 자동 생산 보상
- 신규 플레이어 튜토리얼, 상위 슬라임·장식장 안내와 최초 하늘 진입 연출
- 시스템 업그레이드·이동 메뉴 전환과 공간별 이동 버튼 표시
- 우측 상단 옵션의 배경음·효과음 조절, 진행도 초기화와 게임 계정 삭제
- Google Play Games 로그인
- Firebase Authentication 및 Firestore 기반 클라우드 저장
- Unity Editor 및 비 Android 환경용 로컬 저장

오프라인 보상은 오프라인 플레이를 의미하지 않습니다. Android에서 게임을
시작하려면 Google Play Games 및 Firebase 로그인이 필요합니다.

## 개발 환경

- Unity `6000.3.21f1`
- Android / Universal Render Pipeline 2D
- Unity Input System `1.20.0`
- Google Play Games Plugin for Unity
- Firebase Authentication / Firestore `13.7.0`
- UniTask / DOTween / Lean Pool
- TextMesh Pro / UIEffect

## 빌드 프로필

| 용도 | 프로필 | 앱 버전 | Version Code |
| --- | --- | --- | ---: |
| Play Console 배포 | `Android_Release` | `0.1.07` | `8` |
| 개발 및 기기 확인 | `Android™` | `0.1.03` | `4` |

Android 애플리케이션 ID는
`com.skku_say.Monster_Kindergarten`입니다. Release 프로필은 AAB를 생성하며
`LoginScene` 다음에 `GameScene`을 포함합니다.

위 값은 2026-08-28 프로필 기준입니다. Release는 Development Build가 꺼진
AAB이고 개발 프로필은 Development Build가 켜진 APK입니다. 현재 프로젝트
전역 버전 대신 실제 빌드에 사용하는 프로필 값을 확인해야 합니다.

최근 확인된 로컬 산출물과 빌드 기록은 `Builds/Release/0.1.06/`에 있습니다.
`build-info.txt`에 변경사항·설정·AAB 해시를, `release-notes.txt`에 출시 노트를
기록합니다. `Builds/`는 Git에서 제외되며 파일 생성과 스토어 업로드·검증 완료는
별개입니다. 0.1.07 AAB 생성과 기기 검증은 아직 완료 기록이 없습니다.

## 실행

1. Unity Hub에서 Unity `6000.3.21f1`과 Android Build Support를 설치합니다.
2. 저장소를 Unity 프로젝트로 엽니다.
3. `Assets/01.Scenes/LoginScene.unity`에서 Play Mode를 실행합니다.
4. Android 기능은 Firebase, Google Play Games 및 서명 설정이 적용된 개발
   프로필이나 Release 프로필로 확인합니다.

Editor에서는 `LocalPlayer` 계정과 로컬 저장소를 사용합니다. Google Play Games
로그인, Firebase 저장·복구, 터치 입력, 서명 및 기기 성능은 Android 빌드 또는
Play Console 내부 테스트에서 별도로 검증해야 합니다.

## 저장 구조

- 통화, 슬라임 상태, 업그레이드를 독립된 도메인과 저장소로 관리합니다.
- Android에서는 PlayerPrefs에 즉시 저장하고 Firebase에 지연 반영하는
  `HybridRepository<T>`를 사용합니다.
- 통화·슬라임·업그레이드 저장 데이터에 도메인별 `SchemaVersion`을
  기록합니다.
- 활성 슬라임은 개체별 `InstanceId`, 등급, 특별 여부와 위치를 저장하며,
  화면 좌표는 저장하지 않습니다.
- 일반 슬라임의 영구 도감 등록 상태와 최초 등록일·자연 출현·합성 탄생·유효
  터치·누적 생산량을 등급별로 저장합니다.
- 기존 등급별 개수 저장은 결정적인 ID를 가진 개체 목록으로 승격합니다.
- 현재 지원 버전보다 높은 슬라임 저장 데이터는 구버전 앱이 덮어쓰지
  못하도록 로드를 중단합니다.
- 메인 튜토리얼 중에는 진행 저장을 잠그고 완료 시 세 도메인을 함께 저장합니다.
  장식장 튜토리얼의 입고 결과는 일반 이동과 동일하게 저장합니다.
- 장식장 튜토리얼은 슬라임 복원 후 시작합니다. 미완료 상태에서 재실행해도
  이미 입고된 슬라임이 있으면 추가 입고 없이 이동·정보 안내로 이어집니다.
  완료 플래그는 마지막 대화가 끝난 뒤 사용자별 PlayerPrefs에 기록합니다.

## 옵션, 진행도 초기화와 계정 삭제

- 배경음·효과음 음량은 기기별로 저장하며 재실행 시 복원합니다.
- 진행도 초기화는 확인창을 거친 뒤 현재 계정의 재화·슬라임·업그레이드와
  이 기기의 튜토리얼 완료 기록을 삭제하고 로그인 화면으로 돌아갑니다.
- Android에서는 현재 Firebase UID의 Firestore 문서 3개와 해당 로컬 저장을
  삭제합니다. Editor에서는 `LocalPlayer` 로컬 저장만 삭제합니다.
- 인증 계정과 음량 설정은 삭제하지 않습니다. 초기화 도중에는 게임 진행과
  저장을 막고, 중단된 초기화는 다음 로그인에서 게임 진입 전에 재개합니다.
- 계정 삭제는 같은 데이터 정리를 먼저 완료한 뒤 Firebase 인증 계정을 삭제합니다.
  중단되면 다음 로그인에서 삭제를 마무리하며 Google 계정과 Play 게임 프로필,
  기기별 음량 설정은 삭제하지 않습니다.
- 다른 기기의 로컬 저장까지 무효화하지는 않습니다. 그 기기로 접속하면
  이전 진행도가 클라우드에 다시 저장될 수 있습니다.

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

- [게임 기획 및 Phase 구현 스펙](Documentation/MonsterKindergarten_GAME_DESIGN_IMPLEMENTATION_SPEC.md)
- [Google Play Games 및 Firebase 연동 기록](Documentation/GOOGLE_PLAY_FIREBASE_INTEGRATION.md)
- [C# 코딩 컨벤션](Documentation/CODING_CONVENTION.md)
- [코드 리뷰 및 검증 기록](Documentation/CODE_REVIEW_2026-08-20.md)
- [PlayerPrefs 저장 비용 측정 기록](Documentation/PLAYERPREFS_SAVE_PROFILING.md)

## 현재 상태

기존 Android 내부 테스트에서 Google Play Games 로그인, 재로그인, Firebase
클라우드 저장, 앱 데이터 삭제 후 복구와 오프라인 보상 수령을 확인했습니다.

Phase 1·1.5의 자연 스폰·성장·개체 저장 기반, Phase 2 장식장과 Phase 2-B
관찰 UX에 이어 Phase 3 일반 슬라임 도감을 구현했습니다. 장식장 입고 시 자동
영구 등록하며, 일반 20종의 상세 정보·모션 미리보기와 등급별 누적 통계를
PlayerPrefs와 Firestore에 저장합니다. 특별 슬라임과 특별 도감은 후속 Phase
범위입니다.

2026-08-27에 `0.1.05 / Code 6` Release AAB 산출물을 확인했습니다. 하단 메뉴와
팝업 입력, 튜토리얼 재개, HUD 이미지 아틀라스와 리소스 정리를 추가로 반영했습니다.
빌드 시점의 버전 프로필과 일부 프로젝트 설정이 미커밋 상태였으므로 현재 HEAD만으로
빌드 상태를 완전히 재현할 수는 없습니다.

Unity Editor에서 장식장 해금, 공간 전환, 연속 입고, 중복 경고, 꺼내기,
저장 후 소속 복원, 정보 UI, 관찰 모드와 이동 중 카메라 추적을 확인했습니다.
이는 기존 사용자 확인 기록입니다. 후속 UI 수정도 Unity에서 확인했지만,
0.1.06 / Code 7 AAB는 Play Console 설치 후 로그인에서 GameScene으로 넘어갈 때
종료됐습니다. ADB에서 Safe Area 갱신의 재귀 호출로 인한 스택 오버플로우를
확인해 차단했으며, 수정된 0.1.07 / Code 8의 로그인·기존 저장 복원·도감 통계
저장은 새 빌드에서 다시 확인해야 합니다.
실행 테스트는 사용자가 수행하며, 기존 결과를 이번 빌드의 검증 완료로
확대하지 않습니다.

다음 개발 단계는 Phase 4 가챠권입니다.
