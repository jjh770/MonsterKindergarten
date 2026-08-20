using System;
using System.Collections.Generic;

public class SlimeStatus
{
    // 슬라임의 현황을 나타내는 슬라임 스테이터스
    // 최고 해금 등급 (한번 올라가면 내려가지 않음)
    public ESlimeGrade HighestGrade { get; private set; }

    // 활성 슬라임: Grade별 개수
    private readonly Dictionary<ESlimeGrade, int> _activeSlimes = new();
    public IReadOnlyDictionary<ESlimeGrade, int> ActiveSlimes => _activeSlimes;

    public SlimeStatus(ESlimeGrade highestGrade, Dictionary<ESlimeGrade, int> activeSlimes)
    {
        // 최고 등급 규칙
        if (highestGrade == ESlimeGrade.None || highestGrade == ESlimeGrade.Count)
        {
            throw new ArgumentException($"올바른 등급설정이 아닙니다. : {highestGrade}");
        }
        HighestGrade = highestGrade;

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

    // 최고 등급 갱신 (올라가기만 가능)
    public void UpdateHighestGrade(ESlimeGrade newGrade)
    {
        if (newGrade == ESlimeGrade.None || newGrade == ESlimeGrade.Count)
            throw new ArgumentException($"올바른 등급설정이 아닙니다. : {newGrade}");
        if (newGrade <= HighestGrade)
            throw new ArgumentException($"새 등급은 현재 최고 등급보다 높아야 합니다. : {newGrade} <= {HighestGrade}");

        HighestGrade = newGrade;
    }

    // 슬라임 추가
    public void AddSlime(ESlimeGrade grade)
    {
        if (grade == ESlimeGrade.None || grade == ESlimeGrade.Count)
            throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {grade}");

        if (_activeSlimes.ContainsKey(grade))
            _activeSlimes[grade]++;
        else
            _activeSlimes[grade] = 1;
    }

    // 슬라임 제거
    public void RemoveSlime(ESlimeGrade grade)
    {
        if (grade == ESlimeGrade.None || grade == ESlimeGrade.Count)
            throw new ArgumentException($"유효하지 않은 슬라임 등급입니다. : {grade}");

        if (!_activeSlimes.ContainsKey(grade) || _activeSlimes[grade] <= 0)
            throw new InvalidOperationException($"제거할 슬라임이 없습니다. : {grade}");

        _activeSlimes[grade]--;
        if (_activeSlimes[grade] <= 0)
            _activeSlimes.Remove(grade);
    }
}
