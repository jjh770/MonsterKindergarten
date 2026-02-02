using System;
using System.Collections.Generic;

public class SlimeStatus
{
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public int HighestLevel { get; private set; }

    // 활성 슬라임: Grade별 개수
    private readonly Dictionary<ESlimeGrade, int> _activeSlimes = new();

    public SlimeStatus(int highestLevel, Dictionary<ESlimeGrade, int> activeSlimes)
    {
        // 최고 등급 규칙
        if (highestLevel < 1)
        {
            throw new ArgumentException($"최고 등급은 1 이상이어야 합니다. : {highestLevel}");
        }
        HighestLevel = highestLevel;

        // 활성 슬라임 규칙
        foreach (var pair in activeSlimes)
        {
            if (pair.Key == ESlimeGrade.None || pair.Key == ESlimeGrade.Count)
            {
                throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {pair.Key}");
            }
            if (pair.Value < 0)
            {
                throw new ArgumentException($"슬라임 개수는 0 이상이어야 합니다. : {pair.Key} = {pair.Value}");
            }
            if (pair.Value > 0)
            {
                _activeSlimes[pair.Key] = pair.Value;
            }
        }
    }
}
