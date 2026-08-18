using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialDialogueView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private Button _nextButton;

    public event Action NextRequested;

    private void Awake()
    {
        if (_dialogueText == null || _nextButton == null)
        {
            Debug.LogError("튜토리얼 대화창 프리팹의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _nextButton.onClick.AddListener(OnNextButtonClicked);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_nextButton != null)
        {
            _nextButton.onClick.RemoveListener(OnNextButtonClicked);
        }
    }

    public void Show(string speaker, string message)
    {
        _dialogueText.text = $"<color=#FFD12E><b>{speaker}</b></color>\n{message}";
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _nextButton.Select();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnNextButtonClicked()
    {
        NextRequested?.Invoke();
    }
}
