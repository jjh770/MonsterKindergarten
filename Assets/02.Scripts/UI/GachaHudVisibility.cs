using UnityEngine;
using UnityEngine.Serialization;

// 하단 기능 버튼들의 해금 및 노출을 한곳에서 정한다.
//
// 조건은 하단 메뉴 전환 버튼과 같다. 데이터가 다 올라오고, 게임플레이가 살아 있고,
// 메인 스테이지를 보고 있고, 가챠가 해금됐을 때만 보인다. 장식장에서 숨기는 이유는
// 가챠 결과가 메인 스테이지에 태어나기 때문이다. 보이지 않는 곳에 슬라임을 만들고
// 티켓만 줄어드는 것처럼 보인다.
//
// 전송 모드에서는 BottomPanelSwitcher가 하단 패널 영역을 숨긴다. 두 버튼도 같은
// 표시 상태를 따라야 메뉴 전환 버튼만 사라지고 양옆 버튼이 남는 일이 없다.
//
// 버튼이 스스로 숨지 않고 여기서 대신 끈다. 꺼진 오브젝트는 Update가 돌지 않아
// 스스로 다시 켜질 수 없어서, 자기를 끄는 컴포넌트는 한 번 꺼지면 영영 못 돌아온다.
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙는다.
//
// 이벤트가 아니라 상태를 본다. 조건이 서로 다른 네 곳에서 오고 그중 둘은 꺼지는
// 쪽 이벤트가 없어서, 구독으로 맞추면 빠뜨린 경로가 곧 사라지지 않는 버튼이 된다.
public sealed class GachaHudVisibility : MonoBehaviour
{
    [SerializeField] private BottomPanelSwitcher _panelSwitcher;

    [FormerlySerializedAs("_roots")]
    [Tooltip("가챠 해금과 함께 나타날 오브젝트들입니다.")]
    [SerializeField] private GameObject[] _gachaRoots;

    [Tooltip("자동 합성 해금과 함께 나타날 오브젝트들입니다.")]
    [SerializeField] private GameObject[] _autoMergeRoots;

    private bool _areGachaRootsVisible = true;
    private bool _areAutoMergeRootsVisible = true;

    private void Awake()
    {
        // 대상은 씬에서 연결한다. 비어 있을 때 이름이나 타입으로 찾아 메우면 연결이
        // 빠진 것을 아무도 모른 채 넘어가므로, 에러로 드러내고 멈춘다.
        if (_panelSwitcher == null || !HasRoot(_gachaRoots) || !HasRoot(_autoMergeRoots))
        {
            Debug.LogError("하단 기능 UI 노출 대상이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 판단할 근거가 아직 없다. 켜 두었다가 감추면 깜빡인다.
        Apply(_gachaRoots, false, ref _areGachaRootsVisible);
        Apply(_autoMergeRoots, false, ref _areAutoMergeRootsVisible);
    }

    private void Update()
    {
        SlimeManager slimeManager = SlimeManager.Instance;
        bool isBaseAvailable = GameplayGate.IsMainStageReady &&
                               _panelSwitcher.IsAreaVisible &&
                               slimeManager != null;

        bool isGachaAvailable = isBaseAvailable &&
                                slimeManager.IsGachaUnlocked &&
                                (TutorialProgress.IsCompleted(TutorialIds.Gacha) ||
                                 TutorialManager.IsActive(TutorialIds.Gacha));
        bool isAutoMergeAvailable = isBaseAvailable &&
                                    slimeManager.IsAutoMergeUnlocked;

        Apply(_gachaRoots, isGachaAvailable, ref _areGachaRootsVisible);
        Apply(_autoMergeRoots, isAutoMergeAvailable, ref _areAutoMergeRootsVisible);
    }

    private static void Apply(
        GameObject[] roots,
        bool isVisible,
        ref bool currentVisibility)
    {
        if (isVisible == currentVisibility) return;

        currentVisibility = isVisible;
        foreach (GameObject root in roots)
        {
            if (root == null) continue;

            root.SetActive(isVisible);
        }
    }

    private static bool HasRoot(GameObject[] roots)
    {
        if (roots == null) return false;

        foreach (GameObject root in roots)
        {
            if (root != null) return true;
        }

        return false;
    }
}
