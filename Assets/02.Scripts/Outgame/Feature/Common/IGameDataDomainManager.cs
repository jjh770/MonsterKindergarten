using System;
using Cysharp.Threading.Tasks;

// GameManager가 저장 도메인의 구체 타입을 나열하지 않고도 초기화와 전체 저장을
// 조율할 수 있게 하는 최소 수명주기 계약이다. 실제 데이터 규칙과 저장소 선택은
// 각 도메인 매니저가 계속 소유한다.
public interface IGameDataDomainManager
{
    event Action DataInitialized;

    bool IsInitialized { get; }
    bool HasStoredSaveData { get; }
    bool HasExistingProgress { get; }

    UniTask SaveCurrentAsync();
    void FlushPendingSave();
}
