# 코드 리뷰 및 정리 기록

작성일: 2026-08-20  
대상 브랜치: `main`  
커밋: 14건 (`c6a518f` ~ `124a05b`)  
변경량: 62개 파일, 343줄 추가 / 5,734줄 삭제

프로젝트 전체를 리뷰하고 결함 수정, 죽은 코드 제거, 문서 최신화를 수행했습니다. 제기된 가설 중 일부는 검증 결과 기각되었으며, 그 판정 근거도 함께 기록했습니다.

## 1. 수정한 결함

### 1.1 슬라임 세이브 로드 실패 시 초기화 중단 (`56974d4`)

`PlayerPrefsSlimeStatusRepository.Load()`가 예외 발생 시 `null`을 반환했습니다. 소비부인 `SlimeManager.InitAsync()`는 null을 검사하지 않아 `NullReferenceException`이 발생하고, 그 결과 `OnDataInitialized`가 발화하지 않습니다.

이 이벤트가 없으면 `GameManager.TryInvokeAllInitialized()`가 완료되지 않아 `OnAllDataInitialized`도 발화하지 않고, **스폰과 UI가 모두 멈춘 상태**가 됩니다.

같은 구조의 `PlayerPrefsUpgradeRepository`는 동일한 catch에서 `Default`를 반환하고 있었습니다. 비대칭을 해소하는 방향으로 수정했습니다.

```csharp
// 변경 전
return UniTask.FromResult<SlimeStatusSaveData>(null);

// 변경 후
return UniTask.FromResult(SlimeStatusSaveData.Default);
```

Android에서는 `HybridRepository`가 `LastSaveTime`을 비교해 Firebase 데이터를 선택하므로 클라우드 저장에 영향이 없습니다.

### 1.2 불필요한 async 메서드 (`cdbd6db`)

`LocalCurrencyRepository`의 `Save()`와 `Load()`가 `await` 없이 `async`로 선언되어 CS1998 경고가 발생했습니다. `UniTask`를 직접 반환하도록 변경해 다른 PlayerPrefs 저장소와 구현 방식을 통일했습니다.

## 2. 디미터의 법칙 정리 (`9656904`)

호출부가 반복되거나 캡슐화가 새는 지점만 선별해 정리했습니다.

| 대상 | 조치 |
|---|---|
| `MergeManager`의 `keeper.Slime.SpecData.Grade` | 이미 존재하던 `SlimeController.Grade` 사용 |
| `SlimeManager.Instance.Status.HighestGrade` (4곳) | `SlimeManager.HighestGrade` 프로퍼티 추가 |
| `SlimeStatus.ActiveSlimes` | `IReadOnlyDictionary`로 변경 |

`ActiveSlimes`는 가변 `Dictionary`를 그대로 반환하고 있어, 호출부가 `AddSlime`/`RemoveSlime`의 등급 유효성 검사와 음수 검사를 우회할 수 있었습니다.

Unity 프레임워크 API 체인과 자료구조 탐색은 정리 대상에서 제외했습니다. 판단 기준은 `CODING_CONVENTION.md` 1.2절에 명문화했습니다.

## 3. 제거한 코드

총 약 907줄과 씬 2개를 제거했습니다.

### 3.1 미사용 저장소 코드 (`d9a8d9f`)

| 대상 | 근거 |
|---|---|
| `ICurrencyRepository` | 구현체 0개 |
| `MockSlimeStatusRepository` | 인스턴스화 0건 |
| `JsonUpgradeRepository` | 인스턴스화 0건 |
| `SlimeManager.RemoveSlime()` | 외부 호출 0건 |

`SlimeManager.RemoveSlime()`은 `AddSlime()`과 쌍을 이루도록 설계되었으나, 이 게임에서 디스폰이 발생하는 유일한 경로인 합성이 `MergeSlime()`으로 회계를 일괄 처리하면서 호출부가 생기지 않았습니다.

