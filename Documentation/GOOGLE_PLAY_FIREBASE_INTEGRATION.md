# Google Play Games 및 Firebase 연동 기록

작성일: 2026-08-12  
최종 재검증: 2026-08-14  
대상 플랫폼: Android  
패키지명: `com.skku_say.Monster_Kindergarten`

구현 기록 갱신: 2026-08-27 (과거 기기 검증일과 별개)

> 이 문서에는 실제 SHA 인증서 지문, OAuth 클라이언트 ID와 보안 비밀번호, Firebase UID, 키스토어 경로와 비밀번호를 기록하지 않았습니다.

## 1. 현재 완료 상태

- 이메일/비밀번호 인증을 Google Play Games 로그인으로 교체했습니다.
- Google Play Games 서버 인증 코드를 Firebase Authentication 자격 증명으로 교환하도록 구현했습니다.
- Firebase UID를 사용자별 로컬 저장 키와 Firestore 문서 ID로 사용했습니다.
- Google Play 내부 테스트 트랙에 첫 AAB를 배포했습니다.
- 직접 설치한 릴리스 빌드와 Play 스토어 내부 테스트 빌드에서 로그인을 확인했습니다.
- 동일 Google Play 계정으로 재로그인했을 때 기존 게임 데이터가 복구되는 것을 확인했습니다.
- 앱 데이터 삭제 및 Play 스토어 재설치 후에도 재화, 슬라임 현황, 업그레이드가 복구되는 것을 확인했습니다.

현재 인증 및 저장 흐름은 다음과 같습니다.

```text
Google Play Games 계정
    -> 서버 인증 코드 요청
    -> Firebase Authentication 로그인
    -> Firebase UID 획득
    -> UID별 PlayerPrefs 및 Firestore 데이터 사용
```

## 2. 재검증한 전체 작업 순서

Google과 Firebase의 공식 문서를 기준으로 작업 순서를 다시 검증했습니다.

1. Unity Android 패키지명과 릴리스 키스토어를 확정했습니다.
2. Firebase에 Android 앱을 등록하고 `google-services.json`과 최초 SHA-1을 반영했습니다.
3. Firebase Authentication에서 Google과 Play Games 로그인 제공업체를 구성했습니다.
4. Play Games 서비스 프로젝트를 같은 Google Cloud/Firebase 프로젝트에 연결했습니다.
5. 게임 서버용 Web OAuth와 직접 설치용 Android OAuth 사용자 인증 정보를 등록했습니다.
6. Play Games 리소스 정의와 Web Client ID를 Unity 플러그인에 적용했습니다.
7. Google Play Games 로그인과 Firebase UID 로그인을 구현했습니다.
8. UID 기반 Firestore 저장 구조와 보안 규칙을 적용했습니다.
9. 릴리스 AAB를 내부 테스트 트랙에 배포했습니다.
10. Play 앱 서명 SHA-1을 Play Games와 Firebase에 추가했습니다.
11. 앱 내부 테스트와 Play Games 서비스 테스트 권한을 연결했습니다.
12. Play 스토어 설치본에서 로그인과 클라우드 복구를 검증했습니다.

기존 기록에서는 Play Games Console 구성을 Firebase 기본 구성보다 먼저 설명했습니다. 하지만 실제 작업에서는 Firebase Android 앱, 서명 SHA-1, Google/Play Games 제공업체와 Web OAuth 정보를 먼저 준비해야 사용자 인증 정보를 안정적으로 연결할 수 있었습니다. 이 흐름에 맞춰 문서의 순서를 다시 정리했습니다.

## 3. Unity 및 Firebase 기본 구성

### 3.1 Android 패키지명과 키스토어

1. Unity Player Settings의 Android 패키지명을 Firebase와 Play Console에서 사용할 값으로 확정했습니다.
2. 직접 설치할 릴리스 빌드에 사용할 키스토어와 키 별칭을 만들었습니다.
3. `keytool`로 해당 키의 SHA-1을 확인했습니다.

![키스토어 SHA-1 확인 명령](Images/google-play-firebase/01-keystore-sha-command.png)

