using Cysharp.Threading.Tasks;

public interface IRepository<T> where T : class, ISaveData
{
    public UniTask Save(T data);

    // 읽기에 실패하면 기본값이 아니라 실패 결과를 돌려준다.
    // 실패를 기본값으로 바꾸면 호출부가 신규 계정과 구분할 수 없고,
    // 그 뒤 첫 저장이 남아 있던 데이터를 덮어쓴다.
    public UniTask<SaveLoadResult<T>> Load();
}
