using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPrefsUpgradeRepository : IUpgradeRepository
{
    private readonly string _userId;
    private const string KEY_SUFFIX = "_Upgrade";

    public PlayerPrefsUpgradeRepository(string userId)
    {
        _userId = userId;
    }

    private string GetKey() => $"{_userId}{KEY_SUFFIX}";

    public void Delete()
    {
        PlayerPrefs.DeleteKey(GetKey());
    }

    public UniTask Save(UpgradeSaveData saveData)
    {
        try
        {
            string json = JsonConvert.SerializeObject(saveData);
            PlayerPrefs.SetString(GetKey(), json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsUpgradeRepository] 저장 실패: {e.Message}");
        }

        return UniTask.CompletedTask;
    }

    public UniTask<UpgradeSaveData> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult(UpgradeSaveData.Default);
            }

            string json = PlayerPrefs.GetString(key);
            UpgradeSaveData saveData = JsonConvert.DeserializeObject<UpgradeSaveData>(json);
            if (saveData == null)
            {
                return UniTask.FromResult(UpgradeSaveData.Default);
            }

            saveData.Entries ??= new List<UpgradeEntry>();
            return UniTask.FromResult(saveData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsUpgradeRepository] 로드 실패: {e.Message}");
            return UniTask.FromResult(UpgradeSaveData.Default);
        }
    }
}
