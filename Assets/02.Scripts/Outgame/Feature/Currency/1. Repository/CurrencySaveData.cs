using Firebase.Firestore;
[FirestoreData]
public class CurrencySaveData : ISaveData
{
    [FirestoreProperty]
    public int SchemaVersion { get; set; }

    // 재화 배열
    // 속성 초기화자를 붙이지 말 것. Firestore는 문서에 없는 필드를 채우지 않으므로,
    // 초기값이 없어야 필드 결손이 null로 드러난다. 기본값을 주면 결손이 정상 데이터로
    // 둔갑해 CurrencyManager의 검사를 그대로 통과한다.
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
