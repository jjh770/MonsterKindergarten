using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
            string json = JsonConvert.SerializeObject(saveData);
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
                return UniTask.FromResult(SlimeStatusSaveData.Default);
            }

            string json = PlayerPrefs.GetString(key);
            JObject root = JObject.Parse(json);
            int schemaVersion = root.Value<int?>(nameof(ISaveData.SchemaVersion)) ??
                                SaveSchema.LegacyVersion;

            SlimeStatusSaveData saveData;
            if (schemaVersion < SaveSchema.SlimeCurrentVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    JsonConvert.DeserializeObject<LegacySlimeStatusSaveData>(json);
                saveData = SlimeStatusSaveMigration.Upgrade(legacyData);
            }
            else
            {
                saveData = JsonConvert.DeserializeObject<SlimeStatusSaveData>(json);
            }

            if (saveData == null)
            {
                return UniTask.FromResult(SlimeStatusSaveData.Default);
            }

            saveData.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstance>();
            return UniTask.FromResult(saveData);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 로드 실패: {e.Message}");
            // null을 반환하면 SlimeManager 초기화가 중단되어 게임이 진행 불가 상태가 된다.
            return UniTask.FromResult(SlimeStatusSaveData.Default);
        }
    }
}
