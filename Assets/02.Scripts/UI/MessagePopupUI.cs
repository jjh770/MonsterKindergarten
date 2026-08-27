using DG.Tweening;
using TMPro;
using UnityEngine;

// 잠깐 떴다 사라지는 알림 팝업. 로비 오류와 포인트 부족 안내가 함께 쓴다.
//
// 문구가 고정된 팝업은 _messageText를 비워 두고 Show()를 호출한다.
// 씬 전환은 LoadScene 단일 모드라 이전 씬의 인스턴스가 먼저 파괴되므로,
// 씬마다 하나씩 두어도 Instance가 충돌하지 않는다.
public class MessagePopupUI : MonoBehaviour
{
    public static MessagePopupUI Instance { get; private set; }

    [SerializeField] private GameObject _popupPanel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _popupRectTransform;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private AudioClip _popupSound;

    [Header("Animation")]
    [SerializeField] private float _fadeInDuration = 0.2f;
    [SerializeField] private float _punchDuration = 0.5f;
    [SerializeField] private float _displayDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.2f;
    [SerializeField] private float _punchStrength = 15f;

    private Sequence _currentSequence;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_popupPanel == null ||
            _canvasGroup == null ||
            _popupRectTransform == null)
        {
            Debug.LogError("알림 팝업의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popupPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        _currentSequence?.Kill();
        _currentSequence = null;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show(string message = null)
    {
        // 이전 애니메이션 취소
        _currentSequence?.Kill();

        _popupPanel.SetActive(true);
        _canvasGroup.alpha = 0f;

        if (AudioManager.Instance != null && _popupSound != null)
        {
            AudioManager.Instance.PlaySFX(_popupSound);
        }

        if (_messageText != null && message != null)
        {
            _messageText.text = message;
        }

        _currentSequence = DOTween.Sequence();
        _currentSequence.Append(_canvasGroup.DOFade(1f, _fadeInDuration));
        _currentSequence.Join(_popupRectTransform.DOPunchPosition(
            Vector3.one * _punchStrength,
            _punchDuration,
            10,
            1));
        _currentSequence.AppendInterval(_displayDuration);
        _currentSequence.Append(_canvasGroup.DOFade(0f, _fadeOutDuration));
        _currentSequence.OnComplete(() =>
        {
            _currentSequence = null;
            _popupPanel.SetActive(false);
        });
    }
}
