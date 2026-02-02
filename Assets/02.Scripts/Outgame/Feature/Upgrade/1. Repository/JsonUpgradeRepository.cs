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

    public void Save(UpgradeSaveData data)
    {
        data.LastSaveTime = System.DateTime.Now.ToString("o");
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);
    }

    public UpgradeSaveData Load()
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("File not found: " + filePath);
            return UpgradeSaveData.Default;
        }

        string json = File.ReadAllText(filePath);
        return JsonUtility.FromJson<UpgradeSaveData>(json);
    }
}
