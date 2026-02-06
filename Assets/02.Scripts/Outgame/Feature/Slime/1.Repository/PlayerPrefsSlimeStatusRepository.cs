using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPrefsSlimeStatusRepository : ISlimeStatusRepository
{
    private readonly string _userId;
    private const string KEY_SUFFIX = "_SlimeStatus";

    public PlayerPrefsSlimeStatusRepository(string userId)
    {
        _userId = userId;
    }

    private string GetKey() => $"{_userId}{KEY_SUFFIX}";

    public UniTask Save(SlimeStatusSaveData saveData)
    {
        try
        {
            string json = JsonUtility.ToJson(new SlimeStatusSaveDataWrapper(saveData));
            PlayerPrefs.SetString(GetKey(), json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 저장 실패: {e.Message}");
        }

        return UniTask.CompletedTask;
    }

    public UniTask<SlimeStatusSaveData> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult<SlimeStatusSaveData>(null);
            }

            string json = PlayerPrefs.GetString(key);
            var wrapper = JsonUtility.FromJson<SlimeStatusSaveDataWrapper>(json);
            return UniTask.FromResult(wrapper.ToSaveData());
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 로드 실패: {e.Message}");
            return UniTask.FromResult<SlimeStatusSaveData>(null);
        }
    }
}

// JsonUtility는 Dictionary를 직렬화할 수 없으므로 Wrapper 클래스 사용
[Serializable]
public class SlimeStatusSaveDataWrapper
{
    public int HighestGrade;
    public List<SlimeEntryWrapper> ActiveSlimes = new();
    public string LastSaveTime;

    public SlimeStatusSaveDataWrapper() { }

    public SlimeStatusSaveDataWrapper(SlimeStatusSaveData data)
    {
        HighestGrade = data.HighestGrade;
        LastSaveTime = data.LastSaveTime;
        ActiveSlimes = new List<SlimeEntryWrapper>();

        if (data.ActiveSlimes != null)
        {
            foreach (var entry in data.ActiveSlimes)
            {
                ActiveSlimes.Add(new SlimeEntryWrapper { Grade = entry.Grade, Count = entry.Count });
            }
        }
    }

    public SlimeStatusSaveData ToSaveData()
    {
        var data = new SlimeStatusSaveData
        {
            HighestGrade = HighestGrade,
            LastSaveTime = LastSaveTime,
            ActiveSlimes = new List<SlimeEntry>()
        };

        foreach (var entry in ActiveSlimes)
        {
            data.ActiveSlimes.Add(new SlimeEntry { Grade = entry.Grade, Count = entry.Count });
        }

        return data;
    }
}

[Serializable]
public class SlimeEntryWrapper
{
    public int Grade;
    public int Count;
}
