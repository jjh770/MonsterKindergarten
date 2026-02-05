using UnityEngine;

public class MockSlimeStatusRepostiroy : ISlimeStatusRepository
{
    public void Save()
    {
        Debug.Log("저장됐습니다.");
    }


    public SlimeStatusSaveData Load()
    {
        SlimeStatusSaveData data = new SlimeStatusSaveData();

        data.HighestGrade = ESlimeGrade.Grade2;
        data.ActiveSlimes.Add(ESlimeGrade.Grade1, 3);
        data.ActiveSlimes.Add(ESlimeGrade.Grade2, 2);


        return data;
    }
}