키스토어 파일과 비밀번호는 프로젝트 외부에 보관했습니다. Play Games 로그인은 개발 중 직접 설치하는 APK에도 서명이 필요했습니다.

### 3.2 Firebase Android 앱 등록

1. Firebase 프로젝트에 Android 앱을 등록했습니다.
2. 패키지명이 Unity 및 Play Console과 같은지 확인했습니다.
3. 최신 `google-services.json`을 받아 이름을 변경하지 않고 Unity의 `Assets/` 폴더에 넣었습니다.

![Firebase Android 최신 구성 파일 다운로드](Images/google-play-firebase/02-firebase-download-config.png)

4. 키스토어의 SHA-1을 Firebase Android 앱의 SHA 인증서 지문에 추가했습니다.

![Firebase Android 앱 최초 SHA 등록](Images/google-play-firebase/03-firebase-android-initial-sha.png)

`google-services.json`에는 앱 구성에 필요한 식별자가 들어 있지만 비밀키는 아닙니다. 그래도 문서나 이슈에 내용을 직접 복사하지 않고 프로젝트의 저장소 정책에 맞춰 관리했습니다.

### 3.3 Firebase Authentication 제공업체

1. Firebase Authentication의 로그인 방법에서 새 로그인 제공업체를 추가했습니다.

![Firebase Authentication 로그인 제공업체 목록](Images/google-play-firebase/04-firebase-auth-provider-list.png)

2. Google 제공업체를 선택했습니다.

![Firebase Google 로그인 제공업체 선택](Images/google-play-firebase/05-firebase-google-provider-selection.png)

3. Google 제공업체를 활성화하고 프로젝트 공개 이름과 지원 이메일을 설정했습니다.

![Firebase Google 로그인 제공업체 설정](Images/google-play-firebase/06-firebase-google-provider-config.png)

4. Google 제공업체에 연결된 Web OAuth 클라이언트를 확인했습니다. 이 Web 클라이언트의 ID와 보안 비밀번호를 Play Games 제공업체에 사용했습니다.

![Firebase Google 제공업체 클라이언트 설정](Images/google-play-firebase/07-firebase-google-provider-client-settings.png)

![Firebase Google 제공업체 Web SDK 구성](Images/google-play-firebase/08-firebase-google-web-sdk-config.png)

5. Play Games 제공업체를 활성화하고 같은 게임 서버용 Web OAuth 클라이언트 정보를 입력했습니다.

![Firebase Play Games 로그인 제공업체 설정](Images/google-play-firebase/09-firebase-play-games-provider-config.png)

## 4. Google Play Games 및 Unity 플러그인 구성

### 4.1 Play Games 서비스 프로젝트 연결

Play Games 서비스 설정에서 Firebase가 사용하는 Google Cloud 프로젝트를 선택했습니다. 인증에 사용하는 Firebase, Google Cloud, Play Games 서비스가 같은 프로젝트에 연결되어 있는지도 확인했습니다.

![Play Games 서비스와 Google Cloud 프로젝트 연결](Images/google-play-firebase/10-play-games-project-link.png)

### 4.2 Web 및 Android 사용자 인증 정보

1. Google Cloud에서 게임 서버에 사용할 Web OAuth 클라이언트를 확인했습니다.

![Google Cloud Web OAuth 클라이언트 설정](Images/google-play-firebase/11-google-cloud-web-client.png)

2. Play Games 서비스에 게임 서버 유형의 사용자 인증 정보를 추가하고 같은 Web OAuth 클라이언트를 연결했습니다.

![Play Games 게임 서버 사용자 인증 정보](Images/google-play-firebase/12-play-games-game-server-credential.png)

3. Android 유형의 사용자 인증 정보를 추가하고 패키지명과 직접 설치용 키스토어 SHA-1에 해당하는 Android OAuth 클라이언트를 선택했습니다.

![Play Games Android 사용자 인증 정보 추가](Images/google-play-firebase/13-play-games-add-android-credential.png)

4. Android 사용자 인증 정보의 패키지명과 서명 지문이 Unity 빌드 설정과 일치하는지 확인했습니다.

