using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Firebase.Firestore;

// 두 저장 문서가 서로 다른 시점에 복원돼도 탐험 보상의 차이만 반영할 수 있는 누적 기록이다.
public sealed class ExpeditionSettlement
{
    public static ExpeditionSettlement Empty => new(0, 0d, 0, 0);
    public long Sequence { get; }
    public double Points { get; }
    public long Tickets { get; }
    public int DecorationMask { get; }

    public long LegacyCompletedSequence { get; }
    public IReadOnlyDictionary<string, long> CompletedDepartures { get; }

    public ExpeditionSettlement(long sequence, double points, long tickets, int decorationMask,
        IDictionary<string, long> completedDepartures = null, long legacyCompletedSequence = 0)
    {
        const int AllowedDecorationMask = (1 << (int)EPlaygroundObjectType.FlowerPatch) |
                                          (1 << (int)EPlaygroundObjectType.FlowerCushion) |
                                          (1 << (int)EPlaygroundObjectType.FlowerBasket) |
                                          (1 << (int)EPlaygroundObjectType.FireflyLantern);
        if (sequence < 0 || tickets < 0 || points < 0d ||
            double.IsNaN(points) || double.IsInfinity(points) || points != Math.Floor(points) ||
            (decorationMask & ~AllowedDecorationMask) != 0 ||
            (sequence == 0 && (points != 0d || tickets != 0 || decorationMask != 0)))
        {
            throw new ArgumentException("탐험 보상 누적 기록이 올바르지 않습니다.");
        }

        if (legacyCompletedSequence < 0 || legacyCompletedSequence > sequence)
            throw new ArgumentException("이전 탐험 완료 순번이 올바르지 않아요.");
        var departures = completedDepartures == null
            ? new Dictionary<string, long>(StringComparer.Ordinal)
            : new Dictionary<string, long>(completedDepartures, StringComparer.Ordinal);
        foreach (var pair in departures)
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= legacyCompletedSequence)
                throw new ArgumentException("탐험지별 완료 기록이 올바르지 않아요.");
        if (sequence == 0 && departures.Count != 0)
            throw new ArgumentException("완료 전 탐험지 기록이 존재해요.");
        LegacyCompletedSequence = legacyCompletedSequence;
        CompletedDepartures = new ReadOnlyDictionary<string, long>(departures);
        Sequence = sequence;
        Points = points;
        Tickets = tickets;
        DecorationMask = decorationMask;
    }

    public bool CanAdvanceTo(ExpeditionSettlement next)
    {
        if (next == null || next.Sequence < Sequence) return false;
        if (next.LegacyCompletedSequence < LegacyCompletedSequence) return false;
        foreach (var pair in CompletedDepartures)
            if (!next.CompletedDepartures.TryGetValue(pair.Key, out long value) || value < pair.Value)
                return false;
        if (next.Sequence == Sequence)
        {
            if (next.LegacyCompletedSequence != LegacyCompletedSequence ||
                next.CompletedDepartures.Count != CompletedDepartures.Count) return false;
            foreach (var pair in CompletedDepartures)
                if (next.CompletedDepartures[pair.Key] != pair.Value) return false;
            return next.Points == Points && next.Tickets == Tickets && next.DecorationMask == DecorationMask;
        }
        return next.Points >= Points && next.Tickets >= Tickets &&
               (next.DecorationMask & DecorationMask) == DecorationMask;
    }

    public bool HasCompleted(ExpeditionRun run) => run.Sequence <= LegacyCompletedSequence ||
        (CompletedDepartures.TryGetValue(run.DestinationId, out long sequence) && run.Sequence <= sequence);

    public long GetNextDepartureSequence(IEnumerable<ExpeditionRun> activeRuns)
    {
        long latest = Math.Max(Sequence, LegacyCompletedSequence);
        foreach (long value in CompletedDepartures.Values) latest = Math.Max(latest, value);
        foreach (ExpeditionRun run in activeRuns) latest = Math.Max(latest, run.Sequence);
        return checked(latest + 1);
    }

    public ExpeditionSettlement WithReward(ExpeditionRun run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (HasCompleted(run)) throw new ArgumentException("이미 정산한 탐험이에요.");
        ExpeditionReward reward = run.Reward;
        int mask = reward.Decoration.HasValue ? 1 << (int)reward.Decoration.Value : 0;
        var departures = new Dictionary<string, long>();
        foreach (var pair in CompletedDepartures) departures.Add(pair.Key, pair.Value);
        departures[run.DestinationId] = run.Sequence;
        return new ExpeditionSettlement(
            checked(Sequence + 1), Points + reward.Points,
            checked(Tickets + reward.GachaTickets), DecorationMask | mask,
            departures, LegacyCompletedSequence);
    }
}

[FirestoreData]
public sealed class ExpeditionSettlementSaveData
{
    [FirestoreProperty] public long Sequence { get; set; }
    [FirestoreProperty] public double Points { get; set; }
    [FirestoreProperty] public long Tickets { get; set; }
    [FirestoreProperty] public int DecorationMask { get; set; }

    [FirestoreProperty] public long LegacyCompletedSequence { get; set; }
    [FirestoreProperty] public Dictionary<string, long> CompletedDepartures { get; set; }

    public void UpgradeLegacy()
    {
        LegacyCompletedSequence = Sequence;
        CompletedDepartures = new Dictionary<string, long>();
    }

    public ExpeditionSettlement ToDomain(bool legacy = false)
    {
        if (legacy) return new ExpeditionSettlement(Sequence, Points, Tickets, DecorationMask,
            legacyCompletedSequence: Sequence);
        if (CompletedDepartures == null) throw new ArgumentException("탐험지별 완료 기록이 없어요.");
        return new ExpeditionSettlement(Sequence, Points, Tickets, DecorationMask,
            CompletedDepartures, LegacyCompletedSequence);
    }

    public static ExpeditionSettlementSaveData FromDomain(ExpeditionSettlement value) => new()
    {
        CompletedDepartures = CopyDepartures(value),
        LegacyCompletedSequence = value.LegacyCompletedSequence,
        Sequence = value.Sequence,
        Points = value.Points,
        Tickets = value.Tickets,
        DecorationMask = value.DecorationMask,
    };

    private static Dictionary<string, long> CopyDepartures(ExpeditionSettlement value)
    {
        var result = new Dictionary<string, long>();
        foreach (var pair in value.CompletedDepartures) result.Add(pair.Key, pair.Value);
        return result;
    }
}
