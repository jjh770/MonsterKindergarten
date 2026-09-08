using UnityEngine;

// 가챠 해금과 함께 열리는 버튼들의 노출을 한곳에서 정한다.
//
// 조건은 하단 메뉴 전환 버튼과 같다. 데이터가 다 올라오고, 게임플레이가 살아 있고,
// 메인 스테이지를 보고 있고, 가챠가 해금됐을 때만 보인다. 장식장에서 숨기는 이유는
// 가챠 결과가 메인 스테이지에 태어나기 때문이다. 보이지 않는 곳에 슬라임을 만들고
// 티켓만 줄어드는 것처럼 보인다.
//
// 전송 모드는 여기서 보지 않는다. HudVisibility가 BottomHudRoot를 통째로 치우므로
// 그 자식인 버튼들도 함께 사라진다.
//
// 버튼이 스스로 숨지 않고 여기서 대신 끈다. 꺼진 오브젝트는 Update가 돌지 않아
// 스스로 다시 켜질 수 없어서, 자기를 끄는 컴포넌트는 한 번 꺼지면 영영 못 돌아온다.
// 이 컴포넌트는 항상 켜져 있는 오브젝트에 붙는다.
//
// 이벤트가 아니라 상태를 본다. 조건이 서로 다른 네 곳에서 오고 그중 둘은 꺼지는
// 쪽 이벤트가 없어서, 구독으로 맞추면 빠뜨린 경로가 곧 사라지지 않는 버튼이 된다.
public sealed class GachaHudVisibility : MonoBehaviour
{
    [Tooltip("가챠 해금과 함께 나타날 오브젝트들입니다.")]
    [SerializeField] private GameObject[] _roots;

    private bool _isVisible = true;

    private void Awake()
    {
        if (_roots == null || _roots.Length == 0)
        {
            Debug.LogError("가챠 UI 노출 대상이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        // 판단할 근거가 아직 없다. 켜 두었다가 감추면 깜빡인다.
        Apply(false);
    }

    private void Update()
    {
        Apply(IsAvailable());
    }

    private static bool IsAvailable()
    {
        GameManager gameManager = GameManager.Instance;
        StageManager stageManager = StageManager.Instance;
        SlimeManager slimeManager = SlimeManager.Instance;

        return gameManager != null &&
               gameManager.IsAllDataInitialized &&
               gameManager.IsGameplayActive &&
               stageManager != null &&
               stageManager.IsMainStageActive &&
               slimeManager != null &&
               slimeManager.IsGachaUnlocked;
    }

    private void Apply(bool isVisible)
    {
        if (isVisible == _isVisible) return;

        _isVisible = isVisible;
        foreach (GameObject root in _roots)
        {
            if (root == null) continue;

            root.SetActive(isVisible);
        }
    }
}