![Play Games Android 사용자 인증 정보 상세](Images/google-play-firebase/14-play-games-android-credential.png)

### 4.3 Unity Google Play Games 플러그인

1. Play Games 서비스 설정에서 Android 리소스 정의를 복사했습니다.

![Play Games Android 리소스 정의](Images/google-play-firebase/15-play-games-resource-definition.png)

2. Unity에서 `Window -> Google Play Games -> Setup -> Android Setup`을 열었습니다.
3. Android 리소스 정의와 게임 서버용 Web Client ID를 입력했습니다.
4. `Setup`을 실행해 설정 파일과 리소스를 생성했습니다.

![Unity Google Play Games Android 설정](Images/google-play-firebase/16-unity-play-games-configuration.png)

### 4.4 Unity 인증 구현

주요 파일:

- `Assets/02.Scripts/Outgame/Feature/Account/1.Repository/FirebaseAccountRepository.cs`
- `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset`
- `ProjectSettings/GooglePlayGameSettings.txt`

로그인 순서:

1. `PlayGamesPlatform.Activate()`로 Play Games 플랫폼을 활성화했습니다.
2. 앱 시작 시 `Authenticate()`로 자동 로그인을 시도하도록 구현했습니다.
3. 로그인 버튼에서는 `ManuallyAuthenticate()`로 수동 로그인을 요청하도록 구현했습니다.
4. `RequestServerSideAccess(false)`로 서버 인증 코드를 요청했습니다.
5. `PlayGamesAuthProvider.GetCredential()`로 Firebase 자격 증명을 생성했습니다.
6. `FirebaseAuth.SignInWithCredentialAsync()`로 로그인했습니다.
7. 반환된 Firebase UID를 게임 저장소 생성에 사용했습니다.

현재 로그아웃은 Firebase 세션만 종료하도록 구현했습니다. Play Games 계정 선택은 Android의 Play Games 계정 설정에서 관리하도록 했고, 앱 내부 계정 전환 UI는 추후 작업으로 남겨두었습니다.

Firebase Authentication에서 Play Games 사용자의 생성 시각과 최근 로그인 시각이 갱신되는 것을 확인했습니다.

![Firebase Authentication 사용자 목록](Images/google-play-firebase/17-firebase-auth-users.png)

## 5. Firestore 저장 및 보안 규칙

### 5.1 UID 기반 저장 구조

Firestore 문서 ID로 Firebase UID를 사용했습니다.

| 데이터 | Firestore 경로 | 로컬 저장 |
| --- | --- | --- |
| 재화 | `Currency/{UID}` | UID가 포함된 PlayerPrefs 키 |
| 슬라임 현황 | `SlimeStatus/{UID}` | UID가 포함된 PlayerPrefs 키 |
| 업그레이드 | `Upgrade/{UID}` | UID가 포함된 PlayerPrefs 키 |

세 컬렉션과 `LastSaveTime`이 정상적으로 저장되는 것을 확인했습니다.

![Firestore 저장 결과](Images/google-play-firebase/18-firestore-save-result.png)

### 5.2 Firestore 보안 규칙

인증된 사용자가 자신의 Firebase UID와 같은 문서 ID에만 접근할 수 있도록 제한했습니다.

```text
rules_version = '2';

service cloud.firestore {
  match /databases/{database}/documents {
    function isOwner(userId) {
      return request.auth != null
          && request.auth.uid == userId;
    }

    match /Currency/{userId} {
      allow read, write: if isOwner(userId);
    }

    match /Upgrade/{userId} {
      allow read, write: if isOwner(userId);
    }

    match /SlimeStatus/{userId} {
      allow read, write: if isOwner(userId);
    }
  }
}
```

기존의 날짜 만료 기반 임시 허용 규칙은 제거했습니다. 규칙 게시 후 저장과 불러오기가 정상 작동하는 것도 확인했습니다.

![Firestore 보안 규칙](Images/google-play-firebase/19-firestore-security-rules.png)

## 6. AAB 내부 테스트 및 Play 앱 서명

### 6.1 Android Release 프로필

릴리스 프로필에서 다음 항목을 확인했습니다.

