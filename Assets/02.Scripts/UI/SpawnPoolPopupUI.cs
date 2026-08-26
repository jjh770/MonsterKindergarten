using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SpawnPoolPopupUI : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TextMeshProUGUI _probabilityText;
    [SerializeField] private Button _closeButton;

    public bool IsOpen => gameObject.activeSelf;
    public RectTransform TutorialTarget => _panel;

    public event Action Closed;

    private void Awake()
    {
        if (_panel == null || _probabilityText == null || _closeButton == null)
        {
            Debug.LogError("자연 등장 확률 팝업의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        _closeButton?.onClick.RemoveListener(Close);
    }

    public void Show(IReadOnlyList<SpawnProbability> probabilities)
    {
        if (probabilities == null || _panel == null || _probabilityText == null)
        {
            return;
        }

        var builder = new StringBuilder("현재 자연 등장 확률\n");
        foreach (SpawnProbability probability in probabilities)
        {
            builder.Append("<sprite name=\"")
                .Append(((int)probability.Grade).ToString("00"))
                .Append("\"> Lv.")
                .Append((int)probability.Grade)
                .Append("   ")
                .Append((probability.Probability * 100f).ToString("F1"))
                .Append("%\n");
        }

        builder.Append("\n눌러서 닫기");
        _probabilityText.text = builder.ToString();
        _panel.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            150f + probabilities.Count * 58f);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Close()
    {
        Hide();
        Closed?.Invoke();
    }
}
