using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 화면 문구와 소리 에셋이 깨지지 않았는지 확인한다. 2026-10-19 QA에서 사람이 찾았던 결함을
// 다시 들어오지 못하게 막는 것이 목적이다.
//
// - 글꼴 아틀라스가 정적이라 아틀라스에 없는 글자(가운데점 ·)는 빈칸으로 그려진다.
// - 줄바꿈 앞뒤 공백, 두 칸 공백은 화면에서 들여쓰기처럼 보인다.
// - 뽑기 튜토리얼은 마지막 줄을 뽑기권 지급 대사로 쓰므로 두 줄 이상이어야 한다.
// - 소리 카탈로그의 빈 칸은 그 동작을 무음으로 만든다.
public class ContentAssetTests
{
    private const string TutorialContentPath = "Assets/10.ScriptableObjects/TutorialContent.asset";
    private const string UiMessagesPath = "Assets/10.ScriptableObjects/UiMessages.asset";
    private const string AudioCatalogPath = "Assets/10.ScriptableObjects/GameAudioCatalog.asset";
    private const string GameScenePath = "Assets/01.Scenes/GameScene.unity";
    private const string LoginScenePath = "Assets/01.Scenes/LoginScene.unity";

    private static readonly string[] FontPaths =
    {
        "Assets/07.Fonts/Cafe24Ssurround-v2.asset",
        "Assets/07.Fonts/Maplestory Light SDF.asset",
    };

    // 레거시 필드(배경 안내)는 더 이상 화면에 나오지 않아 검사에서 뺀다.
    private static readonly HashSet<string> RetiredFields = new()
    {
        "_backgroundMenuButtonMessage",
        "_backgroundButtonMessage",
    };

    [Test]
    public void 튜토리얼과_안내_문구의_모든_글자가_두_글꼴에_있다()
    {
        var missing = new List<string>();
        foreach (string path in FontPaths)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            Assert.IsNotNull(font, path);

            foreach ((string label, string text) in EnumerateAssetTexts())
            {
                foreach (char c in StripRichText(text))
                {
                    if (char.IsWhiteSpace(c) || font.HasCharacter(c, false, false)) continue;

                    missing.Add($"{font.name} : '{c}' (U+{(int)c:X4}) in {label}");
                    break;
                }
            }
        }