- LoginScene과 GameScene 포함
- `Build App Bundle (Google Play)` 활성화
- `Development Build` 비활성화
- 릴리스 키스토어로 서명

![Unity Android Release 프로필](Images/google-play-firebase/20-unity-android-release-profile.png)

### 6.2 내부 테스트 준비 및 첫 AAB 배포

1. 내부 테스트 이메일 목록에 테스트할 Google 계정을 추가했습니다.

![Google Play 내부 테스트 테스터 목록](Images/google-play-firebase/21-internal-test-testers.png)

2. 릴리스 AAB를 업로드하고 내부 테스트 트랙에 출시했습니다.
3. 트랙이 `활성`이고 출시가 `내부 테스터에게 제공됨` 상태인지 확인했습니다.

![Google Play 내부 테스트 활성화](Images/google-play-firebase/22-internal-test-active.png)

첫 App Bundle 분석 결과:

- 지원 기기: 약 12,918대
- 최소 Android API: 25
- Target SDK: 36
- 네이티브 플랫폼: `arm64-v8a`
- OpenGL ES: 3.0 이상
- 16KB 메모리 페이지 크기 지원
- 권한: 10개
- 기능: 4개
- 번들에 포함된 현지화 리소스: 85개
- 앱 최적화 평가: 낮음
- R8 구성 및 가독화 파일: 없음
- 네이티브 디버그 기호: 업로드하지 않음

![Google Play App Bundle 세부 분석](Images/google-play-firebase/23-app-bundle-analysis.png)

R8 가독화 파일과 네이티브 디버그 기호가 없다는 항목은 첫 내부 테스트 출시를 막는 오류가 아니었습니다. 최초 출시에서는 테스터 미지정 경고만 바로 해결했습니다.

![Google Play 내부 테스트 출시 경고](Images/google-play-firebase/24-app-bundle-warnings.png)

### 6.3 Play 앱 서명 SHA 추가

Play 스토어에서는 업로드한 AAB를 Google Play 앱 서명 키로 다시 서명합니다. 따라서 직접 설치한 릴리스 빌드와 Play 스토어 설치본은 서로 다른 SHA-1을 사용합니다.

- 업로드 키: 개발자가 AAB를 업로드할 때 사용합니다.
- 앱 서명 키: Google Play가 사용자에게 배포하는 APK에 사용합니다.
- Firebase와 Play Games에서 스토어 설치본을 인증하려면 앱 서명 키 인증서의 SHA-1이 필요합니다.

Play Console UI에서 앱 서명 키와 업로드 키 인증서를 함께 보여주는 화면은 별도로 확보하지 못했습니다. 이 화면 자체는 필수 증빙이 아니어서, 아래의 최종 Android 사용자 인증 정보 2개와 Firebase SHA 지문 2개 등록 상태로 적용 결과를 확인했습니다.

1. Play 앱 서명 SHA-1용 Android OAuth 사용자 인증 정보를 추가했습니다.
2. 기존 키스토어용 Android 인증 정보와 게임 서버용 Web 인증 정보는 그대로 유지했습니다.

![Play Games 최종 사용자 인증 정보 목록](Images/google-play-firebase/25-play-games-final-credentials.png)

3. 같은 Play 앱 서명 SHA-1을 Firebase Android 앱에도 추가했습니다.

![Firebase SHA 최종 등록 결과](Images/google-play-firebase/26-firebase-final-sha-registration.png)

직접 설치본 위에 Play 스토어 설치본을 덮어쓸 때 서명 충돌이 발생할 수 있었습니다. 그래서 기존 직접 설치본을 삭제한 뒤 Play 스토어에서 다시 설치했습니다.

### 6.4 Play Games 서비스 테스트 권한 연결

앱 내부 테스트 권한과 게시 전 Play Games 서비스를 사용할 권한은 서로 구분되어 있었습니다. 이 프로젝트에서는 Play Games 서비스 테스터 설정에 앱의 `internal` 출시 트랙을 연결했습니다.

설정 경로:

`사용자 늘리기 -> Play Games 서비스 -> 설정 및 관리 -> 테스터 -> 출시 트랙`

