using Cysharp.Threading.Tasks;
using UnityEngine;

public class MockSlimeStatusRepository : ISlimeStatusRepository
{
    public UniTask Save(SlimeStatusSaveData saveData)
    {
        Debug.Log("MockSlimeStatusRepository: 저장됐습니다.");
        return default;
    }

    public UniTask<SlimeStatusSaveData> Load()
    {
        var data = new SlimeStatusSaveData
        {
            HighestGrade = (int)ESlimeGrade.Grade2,
            ActiveSlimes = new()
            {
                new SlimeEntry(ESlimeGrade.Grade1, 3),
                new SlimeEntry(ESlimeGrade.Grade2, 2)
            }
        };

        return UniTask.FromResult(data);
    }
}
