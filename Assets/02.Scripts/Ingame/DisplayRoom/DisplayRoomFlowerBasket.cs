using DG.Tweening;
using UnityEngine;

// 꽃바구니. 다가온 슬라임이 바구니 가장자리에 올라 머리를 쏙 넣고 뒤적인 뒤 튀어나온다. 나온
// 슬라임은 한동안 머리에 꽃 한 송이를 얹고 돌아다닌다. 꽃은 한 송이뿐이라 다른 슬라임이 뒤적이면
// 그 슬라임에게 옮겨 간다.
//
// 꽃을 쓴 슬라임이 다른 슬라임과 부딪히면 그 슬라임에게 꽃을 건네고, 둘 사이에서 하트가 떠오른다.
// 받은 슬라임이 곧바로 돌려주며 꽃이 오가지 않도록, 건넨 뒤 잠깐 쉬고 방금 준 슬라임에게는 한동안
// 다시 주지 않는다. 꽃이 남은 시간은 건네도 이어진다.
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

    [Header("Give")]
    [Tooltip("꽃을 건넬 때 두 슬라임 사이에서 떠오르는 하트입니다. 비워 두면 하트 없이 꽃만 건넵니다.")]
    [SerializeField] private SpriteRenderer _giftHeart;
    [Tooltip("꽃을 건넨 뒤 다시 건넬 수 있을 때까지 쉬는 시간입니다(초).")]
    [SerializeField, Min(0f)] private float _giveCooldown = 1.5f;
    [Tooltip("방금 꽃을 준 슬라임에게 다시 줄 수 있을 때까지의 시간입니다(초).")]
    [SerializeField, Min(0f)] private float _giveBackDelay = 3f;

    private Quaternion _basketRotation;
    private Vector3 _basketScale;
    private readonly Collider2D[] _contacts = new Collider2D[8];
    private float _nextGiveTime;
    private SlimeController _lastGiver;
    private float _giveBackTime;
    private Vector3 _heartScale;
    // 건네는 동안 두 슬라임을 잠깐 붙잡아 세운다. 바구니 연출이 쓰는 Hold와는 따로 둔다.
    private readonly PlaygroundSlimeHold _giverHold = new();
    private readonly PlaygroundSlimeHold _receiverHold = new();
    private Sequence _give;

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
        if (_giftHeart != null)
        {
            _heartScale = _giftHeart.transform.localScale;
            SetAlpha(_giftHeart, 0f);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!enabled) return;

        // 건네는 도중에 한쪽이 합쳐지거나 장식장을 떠나면 그만두고 둘 다 놓아 준다.
        if (_give != null && (_giverHold.IsLost || _receiverHold.IsLost)) CancelGive();
        TryGiveCrown();
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
        Vector3 landing = PickLandingBeside(transform.position, _hopOutDistance, new Vector2(-.5f, .1f));
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

    // 꽃을 쓴 슬라임에 지금 닿아 있는 다른 슬라임이 있으면 그 슬라임에게 꽃을 건넨다. 끌거나 다른 소품이
    // 붙잡은 슬라임은 주지도 받지도 않는다.
    private void TryGiveCrown()
    {
        if (_give != null || !_crown.IsFollowing || Time.time < _nextGiveTime) return;

        SlimeController giver = _crown.Target;
        if (giver == null || !giver.IsPlaygroundTarget) return;

        Collider2D body = giver.GetComponent<Collider2D>();
        if (body == null || !body.enabled) return;

        int count = body.GetContacts(_contacts);
        for (int i = 0; i < count; i++)
        {
            SlimeController receiver = _contacts[i] != null ? _contacts[i].GetComponentInParent<SlimeController>() : null;
            if (receiver == null || receiver == giver || !receiver.IsPlaygroundTarget || !receiver.HasLanded) continue;
            if (receiver == _lastGiver && Time.time < _giveBackTime) continue;

            Give(giver, receiver);
            return;
        }
    }

    // 부딪힌 두 슬라임이 그 자리에 멈춰 살짝 눌린다. 준 슬라임이 상대 쪽으로 몸을 기울이며 꽃을 건네고,
    // 꽃이 머리에 닿으면 받은 슬라임이 기뻐 통 뛰며 둘 사이에서 하트가 떠오른다. 끝나면 둘 다 놓아 준다.
    private void Give(SlimeController giver, SlimeController receiver)
    {
        // 붙잡으면 콜라이더가 꺼져 크기를 읽을 수 없으므로 하트 자리는 먼저 정한다.
        Vector3 heartPosition = GetHeartPosition(giver, receiver);
        if (!_giverHold.TryBegin(giver)) return;
        if (!_receiverHold.TryBegin(receiver))
        {
            _giverHold.Release();
            return;
        }

        _lastGiver = giver;
        _giveBackTime = Time.time + _giveBackDelay;
        Transform from = giver.transform;
        Transform to = receiver.transform;
        Vector3 giverScale = _giverHold.BaseScale;
        Vector3 receiverScale = _receiverHold.BaseScale;
        Vector3 toward = to.position - from.position;
        toward.z = 0f;
        toward = toward.sqrMagnitude > .0001f ? toward.normalized : Vector3.right;

        _give = DOTween.Sequence().SetLink(gameObject);
        // 부딪혀 멈춘다.
        _give.Join(from.DOPunchScale(new Vector3(giverScale.x * .12f, -giverScale.y * .12f, 0f), .25f, 5, .5f));
        _give.Join(to.DOPunchScale(new Vector3(receiverScale.x * .12f, -receiverScale.y * .12f, 0f), .25f, 5, .5f));
        // 건넨다.
        _give.AppendCallback(() =>
        {
            _crown.PassTo(receiver, PassSeconds, .45f);
            PlaySound(EAudioSfx.SlimeBounce);
        });
        _give.Append(from.DOPunchPosition(toward * .15f, .35f, 2, .5f));
        _give.AppendInterval(PassSeconds - .35f);
        // 받는다.
        _give.AppendCallback(() =>
        {
            PlaySound(EAudioSfx.SlimeReaction);
            PlayHeart(heartPosition);
        });
        _give.Append(to.DOJump(to.position, .25f, 1, .35f));
        _give.AppendInterval(.25f);
        _give.OnComplete(() =>
        {
            _give = null;
            _nextGiveTime = Time.time + _giveCooldown;
            _giverHold.Release();
            _receiverHold.Release();
        });
    }

    private void CancelGive()
    {
        _give?.Kill();
        _give = null;
        _nextGiveTime = Time.time + _giveCooldown;
        _giverHold.Release();
        _receiverHold.Release();
    }

    // 두 슬라임 가운데, 둘 중 큰 쪽의 머리 높이쯤이다.
    private static Vector3 GetHeartPosition(SlimeController giver, SlimeController receiver)
    {
        Bounds a = giver.GetComponent<Collider2D>().bounds;
        Bounds b = receiver.GetComponent<Collider2D>().bounds;
        Vector3 middle = (a.center + b.center) * .5f;
        return middle + Vector3.up * Mathf.Max(a.extents.y, b.extents.y);
    }

    // 꽃이 한 머리에서 다른 머리로 건너가는 시간이다.
    private const float PassSeconds = .45f;

    // 두 슬라임 사이 위쪽에서 하트가 톡 커지며 떠오르다가 흐려진다.
    private void PlayHeart(Vector3 position)
    {
        if (_giftHeart == null) return;

        Transform heart = _giftHeart.transform;
        heart.DOKill();
        heart.position = new Vector3(position.x, position.y, heart.position.z);
        heart.localScale = Vector3.zero;
        SetAlpha(_giftHeart, 1f);
        Sequence sequence = DOTween.Sequence().SetTarget(heart).SetLink(gameObject);
        sequence.Append(heart.DOScale(_heartScale, .25f).SetEase(Ease.OutBack));
        sequence.Join(heart.DOMove(heart.position + Vector3.up * .5f, .9f).SetEase(Ease.OutQuad));
        sequence.Insert(.5f, _giftHeart.DOFade(0f, .4f));
    }

    private static void SetAlpha(SpriteRenderer renderer, float alpha)
    {
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
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
        CancelGive();
        if (_crown != null) _crown.Stop();
        if (_giftHeart == null) return;

        _giftHeart.transform.DOKill();
        SetAlpha(_giftHeart, 0f);
    }
}
