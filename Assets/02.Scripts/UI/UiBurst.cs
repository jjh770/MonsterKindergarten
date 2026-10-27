using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 한 번의 반짝이 터짐을 어떻게 보일지. 조각과 가운데 빛의 그림, 색, 크기와 퍼지는 정도를 담는다.
// 빛의 그림이 없으면 조각만 터진다.
public struct UiBurstStyle
{
    public Sprite PieceSprite;
    public Color PieceColor;
    public Vector2 PieceSize;
    public int Count;
    public float Radius;
    public float Duration;
    // 조각이 커져 닿는 크기의 범위(배율).
    public Vector2 ScaleRange;
    public bool Rotate;

    public Sprite FlashSprite;
    public Color FlashColor;
    public Vector2 FlashSize;
    public float FlashScale;
}

// 오버레이 UI 위에서 한 점을 중심으로 조각이 사방으로 터지고 가운데에서 빛이 번지는 공용 연출이다.
// 축하 반짝이(UiSparkleBurst)와 상단 바로 날아가는 그림의 출발·도착(UiIconFlight)이 같은 움직임을
// 쓴다. 어느 캔버스에 그릴지는 부르는 쪽의 풀이 정하므로, 각자 자기 레이어의 풀을 넘긴다.
//
// 오버레이 캔버스는 파티클을 그리지 않으므로 Image를 DOTween으로 움직인다. 조각과 빛은 모두 풀에서
// 빌리고, 터짐이 끝나거나 도중에 끊기면(Kill) 돌려준다. 여러 번이 동시에 터져도 서로 끊지 않는다.
public static class UiBurst
{
    // center는 풀의 부모 RectTransform 안의 좌표다. 조각의 앵커와 부모의 피벗이 가운데여야 한다.
    public static Sequence Play(UiImagePool pool, Vector2 center, UiBurstStyle style, GameObject link)
    {
        if (pool == null || style.PieceSprite == null) return null;

        int count = Mathf.Max(1, style.Count);
        bool hasFlash = style.FlashSprite != null;
        var images = new Image[count + (hasFlash ? 1 : 0)];
        float duration = Mathf.Max(0.05f, style.Duration);
        Sequence sequence = DOTween.Sequence();

        if (hasFlash)
        {
            Image flash = pool.Rent();
            images[count] = flash;
            RectTransform rect = flash.rectTransform;
            flash.sprite = style.FlashSprite;
            flash.color = style.FlashColor;
            flash.preserveAspect = false;
            flash.raycastTarget = false;
            rect.sizeDelta = style.FlashSize;
            rect.anchoredPosition = center;
            rect.localScale = Vector3.one * .3f;
            sequence.Insert(0f, rect.DOScale(Vector3.one * style.FlashScale, duration * .6f).SetEase(Ease.OutCubic));
            sequence.Insert(duration * .15f, flash.DOFade(0f, duration * .55f).SetEase(Ease.InQuad));
        }

        for (int i = 0; i < count; i++)
        {
            Image piece = pool.Rent();
            images[i] = piece;
            RectTransform rect = piece.rectTransform;
            piece.sprite = style.PieceSprite;
            piece.color = style.PieceColor;
            piece.preserveAspect = false;
            piece.raycastTarget = false;
            rect.sizeDelta = style.PieceSize;
            rect.anchoredPosition = center;
            rect.localScale = Vector3.zero;

            float angle = (i + Random.Range(-.35f, .35f)) * Mathf.PI * 2f / count;
            float distance = style.Radius * Random.Range(.55f, 1f);
            Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            float scale = Random.Range(style.ScaleRange.x, style.ScaleRange.y);
            float delay = Random.Range(0f, .08f);

            sequence.Insert(delay, rect.DOAnchorPos(target, duration).SetEase(Ease.OutCubic));
            sequence.Insert(delay, rect.DOScale(Vector3.one * scale, duration * .3f).SetEase(Ease.OutBack));
            if (style.Rotate)
            {
                sequence.Insert(delay, rect.DOLocalRotate(
                    new Vector3(0f, 0f, Random.Range(-200f, 200f)), duration, RotateMode.FastBeyond360));
            }

            sequence.Insert(delay + duration * .55f, piece.DOFade(0f, duration * .45f));
        }

        // 끝까지 돌았을 때도, 도중에 끊겼을 때도 OnKill이 불린다. 조각을 한 번에 돌려준다.
        sequence.OnKill(() =>
        {
            foreach (Image image in images)
            {
                pool.Return(image);
            }
        });
        if (link != null) sequence.SetLink(link, LinkBehaviour.KillOnDestroy);
        return sequence;
    }
}