        Assert.IsEmpty(missing, string.Join("\n", missing));
    }

    [Test]
    public void 문구에_줄바꿈_앞뒤_공백이나_두_칸_공백이_없다()
    {
        var problems = new List<string>();
        foreach ((string label, string text) in EnumerateAssetTexts())
        {
            if (text.Contains(" \n") || text.Contains("\n ") || text.Contains("  "))
            {
                problems.Add($"{label} : {text.Replace("\n", "\\n")}");
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void 모든_대화_ID에_대사가_있고_뽑기_대화는_두_줄_이상이다()
    {
        var content = AssetDatabase.LoadAssetAtPath<TutorialContent>(TutorialContentPath);
        Assert.IsNotNull(content, TutorialContentPath);

        foreach (DialogueId id in Enum.GetValues(typeof(DialogueId)))
        {
            IReadOnlyList<DialogueLine> lines = content.GetDialogue(id);
            Assert.IsNotNull(lines, $"{id}");
            Assert.Greater(lines.Count, 0, $"{id}");
            foreach (DialogueLine line in lines)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.Message), $"{id}의 빈 대사");
                Assert.IsFalse(string.IsNullOrWhiteSpace(line.Speaker), $"{id}의 빈 화자");
            }
        }

        Assert.GreaterOrEqual(content.GetDialogue(DialogueId.Gacha).Count, 2,
            "뽑기 튜토리얼은 마지막 줄을 뽑기권 지급 대사로 따로 띄운다");
    }

    [Test]
    public void 소리_카탈로그의_모든_효과음에_클립이_있다()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalogSO>(AudioCatalogPath);
        Assert.IsNotNull(catalog, AudioCatalogPath);

        var missing = new List<string>();
        foreach (EAudioSfx cue in Enum.GetValues(typeof(EAudioSfx)))
        {
            if (catalog.GetSfx(cue) == null) missing.Add(cue.ToString());
        }

        Assert.IsEmpty(missing, string.Join(", ", missing));
        Assert.IsNotNull(catalog.LoginBgm, "로그인 BGM");
        foreach (EBackgroundTheme theme in Enum.GetValues(typeof(EBackgroundTheme)))
        {
            Assert.IsNotNull(catalog.GetRandomThemeBgm(theme), $"{theme} 테마 BGM");
        }

        Assert.IsNotNull(catalog.GetRandomDisplayRoomBgm(), "장식장 BGM");
    }

    [Test]
    public void 씬의_라벨_문구에도_글꼴에_없는_글자가_없다()
    {
        var missing = new List<string>();
        ForEachSceneText(text =>
        {
            TMP_FontAsset font = text.font;
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static) return;

            foreach (char c in StripRichText(text.text))
            {
                if (char.IsWhiteSpace(c) || font.HasCharacter(c, true, false)) continue;

                missing.Add($"{text.gameObject.scene.name}/{PathOf(text.transform)} : '{c}' (U+{(int)c:X4})");
                return;
            }
        });

        Assert.IsEmpty(missing, string.Join("\n", missing));
    }

    // 눌러도 소리가 없는 버튼을 막는다. 코드에서 직접 소리를 내는 버튼만 예외로 둔다.
    [Test]
    public void 씬의_버튼은_클릭음을_가지거나_코드에서_소리를_낸다()
    {
        var codeSounded = new HashSet<string>
        {
            "LoginButton",            // LoginScene이 LoginButton 효과음을 낸다
            "SinglePullButton",       // 티켓을 넣는 연출이 소리를 낸다
            "MultiPullButton",
            "ConfirmButton",          // 오프라인 보상: 받기 연출의 소리
            "AdDoubleButton",
            "EndingPresentation",     // 엔딩이 화면 터치마다 소리를 낸다
        };

        var silent = new List<string>();
        ForEachSceneButton(button =>
        {
            if (button.GetComponent<ButtonSFX>() != null) return;
            if (codeSounded.Contains(button.name)) return;
            if (PathOf(button.transform).Contains("SystemUpgradeCarouselItem")) return; // 구매 성공 때만 코드가 소리를 낸다

            silent.Add($"{button.gameObject.scene.name}/{PathOf(button.transform)}");
        });

        Assert.IsEmpty(silent, string.Join("\n", silent));
    }

    // 코드에서 조립하는 문구는 에셋 검사에 걸리지 않는다. 가운데점은 두 글꼴 모두에 없는데
    // 탐험 화면과 도감이 코드 문자열로 썼다가 빈칸으로 그려졌다.
    [Test]
    public void 코드의_화면_문자열에_글꼴에_없는_가운데점이_없다()
    {
        var literal = new System.Text.RegularExpressions.Regex("\"[^\"\\r\\n]*\"");
        var problems = new List<string>();
        string root = System.IO.Path.Combine(Application.dataPath, "02.Scripts");
        foreach (string file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Editor/")) continue;

            string[] lines = System.IO.File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimStart();
                if (line.StartsWith("//") || line.Contains("Tooltip(") || line.Contains("Debug.Log")) continue;

                foreach (System.Text.RegularExpressions.Match match in literal.Matches(lines[i]))
                {
                    if (match.Value.Contains("·"))
                    {
                        problems.Add($"{System.IO.Path.GetFileName(file)}:{i + 1} {match.Value}");
                    }
                }
            }
        }

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    private static IEnumerable<(string label, string text)> EnumerateAssetTexts()
    {
        foreach (string path in new[] { TutorialContentPath, UiMessagesPath })
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Assert.IsNotNull(asset, path);

            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.String) continue;
                if (RetiredFields.Contains(property.name)) continue;
                if (string.IsNullOrEmpty(property.stringValue)) continue;

                yield return ($"{asset.name}.{property.propertyPath}", property.stringValue);
            }
        }
    }

    // <size=60> 같은 태그 안의 글자는 화면에 그려지지 않으므로 뺀다.
    private static string StripRichText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new System.Text.StringBuilder(text.Length);
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) builder.Append(c);
        }

        return builder.ToString();
    }

    private static void ForEachSceneText(Action<TMP_Text> visit)
    {
        ForEachSceneRoot(root =>
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                visit(text);
            }
        });
    }

    private static void ForEachSceneButton(Action<Button> visit)
    {
        ForEachSceneRoot(root =>
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                visit(button);
            }
        });
    }

    // 열려 있는 씬은 그대로 읽고, 아니면 더해 열었다가 닫는다. 씬을 바꾸지 않는다.
    private static void ForEachSceneRoot(Action<GameObject> visit)
    {
        foreach (string path in new[] { GameScenePath, LoginScenePath })
        {
            Scene scene = EditorSceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    visit(root);
                }
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
