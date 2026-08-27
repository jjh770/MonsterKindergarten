using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 씬에 미리 배치한 옵션과 초기화 확인 UI의 입력·표시만 담당한다.
public sealed class OptionsUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Button _openButton;
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _panelGroup;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private TMP_Text _bgmValue;
    [SerializeField] private TMP_Text _sfxValue;
    [SerializeField] private Button _resetButton;
    [SerializeField] private GameObject _confirmationRoot;
    [SerializeField] private RectTransform _confirmationPanel;
    [SerializeField] private TMP_Text _confirmationMessage;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private TMP_Text _cancelLabel;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField] private Vector2 _buttonMargin = new(35f, 35f);
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;

    private bool _isOpen;
    private bool _isBusy;
    private bool _isClosing;
    private Tween _fadeTween;

    private void Start()
    {
        if (_canvas == null || _openButton == null || _panelRoot == null || _panel == null ||
            _panelGroup == null || _closeButton == null || _bgmSlider == null ||
            _sfxSlider == null || _bgmValue == null || _sfxValue == null ||
            _resetButton == null || _confirmationRoot == null || _confirmationPanel == null ||
            _confirmationMessage == null ||
            _confirmButton == null || _cancelButton == null || _confirmLabel == null ||
            _cancelLabel == null || _clicker == null || _gameExitManager == null)
        {
            Debug.LogError("옵션 UI의 필수 씬 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _panelRoot.SetActive(false);
        _confirmationRoot.SetActive(false);
        _openButton.onClick.AddListener(Open);
        _closeButton.onClick.AddListener(Close);
        _resetButton.onClick.AddListener(ShowResetConfirmation);
        _confirmButton.onClick.AddListener(ConfirmReset);
        _cancelButton.onClick.AddListener(CancelConfirmation);
        _bgmSlider.onValueChanged.AddListener(ChangeBgmVolume);
        _sfxSlider.onValueChanged.AddListener(ChangeSfxVolume);
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameplayActivated += RefreshAvailability;
        RefreshAvailability();
        RefreshLayout();
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _openButton?.onClick.RemoveListener(Open);
        _closeButton?.onClick.RemoveListener(Close);
        _resetButton?.onClick.RemoveListener(ShowResetConfirmation);
        _confirmButton?.onClick.RemoveListener(ConfirmReset);
        _cancelButton?.onClick.RemoveListener(CancelConfirmation);
        _bgmSlider?.onValueChanged.RemoveListener(ChangeBgmVolume);
        _sfxSlider?.onValueChanged.RemoveListener(ChangeSfxVolume);
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameplayActivated -= RefreshAvailability;
        if (_clicker != null) _clicker.ReleaseMode(this);
        if (_gameExitManager != null) _gameExitManager.UnregisterBackHandler(this);
    }

    private void OnRectTransformDimensionsChange() => RefreshLayout();
    private void OnApplicationFocus(bool focused)
    {
        if (focused) RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (_canvas == null || _openButton == null) return;
        SafeAreaInsets insets = SafeAreaUtility.GetInsets(_canvas.transform as RectTransform);
        if (_openButton.transform is RectTransform rect)
            rect.anchoredPosition = new Vector2(-insets.Right - _buttonMargin.x, -insets.Top - _buttonMargin.y);
    }

    private void RefreshAvailability()
    {
        _openButton.interactable = GameManager.Instance != null && GameManager.Instance.IsGameplayActive;
    }

    private void Open()
    {
        if (_isOpen || _isClosing || AudioManager.Instance == null ||
            GameManager.Instance == null || !GameManager.Instance.IsGameplayActive ||
            (StageManager.Instance != null && StageManager.Instance.IsTransitioning)) return;

        _isOpen = true;
        _panelRoot.SetActive(true);
        transform.SetAsLastSibling();
        _panelGroup.alpha = 0f;
        _panelGroup.interactable = true;
        _bgmSlider.SetValueWithoutNotify(AudioManager.Instance.BGMVolume);
        _sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
        _bgmValue.text = $"{Mathf.RoundToInt(_bgmSlider.value * 100f)}%";
        _sfxValue.text = $"{Mathf.RoundToInt(_sfxSlider.value * 100f)}%";
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryClose);
        _fadeTween = _panelGroup.DOFade(1f, _fadeDuration).SetUpdate(true);
    }

    private void Close() => TryClose();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isOpen || _isBusy || _isClosing) return;

        RectTransform activePanel = _confirmationRoot.activeSelf ? _confirmationPanel : _panel;
        if (RectTransformUtility.RectangleContainsScreenPoint(
                activePanel, eventData.position, eventData.pressEventCamera)) return;

        if (_confirmationRoot.activeSelf)
            CancelConfirmation();
        else
            TryClose();
    }

    private bool TryClose()
    {
        if (!_isOpen) return false;
        if (_isBusy || _isClosing) return true;
        if (_confirmationRoot.activeSelf)
        {
            CancelConfirmation();
            return true;
        }

        _isClosing = true;
        _panelGroup.interactable = false;
        AudioManager.Instance?.SaveVolumeSettings();
        _fadeTween?.Kill();
        _fadeTween = _panelGroup.DOFade(0f, _fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            _fadeTween = null;
            _panelRoot.SetActive(false);
            _isOpen = false;
            _isClosing = false;
            _clicker.ReleaseMode(this);
            _gameExitManager.UnregisterBackHandler(this);
        });
        return true;
    }

    private void ChangeBgmVolume(float value)
    {
        AudioManager.Instance?.SetBGMVolume(value);
        _bgmValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void ChangeSfxVolume(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
        _sfxValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private void ShowResetConfirmation()
    {
        if (_isBusy) return;
        _confirmationRoot.SetActive(true);
        _confirmationMessage.text = "현재 계정의 재화, 슬라임, 업그레이드와\n튜토리얼 진행도를 삭제합니다.\n\n이 기기와 클라우드의 진행도는 복구할 수 없어요.\n다른 기기의 로컬 저장은 지워지지 않으며,\n그 기기로 접속하면 이전 진행도가 복원될 수 있어요.\n\n계정과 음량 설정은 유지됩니다.";
        _confirmLabel.text = "초기화";
        _cancelLabel.text = "취소";
        _cancelButton.Select();
    }

    private void CancelConfirmation()
    {
        if (_isBusy) return;
        if (GameplaySaveGate.IsResetting ||
            GameDataResetService.HasPendingReset(AccountManager.Instance?.UserId))
        {
            ReturnToLogin();
            return;
        }
        _confirmationRoot.SetActive(false);
    }

    private void ConfirmReset()
    {
        if (!_isBusy) ResetProgressAsync().Forget();
    }

    private async UniTask ResetProgressAsync()
    {
        _isBusy = true;
        _confirmButton.interactable = false;
        _cancelButton.interactable = false;
        _closeButton.interactable = false;
        _confirmationMessage.text = "데이터를 초기화하고 있어요.\n인터넷 연결을 유지해 주세요.";
        try
        {
            await GameDataResetService.ResetAsync(AccountManager.Instance?.UserId);
            if (this == null) return;
            ReturnToLogin();
        }
        catch (Exception e)
        {
            // 비동기 저장 실패를 UI 경계에서 안내한다. 삭제 결과가 불명확하면 플레이로 복귀하지 않는다.
            Debug.LogWarning($"게임 데이터 초기화 실패: {e.Message}");
            if (this == null) return;
            bool pending = GameplaySaveGate.IsResetting ||
                           GameDataResetService.HasPendingReset(AccountManager.Instance?.UserId);
            _confirmationMessage.text = pending
                ? "초기화를 완료하지 못했어요.\n인터넷 연결을 확인하고 다시 시도해 주세요.\n\n진행도 보호를 위해 게임으로 돌아갈 수 없어요.\n로그인 화면에서도 초기화를 다시 시도할 수 있어요."
                : "초기화를 시작하지 못했어요.\n인터넷 연결과 로그인 상태를 확인해 주세요.\n진행도는 삭제되지 않았어요.";
            _confirmLabel.text = "다시 시도";
            _cancelLabel.text = pending ? "로그인 화면" : "취소";
            _isBusy = false;
            _confirmButton.interactable = true;
            _cancelButton.interactable = true;
            _closeButton.interactable = true;
        }
    }

    private void ReturnToLogin()
    {
        AudioManager.Instance?.SaveVolumeSettings();
        AccountManager.Instance?.Logout();
        if (SceneManagerEx.Instance != null)
            SceneManagerEx.Instance.LoadLoginScene(skipAutomaticLogin: true);
        else
        {
            LobbyScene.SkipNextAutomaticLogin = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
        }
    }
}