![Play Games 서비스 내부 테스트 출시 트랙 연결](Images/google-play-firebase/27-play-games-internal-release-track.png)

출시 트랙을 연결한 뒤에는 내부 테스트 참여 계정을 Play Games 서비스의 개별 테스터 목록에 중복 등록하지 않아도 로그인할 수 있었습니다. 문제가 생길 경우에는 Play Games 서비스의 개별 테스터 등록을 임시 진단 수단으로 사용할 수 있습니다.

### 6.5 테스터 설치 및 검증 순서

1. 내부 테스트 이메일 목록에 Google 계정을 추가했습니다.
2. 해당 계정으로 내부 테스트 참여 링크를 열고 테스트 참여를 완료했습니다.

![Google Play 내부 테스트 참여 화면](Images/google-play-firebase/28-internal-test-opt-in.jpg)

3. Play 스토어에서 테스트 앱을 설치했습니다.

![Google Play 테스트 앱 설치 결과](Images/google-play-firebase/29-play-store-test-app.jpg)

4. 휴대폰의 `설정 -> Google -> 모든 서비스 -> Google 앱 설정 -> Play Games -> 로그인 계정 -> 게임별 계정 변경`에서 Monster Kindergarten에 사용할 계정을 선택했습니다.
5. 앱을 완전히 종료한 뒤 다시 실행해 Play Games 로그인을 확인했습니다.

![Google Play Games 로그인 확인](Images/google-play-firebase/30-play-games-login.jpg)

6. 동일 Firebase UID의 기존 재화, 슬라임 현황, 업그레이드가 복구되는지 확인했습니다.

![Play 스토어 설치본 클라우드 복구 결과](Images/google-play-firebase/31-cloud-save-restored.jpg)

내부 테스트 앱은 일반 Play 스토어 검색에 노출되지 않았습니다. 등록된 Google 계정으로 참여 링크를 열어 테스트에 참여한 뒤 설치했습니다.

## 7. 로컬 및 클라우드 저장 구현

주요 파일:

- `Assets/02.Scripts/Outgame/Feature/Common/HybirdRepository.cs`
- 각 기능의 PlayerPrefs 및 Firebase Repository
- `CurrencySaveData`, `SlimeStatusSaveData`, `UpgradeSaveData`

저장 동작:

1. 데이터를 저장할 때 UTC 기준 `LastSaveTime`을 갱신하도록 구현했습니다.
2. PlayerPrefs에는 즉시 저장하도록 구현했습니다.
3. Firebase 저장은 연속 요청을 합치기 위해 0.6초 지연하도록 구현했습니다.
4. Firebase 저장에 실패하면 오류를 기록하고 로컬 플레이는 유지하도록 구현했습니다.

불러오기 동작:

1. PlayerPrefs와 Firebase를 동시에 불러오도록 구현했습니다.
2. 양쪽의 `LastSaveTime`을 비교했습니다.
3. 더 최신인 데이터를 사용하도록 구현했습니다.
4. 시간이 같으면 Firebase 데이터를 우선하고 로컬에 다시 저장하도록 구현했습니다.

### 클라우드 복구 오류 수정 기록

앱 데이터를 삭제한 뒤 로그인했을 때 재화는 복구됐지만, 슬라임 현황과 업그레이드가 초기화되는 문제가 있었습니다.

원인:

- 슬라임 및 업그레이드의 `LastSaveTime`이 Firestore에 직렬화되지 않았습니다.
- 저장 시간이 같은 경우 로컬 초기 데이터를 선택하고 있었습니다.

수정:

- `SlimeStatusSaveData.LastSaveTime`과 `UpgradeSaveData.LastSaveTime`에 `[FirestoreProperty]`를 추가했습니다.
- 충돌 비교를 `playerprefsTime > firebaseTime`으로 변경해 시간이 같으면 Firebase를 우선하도록 수정했습니다.

결과:

- 재화, 슬라임 현황, 업그레이드가 앱 데이터 삭제 및 재설치 후 모두 정상적으로 복구되는 것을 확인했습니다.

### 슬라임 저장 스키마 후속 상태 (2026-08-25)

