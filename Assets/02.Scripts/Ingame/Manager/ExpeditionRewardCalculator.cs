using System;
using System.Collections.Generic;
using UnityEngine;

// 출발 시점의 편성·업그레이드·도감 보너스로 보상을 고정한다. 광고 부스트는 포함하지 않는다.
public sealed class ExpeditionRewardCalculator
{
    private readonly SlimeManager _slimes;

    public ExpeditionRewardCalculator(SlimeManager slimes)
    {
        _slimes = slimes ?? throw new ArgumentNullException(nameof(slimes));
    }

    public double CalculatePoints(IReadOnlyList<SlimeInstance> members, TimeSpan duration)
    {
        if (members == null) throw new ArgumentNullException(nameof(members));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        double total = 0d;
        foreach (SlimeInstance member in members)
        {
            SlimeSpecData spec = _slimes.Get(member.Grade).SpecData;
            total += PointCalculator.Calculate(spec.Point, member.Grade, EClickType.Auto, member.IsSpecial) *
                     Math.Floor(duration.TotalSeconds / spec.AutoClickInterval);
        }
        return Math.Round(total, MidpointRounding.AwayFromZero);
    }

    public ExpeditionReward Roll(IReadOnlyList<SlimeInstance> members, TimeSpan duration,
        int tickets, float decorationChance)
    {
        if (tickets < 0) throw new ArgumentOutOfRangeException(nameof(tickets));
        if (float.IsNaN(decorationChance) || decorationChance < 0f || decorationChance > 1f)
            throw new ArgumentOutOfRangeException(nameof(decorationChance));
        EPlaygroundObjectType? decoration = null;
        if (UnityEngine.Random.value < decorationChance)
            decoration = (EPlaygroundObjectType)UnityEngine.Random.Range((int)EPlaygroundObjectType.FlowerPatch,
                (int)EPlaygroundObjectType.FireflyLantern + 1);
        return new ExpeditionReward(CalculatePoints(members, duration), tickets, decoration);
    }
}
