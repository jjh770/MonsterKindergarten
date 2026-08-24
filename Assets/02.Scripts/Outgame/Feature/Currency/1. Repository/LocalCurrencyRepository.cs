// 저장하는 방식
// 1-1. PlayerPrefs + double -> string 을 사용해서 Save Load하는 방식
// 1-2. PlayerPrefs + double -> json   을 사용해서 Save Load하는 방식
// 2. CSV. JSon으로 저장하는 방식
// 3. 서버에 저장하는 방식 / DB에 저장하는 방식
// @. 플랫폼에 따라 저장하는 방식이 달라질 수도, 저장을 호출하는 방식도 모두 다름.

// 데이터의 영속성(저장과 불러오기)에 대한 책임을 가지는 Repository 
// 영속성은 게임이 꺼져도 데이터가 남아있어 다음에 플레이할 때 이어할 수 있게 하는 속성
// 비즈니스 로직과 분리한다.

// 비즈니스 로직은 매니저에게.
// 저장 로직은 레포지토리에게.
// 1. 코드가 깔끔해지고 유지보수가 쉬워진다.
using Cysharp.Threading.Tasks;
using System.Globalization;
using UnityEngine;

public class LocalCurrencyRepository : IRepository<CurrencySaveData>
{
    private const string LAST_SAVE_TIME_KEY = "Currency_LastSaveTime";
    private const string SCHEMA_VERSION_KEY = "Currency_SchemaVersion";
    private readonly string _userId;
    public LocalCurrencyRepository(string userId)
    {
        _userId = userId;
    }

    public UniTask Save(CurrencySaveData saveData)
    {
        PlayerPrefs.SetInt($"{_userId}_{SCHEMA_VERSION_KEY}", saveData.SchemaVersion);

        for (int i = 0; i < (int)ECurrencyType.Count; i++)
        {
            var type = (ECurrencyType)i;

            // 소수점 17자리까지 저장
            PlayerPrefs.SetString($"{_userId}_{type.ToString()}", saveData.Currencies[i].ToString("G17", CultureInfo.InvariantCulture));
        }

        PlayerPrefs.SetString($"{_userId}_{LAST_SAVE_TIME_KEY}", saveData.LastSaveTime ?? string.Empty);
        PlayerPrefs.Save();

        return UniTask.CompletedTask;
    }

    public UniTask<CurrencySaveData> Load()
    {
        CurrencySaveData data = CurrencySaveData.Default;
        bool hasExistingData = PlayerPrefs.HasKey($"{_userId}_{LAST_SAVE_TIME_KEY}");

        for (int i = 0; i < (int)ECurrencyType.Count; i++)
        {
            var type = (ECurrencyType)i;
            string key = $"{_userId}_{type.ToString()}";

            if (PlayerPrefs.HasKey(key))
            {
                hasExistingData = true;
                string value = PlayerPrefs.GetString(key, "0");
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double currency))
                {
                    data.Currencies[i] = currency;
                }
            }
        }

        string schemaVersionKey = $"{_userId}_{SCHEMA_VERSION_KEY}";
        if (PlayerPrefs.HasKey(schemaVersionKey))
        {
            data.SchemaVersion = PlayerPrefs.GetInt(schemaVersionKey);
        }
        else if (hasExistingData)
        {
            data.SchemaVersion = SaveSchema.LegacyVersion;
        }

        data.LastSaveTime = PlayerPrefs.GetString($"{_userId}_{LAST_SAVE_TIME_KEY}", null);
        return UniTask.FromResult(data);
    }
}


