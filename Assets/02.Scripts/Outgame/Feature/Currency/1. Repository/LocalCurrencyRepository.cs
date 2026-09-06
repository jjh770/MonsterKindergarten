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
using System;
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

    public void Delete()
    {
        for (int i = 0; i < (int)ECurrencyType.Count; i++)
        {
            PlayerPrefs.DeleteKey($"{_userId}_{(ECurrencyType)i}");
        }
        PlayerPrefs.DeleteKey($"{_userId}_{LAST_SAVE_TIME_KEY}");
        PlayerPrefs.DeleteKey($"{_userId}_{SCHEMA_VERSION_KEY}");
    }

    public UniTask<SaveLoadResult<CurrencySaveData>> Load()
    {
        try
        {
            CurrencySaveData data = CurrencySaveData.Default;
            bool hasExistingData = PlayerPrefs.HasKey($"{_userId}_{LAST_SAVE_TIME_KEY}");

            for (int i = 0; i < (int)ECurrencyType.Count; i++)
            {
                var type = (ECurrencyType)i;
                string key = $"{_userId}_{type.ToString()}";

                if (!PlayerPrefs.HasKey(key)) continue;

                hasExistingData = true;
                string value = PlayerPrefs.GetString(key, "0");
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double currency))
                {
                    // 키가 있는데 해석되지 않으면 손상이다. 0으로 채우면 재화가 조용히 사라진다.
                    return UniTask.FromResult(
                        SaveLoadResult<CurrencySaveData>.Failed(
                            ESaveLoadFailure.Unreadable,
                            $"재화 값을 해석하지 못했습니다. : {type}"));
                }

                data.Currencies[i] = currency;
            }

            if (!hasExistingData)
            {
                return UniTask.FromResult(SaveLoadResult<CurrencySaveData>.NotFound());
            }

            string schemaVersionKey = $"{_userId}_{SCHEMA_VERSION_KEY}";
            data.SchemaVersion = PlayerPrefs.HasKey(schemaVersionKey)
                ? PlayerPrefs.GetInt(schemaVersionKey)
                : SaveSchema.LegacyVersion;

            // 상위 버전은 현재 앱이 해석할 수 없다. 그대로 로드하면 다음 저장이
            // 최신 데이터를 낮은 버전으로 덮어쓴다.
            if (data.SchemaVersion > SaveSchema.CurrencyCurrentVersion)
            {
                return UniTask.FromResult(
                    SaveLoadResult<CurrencySaveData>.Failed(
                        ESaveLoadFailure.UnsupportedVersion,
                        UnsupportedSaveVersionException.BuildMessage(
                            "Currency",
                            data.SchemaVersion,
                            SaveSchema.CurrencyCurrentVersion)));
            }

            data.LastSaveTime = PlayerPrefs.GetString($"{_userId}_{LAST_SAVE_TIME_KEY}", null);
            return UniTask.FromResult(SaveLoadResult<CurrencySaveData>.Loaded(data));
        }
        catch (Exception e)
        {
            return UniTask.FromResult(
                SaveLoadResult<CurrencySaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    $"재화 저장 데이터를 읽지 못했습니다. : {e.Message}"));
        }
    }
}


