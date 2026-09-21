using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 시스템 업그레이드 현황 팝업. 카드는 한 번에 하나만 가운데에 보여서, 지금 무엇을
// 몇 레벨까지 올렸고 그 결과가 어떤지를 한눈에 볼 수 없다. 그걸 한 장에 모은다.
//
// 현재 버튼은 학자 안내로 대체되어 씬에서 비활성화되어 있다. 이전 UI를 당장 삭제하지
// 않고 되돌릴 수 있게 남겨 둔 호환 경로이며, 내용은 학자 안내와 같은 빌더를 쓴다.
public sealed class SystemUpgradeInfoUI : MonoBehaviour
{
    [SerializeField] private Button _openButton;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TextMeshProUGUI _infoText;
    [SerializeField] private Button _closeButton;

    private bool _isInitialized;

    private void Awake()
    {
        if (_openButton == null || _panel == null ||
            _infoText == null || _closeButton == null)
        {
            Debug.LogError("시스템 업그레이드 현황 팝업의 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 높이를 글자에 맞추려면 글자 크기가 고정이어야 한다.
        _infoText.enableAutoSizing = false;
        _openButton.onClick.AddListener(Toggle);
        _closeButton.onClick.AddListener(Close);
        _panel.gameObject.SetActive(false);
        _isInitialized = true;
    }

    private void OnEnable()
    {
        UpgradeManager.OnDataChanged += RefreshIfOpen;
    }

    private void OnDisable()
    {
        UpgradeManager.OnDataChanged -= RefreshIfOpen;
        // 패널이 이동 메뉴로 바뀌어 숨을 때 다시 돌아와도 열린 채로 남지 않게 한다.
        Close();
    }

    private void OnDestroy()
    {
        _openButton?.onClick.RemoveListener(Toggle);
        _closeButton?.onClick.RemoveListener(Close);
    }

    private void Toggle()
    {
        if (_panel.gameObject.activeSelf)
        {
            Close();
            return;
        }

        Open();
    }

    private void Open()
    {
        if (!_isInitialized) return;

        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        if (_panel != null)
        {
            _panel.gameObject.SetActive(false);
        }
    }

    private void RefreshIfOpen()
    {
        if (_panel != null && _panel.gameObject.activeSelf)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        _infoText.text = GameplayInfoTextBuilder.BuildSystemUpgradeText(
            includeCloseHint: true);
        ResizeToText();
    }

    // 자연 등장 확률 팝업과 같은 방식으로 한 번 배치해 본 뒤 실제 줄 높이로 맞춘다.
    // 피벗이 아래쪽이라 높이를 키우면 위로만 늘어난다.
    private void ResizeToText()
    {
        _infoText.ForceMeshUpdate();

        TMP_TextInfo textInfo = _infoText.textInfo;
        if (textInfo == null || textInfo.lineCount == 0) return;

        float top = textInfo.lineInfo[0].ascender;
        float bottom = textInfo.lineInfo[textInfo.lineCount - 1].descender;

        RectTransform textRect = _infoText.rectTransform;
        float verticalPadding = textRect.offsetMin.y - textRect.offsetMax.y;

        _panel.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            top - bottom + verticalPadding);
    }
}
