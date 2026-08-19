using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameExitPopupUI : MonoBehaviour
{
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _exitButton;

    public bool IsVisible => gameObject.activeSelf;

    public event Action CancelRequested;
    public event Action ExitRequested;

    private void Awake()
    {
        if (_cancelButton == null || _exitButton == null)
        {
            Debug.LogError("게임 종료 팝업의 버튼 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _cancelButton.onClick.AddListener(OnCancelClicked);
        _exitButton.onClick.AddListener(OnExitClicked);
    }

    private void OnDestroy()
    {
        _cancelButton?.onClick.RemoveListener(OnCancelClicked);
        _exitButton?.onClick.RemoveListener(OnExitClicked);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _cancelButton.Select();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnCancelClicked()
    {
        CancelRequested?.Invoke();
    }

    private void OnExitClicked()
    {
        ExitRequested?.Invoke();
    }
}