Phase 1.5에서 슬라임 저장을 등급별 개수에서 개체 목록으로 전환했습니다.
현재 슬라임 저장 스키마는 v2이며 각 개체에 `InstanceId`, `Grade`,
`IsSpecial`, `Location`을 기록합니다. 화면 좌표는 저장하지 않습니다.

Phase 2 장식장은 `Location`을 사용해 메인 스테이지와 장식장 소속을 구분합니다.
이동은 `SlimeManager.MoveSlime()`에서 검증과 저장을 함께 처리하며 기존
PlayerPrefs 즉시 저장, Firestore 지연 저장, `LastSaveTime` 충돌 해결 규칙은
바뀌지 않았습니다.

Unity Editor에서는 저장 후 재실행 시 장식장 소속 복원을 확인했습니다.
Phase 2가 포함된 Android 빌드의 Firestore 복원, 기기 간 충돌과 앱 데이터 삭제
복구는 아직 재검증하지 않았으며 Phase 8 릴리스 준비에서 수행합니다.

### 옵션의 진행도 초기화 (2026-08-27 구현 기록)

`OptionsUI`의 확인창에서 초기화를 승인하면 `GameDataResetService`가 다음
범위만 삭제합니다. 계정 탈퇴나 모든 기기의 데이터 무효화 기능은 아닙니다.

- Android: 로그인한 UID의 `Currency`, `SlimeStatus`, `Upgrade` 문서와
  해당 사용자의 이 기기 PlayerPrefs 저장·튜토리얼 완료 기록.
- Editor: `LocalPlayer`의 로컬 진행도·튜토리얼 완료 기록만 삭제.
- 유지: Firebase Authentication 계정, 다른 UID 데이터, 기기 음량 설정.

초기화 흐름:

1. 현재 로그인 UID와 Android 연결 상태를 확인합니다.
2. `GameplaySaveGate.BeginReset()`으로 진행·저장을 잠그고 로컬 중단 표시를 남깁니다.
3. `HybridRepository`는 초기화 이전 세대의 지연 쓰기를 폐기합니다.
   이미 전송된 Firestore 쓰기는 완료를 기다린 뒤 세 문서를 배치 삭제합니다.
4. 로컬 진행도·튜토리얼 완료 기록과 중단 표시를 삭제합니다.
5. 로그아웃하고 로그인 화면으로 돌아가며 이번 자동 로그인은 생략합니다.

초기화가 중단되면 다음 로그인에서 중단 표시를 확인해 게임 진입 전에 다시
처리합니다. 타임아웃은 서버 작업 취소를 보장하지 않으므로 결과가 불명확한
상태에서 기존 게임을 다시 저장하도록 허용하지 않습니다.

**다기기 제한:** 다른 기기의 로컬 저장은 그대로 남습니다. 해당 기기가 나중에
접속하면 이전 진행도가 클라우드에 다시 저장될 수 있습니다. 계정 전체를
초기화하는 세대 번호나 서버 삭제 표식 정책은 아직 구현하지 않았습니다.

튜토리얼 완료 기록은 현재 PlayerPrefs 전용이며 Firestore 동기화 대상이 아닙니다.
장식장 입고 위치는 슬라임 문서에 저장하고, 미완료 튜토리얼은 복원 완료 후
장식장에 이미 있는 개체를 찾아 추가 입고 없이 안내를 재개합니다.

### 0.1.04 빌드 상태

- 릴리스 프로필: `0.1.04 / Version Code 5`, AAB, Development Build 꺼짐.
- 개발 프로필: `0.1.03 / Version Code 4`, APK, Development Build 켜짐.
- 로컬 AAB와 `build-info.txt`는 `Builds/Release/0.1.04/`에 있으며 Git에서 제외됩니다.
- 파일 생성과 설정은 확인했지만 이번 AAB의 초기화·복원·스토어 업로드 결과는
  아직 기록되지 않았습니다. 기존 기기 검증 결과와 구분합니다.

## 8. 실제 기기 검증 결과

