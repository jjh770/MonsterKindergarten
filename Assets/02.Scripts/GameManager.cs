using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int AutoDamage;
    public int ManualDamage;
    public int Gold;

    private void Awake()
    {
        if (Instance != null)
        {
            Instance = null;
        }
        Instance = this;
    }
}
