using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 약관과 방침의 글 원본(Assets/10.ScriptableObjects/Legal)에서 docs의 웹 문서를 만든다. 기획서 §21.11.
// 글은 원본에서만 고친다. docs/index.html과 docs/terms.html은 이 메뉴가 덮어쓰는 결과물이다.
public static class LegalDocumentExporter
{
    private const string SourceFolder = "Assets/10.ScriptableObjects/Legal/";

    private const string PageTemplate =
        "<!DOCTYPE html>\n" +
        "<html lang=\"ko\">\n" +
        "<head>\n" +
        "    <meta charset=\"UTF-8\">\n" +
        "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
        "    <title>{0}</title>\n" +
        "    <style>\n" +
        "        body {{\n" +
        "            max-width: 800px;\n" +
        "            margin: 40px auto;\n" +
        "            padding: 0 20px;\n" +
        "            font-family: Arial, sans-serif;\n" +
        "            line-height: 1.7;\n" +
        "        }}\n" +
        "\n" +
        "        h1 {{\n" +
        "            margin-bottom: 8px;\n" +
        "        }}\n" +
        "\n" +
        "        h2 {{\n" +
        "            margin-top: 32px;\n" +
        "        }}\n" +
        "    </style>\n" +
        "</head>\n" +
        "\n" +
        "<body>\n" +
        "{1}" +
        "</body>\n" +
        "</html>\n";

    [MenuItem("Tools/Legal/Export Web Documents")]
    public static void Export()
    {
        int written = 0;
        written += ExportOne("Privacy.txt", "docs/index.html") ? 1 : 0;
        written += ExportOne("Terms.txt", "docs/terms.html") ? 1 : 0;
        Debug.Log($"약관과 방침 웹 문서 {written}개를 내보냈습니다. docs 폴더를 확인하세요.");
    }

    private static bool ExportOne(string sourceName, string outputRelativePath)
    {
        string sourcePath = SourceFolder + sourceName;
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
        if (asset == null)
        {
            Debug.LogError($"글 원본을 찾을 수 없습니다: {sourcePath}");
            return false;
        }

        string title = LegalTextFormatter.GetTitle(asset.text);
        string html = string.Format(PageTemplate, title, LegalTextFormatter.ToHtmlBody(asset.text));
        // 저장소의 기존 문서와 같은 CRLF로 쓴다. BOM은 붙이지 않는다.
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputPath = Path.Combine(projectRoot, outputRelativePath);
        File.WriteAllText(outputPath, html.Replace("\n", "\r\n"), new UTF8Encoding(false));
        return true;
    }
}
