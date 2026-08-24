using System;
using Firebase.Firestore;

[Serializable]
[FirestoreData]
public sealed class SlimeInstance
{
    [FirestoreProperty]
    public string InstanceId { get; set; }

    [FirestoreProperty]
    public int Grade { get; set; }

    [FirestoreProperty]
    public bool IsSpecial { get; set; }

    [FirestoreProperty]
    public int Location { get; set; }

    public SlimeInstance() { }

    public SlimeInstance(
        string instanceId,
        ESlimeGrade grade,
        bool isSpecial,
        ESlimeLocation location)
    {
        InstanceId = instanceId;
        Grade = (int)grade;
        IsSpecial = isSpecial;
        Location = (int)location;
    }

    public SlimeInstance(SlimeInstance source)
        : this(
            source.InstanceId,
            source.GetGrade(),
            source.IsSpecial,
            source.GetLocation())
    {
    }

    public ESlimeGrade GetGrade() => (ESlimeGrade)Grade;

    public ESlimeLocation GetLocation() => (ESlimeLocation)Location;

    public void PromoteTo(ESlimeGrade grade)
    {
        Grade = (int)grade;
    }

    public static SlimeInstance Create(
        ESlimeGrade grade,
        bool isSpecial = false,
        ESlimeLocation location = ESlimeLocation.MainStage)
    {
        return new SlimeInstance(
            Guid.NewGuid().ToString("N"),
            grade,
            isSpecial,
            location);
    }
}
