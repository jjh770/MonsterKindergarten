using Cysharp.Threading.Tasks;
using UnityEngine;

// 게임 데이터가 준비되면 커튼을 걷는다. GameScene 전용이다.
//
// 커튼 자체는 언제 걷을지 모른다. 로그인 화면은 호출부가 직접 부르고, 여기서는
// SpawnManager.Initialized를 받아 부른다. 그래서 커튼 프리팹은 두 씬이 공유하고
// 이 컴포넌트만 GameScene에 둔다.
//
// OnAllDataInitialized가 아니라 Initialized인 이유는, 전자가 SpawnManager에게
// 복원을 시작하라고 알리는 신호라서다. 구독 순서에 따라 아직 빈 필드가 한 프레임
// 보일 수 있다. Initialized는 복원과 최초 스폰이 끝난 뒤 발화한다.
public sealed class LoadingOverlayRevealer : MonoBehaviour
{
    [SerializeField] private FadeCurtainUI _curtain;

    private void Start()
    {
        if (_curtain == null)
        {
            Debug.LogError("걷어낼 커튼 참조가 비어 있습니다.", this);
            return;
        }

        if (SpawnManager.Instance == null)
        {
            // 커튼을 남겨 두면 화면이 영영 덮인다. 신호를 못 받으면 바로 걷는다.
            Debug.LogError("SpawnManager가 없어 로딩 완료 시점을 알 수 없습니다.", this);
            Reveal();
            return;
        }

        SpawnManager.Instance.Initialized += Reveal;

        // 이미 초기화가 끝난 경우
        if (SpawnManager.Instance.IsInitialized) Reveal();
    }

    private void OnDestroy()
    {
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.Initialized -= Reveal;
        }
    }

    private void Reveal()
    {
        _curtain.RevealAsync().Forget();
    }
}
