using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 유치원과 장식장을 오가는 버튼 하나만 담당한다.
// 버튼은 지금 있는 공간이 아니라 갈 곳을 가리키므로 라벨이 공간마다 바뀐다.
//
// StageUI와 같은 이유로 버튼이 아니라 별도 오브젝트에 붙인다. 버튼이 속한
// 하단 메뉴 패널은 꺼진 채로 시작하므로, 버튼에 붙이면 Awake가 패널이 처음
// 열릴 때까지 밀려 참조 검증이 늦어진다.
public sealed class SpaceToggleButtonUI : MonoBehaviour
{
    private const string EnterDisplayRoomLabel = "장식장으로 이동";
    private const string ExitDisplayRoomLabel = "유치원으로 이동";

    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action Clicked;

    private void Awake()
    {
        if (_button == null || _label == null)
        {
            Debug.LogError("공간 이동 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (_button == null) return;

        _button.onClick.RemoveListener(OnButtonClicked);
    }

    public void SetSpace(bool isDisplayRoom)
    {
        if (_label == null) return;

        _label.text = isDisplayRoom
            ? ExitDisplayRoomLabel
            : EnterDisplayRoomLabel;
    }

    private void OnButtonClicked()
    {
        Clicked?.Invoke();
    }

}
