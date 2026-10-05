using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameExitPopupUI : MonoBehaviour
{
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private PopupMotion _motion;

    // 닫히는 연출 중에도 오브젝트는 켜져 있으므로, 보이는지는 연출과 따로 기억한다.
    private bool _isShown;

    public bool IsVisible => _isShown;

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
        _isShown = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _motion?.PlayOpen();
        _cancelButton.Select();
    }

    public void Hide()
    {
        bool wasShown = _isShown;
        _isShown = false;
        if (!gameObject.activeSelf)
        {
            return;
        }

        // 한 번도 열린 적 없는 채로 켜져 있던 팝업은 연출 없이 바로 끈다.
        if (!wasShown || _motion == null)
        {
            gameObject.SetActive(false);
            return;
        }

        _motion.PlayClose().OnComplete(() =>
        {
            // 닫히는 동안 다시 열렸으면 끄지 않는다.
            if (!_isShown)
            {
                gameObject.SetActive(false);
            }
        });
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
