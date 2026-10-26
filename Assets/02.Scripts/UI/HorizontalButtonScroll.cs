using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 하단 메뉴 줄의 스크롤은 같은 오브젝트의 ScrollRect가 맡는다. 끌기, 관성, 끝에서 늘어났다
// 돌아오는 탄성, 끌기로 바뀐 터치의 클릭 취소, 위를 덮은 UI에 입력을 양보하는 것까지 모두
// EventSystem을 따르는 기본 동작이다. Content의 HorizontalLayoutGroup과 ContentSizeFitter가 폭을
// 정하므로, 버튼은 Content 아래에 두기만 하면 줄에 들어간다.
//
// 이 컴포넌트는 튜토리얼이 줄 끝쪽 버튼을 가리킬 때 그 버튼이 보이도록 줄을 미는 일만 한다.
[RequireComponent(typeof(ScrollRect))]
public sealed class HorizontalButtonScroll : MonoBehaviour, IBeginDragHandler
{
    [SerializeField] private ScrollRect _scrollRect;
    [Tooltip("ScrollToVisible이 항목을 보이는 칸의 가장자리에서 띄우는 거리입니다.")]
    [SerializeField, Min(0f)] private float _visiblePadding = 40f;

    private readonly Vector3[] _worldCorners = new Vector3[4];
    private Tween _scrollTween;

    private void Awake()
    {
        if (_scrollRect == null || _scrollRect.content == null)
        {
            Debug.LogError("하단 메뉴 스크롤의 ScrollRect 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        FinishScrollTween();
    }

    // 튜토리얼이 미는 도중에 손가락이 줄을 잡으면 목적지로 바로 옮기고 끝낸다. 그냥 끊으면
    // onCompleted가 불리지 않아 그것을 기다리는 튜토리얼이 멈춘다.
    public void OnBeginDrag(PointerEventData eventData)
    {
        FinishScrollTween();
    }

    // 항목이 보이는 칸 안에 들어오도록 줄을 옮긴다. 이미 보이면 곧바로 onCompleted를 부른다.
    // 튜토리얼이 줄 끝쪽 버튼을 가리킬 때, 그 버튼이 화면 밖이면 구멍만 화면 밖에 남기 때문에 쓴다.
    public void ScrollToVisible(RectTransform item, Action onCompleted, float duration = 0.3f)
    {
        if (!isActiveAndEnabled || item == null)
        {
            onCompleted?.Invoke();
            return;
        }

        RectTransform content = _scrollRect.content;
        RectTransform viewport = _scrollRect.viewport != null
            ? _scrollRect.viewport
            : (RectTransform)_scrollRect.transform;

        // 항목이 방금 켜졌거나 패널이 방금 열렸을 수 있다. 위치를 읽기 전에 배치부터 다시 잡는다.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float scrollableWidth = content.rect.width - viewport.rect.width;
        if (scrollableWidth <= 0f)
        {
            onCompleted?.Invoke();
            return;
        }

        item.GetWorldCorners(_worldCorners);
        float itemMin = viewport.InverseTransformPoint(_worldCorners[0]).x;
        float itemMax = viewport.InverseTransformPoint(_worldCorners[2]).x;
        float viewportMin = viewport.rect.xMin + _visiblePadding;
        float viewportMax = viewport.rect.xMax - _visiblePadding;

        float shift = 0f;
        if (itemMax > viewportMax)
        {
            shift = viewportMax - itemMax;
        }
        else if (itemMin < viewportMin)
        {
            shift = viewportMin - itemMin;
        }

        // 줄이 왼쪽으로 shift만큼 가면 정규화 위치는 그 비율만큼 오른쪽으로 간다.
        float current = _scrollRect.horizontalNormalizedPosition;
        float target = Mathf.Clamp01(current - shift / scrollableWidth);
        FinishScrollTween();
        _scrollRect.StopMovement();
        if (Mathf.Approximately(target, current))
        {
            onCompleted?.Invoke();
            return;
        }

        _scrollTween = DOTween
            .To(
                () => _scrollRect.horizontalNormalizedPosition,
                value => _scrollRect.horizontalNormalizedPosition = value,
                target,
                duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _scrollTween = null;
                onCompleted?.Invoke();
            });
    }

    private void FinishScrollTween()
    {
        Tween tween = _scrollTween;
        _scrollTween = null;
        tween?.Kill(complete: true);
    }
}
