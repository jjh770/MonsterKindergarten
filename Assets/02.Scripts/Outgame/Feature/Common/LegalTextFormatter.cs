using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// 약관과 방침의 글 원본(TextAsset)을 앱 안에서 보여 줄 글과 웹 문서로 바꾼다. 기획서 §21.11.
//
// 원본이 하나라서 앱 안 팝업과 docs의 웹 문서가 서로 다른 약관을 말하지 않는다. 원본의 문법은 최소한이다.
//   # 제목, ## 소제목, ### 작은 소제목
//   - 목록(두 칸 들여쓰기마다 한 단계 안으로)
//   [글자](주소) 링크. 앱 안에서는 글자만 보이고 웹 문서에서는 링크가 된다.
//   그 밖의 줄은 문단이고, 이어진 줄은 같은 문단에서 줄바꿈된다. 빈 줄은 문단을 끊는다.
// 원본에는 TMP 태그나 HTML을 쓰지 않는다. 글자 '<'는 변환 때 그대로 글자로 나가도록 막는다.
public static class LegalTextFormatter
{
    private enum EBlock
    {
        Heading1,
        Heading2,
        Heading3,
        Paragraph,
        ListItem,
    }

    private sealed class Block
    {
        public EBlock Kind;
        public int Level;
        public readonly List<string> Lines = new();
    }

