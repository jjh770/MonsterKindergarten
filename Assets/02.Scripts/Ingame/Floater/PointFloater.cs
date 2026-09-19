using DG.Tweening;
using Lean.Pool;
using TMPro;
using UnityEngine;

// 포인트 숫자는 화면 공간 UI로 그리고, 움직임만 월드 좌표로 계산해 매 프레임 옮긴다.
//
// 월드 공간 글자였을 때는 상단 바에 가려졌다. HUD 캔버스가 Screen Space - Overlay라
// 월드에 있는 것은 정렬 레이어와 상관없이 항상 그 밑에 그려진다. HUD를 카메라 방식으로
// 바꾸면 튜토리얼, 팝업, 세이프 에어리어 계산이 모두 흔들리므로 글자 쪽을 옮긴다.
public class PointFloater : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _floatDistance = 1f;

    // 누른 자리에서 바로 띄우면 숫자가 슬라임 얼굴을 가린다. 조금 위에서 시작한다.
    [SerializeField] private float _startOffsetY = 0.4f;
    [SerializeField] private float _sideDistance = 0.3f;
    [SerializeField] private float _duration = 0.8f;
    [SerializeField] private Ease _floatEase;
    [SerializeField] private Ease _fadeEase;

    private LeanGameObjectPool _pool;
    private RectTransform _rectTransform;
    private Camera _worldCamera;
    private Vector3 _startWorldPosition;
    private Vector3 _endWorldPosition;

    public void SetPool(LeanGameObjectPool pool)
    {
        _pool = pool;
    }

    public void Play(ClickInfo clickInfo)
    {
        _rectTransform = transform as RectTransform;
        _worldCamera = Camera.main;

        _text.text = $"{CurrencyIcon.Point}+{clickInfo.Point}";
        _text.alpha = 1f;

        _startWorldPosition = new Vector3(
            clickInfo.Position.x,
            clickInfo.Position.y + _startOffsetY,
            0f);
        _endWorldPosition = new Vector3(
            clickInfo.Position.x + UnityEngine.Random.Range(-_sideDistance, _sideDistance),
            _startWorldPosition.y + _floatDistance,
            0f);
        FollowWorldPosition(0f);

        // 위로 떠오르면서 페이드아웃
        DOTween.To(() => 0f, FollowWorldPosition, 1f, _duration)
            .SetEase(_floatEase)
            .SetTarget(this);
        _text.DOFade(0f, _duration).SetEase(_fadeEase).OnComplete(() =>
        {
            _pool.Despawn(gameObject);
        });
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
    }

    // 카메라가 움직여도 누른 자리를 따라가도록 매번 다시 투영한다.
    private void FollowWorldPosition(float progress)
    {
        if (_rectTransform == null || _worldCamera == null) return;

        RectTransform parent = _rectTransform.parent as RectTransform;
        if (parent == null) return;

        Vector3 worldPosition = Vector3.LerpUnclamped(
            _startWorldPosition,
            _endWorldPosition,
            progress);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            _worldCamera,
            worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            _rectTransform.anchoredPosition = localPoint;
        }
    }
}
