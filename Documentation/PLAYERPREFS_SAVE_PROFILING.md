# PlayerPrefs.Save() 비용 측정 기록

작성일: 2026-08-20  
대상 플랫폼: Android  
패키지명: `com.skku_say.Monster_Kindergarten`  
결론: **가설 기각. 코드 수정 불필요.**

## 1. 배경

코드 리뷰 중 다음 가설이 제기되었습니다.

> `CurrencyManager.Add()`가 포인트 획득마다 `Save()`를 호출하고, 그 끝에서 `PlayerPrefs.Save()`가 디스크에 동기 쓰기를 수행한다.
> 슬라임이 최대치일 때 초당 7회 이상 호출되므로 Android에서 프레임 히칭을 유발할 것이다.

호출 경로는 다음과 같습니다.

```text
SlimeController.OnClick()          수동 클릭 + 자동 클릭 공통
  -> CurrencyManager.Add()
    -> Save() -> SaveCurrentAsync()
      -> LocalCurrencyRepository.Save()
        -> PlayerPrefs.Save()      디스크 플러시
```

`HybridRepository`는 Firebase 쓰기에 0.6초 디바운스를 적용하지만 로컬 쓰기에는 적용하지 않습니다. 설계 의도와 실제 비용 분포가 어긋나 보인다는 점이 가설의 출발점이었습니다.

**이 가설은 측정 없이 제기되었습니다.** 검증을 위해 실기기 계측을 수행했습니다.

## 2. 측정 환경

| 항목 | 값 |
|---|---|
| 기기 | samsung SM-S918N (Galaxy S23 Ultra) |
| OS | Android 16.0 (SDK 36) |
| ABI | arm64-v8a |
| Unity | 6000.3.21f1 |
| 빌드 프로필 | `Android™` (Development Build = ON) |
| 측정 시각 | 2026-08-20 16:32 ~ 16:42 (약 9분 44초) |

### 2.1 에디터에서 측정하지 않은 이유

`PlayerPrefs`는 플랫폼 네이티브 저장소를 사용합니다.

- Windows 에디터: 레지스트리
- Android: `SharedPreferences` XML 파일

**완전히 다른 코드 경로이므로 에디터 측정값은 이 가설에 대해 아무 정보도 주지 못합니다.** 반드시 실기기에서 측정해야 합니다.

### 2.2 빌드 전 준비 (중요)

`Android™` 프로필은 기본적으로 `androidUseCustomKeystore: 0`이라 디버그 키스토어로 서명됩니다. Firebase와 Play Games에 등록된 SHA-1은 직접 설치용 키스토어와 Play 앱 서명 키뿐이므로, **디버그 키로 서명하면 Google Play Games 로그인이 실패하여 GameScene에 진입할 수 없습니다.**

측정 전에 `Android™` 프로필의 Publishing Settings에서 릴리스와 동일한 커스텀 키스토어를 지정해야 합니다. Development Build 플래그와 서명 키는 서로 독립적이므로 둘 다 설정할 수 있습니다.

이미 Play 스토어 설치본이 있는 경우 서명 불일치로 설치가 거부됩니다.

```text
INSTALL_FAILED_UPDATE_INCOMPATIBLE
```

이때는 기존 앱을 제거한 뒤 설치합니다. 로컬 세이브는 사라지지만 Firebase UID 기준으로 클라우드에서 복구됩니다.

### 2.3 사용하지 않은 도구와 이유

- **Deep Profile**: 모든 메서드에 계측을 걸어 오버헤드가 큽니다. 측정 대상이 1ms 미만 단위이므로 측정 자체가 결과를 왜곡합니다.
- **Profile Analyzer**: 수정 전후 비교에는 적합하지만, "비용이 존재하는가"라는 1차 질문에는 과한 도구입니다. 1차 측정에서 문제가 확인되면 그때 도입하기로 했습니다.

## 3. 계측 방법