    private static readonly Regex LinkPattern = new(@"\[([^\]]*)\]\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex QuotePattern = new("'([^']*)'", RegexOptions.Compiled);

    // 첫 번째 # 제목이다. 없으면 빈 문자열이다.
    public static string GetTitle(string source)
    {
        foreach (Block block in Parse(source))
        {
            if (block.Kind == EBlock.Heading1) return block.Lines[0];
        }

        return string.Empty;
    }

    // 제목은 팝업의 제목칸이 따로 보여 주므로 본문에서는 뺀다.
    public static string ToTmp(string source)
    {
        var builder = new StringBuilder();
        bool previousWasList = false;
        foreach (Block block in Parse(source))
        {
            if (block.Kind == EBlock.Heading1) continue;

            bool isList = block.Kind == EBlock.ListItem;
            if (builder.Length > 0)
            {
                // 목록 항목끼리는 붙이고, 그 밖에는 한 줄 비운다.
                builder.Append(isList && previousWasList ? "\n" : "\n\n");
            }

            previousWasList = isList;
            switch (block.Kind)
            {
                case EBlock.Heading2:
                    builder.Append("<size=118%><b>").Append(EscapeTmp(StripLinks(block.Lines[0]))).Append("</b></size>");
                    break;
                case EBlock.Heading3:
                    builder.Append("<size=108%><b>").Append(EscapeTmp(StripLinks(block.Lines[0]))).Append("</b></size>");
                    break;
                case EBlock.ListItem:
                    // 들여쓴 단계마다 왼쪽 여백을 더한다. 줄이 넘어가도 같은 여백에서 이어진다.
                    builder.Append("<margin-left=").Append(30 + block.Level * 40).Append("px>- ")
                        .Append(EscapeTmp(StripLinks(block.Lines[0]))).Append("</margin>");
                    break;
                default:
                    builder.Append(EscapeTmp(StripLinks(string.Join("\n", block.Lines))));
                    break;
            }
        }

        return builder.ToString();
    }

    // <body> 안쪽만 만든다. 페이지 틀은 내보내는 쪽이 붙인다.
    public static string ToHtmlBody(string source)
    {
        var builder = new StringBuilder();
        int openLevel = -1;
        foreach (Block block in Parse(source))
        {
            if (block.Kind == EBlock.ListItem)
            {
                while (openLevel < block.Level)
                {
                    openLevel++;
                    builder.Append(Indent(openLevel * 2 + 1)).Append("<ul>\n");
                }

                while (openLevel > block.Level)
                {
                    builder.Append(Indent(openLevel * 2 + 1)).Append("</ul>\n");
                    openLevel--;
                }

                builder.Append(Indent(block.Level * 2 + 2)).Append("<li>").Append(HtmlInline(block.Lines[0])).Append("</li>\n");
                continue;
            }

            while (openLevel >= 0)
            {
                builder.Append(Indent(openLevel * 2 + 1)).Append("</ul>\n");
                openLevel--;
            }

            if (builder.Length > 0) builder.Append('\n');
            switch (block.Kind)
            {
                case EBlock.Heading1:
                    builder.Append("    <h1>").Append(HtmlInline(block.Lines[0])).Append("</h1>\n");
                    break;
                case EBlock.Heading2:
                    builder.Append("    <h2>").Append(HtmlInline(block.Lines[0])).Append("</h2>\n");
                    break;
                case EBlock.Heading3:
                    builder.Append("    <h3>").Append(HtmlInline(block.Lines[0])).Append("</h3>\n");
                    break;
                default:
                    builder.Append("    <p>\n");
                    for (int i = 0; i < block.Lines.Count; i++)
                    {
                        builder.Append("        ").Append(HtmlInline(block.Lines[i]));
                        builder.Append(i < block.Lines.Count - 1 ? "<br>\n" : "\n");
                    }

                    builder.Append("    </p>\n");
                    break;
            }
        }

        while (openLevel >= 0)
        {
            builder.Append(Indent(openLevel * 2 + 1)).Append("</ul>\n");
            openLevel--;
        }

        return builder.ToString();
    }

    private static List<Block> Parse(string source)
    {
        var blocks = new List<Block>();
        Block paragraph = null;
        foreach (string rawLine in (source ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            string trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                paragraph = null;
                continue;
            }

            if (trimmed.StartsWith("### "))
            {
                blocks.Add(NewBlock(EBlock.Heading3, trimmed.Substring(4)));
                paragraph = null;
                continue;
            }

            if (trimmed.StartsWith("## "))
            {
                blocks.Add(NewBlock(EBlock.Heading2, trimmed.Substring(3)));
                paragraph = null;
                continue;
            }

            if (trimmed.StartsWith("# "))
            {
                blocks.Add(NewBlock(EBlock.Heading1, trimmed.Substring(2)));
                paragraph = null;
                continue;
            }

            if (trimmed.StartsWith("- "))
            {
                int indent = line.Length - trimmed.Length;
                Block item = NewBlock(EBlock.ListItem, trimmed.Substring(2));
                item.Level = indent / 2;
                blocks.Add(item);
                paragraph = null;
                continue;
            }

            if (paragraph == null)
            {
                paragraph = new Block { Kind = EBlock.Paragraph };
                blocks.Add(paragraph);
            }

            paragraph.Lines.Add(trimmed);
        }

        return blocks;
    }

    private static Block NewBlock(EBlock kind, string text)
    {
        var block = new Block { Kind = kind };
        block.Lines.Add(text);
        return block;
    }

    private static string StripLinks(string text)
    {
        return LinkPattern.Replace(text, "$1");
    }

    // TMP는 '<'를 태그의 시작으로 읽는다. 원본에는 없어야 하지만, 들어와도 글자로 보이게 한다.
    private static string EscapeTmp(string text)
    {
        // 게임 글꼴에는 아스키 작은따옴표(')가 없어 빈칸으로 나온다. 짝이 맞는 것은 둥근 따옴표로 바꾼다.
        text = QuotePattern.Replace(text, "‘$1’");
        return text.Replace("<", "<noparse><</noparse>");
    }

    private static string HtmlInline(string text)
    {
        var builder = new StringBuilder();
        int last = 0;
        foreach (Match match in LinkPattern.Matches(text))
        {
            builder.Append(EscapeHtml(text.Substring(last, match.Index - last)));
            builder.Append("<a href=\"").Append(EscapeHtml(match.Groups[2].Value)).Append("\">")
                .Append(EscapeHtml(match.Groups[1].Value)).Append("</a>");
            last = match.Index + match.Length;
        }

        builder.Append(EscapeHtml(text.Substring(last)));
        return builder.ToString();
    }

    private static string EscapeHtml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string Indent(int level)
    {
        return new string(' ', level * 4);
    }
}
