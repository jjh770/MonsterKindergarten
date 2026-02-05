using System;
using System.Collections.Generic;

[Serializable]
public class SlimeStatusSaveData
{
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public ESlimeGrade HighestGrade { get; set; }

    // 활성 슬라임: Grade별 개수
    public Dictionary<ESlimeGrade, int> ActiveSlimes = new();


    public static SlimeStatusSaveData Default()
    {
        SlimeStatusSaveData saveData = new SlimeStatusSaveData();
        saveData.HighestGrade = ESlimeGrade.Grade1;
        saveData.ActiveSlimes = new();

        return saveData;
    }
}
