using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// SlimeArtBounds 표를 만든다. 슬라임 프리팹의 애니메이션 프레임과 스펙 표의 정지
// 그림을 모아, 원본 PNG의 알파에서 그림이 차지하는 사각형을 계산한다.
public static class SlimeArtBoundsBuilder
{
    private const string PrefabPath = "Assets/03.Prefabs/Slime.prefab";
    private const string SpecTablePath =
        "Assets/02.Scripts/Outgame/Feature/Slime/2.Domain/SlimeSpecTable.asset";
    private const string AssetPath = "Assets/10.ScriptableObjects/SlimeArtBounds.asset";

    // 셰이더의 AlphaCutoff(0.02)보다 조금 높은 값에서 그림으로 본다. 경계에서 바이리니어
    // 필터가 번지는 만큼 아래에서 두 텍셀을 더 넓힌다.
    private const byte AlphaThreshold = 6;
    private const int Expand = 2;

    [MenuItem("Tools/Slime/Rebuild Art Bounds")]
    public static void Rebuild()
    {
        var sprites = CollectSprites();
        var entries = new List<SlimeArtBounds.Entry>(sprites.Count);
        foreach (Sprite sprite in sprites)
        {
            if (TryComputeBounds(sprite, out Vector2 min, out Vector2 max))
            {
                entries.Add(new SlimeArtBounds.Entry { Sprite = sprite, Min = min, Max = max });
            }
        }

        var asset = AssetDatabase.LoadAssetAtPath<SlimeArtBounds>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<SlimeArtBounds>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        asset.SetEntries(entries.ToArray());
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"슬라임 그림 영역 표를 만들었습니다. : {entries.Count}개 / 스프라이트 {sprites.Count}개");
    }

    private static List<Sprite> CollectSprites()
    {
        var result = new List<Sprite>();
        var seen = new HashSet<Sprite>();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var animator = prefab != null ? prefab.GetComponent<SlimeAnimator>() : null;
        if (animator != null)
        {
            var controllers = new SerializedObject(animator).FindProperty("_levelAnimators");
            for (int i = 0; i < controllers.arraySize; i++)
            {
                var controller = controllers.GetArrayElementAtIndex(i).objectReferenceValue
                    as AnimatorOverrideController;
                if (controller == null) continue;

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                controller.GetOverrides(overrides);
                foreach (var pair in overrides)
                {
                    if (pair.Value == null) continue;

                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(pair.Value))
                    {
                        foreach (var key in AnimationUtility.GetObjectReferenceCurve(pair.Value, binding))
                        {
                            if (key.value is Sprite sprite && seen.Add(sprite)) result.Add(sprite);
                        }
                    }
                }
            }
        }

        var table = AssetDatabase.LoadAssetAtPath<SlimeSpecTable>(SpecTablePath);
        if (table != null)
        {
            foreach (var spec in table.slimeSpecs)
            {
                if (spec != null && spec.Sprite != null && seen.Add(spec.Sprite)) result.Add(spec.Sprite);
            }
        }

        return result;
    }

    private static bool TryComputeBounds(Sprite sprite, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.one;

        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path)) return false;

        // 표는 스프라이트가 원본 이미지 한 장 전체일 때만 맞다. 잘라 쓴 스프라이트는
        // 넣지 않고, 셰이더가 사각형 전체를 그림으로 보게 둔다.
        if (!Mathf.Approximately(sprite.rect.x, 0f) || !Mathf.Approximately(sprite.rect.y, 0f)) return false;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(path))) return false;
            if (!Mathf.Approximately(sprite.rect.width, texture.width) ||
                !Mathf.Approximately(sprite.rect.height, texture.height))
            {
                return false;
            }

            int width = texture.width;
            int height = texture.height;
            Color32[] pixels = texture.GetPixels32();
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a < AlphaThreshold) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0) return false;

            min = new Vector2(
                Mathf.Clamp01((minX - Expand) / (float)width),
                Mathf.Clamp01((minY - Expand) / (float)height));
            max = new Vector2(
                Mathf.Clamp01((maxX + 1 + Expand) / (float)width),
                Mathf.Clamp01((maxY + 1 + Expand) / (float)height));
            return true;
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
