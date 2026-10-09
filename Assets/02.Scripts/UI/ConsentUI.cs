using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 처음 시작할 때 보여 주는 동의 화면이다. 기획서 §21.11.
//
// 이용약관, 개인정보 수집·이용, 만 14세 이상 확인을 한 화면에서 받고, 셋 모두 체크해야 시작할 수 있다.
// 항목마다 전문 보기 링크가 있다. 모두 필수라 선택 항목은 없다.
//
// 표시와 입력만 맡는다. 동의를 기록하고 로그인으로 넘어가는 것은 이 화면을 연 LoginScene이 한다.
// 동의하지 않으면 시작할 수 없으므로 거절과 뒤로가기는 화면을 닫고 처음 상태로 돌려보낸다.
public sealed class ConsentUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private PopupMotion _motion;
    [SerializeField] private Toggle _termsToggle;
    [SerializeField] private Toggle _privacyToggle;
    [SerializeField] private Toggle _ageToggle;
    [SerializeField] private Button _termsLinkButton;
    [SerializeField] private Button _privacyLinkButton;
    [SerializeField] private Button _agreeAllButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _declineButton;
    [Tooltip("약관과 방침을 게임 안에서 보여 주는 팝업입니다. 읽지 못하는 약관에는 동의할 수 없으므로 필수입니다.")]
    [SerializeField] private LegalDocumentUI _legalViewer;

    private bool _isShown;

    // 세 항목에 모두 동의하고 시작을 눌렀다.
    public event Action Agreed;

    // 동의하지 않고 닫았다(거절 버튼 또는 뒤로가기).
    public event Action Declined;

    public bool IsReady =>
        _root != null &&
        _termsToggle != null &&
        _privacyToggle != null &&
        _ageToggle != null &&
        _termsLinkButton != null &&
        _privacyLinkButton != null &&
        _agreeAllButton != null &&
        _startButton != null &&
        _declineButton != null &&
        _legalViewer != null &&
        _legalViewer.IsReady;

    private void Awake()
    {
        if (!IsReady)
        {
            Debug.LogError("동의 화면의 참조가 비어 있습니다.", this);
            return;
        }

        _termsToggle.onValueChanged.AddListener(OnToggleChanged);
        _privacyToggle.onValueChanged.AddListener(OnToggleChanged);
        _ageToggle.onValueChanged.AddListener(OnToggleChanged);
        _termsLinkButton.onClick.AddListener(OpenTerms);
        _privacyLinkButton.onClick.AddListener(OpenPrivacy);
        _agreeAllButton.onClick.AddListener(AgreeAll);
        _startButton.onClick.AddListener(OnStartClicked);
        _declineButton.onClick.AddListener(Decline);
        _root.SetActive(false);
    }

    private void Update()
    {
        // Android의 뒤로가기는 Escape로 들어온다.
        // 약관을 보는 중의 뒤로가기는 그 팝업을 닫는 것이지 동의를 거절하는 것이 아니다.
        if (_isShown && !LegalDocumentUI.IsAnyOpen &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Decline();
        }
    }

    private void OnDestroy()
    {
        if (_termsToggle != null) _termsToggle.onValueChanged.RemoveListener(OnToggleChanged);
        if (_privacyToggle != null) _privacyToggle.onValueChanged.RemoveListener(OnToggleChanged);
        if (_ageToggle != null) _ageToggle.onValueChanged.RemoveListener(OnToggleChanged);
        if (_termsLinkButton != null) _termsLinkButton.onClick.RemoveListener(OpenTerms);
        if (_privacyLinkButton != null) _privacyLinkButton.onClick.RemoveListener(OpenPrivacy);
        if (_agreeAllButton != null) _agreeAllButton.onClick.RemoveListener(AgreeAll);
        if (_startButton != null) _startButton.onClick.RemoveListener(OnStartClicked);
        if (_declineButton != null) _declineButton.onClick.RemoveListener(Decline);
    }

    // 열 때마다 체크를 비운다. 거절하고 돌아왔다가 다시 열었을 때 이전 체크가 남아 있으면 읽지 않고 넘어가기 쉽다.
    public void Show()
    {
        if (!IsReady || _isShown) return;

        _termsToggle.SetIsOnWithoutNotify(false);
        _privacyToggle.SetIsOnWithoutNotify(false);
        _ageToggle.SetIsOnWithoutNotify(false);
        RefreshStartButton();
        _isShown = true;
        _root.SetActive(true);
        _motion?.PlayOpen();
    }

    public void Hide()
    {
        if (_root == null) return;

        bool wasShown = _isShown;
        _isShown = false;
        if (!_root.activeSelf) return;

        if (!wasShown || _motion == null)
        {
            _root.SetActive(false);
            return;
        }

        _motion.PlayClose().OnComplete(() =>
        {
            // 닫히는 동안 다시 열렸으면 끄지 않는다.
            if (!_isShown) _root.SetActive(false);
        });
    }

    private bool AreAllChecked()
    {
        return _termsToggle.isOn && _privacyToggle.isOn && _ageToggle.isOn;
    }

    private void OnToggleChanged(bool _)
    {
        RefreshStartButton();
    }

    private void RefreshStartButton()
    {
        _startButton.interactable = AreAllChecked();
    }

    private void AgreeAll()
    {
        _termsToggle.SetIsOnWithoutNotify(true);
        _privacyToggle.SetIsOnWithoutNotify(true);
        _ageToggle.SetIsOnWithoutNotify(true);
        RefreshStartButton();
    }

    private void OnStartClicked()
    {
        // 버튼이 꺼져 있어도 마지막으로 한 번 더 확인한다.
        if (!_isShown || !AreAllChecked()) return;

        Agreed?.Invoke();
    }

    private void Decline()
    {
        if (!_isShown) return;

        Declined?.Invoke();
    }

    private void OpenTerms()
    {
        _legalViewer.Show(ELegalDocument.Terms);
    }

    private void OpenPrivacy()
    {
        _legalViewer.Show(ELegalDocument.Privacy);
    }
}
