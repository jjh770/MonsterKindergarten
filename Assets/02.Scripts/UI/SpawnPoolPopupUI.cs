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

        // 팝업 높이를 글자에 맞추려면 글자 크기가 고정이어야 한다.
        // 자동 크기 조절이 켜져 있으면 높이와 글자 크기가 서로를 따라가며 흔들린다.
        // 씬에서도 꺼두지만 이 컴포넌트가 성립하는 전제라 코드에서 보장한다.
        _probabilityText.enableAutoSizing = false;
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

        // Awake가 첫 활성화까지 밀리므로 크기를 재기 전에 켠다.
        gameObject.SetActive(true);
        ResizeToText();
        transform.SetAsLastSibling();
    }

    // 줄 수에 상수를 곱해 높이를 어림잡으면 글자가 팝업 밖으로 나간다.
    // 스프라이트가 섞인 줄은 글자만 있는 줄보다 높아서 줄당 높이가 일정하지 않다.
    // 그래서 한 번 배치해 본 뒤 첫 줄 위쪽 끝과 마지막 줄 아래쪽 끝의 간격을 그대로 쓴다.
    // 피벗이 위쪽이라 높이를 키우면 아래로만 늘어난다.
    private void ResizeToText()
    {
        _probabilityText.ForceMeshUpdate();

        TMP_TextInfo textInfo = _probabilityText.textInfo;
        if (textInfo == null || textInfo.lineCount == 0) return;

        float top = textInfo.lineInfo[0].ascender;
        float bottom = textInfo.lineInfo[textInfo.lineCount - 1].descender;

        // 위아래 여백은 글자 영역의 인셋을 그대로 따른다.
        RectTransform textRect = _probabilityText.rectTransform;
        float verticalPadding = textRect.offsetMin.y - textRect.offsetMax.y;

        _panel.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            top - bottom + verticalPadding);
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
