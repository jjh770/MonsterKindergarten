using UnityEngine;
using UnityEngine.UI;

// UI Image에 슬라임 외곽선 셰이더를 입힌다(도감).
//
// 셰이더는 그림이 아틀라스 안 어느 사각형인지(_SpriteUV)를 알아야 이웃 조각을 섞지 않는다.
// SpriteRenderer는 MaterialPropertyBlock으로 그 값을 넘기지만 UI는 렌더러별 블록이 없어서
// 이미지마다 재질 복제본을 하나씩 가진다. 도감에서 그림이 보이는 곳은 항목 몇 개와 상세
// 하나뿐이라 복제본 수가 적다.
//
// 외곽선이 없어야 하는 그림(아직 등록하지 않은 실루엣)은 재질을 비워 기본 UI 재질로 돌린다.
[RequireComponent(typeof(Image))]
public sealed class SlimeOutlineImage : MonoBehaviour
{
    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int ArtRectId = Shader.PropertyToID("_ArtRect");
    private static readonly int SpecialId = Shader.PropertyToID("_Special");
    private static readonly Vector4 FullArtRect = new Vector4(0f, 0f, 1f, 1f);

    [Tooltip("UI용 외곽선 재질입니다. 이미지마다 이것을 복제해 씁니다.")]
    [SerializeField] private Material _template;
    [SerializeField] private SlimeArtBounds _artBounds;

    private Image _image;
    private Material _instance;

    // sprite를 그리고, outlined이면 외곽선을 입힌다. special이면 몸의 광택과 외곽선이 무지개다.
    public void Apply(Sprite sprite, bool outlined, bool special)
    {
        if (_image == null) _image = GetComponent<Image>();

        _image.sprite = sprite;
        if (!outlined || sprite == null || _template == null)
        {
            _image.material = null;
            return;
        }

        if (_instance == null)
        {
            _instance = new Material(_template)
            {
                name = _template.name + " (Instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        _instance.SetVector(SpriteUvId, new Vector4(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height));
        _instance.SetVector(
            ArtRectId,
            _artBounds != null ? _artBounds.GetArtRect(sprite) : FullArtRect);
        _instance.SetFloat(SpecialId, special ? 1f : 0f);
        _image.material = _instance;
    }

    private void OnDestroy()
    {
        if (_instance != null) Destroy(_instance);
    }
}
