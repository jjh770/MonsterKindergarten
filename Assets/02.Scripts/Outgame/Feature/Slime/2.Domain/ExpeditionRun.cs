using System;
using System.Collections.Generic;

// 출발한 탐험 한 번의 고정된 내용이다. 시각은 호출자가 ServerClock에서 받아 전달한다.
// 완료 상태를 따로 저장하지 않고 도착 예정 시각과 비교하므로 앱을 꺼도 시간이 이어진다.
public sealed class ExpeditionRun
{
    private readonly IReadOnlyList<ExpeditionMember> _members;

    public string RunId { get; }
    public string DestinationId { get; }
    public long Sequence { get; }
    public IReadOnlyList<ExpeditionMember> Members => _members;
    public DateTime StartedAtUtc { get; }
    public DateTime ReturnsAtUtc { get; }
    public ExpeditionReward Reward { get; }

    // 생성과 저장 복원은 같은 검증을 거친다. 출발 시에는 새 RunId를 만들어 전달한다.
    public ExpeditionRun(
        string runId,
        string destinationId,
        IEnumerable<ExpeditionMember> members,
        DateTime startedAtUtc,
        DateTime returnsAtUtc,
        ExpeditionReward reward,
        long sequence = 1)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("탐험 ID가 비어 있습니다.", nameof(runId));
        if (string.IsNullOrWhiteSpace(destinationId))
            throw new ArgumentException("탐험 장소 ID가 비어 있습니다.", nameof(destinationId));
        if (members == null) throw new ArgumentNullException(nameof(members));
        if (reward == null) throw new ArgumentNullException(nameof(reward));
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        ValidateUtc(startedAtUtc);
        ValidateUtc(returnsAtUtc);
        if (returnsAtUtc <= startedAtUtc)
            throw new ArgumentException("탐험 도착 시각은 출발 시각보다 늦어야 합니다.");

        var copiedMembers = new List<ExpeditionMember>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExpeditionMember member in members)
        {
            if (member == null || !ids.Add(member.InstanceId))
                throw new ArgumentException("탐험 편성에 빈 항목 또는 중복 슬라임이 있습니다.", nameof(members));
            copiedMembers.Add(member);
        }
        if (copiedMembers.Count == 0)
            throw new ArgumentException("탐험에는 슬라임이 한 마리 이상 필요합니다.", nameof(members));

        RunId = runId;
        Sequence = sequence;
        DestinationId = destinationId;
        _members = copiedMembers.AsReadOnly();
        StartedAtUtc = startedAtUtc;
        ReturnsAtUtc = returnsAtUtc;
        Reward = reward;
    }

    public bool IsReadyToReturn(DateTime trustedUtcNow)
    {
        ValidateUtc(trustedUtcNow);
        return trustedUtcNow >= ReturnsAtUtc;
    }

    public TimeSpan GetRemaining(DateTime trustedUtcNow)
    {
        ValidateUtc(trustedUtcNow);
        if (trustedUtcNow >= ReturnsAtUtc) return TimeSpan.Zero;
        if (trustedUtcNow <= StartedAtUtc) return ReturnsAtUtc - StartedAtUtc;
        return ReturnsAtUtc - trustedUtcNow;
    }

    public double GetProgress01(DateTime trustedUtcNow)
    {
        TimeSpan remaining = GetRemaining(trustedUtcNow);
        return 1d - remaining.TotalSeconds / (ReturnsAtUtc - StartedAtUtc).TotalSeconds;
    }

    private static void ValidateUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("탐험 시각은 UTC여야 합니다.");
    }
}
