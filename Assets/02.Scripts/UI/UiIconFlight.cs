using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 화면의 한 자리에서 상단 바 아이콘으로 그림이 날아가 앉는 공용 연출이다. 출발점에서 그림
// 조각이 터지고, 위로 던져 올린 곡선을 따라 날아가 아이콘 크기로 줄어들며, 닿으면 한 번 더
// 터지고 아이콘이 튕긴다. 재화와 저장은 건드리지 않는다. 닿은 순간에 올릴 것은 부른 쪽이
// 도착 콜백에서 올린다.
//
// HUD보다 앞의 오버레이 캔버스(TicketCollectOverlay) 위에 둔다. 출발점과 도착점 모두 화면
// 좌표로 받아 이 레이어의 좌표로 옮기므로, 출발점이 월드 스페이스 캔버스여도 같은 길을 쓴다.
//
// 한 장을 날리는 Play, 여러 장을 흩뿌렸다가 차례로 날리는 PlayBundle, 이미 레이어에 놓인
// 오브젝트를 날리는 Fly가 같은 궤적과 튕김을 쓴다. 목표의 위치는 날아가는 동안 매 프레임 다시
// 읽는다. 탐험 화면이 닫히는 동안처럼 상단 바가 내려오는 중이어도 움직이는 아이콘에 내려앉는다.
//
// 아이콘의 튕김은 아이콘마다 하나만 돈다. 여러 장이 거의 동시에 닿을 때 펀치를 새로 걸면
// 크기가 겹쳐 널뛰고, 서로 다른 연출이 각자 튕기면 원래 크기로 돌아오지 못한다. 그래서 같은
// 아이콘으로 날아가는 연출은 모두 이 컴포넌트를 거쳐야 한다.
public sealed class UiIconFlight : MonoBehaviour
{
    [Tooltip("터지는 조각과 날아가는 그림의 원본입니다. 이 레이어 안에 비활성으로 두고 복제해 씁니다.")]
    [SerializeField] private Image _pieceTemplate;

    [Header("Flight")]
    [SerializeField, Min(0.05f)] private float _flyDuration = 1f;
    [Tooltip("화면의 짧은 축을 기준으로 위로 띄우는 높이 비율입니다.")]
    [SerializeField, Range(0f, 0.5f)] private float _arcScreenRatio = 0.4f;
    [SerializeField, Range(0f, 0.5f)] private float _arcRandomness = 0.4f;
    [Tooltip("좌우로 흩어지는 폭의 비율입니다. 0이면 매번 같은 세로 궤적이 됩니다.")]
    [SerializeField, Range(0f, 0.3f)] private float _sideSpreadRatio = 0.25f;

    [Header("Burst")]
    [SerializeField, Range(4, 16)] private int _burstParticleCount = 12;
    [SerializeField, Min(0.05f)] private float _burstDuration = 0.38f;
    [SerializeField, Min(0f)] private float _burstLeadTime = 0.15f;
    [SerializeField, Min(0.1f)] private float _burstDistanceScale = 1.25f;

    [Header("Bundle")]
    [Tooltip("여러 장을 날릴 때 출발점 둘레로 먼저 흩어지는 거리입니다(레이어 단위).")]
    [SerializeField] private Vector2 _scatterDistance = new(160f, 110f);
    [SerializeField, Min(0.01f)] private float _scatterDuration = 0.2f;
    [Tooltip("여러 장을 날릴 때 한 장씩 출발하는 간격(초)입니다.")]
    [SerializeField, Min(0f)] private float _bundleInterval = 0.04f;

    [Header("Arrival")]
    [SerializeField, Min(0f)] private float _targetPunchScale = 0.5f;
    [SerializeField, Min(0f)] private float _arrivalSoundCooldown = 0.06f;

    // 날아가는 그림의 출발 크기에 대한 출발 폭발의 크기. 팝업의 큰 그림에서 출발하므로
    // 그림 크기 그대로 터뜨리면 화면을 덮는다.
    private const float GrantStartBurstScale = 0.35f;
    private const float ArrivalBurstIntensity = 0.65f;

    private readonly Vector3[] _corners = new Vector3[4];
    private readonly Dictionary<RectTransform, Vector3> _targetBaseScales = new();
    private readonly Dictionary<RectTransform, Tween> _targetPunches = new();
    private RectTransform _layer;
    private UiImagePool _pool;

