using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 광고 보너스 팝업의 한 줄이다. 제목은 씬에 쓰고, 보상 문구와 상태는 위에서 채운다.
public sealed class AdBonusItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button _watchButton;

    public Button WatchButton => _watchButton;

    public void Apply(string reward, string status, bool canWatch)
    {
        _rewardText.text = reward;
        _statusText.text = status;
        _watchButton.interactable = canWatch;
    }
}
