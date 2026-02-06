using Firebase.Firestore;
using System;
[FirestoreData]
public class CurrencySaveData : ISaveData
{
    // 재화 배열
    [FirestoreProperty]
    public double[] Currencies { get; set; }
    [FirestoreProperty]
    public string LastSaveTime { get; set; }
    // 재화 기본값
    public static CurrencySaveData Default => new CurrencySaveData()
    {
        Currencies = new double[(int)ECurrencyType.Count],
        LastSaveTime = DateTime.UtcNow.ToString("O")
    };
}
