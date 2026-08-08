# Monster Kindergarten 면접 예상 질문 & 답변

## 목차
1. [HybridRepository - 로컬/클라우드 동기화 시스템](#1-hybridrepository---로컬클라우드-동기화-시스템)
2. [클린 아키텍처 + DIP 기반 저장소 추상화](#2-클린-아키텍처--dip-기반-저장소-추상화)
3. [DDD 기반 도메인 모델링](#3-ddd-기반-도메인-모델링)

---

## 1. HybridRepository - 로컬/클라우드 동기화 시스템

### Q1-1. HybridRepository를 만들게 된 배경은 무엇인가요?

> **A**: 클리커 게임 특성상 클릭할 때마다 재화가 변경되어 저장이 필요합니다. 초당 수십 회 저장 요청이 발생하면 Firebase API 호출 비용과 서버 부하가 문제가 됩니다. 또한 네트워크가 불안정한 환경에서도 게임 진행이 가능해야 했습니다. 이 두 가지 문제를 해결하기 위해 로컬 저장은 즉시 수행하고, 서버 저장은 디바운싱하는 하이브리드 구조를 설계했습니다.

---

### Q1-2. 디바운싱 시간을 0.6초로 설정한 이유는?

> **A**: 사용자 테스트 결과, 일반적인 연속 클릭 세션이 0.3~0.5초 간격으로 발생했습니다. 0.6초는 연속 클릭이 끝나는 시점을 커버하면서도 데이터 유실 위험을 최소화하는 균형점입니다. 이 값은 상수(`FIREBASE_INTERVAL`)로 분리해두어 운영 데이터 분석 후 조정할 수 있도록 했습니다.

---

### Q1-3. CancellationToken을 사용한 이유는? Coroutine으로도 가능하지 않나요?

> **A**: Coroutine도 가능하지만 세 가지 이유로 UniTask + CancellationToken을 선택했습니다.
>
> 1. **명시적 취소**: `StopCoroutine`은 참조를 관리해야 하지만, CancellationToken은 `Cancel()` 호출만으로 취소됩니다.
> 2. **예외 처리**: 취소 시 `OperationCanceledException`으로 명확하게 처리할 수 있습니다.
> 3. **GC 최적화**: UniTask는 ValueTask 기반으로 힙 할당이 적습니다.
>
> ```csharp
> // 이전 요청 취소 → 새 토큰 생성 → 0.6초 대기 → 서버 저장
> _firebaseSaveToken?.Cancel();
> _firebaseSaveToken = new CancellationTokenSource();
> await UniTask.Delay(TimeSpan.FromSeconds(0.6f), cancellationToken: token);
> ```

---

### Q1-4. 로컬과 서버 데이터가 충돌하면 어떻게 해결하나요?

> **A**: `LastSaveTime` 타임스탬프를 비교하여 더 최신 데이터를 선택합니다.
>
> ```csharp
> if (playerprefsTime >= firebaseTime)
>     return playerprefs;  // 로컬이 최신
> else {
>     _playerprefsRepository.Save(firebase);  // 로컬 동기화
>     return firebase;  // 서버가 최신
> }
> ```
>
> 추가로 양쪽 모두 null인 경우, 한쪽만 null인 경우 등 엣지 케이스도 처리했습니다.

---

### Q1-5. 네트워크 오류 시에는 어떻게 처리하나요?

> **A**: Firebase 저장 실패 시 예외를 catch하고 로그만 남깁니다. 로컬 저장은 이미 완료되었으므로 다음 저장 시점에 다시 시도됩니다.
>
> ```csharp
> catch (Exception e)
> {
>     Debug.Log($"파이어베이스 저장 실패 : {e.Message}");
>     // 로컬에는 이미 저장됨 → 다음 Save() 호출 시 재시도
> }
> ```
>
> 중요한 점은 사용자 경험을 해치지 않는 것입니다. 저장 실패로 게임이 멈추면 안 됩니다.

---

### Q1-6. Load 시 로컬과 서버를 동시에 불러오는 이유는?

> **A**: `UniTask.WhenAll`로 병렬 로드하여 총 로딩 시간을 단축합니다.
>
> ```csharp
> var (playerprefsData, firebaseData) = await UniTask.WhenAll(
>     _playerprefsRepository.Load(),
>     _firebaseRepository.Load()
> );
> ```
>
> 순차 로드 시 `로컬 1초 + 서버 2초 = 3초`가 걸린다면, 병렬 로드 시 `max(1초, 2초) = 2초`로 줄어듭니다.

---

## 2. 클린 아키텍처 + DIP 기반 저장소 추상화

### Q2-1. 왜 3계층 아키텍처를 선택했나요?

> **A**: 세 가지 목표가 있었습니다.
>
> 1. **관심사 분리**: 저장 로직(Repository), 비즈니스 규칙(Domain), 오케스트레이션(Manager) 분리
> 2. **테스트 용이성**: 각 계층을 독립적으로 테스트 가능
> 3. **변경 영향 최소화**: Firebase → Supabase 교체 시 Repository만 수정
>
> 실제로 WebGL 빌드 시 Firebase를 제외해야 했는데, Manager 코드는 한 줄도 수정하지 않았습니다.

---

### Q2-2. DIP(의존관계 역전 원칙)를 적용한 구체적인 사례는?

> **A**: `CurrencyManager`가 구현체가 아닌 인터페이스에 의존하도록 설계했습니다.
>
> ```csharp
> // Bad: 구현체에 의존
> private LocalCurrencyRepository _repository;
>
> // Good: 인터페이스에 의존
> private IRepository<CurrencySaveData> _repository;
> ```
>
> 런타임에 조건부로 구현체를 주입합니다:
> ```csharp
> #if !UNITY_WEBGL
>     _repository = new HybridRepository<CurrencySaveData>(...);
> #else
>     _repository = new LocalCurrencyRepository(...);
> #endif
> ```

---

### Q2-3. OCP(개방/폐쇄 원칙)는 어디에 적용되었나요?

> **A**: `IFeedback` 인터페이스입니다. 새로운 피드백을 추가할 때 기존 코드를 수정하지 않습니다.
>
> ```csharp
> // SlimeController는 수정하지 않음
> var feedbacks = GetComponentsInChildren<IFeedback>();
> foreach (var feedback in feedbacks)
>     feedback.Play(clickInfo);
>
> // 새 피드백은 클래스만 추가
> public class ScreenShakeFeedback : MonoBehaviour, IFeedback
> {
>     public void Play(ClickInfo info) { /* 화면 흔들림 */ }
> }
> ```

---

### Q2-4. 일방향 의존성이란 무엇이고 왜 중요한가요?

> **A**: 상위 계층(Manager)은 하위 계층(Repository)을 알지만, 하위 계층은 상위를 모르는 구조입니다.
>
> ```
> Manager → Domain → Repository
>    ↓         ↓         ↓
>   알 수     알 수     알 수
>   있음      없음      없음
> ```
>
> 이점:
> - Repository 개발자는 Manager 코드를 몰라도 `ISaveData`만 구현하면 됩니다
> - 계층별 독립적인 개발/테스트 가능
> - 변경 영향이 상위로 전파되지 않음

---

### Q2-5. 테스트는 어떻게 하셨나요?

> **A**: DIP 덕분에 Mock Repository를 주입할 수 있습니다.
>
> ```csharp
> public class MockCurrencyRepository : IRepository<CurrencySaveData>
> {
>     public UniTask Save(CurrencySaveData data) => UniTask.CompletedTask;
>     public UniTask<CurrencySaveData> Load() => UniTask.FromResult(testData);
> }
>
> // 테스트 시
> _repository = new MockCurrencyRepository();
> ```
>
> Firebase 연결 없이도 Manager 로직을 테스트할 수 있습니다.

---

### Q2-6. 이벤트 기반 초기화를 사용한 이유는?

> **A**: 여러 Manager가 비동기로 독립 초기화되기 때문입니다. 순차 초기화는 느리고, 병렬 초기화는 순서 문제가 있습니다.
>
> ```csharp
> // 각 Manager가 초기화 완료 시 이벤트 발행
> UpgradeManager.OnDataInitialized += OnUpgradeDataInitialized;
> SlimeManager.OnDataInitialized += OnSlimeDataInitialized;
> CurrencyManager.Instance.OnDataInitialized += OnCurrencyDataInitialized;
>
> // 모든 초기화 완료 확인 후 게임 시작
> if (_isUpgradeInitialized && _isSlimeInitialized && _isCurrencyInitialized)
>     OnAllDataInitialized?.Invoke();
> ```

---

## 3. DDD 기반 도메인 모델링

### Q3-1. Currency를 왜 struct로 설계했나요?

> **A**: Currency는 DDD의 Value Object입니다. "100골드"와 "100골드"는 같은 값이므로 식별자가 필요 없습니다.
>
> struct의 장점:
> 1. **값 복사 의미론**: 참조 공유로 인한 버그 방지
> 2. **스택 할당**: GC 부담 감소
> 3. **불변성 강제**: `readonly struct`로 변경 불가
>
> ```csharp
> public readonly struct Currency
> {
>     public readonly double Value;
>     // 새 Currency 반환 (기존 값 변경 없음)
>     public static Currency operator +(Currency c1, Currency c2)
>         => new Currency(c1.Value + c2.Value);
> }
> ```

---

### Q3-2. 연산자 오버로딩을 적용한 이유는?

> **A**: 도메인 언어를 자연스럽게 표현하기 위해서입니다.
>
> ```csharp
> // 연산자 오버로딩 없이
> Currency newCurrency = new Currency(currency1.Value + currency2.Value);
>
> // 연산자 오버로딩 적용
> Currency newCurrency = currency1 + currency2;
> ```
>
> 비교, 암시적 변환도 지원합니다:
> ```csharp
> if (playerCurrency >= upgradeCost) { ... }
> Currency gold = 100;  // double → Currency 암시적 변환
> ```

---

### Q3-3. Fail-Fast 원칙이란 무엇이고 왜 적용했나요?

> **A**: 잘못된 데이터가 들어오면 즉시 예외를 발생시켜 버그를 빨리 발견하는 원칙입니다.
>
> ```csharp
> public Currency(double value)
> {
>     if (value < 0)
>         throw new Exception("Currency 값은 0보다 작을 수 없습니다.");
>     Value = value;
> }
> ```
>
> 만약 검증하지 않으면 음수 재화가 UI에 표시되거나, 나중에 다른 곳에서 크래시가 발생합니다. 생성 시점에 실패하면 버그 원인을 바로 찾을 수 있습니다.

---

### Q3-4. 도메인 로직을 Manager가 아닌 Domain 클래스에 넣은 이유는?

> **A**: Rich Domain Model 패턴입니다. 도메인 규칙이 한 곳에 모여 있으면 응집도가 높아집니다.
>
> ```csharp
> // Anemic Model (빈약한 모델) - 규칙이 분산됨
> class Upgrade { public int Level; public int MaxLevel; }
> // Manager에서: if (upgrade.Level < upgrade.MaxLevel) upgrade.Level++;
>
> // Rich Domain Model - 규칙이 캡슐화됨
> class Upgrade {
>     public int Level { get; private set; }
>     public bool IsMaxLevel => Level >= SpecData.MaxLevel;
>     public bool TryLevelUp() {
>         if (IsMaxLevel) return false;
>         Level++;
>         return true;
>     }
> }
> ```

---

### Q3-5. Upgrade 도메인에서 Currency 검증을 하지 않은 이유는?

> **A**: 도메인 간 독립성을 유지하기 위해서입니다. Upgrade가 CurrencyManager를 직접 참조하면 순환 의존이 발생하고 테스트가 어려워집니다.
>
> 도메인 협력은 Manager 계층에서 조율합니다:
> ```csharp
> // UpgradeManager.cs
> public bool TryLevelUp(EUpgradeType type, ESlimeGrade grade)
> {
>     Currency cost = upgrade.Cost;                          // Upgrade 도메인
>     if (!CurrencyManager.Instance.TrySpend(cost)) return false;  // Currency 도메인
>     upgrade.TryLevelUp();                                  // Upgrade 도메인
> }
> ```

---

### Q3-6. Specification 패턴은 어디에 적용했나요?

> **A**: 이메일 검증 로직을 `AccountEmailSpecification`으로 분리했습니다.
>
> ```csharp
> public class AccountEmailSpecification
> {
>     private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
>
>     public bool IsSatisfiedBy(string email)
>     {
>         if (string.IsNullOrEmpty(email)) { _errorMessage = "..."; return false; }
>         if (!EmailRegex.IsMatch(email)) { _errorMessage = "..."; return false; }
>         return true;
>     }
> }
> ```
>
> 장점:
> - 검증 로직 재사용 가능 (회원가입, 비밀번호 찾기 등)
> - 검증 규칙만 독립 테스트 가능
> - Account 클래스가 단순해짐

---

## 4. 공통 질문

### Q4-1. 이 프로젝트에서 가장 어려웠던 점은?

> **A**: HybridRepository의 충돌 해결 로직입니다. 로컬과 서버 데이터가 다를 때 어떤 것을 선택할지, 동기화는 언제 할지 결정해야 했습니다. 타임스탬프 기반으로 해결했지만, 시계 동기화 문제(서버 시간 vs 로컬 시간)를 고려해 UTC 표준 시간을 사용했습니다.

---

### Q4-2. 개선하고 싶은 부분이 있다면?

> **A**: 세 가지가 있습니다.
>
> 1. **의존성 주입 프레임워크 도입**: 현재 `#if` 전처리기로 수동 주입 중인데, VContainer 같은 DI 컨테이너를 도입하면 더 깔끔해집니다.
>
> 2. **Feedback 캐싱**: 현재 `GetComponentsInChildren<IFeedback>()`을 매 클릭마다 호출하는데, Awake에서 캐싱하면 성능이 개선됩니다.
>
> 3. **오프라인 큐**: 네트워크 복구 시 밀린 저장 요청을 순차 처리하는 큐 시스템을 추가하고 싶습니다.

---

### Q4-3. SOLID 원칙 중 가장 중요하다고 생각하는 것은?

> **A**: DIP(의존관계 역전 원칙)입니다. 다른 원칙들은 DIP 없이도 부분 적용 가능하지만, DIP는 전체 아키텍처의 유연성을 결정합니다. 구현체가 아닌 추상화에 의존하면 테스트, 확장, 교체가 모두 쉬워집니다. 이 프로젝트에서 WebGL 빌드 대응이 쉬웠던 것도 DIP 덕분입니다.

---

### Q4-4. 팀 프로젝트에서 이 아키텍처의 장점은?

> **A**: 계층별 분업이 가능합니다.
>
> - **Repository 담당**: Firebase API만 알면 됨, Manager 코드 몰라도 됨
> - **Domain 담당**: 비즈니스 규칙만 집중, 저장 방식 몰라도 됨
> - **Manager 담당**: 전체 흐름 조율, 세부 구현 몰라도 됨
>
> 인터페이스가 계약 역할을 하므로, 병렬 개발 후 통합이 쉽습니다.

---

### Q4-5. 이 아키텍처를 다른 프로젝트에도 적용할 수 있나요?

> **A**: 네, 규모에 따라 조절합니다.
>
> - **소규모 프로젝트**: Domain 계층 생략, Manager + Repository만
> - **중규모 프로젝트**: 3계층 그대로 적용
> - **대규모 프로젝트**: UseCase 계층 추가, 모듈별 분리
>
> 핵심은 "관심사 분리"와 "의존성 방향 통일"입니다. 이 원칙만 지키면 규모에 맞게 조절 가능합니다.

---

*이 문서는 Monster Kindergarten 프로젝트 기술 면접 준비용으로 작성되었습니다.*
