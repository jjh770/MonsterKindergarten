using UnityEngine;
using UnityEngine.UI;

// 뽑기 통 안에 쌓인 캡슐 더미를 맡는다. 물리는 GachaCapsulePile이 계산하고, 여기서는 그 결과를 캡슐
// 그림에 옮기고, 화면을 열 때마다 더미를 새로 쌓고, 튀겨 올리고, 배출구로 하나씩 떨어뜨린다.
// 더미 오브젝트(CapsulePile)에 붙는다. 이 오브젝트의 사각형이 통의 안쪽 벽이고, 자식은 캡슐 그림뿐이다.
//
// 더미는 겉모습일 뿐 무엇이 나오는지와 상관없다. 무지개 캡슐은 넣지 않는다. 통 안에 보이면 특별한
// 슬라임이 흔한 것처럼 보이고, 나오는 캡슐에서도 드러나지 않기 때문이다.
[RequireComponent(typeof(RectTransform))]
public sealed class GachaCapsulePileView : MonoBehaviour
{
    [SerializeField] private Image[] _capsules;
    [Tooltip("일반 캡슐 그림입니다. 더미와 배출되는 캡슐이 함께 씁니다.")]
    [SerializeField] private Sprite[] _capsuleSprites;
    [Tooltip("캡슐끼리 부딪히는 원의 반지름이 그림 반지름의 몇 배인지입니다. 작을수록 서로 겹쳐 뒤섞여 보입니다.")]
    [SerializeField, Range(0.4f, 1f)] private float _colliderRatio = 0.6f;
    [Tooltip("통 안에 보이는 캡슐 수입니다. 남는 캡슐은 감춰서 위쪽에 굴러다닐 공간을 둡니다.")]
    [SerializeField, Min(1)] private int _visibleCount = 24;
    [Tooltip("통 안의 중력입니다. 캔버스 단위/초²입니다.")]
    [SerializeField, Min(0f)] private float _gravity = 2600f;
    [Tooltip("캡슐을 튀겨 올리는 최대 속도입니다. 섞을 때와 통을 세게 흔들 때 씁니다. 캔버스 단위/초입니다.")]
    [SerializeField, Min(0f)] private float _kickSpeed = 1300f;
    [Tooltip("화면을 열 때 캡슐마다 무작위로 정하는 처음 기울기의 최대값입니다.")]
    [SerializeField, Range(0f, 90f)] private float _maxTilt = 45f;
    [Tooltip("캡슐마다 무작위로 정하는 크기의 최소 배율입니다.")]
    [SerializeField, Range(0.5f, 1f)] private float _minScale = 0.85f;

    // 화면을 열기 전에 더미를 미리 쌓아 두는 시간이다.
    private const float WarmUpSeconds = 1.5f;
    private const int CapsulesPerKick = 4;
    private const float StartScatter = 20f;

    private RectTransform _rect;
    private Vector2[] _authoredHome;
    private int[] _order;
    private GachaCapsulePile _pile;
    private bool _running;
    private bool _isReady;

    public RectTransform Rect => _rect;
    public float Gravity => _gravity;
    public float KickSpeed => _kickSpeed;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        if (_capsules == null || _capsules.Length == 0 ||
            _capsuleSprites == null || _capsuleSprites.Length == 0)
        {
            Debug.LogError("뽑기 통 캡슐 더미의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _authoredHome = new Vector2[_capsules.Length];
        _order = new int[_capsules.Length];
        _pile = new GachaCapsulePile(_capsules.Length);
        for (int i = 0; i < _capsules.Length; i++)
        {
            _authoredHome[i] = _capsules[i].rectTransform.anchoredPosition;
            _order[i] = i;
        }

        _isReady = true;
    }

    private void Update()
    {
        if (!_running) return;

        _pile.Advance(Time.unscaledDeltaTime);
        Apply();
    }

    public Sprite PickCapsuleSprite()
    {
        return _capsuleSprites[Random.Range(0, _capsuleSprites.Length)];
    }

    // 씬에 놓은 자리 근처에 크기, 기울기, 색, 앞뒤 순서를 매번 새로 정해 놓고, 보이기 전에 물리를 미리
    // 돌려 바닥부터 쌓인 모습으로 시작한다.
    public void Scramble()
    {
        if (!_isReady) return;

        int count = _capsules.Length;
        _pile.Reset(_rect.rect.size * 0.5f, _gravity);

        // 어느 캡슐을 쓸지와 앞뒤 순서를 함께 섞는다. 섞인 순서의 앞쪽만 통에 넣는다.
        for (int i = count - 1; i > 0; i--)
        {
            int pick = Random.Range(0, i + 1);
            (_order[i], _order[pick]) = (_order[pick], _order[i]);
        }

        int visible = Mathf.Min(_visibleCount, count);
        for (int order = 0; order < count; order++)
        {
            int i = _order[order];
            Image capsule = _capsules[i];
            capsule.transform.SetAsLastSibling();
            if (order >= visible) continue;

            RectTransform rect = capsule.rectTransform;
            float scale = Random.Range(_minScale, 1f);
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f * scale * _colliderRatio;
            Vector2 start = _authoredHome[i] + Random.insideUnitCircle * StartScatter;
            _pile.Place(i, start, radius, Random.Range(-_maxTilt, _maxTilt));

            capsule.sprite = PickCapsuleSprite();
            rect.localScale = Vector3.one * scale;
        }

        _pile.Simulate(WarmUpSeconds);
        Apply();
        _running = true;
    }

    public void Stop()
    {
        _running = false;
        if (_isReady) _pile.FrameAcceleration = Vector2.zero;
    }

    // 통이 움직이며 생기는 가속도다. 통 쪽(GachaMachineGrab)이 매 프레임 정한다.
    public void SetFrameAcceleration(Vector2 acceleration)
    {
        if (_isReady) _pile.FrameAcceleration = acceleration;
    }

    // 몇 개를 골라 위로 튀겨 올린다. 옆으로도 조금씩 흩어 서로 부딪히게 한다. 속도는 _kickSpeed를 넘지 않는다.
    public void Kick(float speed)
    {
        if (!_isReady) return;

        speed = Mathf.Min(speed, _kickSpeed);
        for (int k = 0; k < CapsulesPerKick; k++)
        {
            int index = Random.Range(0, _pile.Count);
            if (!_pile.IsInPile(index)) continue;

            _pile.Kick(index, new Vector2(
                Random.Range(-0.45f, 0.45f) * speed,
                Random.Range(0.6f, 1f) * speed));
        }
    }

    // worldPoint 쪽 바닥의 캡슐 하나를 통 아래로 떨어뜨린다. 나오는 캡슐이 더미에서 빠진 것처럼 보인다.
    public void DropToward(Vector3 worldPoint)
    {
        if (!_isReady) return;

        _pile.DropLowest(_rect.InverseTransformPoint(worldPoint).x);
    }

    private void Apply()
    {
        for (int i = 0; i < _capsules.Length; i++)
        {
            Image capsule = _capsules[i];
            bool visible = _pile.IsVisible(i);
            if (capsule.gameObject.activeSelf != visible) capsule.gameObject.SetActive(visible);
            if (!visible) continue;

            RectTransform rect = capsule.rectTransform;
            rect.anchoredPosition = _pile.GetPosition(i);
            rect.localRotation = Quaternion.Euler(0f, 0f, _pile.GetAngle(i));
        }
    }
}
