using System;

public class Slime
{
    // 슬라임 도메인 -> 슬라임이 가지고 있는 데이터에 대한 규칙 정의
    public readonly SlimeSpecData SpecData;

    public Slime(SlimeSpecData specData)
    {
        // 유효성 검사
        if (specData.Name == null) throw new ArgumentException($"이름은 비어있을 수 없습니다.");
        if (specData.Grade == ESlimeGrade.None || specData.Grade == ESlimeGrade.Count) throw new ArgumentException($"슬라임 등급 설정이 잘못 되었습니다. : {specData.Grade}");
        if (specData.Point < 10) throw new ArgumentException($"기본 획득 포인트는 10보다 작을 수 없습니다. : {specData.Point}");
        if (specData.AutoClickInterval < 3) throw new ArgumentException($"기본 빈도수는 3보다 작을 수 없습니다. : {specData.AutoClickInterval}");
        SpecData = specData;
    }


    // TODO 합칠수있는가? 
    public bool CanMerge(Slime otherSlime)
    {
        return otherSlime.SpecData.Grade == SpecData.Grade;
    }
}
