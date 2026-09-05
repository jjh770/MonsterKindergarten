using Cysharp.Threading.Tasks;
using System;
using UnityEngine;

// 재화 관리자 -> 오로지 "재화" 만 관리하는 클래스
// 클린 아키텍처에서는 "서비스"라는 이름을 씀.
// 게임에서는 보통 "매니저"라고 표현함.

// 일방향 의존성
// 상위 폴더(Manager)는 하위 폴더의 내용을 알 수 있지만
// 하위 폴더(Repository)는 상위 폴더의 내용을 몰라도 개발할 수 있게 하기
public class CurrencyManager : MonoBehaviour
{
    // 재화에 대한 CRUD 생성 조회 사용 소모 + 재화에 대한 이벤트 추가
    // 비즈니스 로직 - 데이터를 어떻게 다룰 것인가에 대한 핵심 규칙
    public static CurrencyManager Instance { get; private set; }

    // 재화 데이터 (배열로 관리)
    private Currency[] _currencies = new Currency[(int)ECurrencyType.Count];

    // 저장소
    // 의존이란 한 객체가 동작하기 위해서 다른 객체를 참조하는 것.
    // DIP(의존관계 역전 원칙) : 구현체에 의존하지 말고 약속에 의존해라.
    private IRepository<CurrencySaveData> _repository;

    // 재화 조회 +@ (편의를 위해 이정도는 눈감아주자)
    public Currency Point => Get(ECurrencyType.Point);
    public DateTime LastSaveTime { get; private set; } = DateTime.MinValue;
    // 저장된 문서를 읽었는지. 문서가 없어 기본값으로 출발한 경우와 구분한다.
    public bool HasStoredSaveData { get; private set; }
    public bool HasExistingProgress
    {
        get
        {
            if (LastSaveTime != DateTime.MinValue) return true;

            foreach (Currency currency in _currencies)
            {
                if ((double)currency != 0d) return true;
            }

            return false;
        }
    }

    //public Currency Point { get; private set; }
    public event Action<ECurrencyType, Currency> OnDataChanged;
    public event Action OnDataInitialized;


    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        await UniTask.Yield();

#if UNITY_ANDROID && !UNITY_EDITOR
        _repository = new HybridRepository<CurrencySaveData>(new LocalCurrencyRepository(AccountManager.Instance.UserId), new FirebaseCurrencyRepository());
#else
        _repository = new LocalCurrencyRepository(AccountManager.Instance.UserId);
#endif

        SaveLoadResult<CurrencySaveData> loadResult = await _repository.Load();
        if (loadResult.IsFailed)
        {
            // 읽지 못한 세션은 초기화하지 않는다. 세션 처리는 SaveDataLoadGuard가 맡는다.
            SaveDataLoadGuard.Report(
                loadResult.Failure,
                $"Currency : {loadResult.FailureMessage}");
            return;
        }

        HasStoredSaveData = loadResult.IsLoaded;
        CurrencySaveData saveData = loadResult.IsLoaded
            ? loadResult.Data
            : CurrencySaveData.Default;
        // 저장은 항상 ECurrencyType.Count 길이의 배열을 쓴다. 길이가 다르거나
        // 배열이 없는 문서는 해석할 수 없다. 그대로 두면 초기화가 중단돼
        // 안내 없이 화면이 멈추고, 0으로 채우면 재화가 조용히 사라진다.
        // 재화 종류를 늘릴 때는 스키마 버전을 올리고 승격 로직을 함께 넣는다.
        double[] currencyValues = saveData.Currencies;
        if (currencyValues == null || currencyValues.Length != _currencies.Length)
        {
            SaveDataLoadGuard.Report(
                ESaveLoadFailure.Unreadable,
                $"Currency : 재화 배열을 해석할 수 없습니다. : " +
                $"{currencyValues?.Length.ToString() ?? "없음"}");
            return;
        }

        // 음수는 Currency 생성자가 예외를 던져 초기화를 멈추고, NaN과 무한대는
        // 그대로 통과해 이후 계산과 표기를 망가뜨린다. 셋 다 정상 경로에 없는 값이다.
        foreach (double value in currencyValues)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                SaveDataLoadGuard.Report(
                    ESaveLoadFailure.Unreadable,
                    $"Currency : 재화 값을 해석할 수 없습니다. : {value}");
                return;
            }
        }

        LastSaveTime = ParseSaveTime(saveData.LastSaveTime);
        for (int i = 0; i < _currencies.Length; i++)
        {
            _currencies[i] = currencyValues[i];
        }

        OnDataInitialized?.Invoke();
    }

    // 재화 조회
    public Currency Get(ECurrencyType currencyType)
    {
        return _currencies[(int)currencyType];
    }

    // 재화 추가
    public void Add(ECurrencyType type, Currency amount)
    {
        _currencies[(int)type] += amount;
        OnDataChanged?.Invoke(type, _currencies[(int)type]);
        Save();
    }

    // 재화 소모
    public bool TrySpend(ECurrencyType type, Currency amount)
    {
        if (_currencies[(int)type] >= amount)
        {
            _currencies[(int)type] -= amount;
            OnDataChanged?.Invoke(type, _currencies[(int)type]);
            Save();
            return true;
        }
        return false;
    }

    // 재화 저장
    private void Save()
    {
        SaveCurrentAsync().Forget();
    }

    public void SaveCurrent()
    {
        SaveCurrentAsync().Forget();
    }

    public UniTask SaveCurrentAsync()
    {
        if (!GameplaySaveGate.IsSavingEnabled)
        {
            return UniTask.CompletedTask;
        }

        LastSaveTime = DateTime.UtcNow;
        return _repository.Save(new CurrencySaveData()
        {
            SchemaVersion = SaveSchema.CurrencyCurrentVersion,
            Currencies = ToSaveData(),
            LastSaveTime = LastSaveTime.ToString("O")
        });
    }

    private static DateTime ParseSaveTime(string saveTime)
    {
        if (DateTime.TryParse(saveTime, out DateTime result))
        {
            return result.ToUniversalTime();
        }

        return DateTime.MinValue;
    }

    // Currency[] -> double[] 변환
    private double[] ToSaveData()
    {
        double[] result = new double[_currencies.Length];
        for (int i = 0; i < _currencies.Length; i++)
        {
            result[i] = (double)_currencies[i];
        }
        return result;
    }

    // 소모 가능한지
    public bool CanAfford(ECurrencyType type, Currency amount)
    {
        return _currencies[(int)type] >= amount;
    }
}
