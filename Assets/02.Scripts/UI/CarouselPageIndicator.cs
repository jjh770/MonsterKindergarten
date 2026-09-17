using UnityEngine;
using UnityEngine.UI;

// 캐러셀이 몇 장 중 몇 번째인지 점으로 보여 준다.
//
// 양옆 카드가 잘려 보이는 것만으로는 넘길 수 있다는 걸 확신하기 어렵다. 점이 있으면
// 장수와 현재 위치가 함께 드러난다.
//
// 점은 씬에 최대 장수만큼 만들어 두고 켜고 끈다. 항목 수는 해금에 따라 늘어나므로
// 런타임에 오브젝트를 만들지 않고 필요한 만큼만 보인다. 한 장뿐이면 넘길 곳이
// 없으니 점을 모두 감춘다.
public sealed class CarouselPageIndicator : MonoBehaviour
{
    [SerializeField] private SystemUpgradeCarousel _carousel;
    [SerializeField] private Image[] _dots;
    [SerializeField] private Color _selectedColor = Color.white;
    [SerializeField] private Color _unselectedColor = new Color(1f, 1f, 1f, 0.35f);

    private void Start()
    {
        if (_carousel == null || _dots == null || _dots.Length == 0)
        {
            Debug.LogError("캐러셀 페이지 표시의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _carousel.SelectionChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_carousel != null)
        {
            _carousel.SelectionChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        int count = _carousel.DataCount;
        bool hasPages = count > 1;

        for (int i = 0; i < _dots.Length; i++)
        {
            Image dot = _dots[i];
            if (dot == null) continue;

            bool isVisible = hasPages && i < count;
            dot.gameObject.SetActive(isVisible);
            if (!isVisible) continue;

            dot.color = i == _carousel.SelectedIndex
                ? _selectedColor
                : _unselectedColor;
        }
    }
}
