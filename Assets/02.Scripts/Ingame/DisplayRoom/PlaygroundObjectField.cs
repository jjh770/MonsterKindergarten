using System;
using System.Collections.Generic;
using UnityEngine;

// 저장에 남은 배치를 읽어 실제 오브젝트를 만든다. 놀이터에 무엇이 서 있는지는
// 이 컴포넌트 한 곳이 소유한다.
//
// 가챠권이 GachaTicketField를 두는 것과 같은 분담이다. 저장은 "무엇이 어디에"만
// 알고, 화면에 세우고 치우는 일은 여기가 맡는다.
public sealed class PlaygroundObjectField : MonoBehaviour
{
    [Serializable]
    private sealed class PrefabBinding
    {
        [SerializeField] private EPlaygroundObjectType _type;
        [SerializeField] private GameObject _prefab;

        public EPlaygroundObjectType Type => _type;
        public GameObject Prefab => _prefab;
    }

    [Tooltip("종류마다 만들 프리팹입니다.")]
    [SerializeField] private PrefabBinding[] _prefabs = Array.Empty<PrefabBinding>();

    [Tooltip("만든 오브젝트를 담을 자리입니다. 장식장에서만 켜지는 곳이어야 합니다.")]
    [SerializeField] private Transform _root;

    private readonly List<GameObject> _spawned = new();

    // 배치 모드 동안은 꺼 둔다. 다시 세울 때도 이 값을 따라가야 한다. 안 그러면
    // 하나 놓을 때마다 방 전체가 되살아난다.
    private bool _isInteractive = true;

    // 화면에 선 순서는 저장 목록의 순서와 같다. 배치 모드가 이 번호로 옮기고 치운다.
    public IReadOnlyList<GameObject> Spawned => _spawned;

    public event Action Rebuilt;

    private void Awake()
    {
        if (_root == null)
        {
            Debug.LogError("놀이터 오브젝트를 담을 자리가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        SlimeManager.OnPlaygroundChanged += Rebuild;
        SlimeManager.OnDataInitialized += Rebuild;
        Rebuild();
    }

    private void OnDestroy()
    {
        SlimeManager.OnPlaygroundChanged -= Rebuild;
        SlimeManager.OnDataInitialized -= Rebuild;
    }

    // 놀이터를 만지는 동안 기능을 세운다. 무엇이 왜 세우는지는 부르는 쪽이 안다.
    public void SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;

        foreach (GameObject spawned in _spawned)
        {
            ApplyInteractive(spawned);
        }
    }

    private void ApplyInteractive(GameObject target)
    {
        if (target == null) return;

        foreach (IPlaygroundObject playgroundObject in
                 target.GetComponentsInChildren<IPlaygroundObject>(true))
        {
            playgroundObject.SetInteractive(_isInteractive);
        }
    }

    // 몇 개 되지 않으므로 통째로 다시 세운다. 어느 항목이 바뀌었는지 맞춰 가며
    // 고치면 저장 목록의 번호와 화면의 번호가 어긋날 자리가 생긴다.
    public void Rebuild()
    {
        if (!enabled) return;

        Clear();

        SlimeManager manager = SlimeManager.Instance;
        if (manager == null) return;

        foreach (PlacedPlaygroundObject placed in manager.PlacedPlaygroundObjects)
        {
            GameObject prefab = FindPrefab(placed.Type);
            if (prefab == null)
            {
                // 프리팹을 못 찾아도 자리를 비우지 않는다. 비우면 저장 목록의
                // 번호와 화면의 번호가 어긋나 엉뚱한 것을 치우게 된다.
                _spawned.Add(null);
                continue;
            }

            GameObject instance = Instantiate(prefab, _root);
            instance.transform.localPosition = new Vector3(placed.X, placed.Y, 0f);
            ApplyInteractive(instance);
            _spawned.Add(instance);
        }

        Rebuilt?.Invoke();
    }

    private GameObject FindPrefab(EPlaygroundObjectType type)
    {
        foreach (PrefabBinding binding in _prefabs)
        {
            if (binding.Type == type) return binding.Prefab;
        }

        return null;
    }

    private void Clear()
    {
        foreach (GameObject spawned in _spawned)
        {
            if (spawned != null) Destroy(spawned);
        }

        _spawned.Clear();
    }
}
