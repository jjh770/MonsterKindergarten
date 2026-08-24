using Firebase.Firestore;
[FirestoreData]
public class CurrencySaveData : ISaveData
{
    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    // 재화 배열
    [FirestoreProperty]
    public double[] Currencies { get; set; }
    [FirestoreProperty]
    public string LastSaveTime { get; set; }
    // 재화 기본값
    public static CurrencySaveData Default => new CurrencySaveData()
    {
        SchemaVersion = SaveSchema.CurrencyCurrentVersion,
        Currencies = new double[(int)ECurrencyType.Count],
        LastSaveTime = null
    };
}
