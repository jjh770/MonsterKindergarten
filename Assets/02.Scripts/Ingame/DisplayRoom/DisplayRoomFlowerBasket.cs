using DG.Tweening;
using UnityEngine;

// 꽃바구니. 다가온 슬라임이 바구니 가장자리에 올라 머리를 쏙 넣고 뒤적인 뒤 튀어나온다. 나온
// 슬라임은 한동안 머리에 꽃 한 송이를 얹고 돌아다닌다. 꽃은 한 송이뿐이라 다른 슬라임이 뒤적이면
// 그 슬라임에게 옮겨 간다.
public sealed class DisplayRoomFlowerBasket : PlaygroundDecoration
{
    [Tooltip("바구니 그림입니다. 뒤적이는 동안 흔들립니다.")]
    [SerializeField] private Transform _basket;
    [Tooltip("슬라임이 올라설 바구니 가장자리입니다.")]
    [SerializeField] private Transform _rim;
    [SerializeField] private PlaygroundSlimeAttachment _crown;
    [SerializeField, Min(0.5f)] private float _crownDuration = 15f;
    [SerializeField, Min(0.2f)] private float _rummageDuration = 0.9f;
    [SerializeField, Min(0f)] private float _jumpPower = 0.4f;
    [SerializeField, Min(0f)] private float _hopOutDistance = 0.9f;

    private Quaternion _basketRotation;
    private Vector3 _basketScale;

    protected override void Awake()
    {
        base.Awake();
        if (!enabled) return;

        if (_basket == null || _rim == null || _crown == null)
        {
            Debug.LogError("꽃바구니의 바구니, 가장자리, 꽃 장식 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _basketRotation = _basket.localRotation;
        _basketScale = _basket.localScale;
    }

    protected override void Build(SlimeController slime, Sequence sequence)
    {
        Transform body = slime.transform;
        Vector3 scale = Hold.BaseScale;

        // 가장자리에 올라서서 머리를 넣는다.
        sequence.Append(body.DOJump(_rim.position, _jumpPower, 1, .4f));
        sequence.AppendCallback(() => PlaySound(EAudioSfx.SlimeLand));
        sequence.Append(body.DOMove(_rim.position + Vector3.down * .15f, .2f).SetEase(Ease.InQuad));
        sequence.Join(body.DOScale(new Vector3(scale.x * 1.08f, scale.y * .82f, scale.z), .2f));

        // 뒤적뒤적
        float rummage = sequence.Duration();
        sequence.InsertCallback(rummage, () => PlaySound(EAudioSfx.SlimeReaction));
        sequence.Insert(rummage, _basket.DOShakeRotation(_rummageDuration, new Vector3(0f, 0f, 12f), 12, 90f, false));
        sequence.Insert(rummage, body.DOShakePosition(_rummageDuration, new Vector3(.05f, .02f, 0f), 14, 90f, false, false));
        sequence.AppendInterval(_rummageDuration);

        // 꽃을 얹고 튀어나온다.
        Vector3 side = new(UnityEngine.Random.value < .5f ? -1f : 1f, UnityEngine.Random.Range(-.5f, .1f), 0f);
        Vector3 landing = ClampToPlayground(transform.position + side.normalized * _hopOutDistance, transform.parent);
        sequence.AppendCallback(() =>
        {
            PlaySound(EAudioSfx.ShopPurchase);
            _crown.Follow(slime, _crownDuration);
            _basket.DOPunchScale(_basketScale * .12f, .3f, 6, .5f);
        });
        sequence.Append(body.DOScale(new Vector3(scale.x * .9f, scale.y * 1.15f, scale.z), .15f));
        sequence.Append(body.DOJump(landing, _jumpPower, 1, .45f));
        sequence.Join(body.DOScale(scale, .3f).SetEase(Ease.OutBack));
    }

    protected override void ResetVisuals()
    {
        if (_basket == null) return;
        _basket.DOKill();
        _basket.localRotation = _basketRotation;
        _basket.localScale = _basketScale;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (_crown != null) _crown.Stop();
    }
}
