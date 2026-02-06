using Cysharp.Threading.Tasks;

public interface ISlimeStatusRepository
{
    UniTaskVoid Save(SlimeStatusSaveData data);
    UniTask<SlimeStatusSaveData> Load();
}
