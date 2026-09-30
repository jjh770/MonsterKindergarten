using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 씬에 둔 비활성 Image 원본을 복제해 돌려 쓰는 풀이다.
//
// 효과마다 new GameObject로 UI 계층을 즉석에서 만들면 프로젝트가 정한 규칙(정적 UI는 씬과
// 프리팹에 미리 둔다)에 어긋나고, 조각을 만들고 지우는 일이 효과가 터질 때마다 반복된다.
// 원본은 씬에 두고 여기서는 복제만 한다. 모자라면 늘어나므로 동시에 몇 개가 필요한지
// 미리 알 수 없어도 조각이 끊기지 않는다.
//
// 빌려 간 쪽이 스프라이트, 색, 크기, 위치를 모두 정해야 한다. 돌려받을 때 되돌리지 않으므로
// 이전에 쓰던 값이 남아 있다. 회전과 형제 순서(맨 앞)만 이쪽에서 초기화한다.
public sealed class UiImagePool
{
    private readonly Image _template;
    private readonly RectTransform _parent;
    private readonly Stack<Image> _free = new();

    public UiImagePool(Image template, RectTransform parent, int prewarmCount = 0)
    {
        _template = template;
        _parent = parent;

        _template.gameObject.SetActive(false);
        for (int i = 0; i < prewarmCount; i++)
        {
            Image image = Create();
            image.gameObject.SetActive(false);
            _free.Push(image);
        }
    }

    // 활성 상태의 조각을 맨 앞에 놓아 돌려준다.
    public Image Rent()
    {
        Image image = null;

        // 씬이 정리되는 중에 파괴된 조각이 섞여 있을 수 있어 건너뛴다.
        while (_free.Count > 0 && image == null)
        {
            image = _free.Pop();
        }

        if (image == null) image = Create();

        image.rectTransform.localRotation = Quaternion.identity;
        image.rectTransform.SetAsLastSibling();
        image.gameObject.SetActive(true);
        return image;
    }

    public void Return(Image image)
    {
        if (image == null) return;

        image.gameObject.SetActive(false);
        _free.Push(image);
    }

    private Image Create()
    {
        return Object.Instantiate(_template, _parent);
    }
}
