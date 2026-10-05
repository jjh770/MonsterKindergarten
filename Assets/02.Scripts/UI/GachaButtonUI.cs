using System;
using UnityEngine;

// 뽑기권을 쓰는 버튼 하나만 담당한다. 실행 순서는 GachaService가 정한다.
//
// 언제 보일지는 GachaHudVisibility가 정한다. 여기서는 개수 표기와 클릭만 다룬다.
//
// 티켓이 없어도 버튼을 흐리게 두지 않는다. 흐린 버튼은 왜 못 쓰는지 알려주지 않고,
// 뽑기권이라는 것이 있다는 사실 자체가 아직 낯선 시점이라 안내가 필요하다.
//
// PullSucceeded는 뽑은 순간이 아니라 연출이 끝난 뒤에 발화한다. 튜토리얼이 이 신호를
// 받아 결과 슬라임을 가리키는데, 연출 도중에는 그 슬라임이 숨겨져 있기 때문이다.
public sealed class GachaButtonUI : MonoBehaviour
{
    private static string NoTicketMessage => UiMessages.NoTicket;

    private static string NoRoomMessage => UiMessages.NoRoomForPull;

    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private ToastMessageUI _toast;

    [Tooltip("비워 두면 연출 없이 결과가 바로 필드에 나타납니다.")]
    [SerializeField] private GachaResultDirector _resultDirector;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action<SlimeController> PullSucceeded;

    private void Awake()
    {
        if (_button == null || _toast == null)
        {
            Debug.LogError("뽑기 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

    }

    private void OnButtonClicked()
    {
        // 결과 연출이 필드를 가리고 있는 동안 두 번째 요청이 들어오면 새 슬라임과
        // 티켓 소비만 발생하고 연출은 거절된다. 서비스 호출 전에 막는다.
        if (_resultDirector != null && _resultDirector.IsPlaying) return;

        // 뽑기 튜토리얼이 끝나기 전의 첫 뽑기는 튜토리얼이 준 무료 한 장이다.
        EGachaFailure failure = GachaService.TryPull(
            out SlimeController spawned,
            out EGachaRarity rarity,
            isTutorialPull: !TutorialProgress.IsCompleted(TutorialIds.Gacha));

        if (failure == EGachaFailure.None && spawned != null)
        {
            if (_resultDirector != null)
            {
                _button.interactable = false;
                _resultDirector.Play(spawned, rarity, () =>
                {
                    if (_button != null) _button.interactable = true;
                    PullSucceeded?.Invoke(spawned);
                });
            }
            else
            {
                PullSucceeded?.Invoke(spawned);
            }
        }

        switch (failure)
        {
            case EGachaFailure.NoTicket:
                _toast.Show(NoTicketMessage);
                break;
            case EGachaFailure.NoRoom:
                _toast.Show(NoRoomMessage);
                break;
        }
    }

}
