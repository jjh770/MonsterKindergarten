using DG.Tweening;
using UnityEngine;

// 소품이 슬라임 한 마리를 잠시 붙잡아 연출하는 동안의 잠금과 되돌리기를 맡는다.
//
// 대포가 쓰는 규칙을 그대로 따른다. 잠그면 콜라이더와 물리가 꺼져 터치도 드래그도 닿지 않고,
// 다른 소품도 IsPlaygroundTarget으로 이 슬라임을 건드리지 않는다. 그림을 숨겼다면 되돌릴 때
// 그림을 먼저 켜고 잠금을 푼다. 순서가 바뀌면 보이지 않거나 만질 수 없는 슬라임이 남는다.
//
// 되돌리기는 여러 번 불러도 안전하다. 소품이 꺼지거나(공간 전환, 소품 놓기) 연출이 끊기는
// 모든 길에서 부르면 된다.
public sealed class PlaygroundSlimeHold
{
    private Vector3 _baseScale;
    private bool _isHidden;

    public SlimeController Slime { get; private set; }
    public bool IsHolding => Slime != null;
    public Vector3 BaseScale => _baseScale;

    // 붙잡은 슬라임이 풀로 돌아갔거나 장식장을 떠났다. 연출을 이어 갈 수 없다.
    public bool IsLost => Slime != null &&
                          (!Slime.isActiveAndEnabled || Slime.Location != ESlimeLocation.DisplayRoom);

    public bool TryBegin(SlimeController slime)
    {
        if (slime == null || Slime != null || !slime.IsPlaygroundTarget) return false;

        Slime = slime;
        // 잠금이 돌고 있던 펀치를 원래 크기로 정리하므로 크기는 잠근 뒤에 읽는다. 방향을 돌리던
        // 회전은 끝까지 마치게 해야 옆으로 선 채 멈추지 않는다.
        slime.SetPresentationLocked(true);
        slime.transform.DOKill(complete: true);
        _baseScale = slime.transform.localScale;
        return true;
    }

    public void Hide()
    {
        if (Slime == null) return;

        Slime.SetPresentationVisualHidden(true);
        _isHidden = true;
    }

    public void Show()
    {
        if (Slime == null || !_isHidden) return;

        Slime.SetPresentationVisualHidden(false);
        _isHidden = false;
    }

    public void Release()
    {
        SlimeController slime = Slime;
        Slime = null;
        if (slime == null) return;

        if (_isHidden)
        {
            slime.SetPresentationVisualHidden(false);
            _isHidden = false;
        }

        slime.transform.DOKill(complete: true);
        slime.transform.localScale = _baseScale;
        slime.SetPresentationLocked(false);
    }
}
