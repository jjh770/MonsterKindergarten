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

    public UniTask<SaveLoadResult<UpgradeSaveData>> Load()
    {
        try
        {
            string key = GetKey();
            if (!PlayerPrefs.HasKey(key))
            {
                return UniTask.FromResult(
                    SaveLoadResult<UpgradeSaveData>.NotFound());
            }

            string json = PlayerPrefs.GetString(key);
            UpgradeSaveData saveData = JsonConvert.DeserializeObject<UpgradeSaveData>(json);
            if (saveData == null)
            {
                return UniTask.FromResult(
                    SaveLoadResult<UpgradeSaveData>.Failed(
                        ESaveLoadFailure.Unreadable,
                        "업그레이드 저장 데이터를 변환하지 못했습니다."));
            }

            // 상위 버전은 현재 앱이 해석할 수 없다. 그대로 로드하면 다음 저장이
            // 최신 데이터를 낮은 버전으로 덮어쓴다.
            if (saveData.SchemaVersion > SaveSchema.UpgradeCurrentVersion)
            {
                return UniTask.FromResult(
                    SaveLoadResult<UpgradeSaveData>.Failed(
                        ESaveLoadFailure.UnsupportedVersion,
                        UnsupportedSaveVersionException.BuildMessage(
                            "Upgrade",
                            saveData.SchemaVersion,
                            SaveSchema.UpgradeCurrentVersion)));
            }

            saveData.Entries ??= new List<UpgradeEntry>();
            return UniTask.FromResult(
                SaveLoadResult<UpgradeSaveData>.Loaded(saveData));
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerPrefsUpgradeRepository] 로드 실패: {e.Message}");
            // 기본값으로 바꾸면 손상된 저장이 신규 계정이 되고, 첫 저장이 원본을 덮어쓴다.
            return UniTask.FromResult(
                SaveLoadResult<UpgradeSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    e.Message));
        }
    }
}
