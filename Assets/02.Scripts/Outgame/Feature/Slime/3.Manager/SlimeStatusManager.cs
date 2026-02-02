using UnityEngine;

public class SlimeStatusManager : MonoBehaviour
{
    public static SlimeStatusManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }


    }
}
