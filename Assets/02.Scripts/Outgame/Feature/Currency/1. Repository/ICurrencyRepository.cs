// 저장소가 가져야할 약속
public interface ICurrencyRepository
{
    public void Save(CurrencySaveData saveData);
    public CurrencySaveData Load();
}
