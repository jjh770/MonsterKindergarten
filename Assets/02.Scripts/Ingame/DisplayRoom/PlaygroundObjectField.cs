using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 저장에 남은 배치를 읽어 실제 오브젝트를 만든다. 놀이터에 무엇이 서 있는지는
// 이 컴포넌트 한 곳이 소유한다.
//
// 뽑기권이 GachaTicketField를 두는 것과 같은 분담이다. 저장은 "무엇이 어디에"만
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

    [SerializeField] private SlimeManager _slimeManager;

    [Header("Place / Remove")]
    [Tooltip("소품이 바닥에 닿을 때 발치에 만드는 먼지입니다.")]
    [SerializeField] private PlaygroundDustPuff _dustPrefab;
    [Tooltip("놓은 소품이 떨어지기 시작하는 높이입니다(월드 단위).")]
    [SerializeField, Min(0f)] private float _dropHeight = 1.2f;
    [SerializeField, Min(0.05f)] private float _dropDuration = 0.32f;
    [SerializeField, Min(0.05f)] private float _removeDuration = 0.25f;

    private readonly List<GameObject> _spawned = new();

    // 배치 모드 동안은 꺼 둔다. 다시 세울 때도 이 값을 따라가야 한다. 안 그러면
    // 하나 놓을 때마다 방 전체가 되살아난다.
    private bool _isInteractive = true;

    // 화면에 선 순서는 저장 목록의 순서와 같다. 배치 모드가 이 번호로 옮기고 치운다.
    public IReadOnlyList<GameObject> Spawned => _spawned;

    public event Action Rebuilt;

    private void Awake()
    {
        if (_root == null || _slimeManager == null)
        {
            Debug.LogError("놀이터 오브젝트 필드의 GameScene 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _slimeManager.PlaygroundChanged += Rebuild;
        _slimeManager.DataInitialized += Rebuild;
        Rebuild();
    }

    private void OnDestroy()
    {
        if (_slimeManager != null)
        {
            _slimeManager.PlaygroundChanged -= Rebuild;
            _slimeManager.DataInitialized -= Rebuild;
        }
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

        SlimeManager manager = _slimeManager;
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

    // 방금 놓은 소품을 위에서 떨어뜨린다. 바닥에 닿으면 납작해졌다 돌아오고 먼지와 착지음이 난다.
    // 저장은 이미 끝났고 화면만 움직이므로, 도중에 다시 세워져도 잃는 것은 없다.
    public void PlayPlaced(int index)
    {
        GameObject target = index >= 0 && index < _spawned.Count ? _spawned[index] : null;
        if (target == null) return;

        Transform body = target.transform;
        Vector3 rest = body.localPosition;
        Vector3 scale = body.localScale;
        body.localPosition = rest + Vector3.up * _dropHeight;
        Sequence sequence = DOTween.Sequence().SetLink(target);
        sequence.Append(body.DOLocalMove(rest, _dropDuration).SetEase(Ease.InQuad));
        sequence.AppendCallback(() =>
        {
            AudioManager.Instance?.PlaySFXRandomPitch(EAudioSfx.SlimeLand);
            if (_dustPrefab != null) Instantiate(_dustPrefab, FindFoot(body), Quaternion.identity, _root);
        });
        sequence.Append(body.DOScale(new Vector3(scale.x * 1.15f, scale.y * .8f, scale.z), .08f).SetEase(Ease.OutQuad));
        sequence.Append(body.DOScale(scale, .25f).SetEase(Ease.OutBack));
    }

    // 먼지가 일어날 발치다. 소품의 기준점은 그림 가운데쯤이라, 몸통 그림의 아래 끝을 찾는다.
    // 빛처럼 몸통 앞에 겹쳐 그리는 그림(정렬 순서 1 이상)과, 슬라임에 붙기 전까지 투명하게 기다리는
    // 하트나 빛 두르기는 크기가 몸통과 달라 빼고 본다.
    private static Vector3 FindFoot(Transform body)
    {
        Vector3 foot = body.position;
        bool found = false;
        foreach (SpriteRenderer sprite in body.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sprite.sprite == null || sprite.sortingOrder > 0 || sprite.color.a < .5f) continue;

            float bottom = sprite.bounds.min.y;
            if (found && bottom >= foot.y) continue;

            foot.y = bottom;
            found = true;
        }

        // 아래 끝에 딱 붙이면 먼지 대부분이 바닥 아래로 빠져 보이므로 조금 올린다.
        if (found) foot.y += .08f;
        return foot;
    }

    // 놓인 소품을 치운다. 저장에서 지우기 전에 화면 목록에서 떼어 두어, 지운 뒤 다시 세울 때
    // 함께 사라지지 않고 작아지며 사라지게 한다. 지우지 못하면 떼어 둔 것을 버리고 저장대로
    // 다시 세운다.
    public bool TryRemoveAnimated(int index, Func<bool> remove)
    {
        GameObject target = index >= 0 && index < _spawned.Count ? _spawned[index] : null;
        if (target != null) _spawned[index] = null;
        if (!remove())
        {
            if (target != null) Destroy(target);
            Rebuild();
            return false;
        }

        if (target == null) return true;

        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>())
        {
            collider.enabled = false;
        }

        Transform body = target.transform;
        Sequence sequence = DOTween.Sequence().SetLink(target);
        sequence.Append(body.DOScale(body.localScale * 1.1f, .08f).SetEase(Ease.OutQuad));
        sequence.Append(body.DOScale(Vector3.zero, _removeDuration).SetEase(Ease.InBack));
        sequence.OnComplete(() => Destroy(target));
        return true;
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