이후 단독 디스폰 경로를 추가할 때 `SlimeSpawner.Despawn()`에서 `RemoveSlime()`을 그대로 호출하면 **합성 시 3회 차감이 발생해 카운트가 깨집니다.** `MergeSlime()` 위에 주의 주석을 남겼습니다.

`ISlimeStatusRepository`와 `IUpgradeRepository`는 살아 있는 구현체가 4개 있어 제거하지 않았습니다.

### 3.2 개발용 코드 (`049929e`)

- F1 즉시 스폰 치트키와 미사용 `UnityEngine.InputSystem` 참조 제거
- Firebase 저장·로드 성공 로그, 오프라인 보상 로그 제거
- 실패 로그를 `Debug.Log`에서 `LogWarning` / `LogError`로 승격

### 3.3 학습용 샘플 (`d48ac2d`, `124a05b`)

| 대상 | 규모 |
|---|---|
| `Assets/WebAPITutorial/` + `WebAPITutorial.unity` | 438줄 + 씬 |
| `Assets/02.Scripts/Test/` + `FirebaseTutorial.unity` | 399줄 + 씬 |

`Dog.cs`는 `FirebaseTutorial.cs`가 6곳에서 사용하고, `FirebaseTutorial.cs`는 씬에 부착되어 있어 셋을 한 덩어리로 처리했습니다. 폴더만 삭제하면 고아 `.meta`가 남으므로 함께 제거했습니다.

정리 결과 `Assets/01.Scenes/`에는 `LoginScene`, `GameScene`만, `Assets/02.Scripts/`에는 `Core`, `Ingame`, `Outgame`, `UI`, `Utility`만 남았습니다.

## 4. 문서

| 커밋 | 내용 |
|---|---|
| `c6a518f` | Gemini Code Assist 설정 제거, 스타일 가이드를 `CODING_CONVENTION.md`로 승격 |
| `639537e` | `PLAYERPREFS_SAVE_PROFILING.md` 신규 작성 |
| `beeb5dc` | README, CLAUDE.md, AGENTS.md 최신화 |

`.gemini/config.yaml`은 2026-01-26 이후 갱신되지 않아 실제 코드와 어긋나 있었습니다. 존재하지 않는 폴더를 제외 경로로 지정하고, UniTask 도입 이후에도 코루틴 사용을 권장하고 있었습니다. Gemini Code Assist를 더 이상 쓰지 않기로 하여 설정을 제거하고, 봇 종속성이 없는 규칙만 문서로 승격했습니다.

`CODING_CONVENTION.md`에는 디미터의 법칙 적용 예외(자료구조, 프레임워크 API, 1~2회 등장 체인)와 우선 대응 대상을 추가했습니다. 이동 과정에서 닫히지 않은 코드 펜스와 깨진 헤딩도 수정했습니다.

README에는 `10.ScriptableObjects` 폴더 설명 오류가 있었습니다. 슬라임과 업그레이드 밸런스 데이터는 실제로 `Assets/02.Scripts/Outgame/Feature/` 아래에 있습니다.

## 5. 빌드 및 측정 환경

### 5.1 개발 프로필 정비 (`9e98cf8`, `1a102ce`)

`Android™` 프로필이 디버그 키스토어로 서명되어 Google Play Games 로그인이 실패했습니다. 릴리스와 동일한 커스텀 키스토어를 지정해 개발 빌드에서도 로그인이 가능하도록 했습니다.

`Mobile - Development`와 `Desktop - Development` 프로필은 제거했습니다. 전자는 LoginScene이 빠져 있어 `AccountManager`가 없는 상태로 실행되어 즉시 예외가 발생하고, 두 프로필 모두 개발 빌드 플래그가 꺼져 있어 이름과 실제가 달랐습니다.

Android Logcat 패키지를 추가하고, 기존 템플릿 파일과 어긋나 있던 커스텀 Gradle 템플릿 설정을 맞췄습니다.

### 5.2 성능 측정

`PlayerPrefs.Save()` 비용을 실기기에서 측정했습니다. 상세 내용은 `PLAYERPREFS_SAVE_PROFILING.md`에 있습니다.

