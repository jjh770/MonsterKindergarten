using System;
using System.Collections.Generic;

public readonly struct SpecialSlimeCollectionStatsSnapshot
{
    public string FirstRegisteredAt { get; }
    public long ObtainedCount { get; }
    public long ManualTouchCount { get; }
    public double ProducedPointTotal { get; }

    public SpecialSlimeCollectionStatsSnapshot(
        string firstRegisteredAt,
        long obtainedCount,
        long manualTouchCount,
        double producedPointTotal)
    {
        FirstRegisteredAt = firstRegisteredAt;
        ObtainedCount = obtainedCount;
        ManualTouchCount = manualTouchCount;
        ProducedPointTotal = producedPointTotal;
    }
}

// 특별 도감의 등급별 기록. 일반 도감의 NormalSlimeCollectionStats와 같은 모양이다.
//
// 일반과 다른 점은 출처다. 특별한 슬라임은 자연 스폰이나 합성으로 태어나지 않고 가챠로만
// 얻으므로 자연 출현과 합성 탄생 대신 가챠 획득 횟수를 센다.
//
// 저장 목록은 비어 있거나 짧거나 길 수 있다. 이전 문서에는 이 필드가 없고, 손상된 문서는
// 길이가 어긋날 수 있으므로 읽을 때마다 스무 칸으로 맞춘다.
public sealed class SpecialSlimeCollectionStats
{
    private readonly string[] _firstRegisteredAt;
    private readonly long[] _obtainedCounts;
    private readonly long[] _manualTouchCounts;
    private readonly double[] _producedPointTotals;

    public SpecialSlimeCollectionStats(SlimeStatusSaveData saveData)
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        _firstRegisteredAt = SlimeStatusSaveData
            .NormalizeStringStats(saveData.SpecialFirstRegisteredAt).ToArray();
        _obtainedCounts = SlimeStatusSaveData
            .NormalizeLongStats(saveData.SpecialObtainedCounts).ToArray();
        _manualTouchCounts = SlimeStatusSaveData
            .NormalizeLongStats(saveData.SpecialManualTouchCounts).ToArray();
        _producedPointTotals = SlimeStatusSaveData
            .NormalizeDoubleStats(saveData.SpecialProducedPointTotals).ToArray();
    }

    public SpecialSlimeCollectionStatsSnapshot Get(ESlimeGrade grade)
    {
        int index = GetIndex(grade);
        return new SpecialSlimeCollectionStatsSnapshot(
            _firstRegisteredAt[index],
            _obtainedCounts[index],
            _manualTouchCounts[index],
            _producedPointTotals[index]);
    }

    public bool RecordRegistration(ESlimeGrade grade, DateTime registeredAtUtc)
    {
        int index = GetIndex(grade);
        if (!string.IsNullOrEmpty(_firstRegisteredAt[index])) return false;

        _firstRegisteredAt[index] = registeredAtUtc.ToUniversalTime().ToString("o");
        return true;
    }

    public void RecordObtained(ESlimeGrade grade)
    {
        Increment(_obtainedCounts, GetIndex(grade));
    }

    public void RecordProduction(
        ESlimeGrade grade,
        EClickType clickType,
        double point)
    {
        int index = GetIndex(grade);
        if (clickType == EClickType.Manual)
        {
            Increment(_manualTouchCounts, index);
        }

        if (point <= 0d || double.IsNaN(point)) return;

        double total = _producedPointTotals[index] + point;
        _producedPointTotals[index] = double.IsInfinity(total)
            ? double.MaxValue
            : total;
    }

    public List<string> BuildFirstRegisteredAt() => new(_firstRegisteredAt);
    public List<long> BuildObtainedCounts() => new(_obtainedCounts);
    public List<long> BuildManualTouchCounts() => new(_manualTouchCounts);
    public List<double> BuildProducedPointTotals() => new(_producedPointTotals);

    private static void Increment(long[] values, int index)
    {
        if (values[index] < long.MaxValue)
        {
            values[index]++;
        }
    }

    private static int GetIndex(ESlimeGrade grade)
    {
        int index = (int)grade - (int)ESlimeGrade.Grade1;
        if (index < 0 || index >= SlimeStatusSaveData.NormalCollectionSize)
        {
            throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
        }

        return index;
    }
}
