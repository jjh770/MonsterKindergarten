using DG.Tweening;
using UnityEngine;

// 팝업 본체가 열릴 때 살짝 작은 크기에서 튀어 오르고, 닫힐 때 움츠러드는 공용 연출.
// 언제 열고 닫을지는 팝업을 소유한 컴포넌트가 정한다. 이 컴포넌트는 크기와 투명도만 움직인다.
public sealed class PopupMotion : MonoBehaviour
{
    [Tooltip("크기가 움직일 팝업 본체입니다. 비우면 이 오브젝트를 씁니다. 화면을 덮는 배경은 여기에 넣지 않습니다.")]
    [SerializeField] private RectTransform _panel;
    [Tooltip("지정하면 투명도와 입력 차단도 함께 맡습니다. 호출하는 쪽이 이미 페이드하는 팝업은 비워 둡니다.")]
    [SerializeField] private CanvasGroup _group;

    [Header("Open")]
    [SerializeField, Range(0.5f, 1f)] private float _openStartScale = 0.7f;
    [SerializeField, Min(0.01f)] private float _openDuration = 0.3f;
    [Tooltip("원래 크기를 넘어 튀어 오르는 정도입니다. 1.7이 기본 OutBack이고 0이면 튀지 않습니다.")]
    [SerializeField, Min(0f)] private float _overshoot = 1.7f;

    [Header("Close")]
    [SerializeField, Range(0.5f, 1f)] private float _closeEndScale = 0.7f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.25f;

    private Tween _scaleTween;
    private Tween _fadeTween;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        if (_panel == null)
        {
            _panel = (RectTransform)transform;
        }

        _baseScale = _panel.localScale;
    }

    private void OnDisable()
    {
        // 연출 도중에 꺼지면 줄어든 크기와 투명도가 남아 다음에 켤 때 어긋난다.
        KillTweens();
        _panel.localScale = _baseScale;
        if (_group != null)
        {
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }
    }

    // 팝업을 켠 직후에 부른다.
    public Tween PlayOpen()
    {
        KillTweens();
        _panel.localScale = _baseScale * _openStartScale;
        _scaleTween = _panel
            .DOScale(_baseScale, _openDuration)
            .SetEase(Ease.OutBack, _overshoot)
            .SetUpdate(true);

        if (_group != null)
        {
            _group.alpha = 0f;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            _fadeTween = _group.DOFade(1f, _openDuration * 0.6f).SetUpdate(true);
        }

        return _scaleTween;
    }

    // 닫기 시작과 함께 부른다. 돌려받은 트윈이 끝나면 호출한 쪽이 팝업을 끈다.
    // 입력은 닫기 시작과 동시에 막아, 사라지는 동안 뒤 화면의 눌림을 가로채지 않는다.
    public Tween PlayClose()
    {
        KillTweens();
        if (_group != null)
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _fadeTween = _group.DOFade(0f, _closeDuration).SetUpdate(true);
        }

        _scaleTween = _panel
            .DOScale(_baseScale * _closeEndScale, _closeDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);
        return _scaleTween;
    }

    private void KillTweens()
    {
        _scaleTween?.Kill();
        _fadeTween?.Kill();
        _scaleTween = null;
        _fadeTween = null;
    }
}