`LocalCurrencyRepository.Save()`에서 `PlayerPrefs.Save()` 호출만 감쌌습니다. `PlayerPrefs.SetString()`은 메모리 딕셔너리 쓰기라 측정 대상에서 제외했습니다.

```csharp
#if DEVELOPMENT_BUILD
        long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        PlayerPrefs.Save();
        RecordSaveCost(System.Diagnostics.Stopwatch.GetTimestamp() - startTicks);
#else
        PlayerPrefs.Save();
#endif
```

### 3.1 설계 의도

- **`#if DEVELOPMENT_BUILD`**: 릴리스 빌드에는 순수 `PlayerPrefs.Save()`만 남아 영향이 없습니다. 에디터에서도 정의되지 않으므로, 의미 없는 에디터 수치에 오도될 여지를 차단합니다.
- **`Stopwatch.GetTimestamp()`**: `Stopwatch` 인스턴스를 할당하지 않고 틱만 읽습니다. 계측이 GC를 유발하면 안 되기 때문입니다.
- **평균이 아닌 최대값과 임계 초과 횟수**: 가설의 핵심은 "가끔 튄다"였습니다. 평균만으로는 검증할 수 없으므로 `max`, `>2ms` 횟수, `>5ms` 횟수를 함께 수집했습니다.
- **구간별 호출 빈도**: 추정했던 7.5회/초가 실제와 맞는지 함께 검증했습니다.

100회마다 다음 형식으로 출력했습니다.

```text
[PlayerPrefs] n=100 avg=0.157ms max=0.689ms >2ms=0 >5ms=0 rate=2.6/s
```

### 3.2 측정 조건

슬라임을 최대치까지 채운 상태에서 측정했습니다. 최대 슬라임 수는 기본 10마리에 `MaxCountAdd` 업그레이드(Linear, PointMultiplier 1.0, MaxLevel 20)를 더해 30마리입니다. 초반 상태로 측정하면 호출 빈도가 낮아 최악 조건이 재현되지 않습니다.

## 4. 로그 확인 방법

Android Logcat 패키지를 사용했습니다.

```text
com.unity.mobile.android-logcat
```

`Window -> Analysis -> Android Logcat`으로 엽니다. 기본 상태에서는 기기 전체의 시스템 로그가 출력되어 게임 로그를 찾을 수 없습니다.

필터 두 가지를 적용하면 계측 로그만 남습니다.

1. 패키지 드롭다운에서 `com.skku_say.Monster_Kindergarten` 선택 (앱이 실행 중이어야 목록에 표시됨)
2. 검색창에 `PlayerPrefs` 입력

패키지 필터가 없으면 블루투스, 배터리, DNS 등 시스템 로그가 대부분을 차지합니다.

### 4.1 대안

- **Unity Console**: Console 창의 Connected Player 드롭다운을 `Editor`에서 `AndroidPlayer`로 변경하면 기기 로그가 표시됩니다. 별도 설치가 필요 없습니다.
- **adb logcat**: Unity 번들 adb를 사용합니다. 경로는 `<Unity 설치 경로>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe` 입니다.

## 5. 측정 결과

<img src="Images/playerprefs-profiling/Android%20Logcat.png" alt="PlayerPrefs 계측 결과" style="zoom:150%;" />

2500회 호출, 약 9분 44초간 측정했습니다.

| 지표 | 측정값 |
|---|---|
| 평균 | **0.150 ms** |
| 최대 (2500회 중) | **0.689 ms** |
| 2ms 초과 횟수 | **0회** |
| 5ms 초과 횟수 | **0회** |
| 호출 빈도 | 2.4 ~ 7.5회/초 (평균 약 4.3회/초) |

`max` 값은 측정 초반에 0.689ms를 기록한 뒤 **2500회가 끝날 때까지 한 번도 갱신되지 않았습니다.** 평균의 4.6배가 천장이며, 이는 60fps 프레임 예산 16.7ms의 약 1/24입니다.

