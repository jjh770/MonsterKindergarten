using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼을 누르면 말랑하게 눌렸다가, 놓으면 살짝 튀어 오르며 돌아온다.
// 크기만 건드리고 클릭 처리와 효과음은 Button과 ButtonSFX가 그대로 맡는다.
[RequireComponent(typeof(Button))]
public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.92f;
    [SerializeField, Min(0.01f)] private float _pressDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float _releaseDuration = 0.3f;
    [Tooltip("놓을 때 원래 크기를 넘어 튀어 오르는 정도입니다. 0이면 튀지 않습니다.")]
    [SerializeField, Min(0f)] private float _overshoot = 2.5f;

    private Button _button;
    private Tween _tween;
    private Vector3 _baseScale;
    private bool _isPressed;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnDisable()
    {
        // 누른 채로 꺼지면 줄어든 크기가 남는다. 다시 켜질 때 기준이 어긋나지 않게 되돌린다.
        bool isTweening = _tween != null && _tween.IsActive();
        if (isTweening || _isPressed)
        {
            _tween?.Kill();
            transform.localScale = _baseScale;
        }

        _tween = null;
        _isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.IsInteractable())
        {
            return;
        }

        // 기준 크기는 누를 때 읽는다. 다른 코드가 버튼의 크기를 직접 바꾸는 곳이 있어
        // (BackgroundThemeUI의 표시/숨김) 켜질 때 읽어 두면 그 값이 어긋난다.
        // 돌아오는 중에 다시 누르면 아직 줄어 있는 값이 아니라 먼저 읽어 둔 기준을 쓴다.
        if (_tween == null || !_tween.IsActive())
        {
            _baseScale = transform.localScale;
        }

        _isPressed = true;
        _tween?.Kill();
        _tween = transform
            .DOScale(_baseScale * _pressedScale, _pressDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    // 버튼을 알림처럼 한 번 출렁인다. 다른 코드가 버튼 크기를 따로 흔들면, 그 도중에 눌렀을 때
    // 커진 크기를 기준으로 읽어 버튼이 커진 채 남고 누를 때마다 더 커진다. 버튼 크기를 바꾸는
    // 연출은 모두 이 컴포넌트를 거쳐 기준 크기를 한 곳에서만 다룬다. 누르는 중에는 건너뛴다.
    public void Punch(float strength, float duration)
    {
        if (_isPressed) return;

        if (_tween == null || !_tween.IsActive())
        {
            _baseScale = transform.localScale;
        }

        _tween?.Kill();
        transform.localScale = _baseScale;
        _tween = transform
            .DOPunchScale(_baseScale * strength, duration, 6, 0.5f)
            .SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;
        _tween?.Kill();
        _tween = transform
            .DOScale(_baseScale, _releaseDuration)
            .SetEase(Ease.OutBack, _overshoot)
            .SetUpdate(true);
    }
}
