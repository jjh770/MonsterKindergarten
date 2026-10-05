using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagerEx : MonoBehaviour
{
    private static SceneManagerEx _instance;
    public static SceneManagerEx Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private bool _isLoadingGameScene;

    // 비동기로 부른다. 동기 로드는 불러오는 동안 프레임이 멈춰 로딩 화면의 애니메이션이 서 버린다.
    // 비동기는 로드 중에도 화면이 갱신되고, 장면은 준비되면 알아서 바뀐다. 호출은 한 번만 받는다.
    public void LoadGameScene()
    {
        if (_isLoadingGameScene) return;

        _isLoadingGameScene = true;
        // 이 매니저는 씬을 넘어 남으므로, 로드가 끝나면 다시 받을 수 있게 푼다. 풀지 않으면
        // 로그인 화면으로 돌아왔다가 다시 들어갈 때 호출이 영영 무시된다.
        SceneManager.LoadSceneAsync("GameScene").completed += _ => _isLoadingGameScene = false;
    }

    public void LoadLoginScene()
    {
        SceneManager.LoadScene("LoginScene");
    }
}
