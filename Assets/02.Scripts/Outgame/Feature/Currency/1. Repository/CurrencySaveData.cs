using Firebase.Firestore;
[FirestoreData]
public class CurrencySaveData
{
    // 재화 배열
    [FirestoreProperty]
    public double[] Currencies { get; set; }

    // 재화 기본값
    public static CurrencySaveData Default => new CurrencySaveData()
    {
        Currencies = new double[(int)ECurrencyType.Count]
    };
}
