using Cysharp.Threading.Tasks;
using System.IO;
using UnityEngine;

public class JsonUpgradeRepository : IUpgradeRepository
{
    private readonly string filePath;
    private readonly string _userId;

    public JsonUpgradeRepository(string userId)
    {
        _userId = userId;
        filePath = Path.Combine(Application.persistentDataPath, $"{userId}_upgrade_save.json");
    }

    public UniTask Save(UpgradeSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);
        return default;
    }

    public UniTask<UpgradeSaveData> Load()
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("File not found: " + filePath);
            return UniTask.FromResult(UpgradeSaveData.Default);
        }

        string json = File.ReadAllText(filePath);
        return UniTask.FromResult(JsonUtility.FromJson<UpgradeSaveData>(json));
    }
}