아래는 기존 인증·저장 통합의 확인 기록입니다. `0.1.04` 장식장·옵션 초기화가
포함된 AAB의 검증 완료를 의미하지 않습니다.

- [x] 직접 설치한 릴리스 빌드에서 Google Play Games 로그인
- [x] Play 스토어 내부 테스트 빌드 설치
- [x] Play 앱 서명용 OAuth 인증 정보 적용 후 Google Play Games 로그인
- [x] Play Games 서비스에 `internal` 출시 트랙 연결
- [x] 개별 Play Games 테스터로 등록하지 않은 새 내부 테스트 계정으로 로그인
- [x] Firebase Authentication 사용자 생성 및 최근 로그인 시각 갱신
- [x] UID별 Firestore 데이터 저장
- [x] 앱 종료 후 재로그인 및 데이터 복구
- [x] 앱 데이터 삭제 후 재화, 슬라임 현황, 업그레이드 복구
- [x] 직접 설치본 삭제 후 Play 스토어 설치본에서 동일 계정 데이터 복구
- [x] Firestore UID 소유자 제한 규칙 적용 후 저장 및 불러오기

동일 Google Play 계정으로 기존 데이터가 복구된 것은 정상 동작입니다. 앱 설치 경로가 달라도 같은 계정은 같은 Firebase UID와 Firestore 문서를 사용하도록 구현했습니다.

## 9. 다음 릴리스 전에 확인할 항목

- [ ] Unity Android Release 프로필에서 Public 네이티브 디버그 심볼 ZIP 생성
- [ ] 각 버전에 대응하는 심볼 파일을 Play Console에 업로드
- [ ] Android 권한 10개가 모두 필요한지 검토
- [ ] R8 및 코드 축소는 기능 안정화 후 별도 적용
- [ ] R8 또는 Managed Stripping 변경 후 Google Play 로그인과 Firebase 저장 재검증
- [ ] 실제 지원 언어와 스토어 등록정보 언어 정리
- [ ] 앱 내부 로그아웃 및 Google 계정 전환 UX 검토
- [ ] 개인정보처리방침, 데이터 보안, 계정 삭제 등 정식 출시 항목 준비
- [ ] 새 개인 개발자 계정의 비공개 테스트 및 정식 출시 자격 요건 확인

## 10. 보안 및 저장소 관리

다음 항목은 Git에 커밋하지 않도록 관리했습니다.

- 키스토어 파일과 비밀번호
- 실제 SHA 인증서 지문을 모아둔 개인 문서
- OAuth 클라이언트 보안 비밀번호
- Firebase UID 등 개인 테스트 계정 식별 정보
- 개인 기기 ID

OAuth 클라이언트 ID, Play Games 앱 ID, Firebase Android 설정은 클라이언트 앱 구성을 위한 식별자지만 불필요하게 외부 문서에 복사하지 않았습니다. 키스토어와 비밀번호는 프로젝트 외부에 별도로 백업했습니다.

## 11. 완료 판단

현재 단계에서 다음 목표를 완료했습니다.

- Google Play Games 기반 로그인
- Firebase Authentication 연결
- Firebase UID 기반 사용자 데이터 분리
- PlayerPrefs와 Firestore의 하이브리드 저장
- Play 스토어 앱 서명 빌드 인증
- 삭제 및 재설치 후 클라우드 복구

향후 작업은 인증 연결 수정이 아니라 출시 준비, 최적화, 계정 전환 UX 및 게임성 보강 단계로 분류했습니다.

## 12. 공식 참고 자료

- [Firebase Unity에서 Google Play Games 인증](https://firebase.google.com/docs/auth/unity/play-games)
- [Google Play Games Unity 플러그인 설정](https://developer.android.com/games/pgs/unity/unity-start)
- [Firebase Unity 프로젝트 설정](https://firebase.google.com/docs/unity/setup)
- [Cloud Firestore 보안 규칙 조건](https://firebase.google.com/docs/firestore/security/rules-conditions)
- [Google Play 앱 서명](https://support.google.com/googleplay/android-developer/answer/9842756)
- [Google Play 내부 테스트 설정](https://support.google.com/googleplay/android-developer/answer/9845334)
