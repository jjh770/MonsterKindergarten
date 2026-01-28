public class FirebaseCurrencyRepository : ICurrencyRepository
{
    public void Save(CurrencySaveData saveData)
    {
        // Firebase : 데이터를 서버에 저장하는 플랫폼
        // TODO : 차주에 Firebase 수업 후 채워넣기
    }

    public CurrencySaveData Load()
    {
        // TODO : 차주에 Firebase 수업 후 채워넣기
        return CurrencySaveData.Default;
    }

}