## 6. 검증 결과 기각된 가설

리뷰 과정에서 제기했으나 조사 결과 **도달 불가로 확인되어 종결한 항목**입니다. 재검토 시 참고하십시오.

| 항목 | 기각 근거 |
|---|---|
| `PlayerPrefs.Save()`가 프레임 히칭을 유발한다 | 실기기 측정 결과 평균 0.150ms / 최대 0.689ms / 2ms 초과 0회 |
| `SlimeSpawner.GetActiveTargets()` 순회 중 컬렉션 변경 예외 | 소비자가 `AutoClicker` 한 곳뿐이며 순회 중 디스폰 경로가 없음 |
| `MergeSlime()`의 이중 `RemoveSlime`으로 상태가 깨진다 | 스폰/디스폰 회계 불변식이 유지되고, `CanMerge`가 Grade10 합성을 차단해 예외 조건이 성립하지 않음 |
| `AccountManager.UserId`가 null인 채로 저장 키가 생성된다 | `Logout()`과 `LoadLoginScene()` 호출부가 0건이고, 로그인 성공 시에만 GameScene이 로드됨 |
| 매니저 3종의 `await UniTask.Yield()`가 타이밍 해킹이다 | `UserId` 대기용이 아니라 **이벤트 구독 순서 보장용**. 제거하면 `GameManager`가 초기화 이벤트를 놓침 |

마지막 항목은 CLAUDE.md에 "제거하지 말 것"으로 명시했습니다.

## 7. 남은 과제

| 항목 | 규모 | 선행 조건 |
|---|---|---|
| 미호출 public 메서드 12건 | 작음 | 오디오 볼륨 설정 UI 및 `Crypto` 유틸리티 유지 여부 확인 |
| `Slime.cs:19`의 이미 해결된 TODO 주석 | 1줄 | |
| DOTween 용량 설정 | 1줄 | |
| `GameManager` 책임 분리 | 큼 | |
| 매 프레임 힙 할당 | 미정 | **Profiler GC Alloc 측정** |

`GameManager`는 초기화 게이트, 오프라인 보상, 튜토리얼 진행도, 게임플레이 상태 4가지를 담당하고 있습니다. `OfflineRewardService` 분리가 첫 단계로 적절합니다.

미호출 public 메서드 집계는 Unity 인터페이스가 직접 호출하는 `TutorialSpotlightView.IsRaycastLocationValid()`와 `OnPointerClick()`을 제외합니다. 기존 11건 외에 외부 소비자가 없는 `Crypto.VerifyPassword()`를 포함해 12건입니다.

DOTween은 4시간 이상 방치 후 오프라인 보상 팝업을 열면 Sequence를 51개 생성해 기본 용량 50을 초과합니다. 자동 확장되므로 기능 문제는 없으며, 콘솔 경고 제거 목적입니다.

## 8. 검증 방법

각 정리 작업은 다음을 확인한 뒤 수행했습니다.

1. 대상 타입·메서드의 참조 수 (`grep` 전수 조사)
2. 스크립트 GUID의 씬·프리팹·에셋 참조 여부
3. `.meta` 파일 짝 무결성 (양방향)
4. 살아 있는 인터페이스 구현 관계
5. Unity 컴파일 결과 (`Editor.log`에서 `error CS` / `warning CS` 확인)

컴파일 검증은 어셈블리 빌드 시각이 소스 수정 시각보다 나중인지 확인한 뒤, 마지막 컴파일 시작 행 이후 구간만 대상으로 했습니다. 로그가 누적 파일이라 이전 실행의 경고가 섞이기 때문입니다.

## 9. 결과

| 지표 | 이전 | 이후 |
|---|---|---|
| 컴파일 경고 | 7건 | **0건** |
| 고아 `.meta` | 2건 | **0건** |
| 학습용 잔재 | 837줄 + 씬 2개 | **0** |
| 빌드 프로필 | 4개 (2개 오작동) | 2개 |
| 스크립트 수 | — | 92개 |
