using System;
using System.Collections.Generic;

public enum EExpeditionDepartureFailure
{
    None,
    EmptyParty,
    TooManyMembers,
    InvalidMember,
    DuplicateMember,
    MemberNotInDisplayRoom,
    MissingRequiredGrade,
}

// 출발 가능 여부와 출발 시점 편성의 복사를 담당한다. 해금과 진행 중 탐험 유무는 소유자가 판단한다.
public static class ExpeditionDeparture
{
    public static bool TryCreateRun(
        ExpeditionDestination destination,
        IReadOnlyList<SlimeInstance> selectedMembers,
        DateTime trustedUtcNow,
        ExpeditionReward reward,
        out ExpeditionRun run,
        out EExpeditionDepartureFailure failure,
        long sequence = 1)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        if (!TryCreateRun(destination.DestinationId, selectedMembers, ExpeditionDestination.MaxMembers,
                trustedUtcNow, destination.Duration, reward, out run, out failure, sequence))
            return false;

        if (destination.HasRequiredGrades(selectedMembers)) return true;
        run = null;
        failure = EExpeditionDepartureFailure.MissingRequiredGrade;
        return false;
    }

    public static bool TryCreateRun(
        string destinationId,
        IReadOnlyList<SlimeInstance> selectedMembers,
        int maxMembers,
        DateTime trustedUtcNow,
        TimeSpan duration,
        ExpeditionReward reward,
        out ExpeditionRun run,
        out EExpeditionDepartureFailure failure,
        long sequence = 1)
    {
        if (maxMembers < 1) throw new ArgumentOutOfRangeException(nameof(maxMembers));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));

        run = null;
        failure = EExpeditionDepartureFailure.None;
        if (selectedMembers == null || selectedMembers.Count == 0)
        {
            failure = EExpeditionDepartureFailure.EmptyParty;
            return false;
        }
        if (selectedMembers.Count > maxMembers)
        {
            failure = EExpeditionDepartureFailure.TooManyMembers;
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var snapshots = new List<ExpeditionMember>(selectedMembers.Count);
        foreach (SlimeInstance member in selectedMembers)
        {
            if (member == null)
            {
                failure = EExpeditionDepartureFailure.InvalidMember;
                return false;
            }
            if (!ids.Add(member.InstanceId))
            {
                failure = EExpeditionDepartureFailure.DuplicateMember;
                return false;
            }
            if (member.Location != ESlimeLocation.DisplayRoom)
            {
                failure = EExpeditionDepartureFailure.MemberNotInDisplayRoom;
                return false;
            }

            snapshots.Add(new ExpeditionMember(member.InstanceId, member.Grade, member.IsSpecial));
        }

        run = new ExpeditionRun(
            Guid.NewGuid().ToString("N"),
            destinationId,
            snapshots,
            trustedUtcNow,
            trustedUtcNow.Add(duration),
            reward,
            sequence);
        return true;
    }
}
