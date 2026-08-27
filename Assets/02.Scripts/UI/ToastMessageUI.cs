using DG.Tweening;
using TMPro;
using UnityEngine;

// 잠깐 떴다 사라지는 안내 문구.
// 특정 화면에 속하지 않으므로 어느 UI든 참조해서 바로 호출할 수 있다.
//
// 이 컴포넌트는 자기 GameObject를 켜고 끈다. 씬에서 꺼진 채 시작해도
// 첫 Show에서 활성화되며 Awake에 의존하는 상태를 두지 않는다.
public sealed class ToastMessageUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _text;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float _displayDuration = 1.2f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.2f;

    private Sequence _sequence;

    private void OnDestroy()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    public void Show(string message)
    {
        if (_canvasGroup == null || _text == null)
        {
            Debug.LogError("토스트 UI의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _sequence?.Kill();
        _text.text = message;
        gameObject.SetActive(true);

        // 정보 패널이나 선택 모드 위에 떠야 하므로 매번 최상단으로 올린다.
        transform.SetAsLastSibling();
        _canvasGroup.alpha = 0f;
        _sequence = DOTween.Sequence()
            .Append(_canvasGroup.DOFade(1f, _fadeInDuration))
            .AppendInterval(_displayDuration)
            .Append(_canvasGroup.DOFade(0f, _fadeOutDuration))
            .OnComplete(() =>
            {
                _sequence = null;
                gameObject.SetActive(false);
            });
    }

    public void Hide()
    {
        _sequence?.Kill();
        _sequence = null;
        gameObject.SetActive(false);
    }
}
