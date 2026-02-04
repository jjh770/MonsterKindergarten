// 저장소가 가져야할 약속
using Cysharp.Threading.Tasks;

public interface ICurrencyRepository
{
    public UniTaskVoid Save(CurrencySaveData saveData);
    public UniTask<CurrencySaveData> Load();
}
