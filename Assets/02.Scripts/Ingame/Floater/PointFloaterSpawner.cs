using Lean.Pool;
using UnityEngine;

public class PointFloaterSpawner : MonoBehaviour
{
    public static PointFloaterSpawner Instance { get; private set; }
    private LeanGameObjectPool _pool;

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

    public void ShowFloater(ClickInfo clickInfo)
    {
        // 1. 풀로부터 Floater 를 가져오고
        GameObject floaterObject = _pool.Spawn(clickInfo.Position, Quaternion.identity);
        PointFloater floater = floaterObject.GetComponent<PointFloater>();
        // 2. 풀 참조 설정
        floater.SetPool(_pool);
        // 3. 클릭한 위치에 생성하기
        floater.Play(clickInfo.Point, clickInfo.Position, clickInfo.Level);
    }
}
