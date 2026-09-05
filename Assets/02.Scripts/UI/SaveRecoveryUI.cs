using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 저장된 진행도를 읽지 못해 게임에 들어갈 수 없을 때 띄우는 확인 패널.
//
// 표시와 입력 상태만 소유한다. 어떤 실패에 이 패널을 열지, 확인 뒤 무엇을 할지,
// 어떤 문구를 보여줄지는 로그인 흐름을 아는 LobbyScene이 정한다.
public sealed class SaveRecoveryUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    public event Action ConfirmRequested;

    // 참조가 하나라도 비면 호출부가 기존 안내로 대체할 수 있어야 한다.
    public bool IsReady =>
        _root != null &&
        _messageText != null &&
        _confirmButton != null &&
        _cancelButton != null;

    private void Awake()
    {
        if (!IsReady)
        {
            Debug.LogError("복구 확인 패널의 참조가 비어 있습니다.", this);
            return;
        }

        _confirmButton.onClick.AddListener(OnConfirmClicked);
        _cancelButton.onClick.AddListener(Hide);
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Hide);
    }

    public void Show(string message)
    {
        if (!IsReady) return;

        SetMessage(message);
        SetInteractable(true);
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    public void SetMessage(string message)
    {
        if (_messageText != null) _messageText.text = message;
    }

    // 진행 중에는 확인과 취소를 함께 잠근다.
    public void SetInteractable(bool isInteractable)
    {
        if (_confirmButton != null) _confirmButton.interactable = isInteractable;
        if (_cancelButton != null) _cancelButton.interactable = isInteractable;
    }

    private void OnConfirmClicked()
    {
        ConfirmRequested?.Invoke();
    }
}
