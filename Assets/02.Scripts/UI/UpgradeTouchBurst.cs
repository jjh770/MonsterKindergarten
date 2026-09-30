using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 성공을 손가락이 닿은 자리에서 터뜨린다.
//
// 별이 원을 그리며 사방으로 퍼져 나가고, 화살표 이미지 몇 개가 위로 올라간다.
// 진짜 ParticleSystem이 아니라 UI 이미지를 DOTween으로 움직인다. 업그레이드 서랍이
// Screen Space - Overlay 캔버스에 있어서, 파티클은 그 위에 그려지지 않기 때문이다.
//
// 서랍 위에 그려져야 하고 서랍 밖으로 올라가도 잘리면 안 되므로, 이 컴포넌트는 서랍과 별개인
// 전체 화면 레이어에 둔다. 입력은 받지 않는다. 조각은 씬에 둔 원본을 복제해 풀로 쓰며,
// 연타로 풀이 모자라면 가장 오래된 조각부터 다시 쓴다.
public sealed class UpgradeTouchBurst : MonoBehaviour
{
    [SerializeField] private RectTransform _root;
    [Tooltip("별 원본입니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _starTemplate;
    [Tooltip("화살표 원본입니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _arrowTemplate;

    [Header("Pool")]
    [Tooltip("동시에 겹쳐 보일 수 있는 폭발 수입니다. 풀 크기가 이 값에 비례합니다.")]
    [SerializeField, Range(1, 6)] private int _maxConcurrentBursts = 3;

    [Header("Star")]
    [SerializeField, Range(4, 20)] private int _starCount = 10;
    [Tooltip("별이 퍼져 나가 멈추는 원의 반지름입니다.")]
    [SerializeField, Min(0f)] private float _starRadius = 210f;
    [SerializeField, Min(0.1f)] private float _starDuration = 0.85f;
    [Tooltip("퍼지는 동안 도는 각도(도)의 최댓값입니다. 별마다 방향이 갈립니다.")]
    [SerializeField, Min(0f)] private float _starSpin = 120f;
    [SerializeField] private Vector2 _starSizeRange = new Vector2(0.6f, 0.6f);

    [Header("Arrow")]
    [SerializeField, Range(0, 8)] private int _arrowCount = 5;
    [SerializeField, Min(0f)] private float _arrowSpread = 120f;
    [SerializeField, Min(0f)] private float _arrowRiseDistance = 300f;
    [SerializeField, Min(0.1f)] private float _arrowDuration = 0.85f;
    [SerializeField, Min(0f)] private float _arrowStagger = 0.06f;

    private sealed class Piece
    {
        public RectTransform Rect;
        public Graphic Graphic;
        public Sequence Tween;
    }

    private readonly List<Piece> _stars = new();
    private readonly List<Piece> _arrows = new();
    private int _starCursor;
    private int _arrowCursor;

    private void Awake()
    {
        if (_root == null || _starTemplate == null || _arrowTemplate == null)
        {
            Debug.LogError("업그레이드 터치 효과의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _starTemplate.gameObject.SetActive(false);
        _arrowTemplate.gameObject.SetActive(false);
        for (int i = 0; i < _maxConcurrentBursts * _starCount; i++)
        {
            _stars.Add(CreatePiece(_starTemplate.gameObject, $"Star{i + 1}"));
        }

        for (int i = 0; i < _maxConcurrentBursts * _arrowCount; i++)
        {
            _arrows.Add(CreatePiece(_arrowTemplate.gameObject, $"Arrow{i + 1}"));
        }
    }

    private void OnDisable()
    {
        StopAll(_stars);
        StopAll(_arrows);
    }

    private void OnDestroy()
    {
        StopAll(_stars);
        StopAll(_arrows);
    }

    // screenPosition은 화면 좌표(픽셀)다. 오버레이 캔버스라 카메라는 쓰지 않는다.
    public void Play(Vector2 screenPosition)
    {
        if (!enabled) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _root, screenPosition, null, out Vector2 origin))
        {
            return;
        }

        // 한 바퀴를 고르게 나눈다. 시작 각도는 폭발마다 달라 매번 같은 모양이 되지 않게 한다.
        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; i < _starCount; i++)
        {
            Piece piece = Next(_stars, ref _starCursor);
            if (piece == null) break;

            float angle = startAngle + 360f * i / _starCount;
            EmitStar(
                piece,
                origin,
                angle,
                _starRadius * Random.Range(0.92f, 1.08f),
                Random.Range(_starSizeRange.x, _starSizeRange.y),
                Random.Range(-_starSpin, _starSpin));
        }

        for (int i = 0; i < _arrowCount; i++)
        {
            Piece piece = Next(_arrows, ref _arrowCursor);
            if (piece == null) break;

            // 화살표는 거의 곧게 위로 간다. 좌우로만 조금씩 벌어진다.
            float normalized = _arrowCount <= 1 ? 0.5f : i / (float)(_arrowCount - 1);
            float sideAngle = Mathf.Lerp(-_arrowSpread, _arrowSpread, normalized) * 0.5f;
            EmitArrow(piece, origin, sideAngle, i * _arrowStagger);
        }
    }

    // 별은 중심에서 커지며 원 위로 퍼져 나가고, 끝에서 옅어진다. 위로 떠오르지는 않는다.
    private void EmitStar(
        Piece piece,
        Vector2 origin,
        float angleFromUp,
        float radius,
        float scale,
        float spin)
    {
        piece.Tween?.Kill();

        Vector2 direction = Quaternion.Euler(0f, 0f, angleFromUp) * Vector2.up;
        Vector2 target = origin + direction * radius;

        piece.Rect.gameObject.SetActive(true);
        piece.Rect.SetAsLastSibling();
        piece.Rect.anchoredPosition = origin;
        piece.Rect.localScale = Vector3.one * (scale * 0.2f);
        piece.Rect.localRotation = Quaternion.identity;
        piece.Graphic.color = new Color(1f, 1f, 1f, 0f);

        float duration = _starDuration;
        Sequence sequence = DOTween.Sequence();
        sequence.Insert(0f, piece.Rect
            .DOAnchorPos(target, duration)
            .SetEase(Ease.OutCubic));
        sequence.Insert(0f, piece.Rect
            .DOScale(scale, duration * 0.4f)
            .SetEase(Ease.OutBack));
        sequence.Insert(0f, piece.Rect
            .DOLocalRotate(new Vector3(0f, 0f, spin), duration, RotateMode.FastBeyond360)
            .SetEase(Ease.OutCubic));
        sequence.Insert(0f, piece.Graphic.DOFade(1f, duration * 0.15f));

        // 도착한 별이 잠깐 머물다 작아지며 사라진다.
        sequence.Insert(duration * 0.55f, piece.Rect
            .DOScale(scale * 0.35f, duration * 0.45f)
            .SetEase(Ease.InQuad));
        sequence.Insert(duration * 0.55f, piece.Graphic.DOFade(0f, duration * 0.45f));

        sequence.OnComplete(() =>
        {
            piece.Tween = null;
            piece.Rect.gameObject.SetActive(false);
        });
        piece.Tween = sequence;
    }

    // 화살표는 터치한 자리에서 커지며 곧장 위로 올라가다 옅어진다.
    private void EmitArrow(Piece piece, Vector2 origin, float angleFromUp, float delay)
    {
        piece.Tween?.Kill();

        Vector2 direction = Quaternion.Euler(0f, 0f, angleFromUp) * Vector2.up;
        Vector2 start = origin + direction * 30f;
        Vector2 target = start + new Vector2(direction.x * 60f, _arrowRiseDistance);

        piece.Rect.gameObject.SetActive(true);
        piece.Rect.SetAsLastSibling();
        piece.Rect.anchoredPosition = start;
        piece.Rect.localScale = Vector3.one * 0.2f;
        piece.Rect.localRotation = Quaternion.identity;
        piece.Graphic.color = new Color(1f, 1f, 1f, 0f);

        float duration = _arrowDuration;
        Sequence sequence = DOTween.Sequence();
        sequence.Insert(delay, piece.Rect
            .DOAnchorPos(target, duration)
            .SetEase(Ease.OutQuad));
        sequence.Insert(delay, piece.Rect
            .DOScale(1f, duration * 0.3f)
            .SetEase(Ease.OutBack));
        sequence.Insert(delay, piece.Graphic.DOFade(1f, duration * 0.15f));
        sequence.Insert(delay + duration * 0.5f, piece.Graphic
            .DOFade(0f, duration * 0.5f));

        sequence.OnComplete(() =>
        {
            piece.Tween = null;
            piece.Rect.gameObject.SetActive(false);
        });
        piece.Tween = sequence;
    }

    private Piece CreatePiece(GameObject templateObject, string pieceName)
    {
        GameObject clone = Instantiate(templateObject, _root);
        clone.name = pieceName;
        clone.SetActive(false);
        return new Piece
        {
            Rect = clone.GetComponent<RectTransform>(),
            Graphic = clone.GetComponent<Graphic>(),
        };
    }

    // 풀을 한 바퀴 돌며 나눠 준다. 다 쓰고 있으면 가장 오래된 조각을 끊고 다시 쓴다.
    private static Piece Next(List<Piece> pool, ref int cursor)
    {
        if (pool.Count == 0) return null;

        Piece piece = pool[cursor];
        cursor = (cursor + 1) % pool.Count;
        return piece;
    }

    private static void StopAll(List<Piece> pool)
    {
        foreach (Piece piece in pool)
        {
            piece.Tween?.Kill();
            piece.Tween = null;
            if (piece.Rect != null) piece.Rect.gameObject.SetActive(false);
        }
    }
}
