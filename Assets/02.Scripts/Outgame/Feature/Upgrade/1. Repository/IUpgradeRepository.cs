public interface IUpgradeRepository
{
    void Save(UpgradeSaveData data);
    UpgradeSaveData Load();
}
