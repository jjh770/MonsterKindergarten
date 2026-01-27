using Lean.Pool;
using UnityEngine;

public class MaplestoryFloaterSpawner : MonoBehaviour
{
    public static MaplestoryFloaterSpawner Instance { get; private set; }

    [SerializeField] private LeanGameObjectPool _pool;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        _pool = GetComponent<LeanGameObjectPool>();
    }

    public void ShowMaplestory(ClickInfo clickInfo)
    {
        // 1. 풀로부터 Floater 를 가져오고
        GameObject floaterObject = _pool.Spawn(clickInfo.Position, Quaternion.identity);
        MaplestoryFloater floater = floaterObject.GetComponent<MaplestoryFloater>();
        // 2. 클릭한 위치에 생성하기
        floater.Show(clickInfo.Point);
    }
}
