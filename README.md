# Monster Kindergarten

슬라임을 생성하고 합성해 더 높은 등급으로 성장시키는 Android 방치형 클리커 게임입니다. 클릭과 자동 생산으로 포인트를 모으고, 슬라임 및 스폰 관련 업그레이드를 진행합니다.

## 주요 기능

- 슬라임 생성 및 동일 등급 합성
- 수동 클릭·자동 생산 포인트 획득
- 접속하지 않은 시간에 따른 오프라인 자동 생산 보상
- 등급별 수동·자동 생산량 업그레이드
- 스폰 간격 및 최대 슬라임 수 업그레이드
- 신규 플레이어용 튜토리얼
- BGM 및 효과음
- Google Play Games 로그인
- Firebase Authentication 및 Firestore 기반 클라우드 저장
- Unity Editor용 로컬 저장

## 개발 환경

- Unity `6000.3.21f1`
- Android
- Universal Render Pipeline 2D
- Input System
- Google Play Games Plugin for Unity
- Firebase Authentication / Firestore
- UniTask / DOTween

## 실행

1. Unity Hub에서 Unity `6000.3.21f1`과 Android Build Support를 설치합니다.
2. 저장소를 Unity 프로젝트로 엽니다.
3. `Assets/01.Scenes/LoginScene.unity`에서 Play Mode를 실행합니다.
4. Android 로그인과 클라우드 저장은 프로젝트에 맞는 Firebase 및 Google Play Games 설정이 필요합니다.

## 문서

- [Google Play Games 및 Firebase 연동 기록](Documentation/GOOGLE_PLAY_FIREBASE_INTEGRATION.md)
- [C# 코딩 컨벤션](Documentation/CODING_CONVENTION.md)
- [PlayerPrefs 저장 비용 측정 기록](Documentation/PLAYERPREFS_SAVE_PROFILING.md)

## 주요 폴더

- `Assets/01.Scenes`: 로그인 및 게임 장면
- `Assets/02.Scripts`: 게임 로직, 저장소, UI 코드
- `Assets/03.Prefabs`: 슬라임과 UI 프리팹
- `Assets/10.ScriptableObjects`: 튜토리얼 데이터

슬라임과 업그레이드 밸런스 데이터는 `Assets/02.Scripts/Outgame/Feature/` 아래 각 도메인 폴더에 있습니다.

## 현재 상태

Google Play 내부 테스트를 통해 로그인, 재로그인, 클라우드 저장, 앱 데이터 삭제 후 복구 및 오프라인 보상 수령을 확인했습니다. 프로젝트는 현재 개발 중입니다.
