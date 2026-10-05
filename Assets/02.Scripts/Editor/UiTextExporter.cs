using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 화면에 보이는 문구를 한 파일로 뽑는다. 문구는 씬 라벨, 튜토리얼 에셋, 코드 상수에 나뉘어 있어서
// 말투를 점검하거나 용어를 바꿀 때 어디에 있는지 한 번에 보려는 도구다. 읽기 전용이라 에셋은 건드리지 않는다.
public static class UiTextExporter
{
    private static readonly string[] ScenePaths =
    {
        "Assets/01.Scenes/GameScene.unity",
        "Assets/01.Scenes/LoginScene.unity",
    };

    private const string UiMessagesPath = "Assets/10.ScriptableObjects/UiMessages.asset";
    private const string TutorialContentPath = "Assets/10.ScriptableObjects/TutorialContent.asset";
    private const string PrefabFolder = "Assets/03.Prefabs";
    private const string OutputPath = "Builds/UiTextReport.txt";

    [MenuItem("Tools/Text/Export UI Text Report")]
    public static void Export()
    {
        var builder = new StringBuilder();
        AppendMessages(builder);
        AppendTutorial(builder);
        AppendPrefabs(builder);
        AppendScenes(builder);

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, builder.ToString(), new UTF8Encoding(false));
        Debug.Log($"문구 목록을 저장했습니다. : {OutputPath}");
        EditorUtility.RevealInFinder(OutputPath);
    }

    // 코드가 띄우는 문장. UiMessages 에셋의 문자열 필드를 읽는다.
    private static void AppendMessages(StringBuilder builder)
    {
        builder.AppendLine("## 코드 문장 (UiMessages 에셋)");
        AppendStrings(builder, AssetDatabase.LoadAssetAtPath<UiMessagesSO>(UiMessagesPath));
        builder.AppendLine();
    }

    private static void AppendTutorial(StringBuilder builder)
    {
        builder.AppendLine("## 튜토리얼 (TutorialContent)");
        AppendStrings(builder, AssetDatabase.LoadAssetAtPath<TutorialContent>(TutorialContentPath));
        builder.AppendLine();
    }

    private static void AppendStrings(StringBuilder builder, Object asset)
    {
        if (asset == null) return;

        var serialized = new SerializedObject(asset);
        SerializedProperty property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                AppendLine(builder, property.propertyPath, property.stringValue);
            }
        }
    }

    private static void AppendPrefabs(StringBuilder builder)
    {
        builder.AppendLine("## 프리팹 라벨");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AppendTexts(builder, path, root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        builder.AppendLine();
    }

    // 열려 있는 씬을 바꾸지 않도록 이미 열려 있으면 그대로 읽고, 아니면 더해 열었다가 닫는다.
    private static void AppendScenes(StringBuilder builder)
    {
        foreach (string path in ScenePaths)
        {
            builder.AppendLine($"## 씬 라벨 ({Path.GetFileName(path)})");
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                AppendTexts(builder, null, root);
            }

            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            builder.AppendLine();
        }
    }

    private static void AppendTexts(StringBuilder builder, string container, GameObject root)
    {
        var lines = new List<string>();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (string.IsNullOrWhiteSpace(text.text)) continue;

            string label = container == null ? GetPath(text.transform) : $"{container} : {GetPath(text.transform)}";
            lines.Add(Format(label, text.text));
        }

        lines.Sort();
        foreach (string line in lines)
        {
            builder.AppendLine(line);
        }
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            path = $"{parent.name}/{path}";
        }

        return path;
    }

    private static void AppendLine(StringBuilder builder, string label, string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            builder.AppendLine(Format(label, text));
        }
    }

    private static string Format(string label, string text)
    {
        return $"{label} | {text.Replace("\r", string.Empty).Replace("\n", "\\n")}";
    }
}
