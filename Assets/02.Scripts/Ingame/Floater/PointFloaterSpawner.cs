using Lean.Pool;
using UnityEngine;

public class PointFloaterSpawner : MonoBehaviour
{
    public static PointFloaterSpawner Instance { get; private set; }

    [Tooltip("숫자를 그릴 오버레이 캔버스입니다. HUD보다 위, 팝업보다 아래에 둡니다.")]
    [SerializeField] private RectTransform _floaterRoot;

    private LeanGameObjectPool _pool;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        _pool = GetComponent<LeanGameObjectPool>();
    }

    public void ShowFloater(ClickInfo clickInfo)
    {
        // 1. 풀로부터 Floater 를 가져오고
        GameObject floaterObject = _pool.Spawn(_floaterRoot, false);
        PointFloater floater = floaterObject.GetComponent<PointFloater>();
        // 2. 풀 참조 설정
        floater.SetPool(_pool);
        // 3. 클릭한 위치에 생성하기
        floater.Play(clickInfo);
    }
}
