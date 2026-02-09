using Cysharp.Threading.Tasks;
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

    public UniTask Save(UpgradeSaveData saveData)
    {
        try
        {
            var wrapper = new UpgradeSaveDataWrapper(saveData);
            string json = JsonUtility.ToJson(wrapper);
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
            var wrapper = JsonUtility.FromJson<UpgradeSaveDataWrapper>(json);
            return UniTask.FromResult(wrapper.ToSaveData());
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsUpgradeRepository] 로드 실패: {e.Message}");
            return UniTask.FromResult(UpgradeSaveData.Default);
        }
    }
}

[Serializable]
public class UpgradeSaveDataWrapper
{
    public List<UpgradeEntryWrapper> Entries = new();
    public string LastSaveTime;

    public UpgradeSaveDataWrapper() { }

    public UpgradeSaveDataWrapper(UpgradeSaveData data)
    {
        LastSaveTime = data.LastSaveTime;
        Entries = new List<UpgradeEntryWrapper>();

        if (data.Entries != null)
        {
            foreach (var entry in data.Entries)
            {
                Entries.Add(new UpgradeEntryWrapper
                {
                    Type = entry.Type,
                    Grade = entry.Grade,
                    Level = entry.Level
                });
            }
        }
    }

    public UpgradeSaveData ToSaveData()
    {
        var data = new UpgradeSaveData
        {
            LastSaveTime = LastSaveTime,
            Entries = new List<UpgradeEntry>()
        };

        foreach (var entry in Entries)
        {
            data.Entries.Add(new UpgradeEntry
            {
                Type = entry.Type,
                Grade = entry.Grade,
                Level = entry.Level
            });
        }

        return data;
    }
}

[Serializable]
public class UpgradeEntryWrapper
{
    public int Type;
    public int Grade;
    public int Level;
}
