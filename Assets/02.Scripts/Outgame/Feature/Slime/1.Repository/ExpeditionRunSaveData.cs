using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Firebase.Firestore;

[FirestoreData]
public sealed class ExpeditionMemberSaveData
{
    [FirestoreProperty] public string InstanceId { get; set; }
    [FirestoreProperty] public int Grade { get; set; }
    [FirestoreProperty] public bool IsSpecial { get; set; }
}

[FirestoreData]
public sealed class ExpeditionRunSaveData
{
    [FirestoreProperty] public string RunId { get; set; }
    [FirestoreProperty] public string DestinationId { get; set; }
    [FirestoreProperty] public long Sequence { get; set; }
    [FirestoreProperty] public List<ExpeditionMemberSaveData> Members { get; set; }
    [FirestoreProperty] public string StartedAtUtc { get; set; }
    [FirestoreProperty] public string ReturnsAtUtc { get; set; }
    [FirestoreProperty] public double Points { get; set; }
    [FirestoreProperty] public int Tickets { get; set; }
    // -1은 장식 없음이다. 필드 결손의 기본값 0은 범퍼라 도메인 검증에서 거부된다.
    [FirestoreProperty] public int Decoration { get; set; }

    public ExpeditionRun ToDomain()
    {
        if (Members == null || Members.Any(member => member == null))
            throw new ArgumentException("탐험 편성을 읽을 수 없습니다.");
        return new ExpeditionRun(
            RunId, DestinationId,
            Members.Select(member => new ExpeditionMember(member.InstanceId, (ESlimeGrade)member.Grade, member.IsSpecial)),
            ParseUtc(StartedAtUtc), ParseUtc(ReturnsAtUtc),
            new ExpeditionReward(Points, Tickets, Decoration == -1 ? null : (EPlaygroundObjectType)Decoration),
            Sequence);
    }

    public static ExpeditionRunSaveData FromDomain(ExpeditionRun run)
    {
        if (run == null) return null;
        return new ExpeditionRunSaveData
        {
            RunId = run.RunId, DestinationId = run.DestinationId, Sequence = run.Sequence,
            Members = run.Members.Select(member => new ExpeditionMemberSaveData
            {
                InstanceId = member.InstanceId, Grade = (int)member.Grade, IsSpecial = member.IsSpecial,
            }).ToList(),
            StartedAtUtc = run.StartedAtUtc.ToString("O"), ReturnsAtUtc = run.ReturnsAtUtc.ToString("O"),
            Points = run.Reward.Points, Tickets = run.Reward.GachaTickets,
            Decoration = run.Reward.Decoration.HasValue ? (int)run.Reward.Decoration.Value : -1,
        };
    }

    private static DateTime ParseUtc(string value)
    {
        if (!DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime result) || result.Kind != DateTimeKind.Utc)
            throw new ArgumentException("탐험 UTC 시각을 읽을 수 없습니다.");
        return result;
    }
}
