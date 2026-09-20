public readonly struct SlimeMergeRequest
{
    public string KeeperId { get; }
    public string RemovedId { get; }
    public ESlimeGrade ToGrade { get; }

    public SlimeMergeRequest(string keeperId, string removedId, ESlimeGrade toGrade)
    {
        KeeperId = keeperId;
        RemovedId = removedId;
        ToGrade = toGrade;
    }
}
