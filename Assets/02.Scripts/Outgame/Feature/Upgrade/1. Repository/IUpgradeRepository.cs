using Cysharp.Threading.Tasks;

public interface IUpgradeRepository
{
    UniTaskVoid Save(UpgradeSaveData data);
    UniTask<UpgradeSaveData> Load();
}
