using UnityEngine;

// 슬라임 외곽선 셰이더에 SpriteRenderer마다 다른 값을 넣는다.
//
// _SpriteUV: 지금 그리는 스프라이트가 텍스처(아틀라스) 안에서 차지하는 사각형이다.
// 애니메이션이 프레임마다 스프라이트를 바꾸므로 바뀐 것을 알아챌 때마다 다시 넣는다.
// 이 값이 없으면 셰이더가 아틀라스 전체를 한 스프라이트로 착각해 외곽선이 어긋난다.
//
// _Special: 특별한 슬라임인지. 스폰과 합성 때 다시 읽는다. 합성 결과는 언제나 일반이라
// 특별한 슬라임이 합성되면 이 값이 0으로 돌아온다.
//
// MaterialPropertyBlock은 렌더러마다 하나를 재사용한다. 값을 넣을 때마다 새로 만들면
// 프레임마다 쓰레기가 생긴다.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SlimeSpriteMaterialDriver : MonoBehaviour
{
    private static readonly int SpriteUvId = Shader.PropertyToID("_SpriteUV");
    private static readonly int SpecialId = Shader.PropertyToID("_Special");

    private SpriteRenderer _renderer;
    private SlimeController _slime;
    private MaterialPropertyBlock _block;
    private Sprite _appliedSprite;
    private bool _isDirty = true;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        _isDirty = true;
        if (_slime == null) return;

        _slime.OnSpawned += MarkDirty;
        _slime.OnPromoted += MarkDirty;
    }

    private void OnDisable()
    {
        if (_slime == null) return;

        _slime.OnSpawned -= MarkDirty;
        _slime.OnPromoted -= MarkDirty;
    }

    // 애니메이터는 Update 뒤에 스프라이트를 바꾸므로 그 결과를 그리기 직전에 읽는다.
    private void LateUpdate()
    {
        if (_renderer.sprite != _appliedSprite || _isDirty)
        {
            Apply();
        }
    }

    // 스프라이트를 코드로 바꾼 직후처럼 다음 LateUpdate를 기다릴 수 없을 때 부른다.
    public void Apply()
    {
        CacheComponents();
        Sprite sprite = _renderer.sprite;
        _appliedSprite = sprite;
        _isDirty = false;
        if (sprite == null) return;

        _block ??= new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(_block);

        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        _block.SetVector(SpriteUvId, new Vector4(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height));
        _block.SetFloat(SpecialId, _slime != null && _slime.IsSpecial ? 1f : 0f);
        _renderer.SetPropertyBlock(_block);
    }

    private void MarkDirty()
    {
        _isDirty = true;
    }

    // Awake 없이 Apply가 먼저 불리는 경우(에디터 미리보기)에도 참조가 비지 않게 한다.
    private void CacheComponents()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_slime == null) _slime = GetComponent<SlimeController>();
    }
}
