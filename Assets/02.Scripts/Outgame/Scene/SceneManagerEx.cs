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

    public void LoadGameScene()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void LoadLoginScene(bool skipAutomaticLogin = false)
    {
        LobbyScene.SkipNextAutomaticLogin = skipAutomaticLogin;
        SceneManager.LoadScene("LoginScene");
    }
}
