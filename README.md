# Monster Kindergarten

슬라임을 생성하고 합성해 더 높은 등급으로 성장시키는 Android 방치형 클리커 게임입니다. 클릭과 자동 생산으로 포인트를 모으고, 슬라임 및 스폰 관련 업그레이드를 진행합니다.

## 주요 기능

- 슬라임 생성 및 동일 등급 합성
- 수동 클릭·자동 생산 포인트 획득
- 등급별 수동·자동 생산량 업그레이드
- 스폰 간격 및 최대 슬라임 수 업그레이드
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

상세한 연동 과정은 [Google Play Games 및 Firebase 연동 기록](Documentation/GOOGLE_PLAY_FIREBASE_INTEGRATION.md)을 참고하세요.

## 주요 폴더

- `Assets/01.Scenes`: 로그인 및 게임 장면
- `Assets/02.Scripts`: 게임 로직, 저장소, UI 코드
- `Assets/03.Prefabs`: 슬라임과 UI 프리팹
- `Assets/10.ScriptableObjects`: 슬라임 및 업그레이드 데이터

## 현재 상태

Google Play 내부 테스트를 통해 로그인, 재로그인, 클라우드 저장 및 앱 데이터 삭제 후 복구를 확인했습니다. 프로젝트는 현재 개발 중입니다.
