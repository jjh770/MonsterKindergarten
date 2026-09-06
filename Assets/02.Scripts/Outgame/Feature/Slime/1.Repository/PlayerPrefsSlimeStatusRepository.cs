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

    public void Delete()
    {
        PlayerPrefs.DeleteKey(GetKey());
    }

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

    public UniTask<SaveLoadResult<SlimeStatusSaveData>> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.NotFound());
            }

            string json = PlayerPrefs.GetString(key);
            JObject root = JObject.Parse(json);
            int schemaVersion = root.Value<int?>(nameof(ISaveData.SchemaVersion)) ??
                                SaveSchema.LegacyVersion;

            if (schemaVersion > SaveSchema.SlimeCurrentVersion)
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.Failed(
                        ESaveLoadFailure.UnsupportedVersion,
                        UnsupportedSaveVersionException.BuildMessage(
                            "SlimeStatus",
                            schemaVersion,
                            SaveSchema.SlimeCurrentVersion)));
            }

            SlimeStatusSaveData saveData;
            if (schemaVersion < SaveSchema.SlimeInstanceVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    JsonConvert.DeserializeObject<LegacySlimeStatusSaveData>(json);
                saveData = SlimeStatusSaveMigration.Upgrade(legacyData);
            }
            else
            {
                saveData = JsonConvert.DeserializeObject<SlimeStatusSaveData>(json);
                if (schemaVersion < SaveSchema.SlimeCurrentVersion)
                {
                    saveData = SlimeStatusSaveMigration.UpgradeInstanceData(
                        saveData);
                }
            }

            if (saveData == null)
            {
                return UniTask.FromResult(
                    SaveLoadResult<SlimeStatusSaveData>.Failed(
                        ESaveLoadFailure.Unreadable,
                        "슬라임 저장 데이터를 변환하지 못했습니다."));
            }

            saveData.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstanceSaveData>();
            saveData.NormalCollectionRegistered =
                SlimeStatusSaveData.NormalizeNormalCollection(
                    saveData.NormalCollectionRegistered);
            SlimeStatusSaveData.NormalizeCollectionStats(saveData);
            return UniTask.FromResult(
                SaveLoadResult<SlimeStatusSaveData>.Loaded(saveData));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsSlimeStatusRepository] 로드 실패: {e.Message}");
            // 기본값으로 바꾸면 손상된 저장이 신규 계정이 되고, 첫 저장이 원본을 덮어쓴다.
            // 초기화가 멈추지 않도록 실패는 SaveDataLoadGuard가 세션 단위로 처리한다.
            return UniTask.FromResult(
                SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    e.Message));
        }
    }
}