부하로 환산하면 평균 4.3회/초 기준 **초당 0.65ms**, 프레임 예산 대비 **약 0.07%**입니다. 피크 7.5회/초에서도 0.11% 수준입니다.

### 5.1 fsync 로그 관찰

측정 중 Android 시스템 로그가 두 번 관측되었습니다.

```text
SharedPreferencesImpl: Time required to fsync /data/user/0/com.skku_say.Monster_Kindergarten/shared_prefs/...
```

2500회 저장 중 2회만 기록되었고, 해당 시점에도 계측된 `max` 값은 변하지 않았습니다. Android가 실제 디스크 fsync를 백그라운드 스레드로 처리하고 있음을 시사합니다.

**이번 Galaxy S23 Ultra 측정에서는 "메인 스레드 동기 디스크 I/O가 프레임 히칭을 유발한다"는 가설을 뒷받침하는 결과가 관측되지 않았습니다.**

## 6. 결론

| 판정 기준 (측정 전 수립) | 실제 결과 | 판정 |
|---|---|---|
| `max` < 1ms, 초과 0회 → 종결 | max 0.689ms, 초과 0회 | **해당** |
| max 2~5ms → 체감 시 처리 | - | - |
| max > 5ms → 수정 필요 | - | - |

**가설을 기각합니다. 성능을 이유로 한 수정은 필요하지 않습니다.**

호출 빈도 추정(7.5회/초)은 정확했으나, 호출당 비용과 꼬리 지연 위험을 과대평가한 것이 오류의 원인이었습니다.

### 6.1 부수 영향

같은 리뷰에서 제기된 "잦은 디스크 쓰기가 세이브 손상 창을 넓힌다"는 논리도 근거를 잃었습니다. `PlayerPrefsSlimeStatusRepository`의 null 반환 수정은 유지하되, 그 근거는 손상 확률이 아니라 `PlayerPrefsUpgradeRepository`와의 예외 처리 비대칭 해소입니다.

### 6.2 한계

- 플래그십 기기 1대에서만 측정했습니다. 중저가 기기에서는 절대값이 더 클 수 있습니다. 다만 최대값이 0.689ms이므로 10배 느려져도 프레임 예산의 절반에 미치지 않습니다.
- Development Build로 측정했습니다. 릴리스 빌드는 일반적으로 더 빠르므로 측정값은 보수적인 상한으로 볼 수 있습니다.

## 7. 재현 방법

1. `Android™` 프로필에 릴리스와 동일한 커스텀 키스토어를 지정합니다. (2.2 참조)
2. `LocalCurrencyRepository.Save()`에 3장의 계측 코드를 삽입합니다.
3. Development Build로 빌드하여 실기기에 설치합니다.
4. 로그인 통과를 먼저 확인합니다. 여기서 막히면 1번이 적용되지 않은 것입니다.
5. 슬라임을 최대치까지 채웁니다.
6. Android Logcat에서 패키지와 `PlayerPrefs`로 필터링합니다.
7. 최소 1000회 이상 누적된 뒤 `max`와 임계 초과 횟수를 확인합니다.
8. 측정 종료 후 계측 코드를 완전히 제거합니다.

## 8. 남은 교훈

측정 전에 이 항목은 리팩터링 목록 1순위로 분류되어 있었습니다. 측정 결과 근거 없는 순위였음이 확인되어 항목을 종결했습니다.

같은 리뷰에서 제기된 "매 프레임 힙 할당"(`AutoClicker`의 `new List`, `Clicker`의 `OverlapCircleAll`) 항목도 측정 없이 제기된 것입니다. **Profiler의 GC Alloc 수치를 확인하기 전까지는 우선순위를 부여하지 않습니다.**

성능 항목은 코드를 읽어 추정하지 않고 실기기에서 측정한 뒤 판단합니다.
