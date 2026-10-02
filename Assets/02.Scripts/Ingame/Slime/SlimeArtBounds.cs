using System;
using System.Collections.Generic;
using UnityEngine;

// 슬라임 그림이 스프라이트 사각형의 어디에 있는지 담아 둔 표다. 값은 0~1이고 사각형의
// 왼쪽 아래가 원점이다.
//
// 아웃라인 셰이더는 그림이 없는 픽셀마다 주변을 여러 번 읽어 그림이 가까이 있는지
// 확인한다. 사각형은 그림보다 훨씬 커서 그 확인이 대부분 헛수고다. 그림이 있는 영역과
// 아웃라인 반지름 밖의 픽셀은 확인 없이 건너뛰게 이 표를 셰이더에 넘긴다.
//
// 값은 애니메이션 프레임과 정지 그림의 알파에서 계산한다. 그림을 바꾸면 메뉴
// Tools/Slime/Rebuild Art Bounds로 다시 만들어야 한다. 표에 없는 스프라이트는 사각형
// 전체를 그림으로 보므로 틀린 그림이 나오지는 않고 느려지기만 한다.
public sealed class SlimeArtBounds : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public Sprite Sprite;
        public Vector2 Min;
        public Vector2 Max;
    }

    [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

    private static readonly Vector4 FullArtRect = new Vector4(0f, 0f, 1f, 1f);

    // 스프라이트로 바로 찾기 위한 사전이다. 표가 바뀌면 다시 만든다.
    [NonSerialized] private Dictionary<Sprite, Vector4> _lookup;

    public Entry[] Entries => _entries;

    // 그림이 사각형의 어디에 있는지(왼쪽 아래 xy, 오른쪽 위 zw)를 돌려준다. 표에 없거나 값을
    // 믿을 수 없으면 사각형 전체를 돌려준다. 그 경우 틀린 그림이 나오지는 않고 느려지기만 한다.
    public Vector4 GetArtRect(Sprite sprite)
    {
        if (sprite == null) return FullArtRect;

        // 표의 값은 사각형이 스프라이트의 전부일 때만 맞다. 그림 모양으로 잘린(Tight)
        // 스프라이트는 사각형이 다르므로 전체로 둔다.
        Rect rect = sprite.textureRect;
        if (!Mathf.Approximately(rect.width, sprite.rect.width) ||
            !Mathf.Approximately(rect.height, sprite.rect.height))
        {
            return FullArtRect;
        }

        if (_lookup == null)
        {
            _lookup = new Dictionary<Sprite, Vector4>();
            foreach (Entry entry in _entries)
            {
                if (entry.Sprite == null) continue;

                _lookup[entry.Sprite] =
                    new Vector4(entry.Min.x, entry.Min.y, entry.Max.x, entry.Max.y);
            }
        }

        return _lookup.TryGetValue(sprite, out Vector4 art) ? art : FullArtRect;
    }

    private void OnValidate()
    {
        _lookup = null;
    }

    public void SetEntries(Entry[] entries)
    {
        _entries = entries ?? Array.Empty<Entry>();
        _lookup = null;
    }
}