    // 날아가는 그림을 올려 둘 레이어. 실제 오브젝트를 날리는 쪽은 여기로 옮겨 붙인 뒤 Fly를 부른다.
    public RectTransform Layer => _layer;

    private void Awake()
    {
        _layer = transform as RectTransform;
        if (_layer == null || _pieceTemplate == null)
        {
            Debug.LogError("아이콘 비행 연출의 레이어나 조각 원본이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 일괄 회수는 한꺼번에 여러 장이 터진다. 폭발 몇 번 분량을 미리 만들어 두고 나머지는
        // 모자랄 때 늘어난다.
        _pool = new UiImagePool(_pieceTemplate, _layer, (_burstParticleCount + 1) * 4);
    }

    private void OnDestroy()
    {
        foreach (Tween punch in _targetPunches.Values)
        {
            punch?.Kill(complete: false);
        }

        _targetPunches.Clear();
        foreach (KeyValuePair<RectTransform, Vector3> pair in _targetBaseScales)
        {
            if (pair.Key != null) pair.Key.localScale = pair.Value;
        }
    }

    // source(오버레이 UI)의 자리와 크기에서 sprite 한 장을 target으로 날려 보낸다.
    // 어느 경로로 빠지든 onArrived는 부른다. 연출을 못 보여 준 것이 보상을 주지 않을
    // 이유는 되지 않는다.
    public void Play(RectTransform source, Sprite sprite, RectTransform target, Action onArrived)
    {
        if (!enabled || _pool == null || source == null || sprite == null || target == null)
        {
            onArrived?.Invoke();
            return;
        }

        // 출발점은 Screen Space Overlay 팝업이라 카메라 없이 화면 좌표로 옮긴다.
        Vector2 startScreenPosition = RectTransformUtility.WorldToScreenPoint(null, source.position);
        Vector2 sourceScreenSize = GetScreenSize(source, null);
        if (!TryGetLayerSize(source, null, out Vector2 size))
        {
            onArrived?.Invoke();
            return;
        }

        // 도착 크기는 아이콘에 맞춘다. 출발 그림은 팝업의 큰 그림이라 일정한 비율로 줄이면
        // 아이콘보다 훨씬 큰 채로 닿는다.
        Vector2 targetScreenSize = GetScreenSize(target, null);
        float endScale = Mathf.Clamp(
            Mathf.Min(
                Mathf.Abs(targetScreenSize.x) / Mathf.Max(1f, Mathf.Abs(sourceScreenSize.x)),
                Mathf.Abs(targetScreenSize.y) / Mathf.Max(1f, Mathf.Abs(sourceScreenSize.y))),
            0.02f,
            1f);

        Image flying = _pool.Rent();
        RectTransform flyingRect = flying.rectTransform;
        flying.sprite = sprite;
        flying.color = Color.white;
        flying.raycastTarget = false;
        flying.preserveAspect = false;
        flyingRect.sizeDelta = size;
        flyingRect.localScale = Vector3.one;
        if (TryGetLayerPosition(startScreenPosition, out Vector2 startLocal))
        {
            flyingRect.anchoredPosition = startLocal;
        }

        Sequence sequence = Fly(
            flyingRect,
            startScreenPosition,
            size,
            target,
            endScale,
            sprite,
            GrantStartBurstScale,
            ArrivalBurstIntensity,
            () =>
            {
                _pool.Return(flying);
                AudioManager.Instance?.PlaySFXWithCooldown(
                    EAudioSfx.TicketCollect,
                    _arrivalSoundCooldown);
                onArrived?.Invoke();
            });
        sequence?.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    // fromWorld(오버레이 UI의 위치)에서 sprite를 count장 날린다. 출발점에서 한 번 터진 뒤 둘레로
    // 흩어졌다가 한 장씩 차례로 target에 내려앉는다. 보상이 여러 장이라는 것을 장수로 보여 줄 때 쓴다.
    // 소리는 보상마다 달라서 부른 쪽이 onEachArrived에서 낸다(index는 0부터). 연출을 못 보여 주는
    // 경로로 빠져도 onCompleted는 부른다.
    public void PlayBundle(
        Vector3 fromWorld,
        Sprite sprite,
        Vector2 iconSize,
        RectTransform target,
        int count,
        Action<int> onEachArrived,
        Action onCompleted)
    {
        Vector2 startScreenPosition = RectTransformUtility.WorldToScreenPoint(null, fromWorld);
        if (!enabled || _pool == null || sprite == null || target == null || count <= 0 ||
            !TryGetLayerPosition(startScreenPosition, out Vector2 startLocal) ||
            !TryGetLayerSize(target, null, out Vector2 targetSize))
        {
            onCompleted?.Invoke();
            return;
        }

        float endScale = Mathf.Clamp(
            Mathf.Min(
                targetSize.x / Mathf.Max(1f, iconSize.x),
                targetSize.y / Mathf.Max(1f, iconSize.y)),
            0.02f,
            1f);

        PlayBurst(sprite, startLocal, iconSize, 1f);

        int remaining = count;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            Image image = _pool.Rent();
            RectTransform rect = image.rectTransform;
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            rect.sizeDelta = iconSize;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = startLocal;
            rect.SetAsLastSibling();

            Vector2 scatter = startLocal + new Vector2(
                UnityEngine.Random.Range(-_scatterDistance.x, _scatterDistance.x),
                UnityEngine.Random.Range(-_scatterDistance.y, _scatterDistance.y));

            Sequence sequence = DOTween.Sequence();
            sequence.AppendInterval(index * _bundleInterval);
            sequence.Append(rect.DOAnchorPos(scatter, _scatterDuration).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() =>
            {
                // 첫 장만 도착에서 터뜨린다. 스무 장이 모두 터지면 아이콘이 조각에 묻힌다.
                Fly(
                    rect,
                    RectTransformUtility.WorldToScreenPoint(null, rect.position),
                    iconSize,
                    target,
                    endScale,
                    sprite,
                    0f,
                    index == 0 ? ArrivalBurstIntensity : 0f,
                    () =>
                    {
                        _pool.Return(image);
                        onEachArrived?.Invoke(index);
                        if (--remaining == 0) onCompleted?.Invoke();
                    })
                    ?.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            });
            sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }
    }

    // 이미 Layer 위에 놓인 flying을 target까지 날린다. 크기는 size에서 endScale배로 줄고,
    // 출발에서 size * startBurstScale 크기로, 도착에서 arrivalBurstIntensity 세기로 burstSprite
    // 조각이 터진다(0이면 그 폭발은 없다). 도착하면 target을 튕기고 onArrived를 부른다.
    // 위치를 레이어 좌표로 옮기지 못하면 연출 없이 onArrived만 부르고 null을 돌려준다.
    public Sequence Fly(
        RectTransform flying,
        Vector2 startScreenPosition,
        Vector2 size,
        RectTransform target,
        float endScale,
        Sprite burstSprite,
        float startBurstScale,
        float arrivalBurstIntensity,
        Action onArrived)
    {
        if (!enabled || _pool == null || target == null ||
            !TryGetLayerPosition(startScreenPosition, out Vector2 startLocal))
        {
            onArrived?.Invoke();
            return null;
        }

        ArcShape arc = CreateArcShape();
        flying.SetAsLastSibling();

        Sequence sequence = DOTween.Sequence();
        if (startBurstScale > 0f)
        {
            PlayBurst(burstSprite, startLocal, size * startBurstScale, 1f);
            sequence.AppendInterval(_burstLeadTime);
        }

        sequence.Append(
            DOVirtual.Float(0f, 1f, _flyDuration, progress =>
            {
                if (target == null) return;

                Vector2 screenPosition = EvaluateArc(
                    startScreenPosition,
                    RectTransformUtility.WorldToScreenPoint(null, target.position),
                    arc,
                    progress);
                if (TryGetLayerPosition(screenPosition, out Vector2 localPosition))
                {
                    flying.anchoredPosition = localPosition;
                }
            })
                .SetEase(Ease.InOutQuad));
        sequence.Join(
            flying.DOScale(Vector3.one * endScale, _flyDuration)
                .SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            if (arrivalBurstIntensity > 0f && target != null &&
                TryGetLayerPosition(
                    RectTransformUtility.WorldToScreenPoint(null, target.position),
                    out Vector2 targetLocal))
            {
                PlayBurst(burstSprite, targetLocal, size * endScale, arrivalBurstIntensity);
            }

            PunchTarget(target);
            onArrived?.Invoke();
        });
        return sequence;
    }

    public bool TryGetLayerPosition(Vector2 screenPosition, out Vector2 localPosition)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _layer,
            screenPosition,
            null,
            out localPosition);
    }

    // rect가 화면에 그려진 크기를 이 레이어의 단위로 잰다. camera는 rect가 있는 캔버스의 카메라다
    // (오버레이면 null, 월드 스페이스 캔버스면 그 캔버스를 비추는 카메라).
    public bool TryGetLayerSize(RectTransform rect, Camera camera, out Vector2 size)
    {
        rect.GetWorldCorners(_corners);
        Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
        Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
        if (!TryGetLayerPosition(bottomLeftScreen, out Vector2 bottomLeft) ||
            !TryGetLayerPosition(topRightScreen, out Vector2 topRight))
        {
            size = default;
            return false;
        }

        size = new Vector2(
            Mathf.Max(1f, Mathf.Abs(topRight.x - bottomLeft.x)),
            Mathf.Max(1f, Mathf.Abs(topRight.y - bottomLeft.y)));
        return true;
    }

    private Vector2 GetScreenSize(RectTransform rect, Camera camera)
    {
        rect.GetWorldCorners(_corners);
        return (Vector2)RectTransformUtility.WorldToScreenPoint(camera, _corners[2]) -
               RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
    }

    // 별도 텍스처를 늘리지 않고 날아가는 그림을 작은 반짝이 조각으로 재사용한다.
    // 출발점에서는 터치 반응, 도착점에서는 아이콘으로 흡수됐다는 반응을 만든다.
    private void PlayBurst(Sprite sprite, Vector2 position, Vector2 iconSize, float intensity)
    {
        if (_pool == null || sprite == null) return;

        int count = Mathf.Max(1, _burstParticleCount);
        float baseSize = Mathf.Max(20f, Mathf.Min(iconSize.x, iconSize.y) * 0.38f);
        float distance = Mathf.Max(iconSize.x, iconSize.y) * _burstDistanceScale * intensity;

        var pieces = new Image[count + 1];
        Sequence burst = DOTween.Sequence();

        // 중심에서 한 번 크게 번지는 잔상을 먼저 보여 줘 작은 조각만 흩어질 때보다
        // 터치와 도착 순간을 또렷하게 읽을 수 있게 한다.
        Image flashImage = _pool.Rent();
        pieces[0] = flashImage;
        RectTransform flash = flashImage.rectTransform;
        flash.anchoredPosition = position;
        flash.sizeDelta = iconSize * (1.35f * Mathf.Max(0.8f, intensity));
        flash.localScale = Vector3.one * 0.55f;
        flashImage.sprite = sprite;
        flashImage.preserveAspect = false;
        flashImage.color = new Color(1f, 0.84f, 0.2f, 0.85f);

        float flashDuration = _burstDuration * 0.75f;
        burst.Join(flash.DOScale(1.7f, flashDuration).SetEase(Ease.OutCubic));
        burst.Join(flashImage.DOFade(0f, flashDuration).SetEase(Ease.InQuad));

        for (int i = 0; i < count; ++i)
        {
            float angle = 360f * i / count + UnityEngine.Random.Range(-12f, 12f);
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;

            Image image = _pool.Rent();
            pieces[i + 1] = image;
            RectTransform particle = image.rectTransform;
            particle.anchoredPosition = position;
            particle.sizeDelta = Vector2.one * baseSize;
            particle.localScale = Vector3.one * 0.65f;
            image.sprite = sprite;
            image.preserveAspect = false;
            image.color = new Color(1f, 0.88f, 0.25f, 1f);

            float particleDistance = distance * UnityEngine.Random.Range(0.75f, 1.15f);
            burst.Join(
                particle.DOAnchorPos(position + direction * particleDistance, _burstDuration)
                    .SetEase(Ease.OutCubic));
            burst.Join(
                particle.DOScale(UnityEngine.Random.Range(1.15f, 1.55f), _burstDuration)
                    .SetEase(Ease.OutBack));
            burst.Join(image.DOFade(0f, _burstDuration));
        }

        burst.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        burst.OnComplete(() =>
        {
            foreach (Image piece in pieces)
            {
                _pool.Return(piece);
            }
        });
    }

    // 이미 커졌다 작아지는 중이면 그 연출에 얹는다. 스무 장이 한꺼번에 닿아도 아이콘은
    // 한 번만 움직인다. 처음 본 아이콘의 크기를 원래 크기로 기억해 두고 끝날 때마다 되돌린다.
    private void PunchTarget(RectTransform target)
    {
        if (target == null) return;
        if (_targetPunches.TryGetValue(target, out Tween running) && running != null && running.IsActive()) return;

        if (!_targetBaseScales.TryGetValue(target, out Vector3 baseScale))
        {
            baseScale = target.localScale;
            _targetBaseScales[target] = baseScale;
        }

        target.localScale = baseScale;
        Tween punch = target.DOPunchScale(Vector3.one * _targetPunchScale, 0.2f, 5, 0.5f);
        void Finish()
        {
            _targetPunches.Remove(target);
            if (target != null) target.localScale = baseScale;
        }

        punch.OnComplete(Finish);
        punch.OnKill(Finish);
        _targetPunches[target] = punch;
    }

    // 한 번의 비행이 그리는 곡선의 모양. 무작위 값은 출발할 때 한 번만 뽑고, 목표의 위치는
    // 매 프레임 다시 읽어 곡선에 넣는다. 제어점을 출발할 때 굳히면 화면 밖에서 내려오는 목표를
    // 기준으로 휘어 곡선이 과하게 꺾인다.
    private readonly struct ArcShape
    {
        public readonly float FirstAlong;
        public readonly float SecondAlong;
        public readonly float Lift;
        public readonly float FirstSide;
        public readonly float SecondSide;

        public ArcShape(float firstAlong, float secondAlong, float lift, float firstSide, float secondSide)
        {
            FirstAlong = firstAlong;
            SecondAlong = secondAlong;
            Lift = lift;
            FirstSide = firstSide;
            SecondSide = secondSide;
        }
    }

    // 위로 한 번 띄웠다가 아이콘으로 내려앉게 만든다. 경로에 수직인 방향으로 휘면 출발점과
    // 아이콘의 위치 관계에 따라 궤적이 옆으로 눕거나 아래로 처진다. 화면 기준 위쪽은 그
    // 관계와 무관하게 늘 같은 방향이라 던져 올린 느낌이 유지된다.
    //
    // 좌우 흔들림은 따로 뽑는다. 높이만 무작위로 두면 여러 장을 연달아 날릴 때 궤적이
    // 한 평면에 겹쳐 보인다.
    private ArcShape CreateArcShape()
    {
        // 화면의 짧은 축을 기준으로 삼아야 해상도가 달라져도 궤적이 같아 보인다.
        // 넓은 화면에서는 세로 영역(PortraitColumn)의 폭이 짧은 축이다.
        float shortSide = Mathf.Min(PortraitColumn.GetPixelRect().width, Screen.height);
        float lift = shortSide * _arcScreenRatio *
                     UnityEngine.Random.Range(1f - _arcRandomness, 1f + _arcRandomness);
        float spread = shortSide * _sideSpreadRatio;
        return new ArcShape(
            UnityEngine.Random.Range(0.2f, 0.35f),
            UnityEngine.Random.Range(0.6f, 0.8f),
            lift,
            UnityEngine.Random.Range(-spread, spread),
            UnityEngine.Random.Range(-spread, spread));
    }

    private static Vector2 EvaluateArc(Vector2 start, Vector2 end, ArcShape arc, float progress)
    {
        Vector2 path = end - start;
        Vector2 firstControlPoint = start + path * arc.FirstAlong +
                                    Vector2.up * arc.Lift + Vector2.right * arc.FirstSide;
        // 두 번째는 덜 띄운다. 같은 높이로 두면 정점이 평평해져 포물선이 아니라
        // 위로 밀려 올라간 직선처럼 보인다.
        Vector2 secondControlPoint = start + path * arc.SecondAlong +
                                     Vector2.up * (arc.Lift * 0.35f) + Vector2.right * arc.SecondSide;

        float inverse = 1f - progress;
        return inverse * inverse * inverse * start +
               3f * inverse * inverse * progress * firstControlPoint +
               3f * inverse * progress * progress * secondControlPoint +
               progress * progress * progress * end;
    }
}
