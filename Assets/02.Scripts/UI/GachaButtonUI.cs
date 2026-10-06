using System;
using System.Collections.Generic;
using UnityEngine;

// 뽑기 기계 화면을 여는 버튼 하나만 담당한다. 티켓을 쓰고 결과를 만드는 것은 기계 화면 안의 1회·5회
// 버튼이고, 그 순서는 GachaService가 정한다.
//
// 언제 보일지는 GachaHudVisibility가 정한다. 여기서는 클릭만 다룬다.
//
// 티켓이 없어도 버튼을 흐리게 두지 않고 기계 화면은 열린다. 흐린 버튼은 왜 못 쓰는지 알려주지 않고,
// 뽑기권이라는 것이 있다는 사실 자체가 아직 낯선 시점이라 안내가 필요하다. 기계 화면이 보유 장수를 알려 준다.
//
// MachineOpened는 화면이 열리는 순간, PullCommitted는 티켓이 쓰이고 결과가 저장된 순간, PullSucceeded는
// 연출이 끝나 화면이 닫힌 뒤에 발화한다. 튜토리얼은 연출 도중에 결과 슬라임이 숨겨져 있어서 마지막 신호를
// 받아 가리키고, 되돌릴 수 없는 순간은 두 번째 신호로 안다.
public sealed class GachaButtonUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private GachaResultDirector _resultDirector;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action MachineOpened;
    public event Action PullCommitted;
    public event Action<SlimeController> PullSucceeded;

    private void Awake()
    {
        if (_button == null || _resultDirector == null)
        {
            Debug.LogError("뽑기 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
        _resultDirector.PullCommitted += OnPullCommitted;
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        if (_resultDirector != null)
        {
            _resultDirector.PullCommitted -= OnPullCommitted;
        }
    }

    private void OnButtonClicked()
    {
        // 화면이 열려 있는 동안 두 번째 요청이 들어오면 겹쳐 열린다. 연출 중이면 막는다.
        if (_resultDirector.IsPlaying) return;

        _button.interactable = false;
        MachineOpened?.Invoke();
        _resultDirector.Open(OnSessionFinished);
    }

    private void OnPullCommitted()
    {
        PullCommitted?.Invoke();
    }

    private void OnSessionFinished(IReadOnlyList<SlimeController> pulled)
    {
        if (_button != null) _button.interactable = true;

        if (pulled != null && pulled.Count > 0)
        {
            PullSucceeded?.Invoke(pulled[0]);
        }
    }
}
