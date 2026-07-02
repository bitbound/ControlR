using System.Text;

namespace ControlR.Web.Client.Services;

public interface IMarkdownParser
{
  string ToHtml(string markdown);
}

public sealed class MarkdownParser : IMarkdownParser
{
  public string ToHtml(string markdown)
  {
    if (markdown is null)
      return string.Empty;

    if (markdown.Length == 0)
      return string.Empty;

    var sb = new StringBuilder(markdown.Length * 2);
    var normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
    var lines = normalized.Split('\n');
    var i = 0;

    while (i < lines.Length)
    {
      i = ParseBlock(lines, i, sb);
    }

    return sb.ToString();
  }

  private static void AppendHtmlEncoded(StringBuilder sb, char c)
  {
    switch (c)
    {
      case '<':
        sb.Append("&lt;");
        break;
      case '>':
        sb.Append("&gt;");
        break;
      case '&':
        sb.Append("&amp;");
        break;
      case '"':
        sb.Append("&quot;");
        break;
      case '\'':
        sb.Append("&#39;");
        break;
      default:
        sb.Append(c);
        break;
    }
  }

  private static void AppendHtmlEncoded(StringBuilder sb, ReadOnlySpan<char> text)
  {
    foreach (var c in text)
    {
      AppendHtmlEncoded(sb, c);
    }
  }

  private static string ExtractOrderedContent(ReadOnlySpan<char> trimmedLine)
  {
    var dotPos = trimmedLine.IndexOf('.');
    if (dotPos < 0)
      return trimmedLine.Trim().ToString();
    return trimmedLine.Slice(dotPos + 1).Trim().ToString();
  }

  private static string ExtractUnorderedContent(ReadOnlySpan<char> trimmedLine)
  {
    return trimmedLine.Slice(2).Trim().ToString();
  }

  private static int FindSingleCharClose(ReadOnlySpan<char> text, char c)
  {
    for (var i = 0; i < text.Length; i++)
    {
      if (text[i] == c)
      {
        if (i + 1 < text.Length && text[i + 1] == c)
        {
          i++;
          continue;
        }
        return i;
      }
    }
    return -1;
  }

  private static int GetIndentLevel(ReadOnlySpan<char> line)
  {
    var count = 0;
    while (count < line.Length && line[count] == ' ')
      count++;
    return count;
  }

  private static bool IsAllowedUrlScheme(ReadOnlySpan<char> url)
  {
    if (url.Length == 0)
      return true;

    var schemeEnd = url.IndexOf(':');
    if (schemeEnd < 0)
      return true; // No scheme = relative URL, OK

    // Only http and https schemes are allowed
    var scheme = url[..schemeEnd];
    return scheme is "http" or "https";
  }

  private static bool IsHorizontalRule(ReadOnlySpan<char> line)
  {
    if (line.Length < 3)
      return false;

    var first = line[0];
    if (first is not ('-' or '*' or '_'))
      return false;

    for (var i = 0; i < line.Length; i++)
    {
      if (line[i] != first && line[i] != ' ')
        return false;
    }

    return true;
  }

  private static bool IsUnorderedListItem(ReadOnlySpan<char> line)
  {
    if (line.Length < 2)
      return false;

    return (line[0] is '-' or '*' or '+') && line[1] == ' ';
  }

  private static int ParseBlock(string[] lines, int start, StringBuilder sb)
  {
    var line = lines[start].AsSpan().TrimEnd('\r');

    if (line.IsEmpty || line.TrimStart().IsEmpty)
      return start + 1;

    var c = line[0];

    if (c == '#')
      return ParseHeading(lines, start, sb);

    if (line.StartsWith("```"))
      return ParseFencedCodeBlock(lines, start, sb);

    if (c == '>')
      return ParseBlockquote(lines, start, sb);

    if (c is '-' or '*' or '_' or '+')
    {
      if (IsHorizontalRule(line))
      {
        sb.Append("<hr />\n");
        return start + 1;
      }

      if (c is '-' or '*' or '+')
      {
        if (IsUnorderedListItem(line))
          return ParseUnorderedList(lines, start, sb);
      }

      return ParseParagraph(lines, start, sb);
    }

    if (char.IsAsciiDigit(c))
    {
      if (StartsWithOrderedMarker(line))
        return ParseOrderedList(lines, start, sb);

      return ParseParagraph(lines, start, sb);
    }

    return ParseParagraph(lines, start, sb);
  }

  private static int ParseBlockquote(string[] lines, int start, StringBuilder sb)
  {
    sb.Append("<blockquote>");
    var i = start;
    var firstLine = true;

    while (i < lines.Length)
    {
      var line = lines[i].AsSpan().TrimEnd('\r');
      if (line.IsEmpty || line[0] != '>')
        break;

      var text = line.Slice(1);
      if (text.Length > 0 && text[0] == ' ')
        text = text.Slice(1);

      if (!firstLine)
        sb.Append(' ');

      ParseInline(text, sb);
      firstLine = false;
      i++;
    }

    sb.Append("</blockquote>\n");
    return i;
  }

  private static int ParseFencedCodeBlock(string[] lines, int start, StringBuilder sb)
  {
    var firstLine = lines[start].AsSpan().TrimEnd('\r');
    var language = firstLine.Slice(3).Trim().ToString();

    sb.Append("<pre><code");
    if (language.Length > 0)
    {
      sb.Append(" class=\"language-");
      AppendHtmlEncoded(sb, language.AsSpan());
      sb.Append('"');
    }
    sb.Append('>');

    var i = start + 1;
    while (i < lines.Length)
    {
      var line = lines[i].AsSpan().TrimEnd('\r');
      if (line.StartsWith("```"))
        break;

      AppendHtmlEncoded(sb, lines[i].AsSpan());
      sb.Append('\n');
      i++;
    }

    sb.Append("</code></pre>\n");
    return i + 1;
  }

  private static int ParseHeading(string[] lines, int start, StringBuilder sb)
  {
    var line = lines[start].AsSpan().TrimEnd('\r');
    var level = 0;

    while (level < line.Length && line[level] == '#')
      level++;

    if (level > 6)
      return ParseParagraph(lines, start, sb);

    var pos = level;
    while (pos < line.Length && line[pos] == ' ')
      pos++;

    var content = line.Slice(pos);

    // Trim trailing spaces and hashes
    content = content.TrimEnd();
    var end = content.Length;
    while (end > 0 && content[end - 1] == '#')
      end--;
    if (end < content.Length)
      content = content[..end].TrimEnd();

    sb.Append("<h").Append(level).Append('>');
    ParseInline(content, sb);
    sb.Append("</h").Append(level).Append(">\n");

    return start + 1;
  }

  private static void ParseInline(ReadOnlySpan<char> text, StringBuilder sb)
  {
    var i = 0;
    while (i < text.Length)
    {
      var remaining = text.Length - i;
      var c = text[i];

      // Bold-Italic ***text***
      if (remaining >= 3 && c == '*' && text[i + 1] == '*' && text[i + 2] == '*')
      {
        var searchFrom = text.Slice(i + 3);
        var endIdx = searchFrom.IndexOf("***");
        if (endIdx >= 0)
        {
          sb.Append("<b><i>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</i></b>");
          i += 3 + endIdx + 3;
          continue;
        }
      }

      // Bold-Italic ___text___
      if (remaining >= 3 && c == '_' && text[i + 1] == '_' && text[i + 2] == '_')
      {
        var searchFrom = text.Slice(i + 3);
        var endIdx = searchFrom.IndexOf("___");
        if (endIdx >= 0)
        {
          sb.Append("<b><i>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</i></b>");
          i += 3 + endIdx + 3;
          continue;
        }
      }

      // Bold **text**
      if (remaining >= 2 && c == '*' && text[i + 1] == '*')
      {
        var searchFrom = text.Slice(i + 2);
        var endIdx = searchFrom.IndexOf("**");
        if (endIdx >= 0)
        {
          sb.Append("<b>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</b>");
          i += 2 + endIdx + 2;
          continue;
        }
      }

      // Bold __text__
      if (remaining >= 2 && c == '_' && text[i + 1] == '_')
      {
        var searchFrom = text.Slice(i + 2);
        var endIdx = searchFrom.IndexOf("__");
        if (endIdx >= 0)
        {
          sb.Append("<b>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</b>");
          i += 2 + endIdx + 2;
          continue;
        }
      }

      // Italic *text* — only open if NOT followed by * (would be ** or ***)
      if (c == '*' && !(remaining >= 2 && text[i + 1] == '*'))
      {
        var searchFrom = text.Slice(i + 1);
        var endIdx = FindSingleCharClose(searchFrom, '*');
        if (endIdx >= 0)
        {
          sb.Append("<i>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</i>");
          i += 1 + endIdx + 1;
          continue;
        }
      }

      // Italic _text_ — only open if NOT followed by _ (would be __ or ___)
      if (c == '_' && !(remaining >= 2 && text[i + 1] == '_'))
      {
        var searchFrom = text.Slice(i + 1);
        var endIdx = FindSingleCharClose(searchFrom, '_');
        if (endIdx >= 0)
        {
          sb.Append("<i>");
          ParseInline(searchFrom[..endIdx], sb);
          sb.Append("</i>");
          i += 1 + endIdx + 1;
          continue;
        }
      }

      // Inline code `text`
      if (c == '`')
      {
        var searchFrom = text.Slice(i + 1);
        var endIdx = searchFrom.IndexOf('`');
        if (endIdx >= 0)
        {
          sb.Append("<code>");
          AppendHtmlEncoded(sb, searchFrom[..endIdx]);
          sb.Append("</code>");
          i += 1 + endIdx + 1;
          continue;
        }
      }

      // Link [text](url)
      if (c == '[')
      {
        var depth = 1;
        var closeBracket = -1;
        for (var j = i + 1; j < text.Length && depth > 0; j++)
        {
          if (text[j] == '[')
            depth++;
          else if (text[j] == ']')
            depth--;
          if (depth == 0)
          {
            closeBracket = j;
            break;
          }
        }
        if (closeBracket >= 0)
        {
          var parenStart = closeBracket + 1;
          if (parenStart < text.Length && text[parenStart] == '(')
          {
            var closeParen = text.Slice(parenStart + 1).IndexOf(')');
            if (closeParen >= 0)
            {
              var linkText = text.Slice(i + 1, closeBracket - i - 1);
              var url = text.Slice(parenStart + 1, closeParen);

              if (IsAllowedUrlScheme(url))
              {
                sb.Append("<a href=\"");
                AppendHtmlEncoded(sb, url);
                sb.Append("\">");
                ParseInline(linkText, sb);
                sb.Append("</a>");
                i = parenStart + 1 + closeParen + 1;
                continue;
              }
            }
          }
        }
      }

      // Image ![alt](url)
      if (c == '!' && remaining >= 2 && text[i + 1] == '[')
      {
        var depth = 1;
        var closeBracket = -1;
        for (var j = i + 2; j < text.Length && depth > 0; j++)
        {
          if (text[j] == '[')
            depth++;
          else if (text[j] == ']')
            depth--;
          if (depth == 0)
          {
            closeBracket = j;
            break;
          }
        }
        if (closeBracket >= 0)
        {
          var parenStart = closeBracket + 1;
          if (parenStart < text.Length && text[parenStart] == '(')
          {
            var closeParen = text.Slice(parenStart + 1).IndexOf(')');
            if (closeParen >= 0)
            {
              var altText = text.Slice(i + 2, closeBracket - i - 2);
              var url = text.Slice(parenStart + 1, closeParen);

              if (IsAllowedUrlScheme(url))
              {
                sb.Append("<img src=\"");
                AppendHtmlEncoded(sb, url);
                sb.Append("\" alt=\"");
                AppendHtmlEncoded(sb, altText);
                sb.Append("\" />");
                i = parenStart + 1 + closeParen + 1;
                continue;
              }
            }
          }
        }
      }

      // Escape sequence \X
      if (c == '\\' && remaining >= 2)
      {
        var next = text[i + 1];
        switch (next)
        {
          case '\\':
          case '`':
          case '*':
          case '_':
          case '{':
          case '}':
          case '[':
          case ']':
          case '(':
          case ')':
          case '#':
          case '+':
          case '-':
          case '.':
          case '!':
          case '|':
          case '<':
          case '>':
          case '~':
            AppendHtmlEncoded(sb, next);
            i += 2;
            continue;
        }
      }

      AppendHtmlEncoded(sb, c);
      i++;
    }
  }

  private static int ParseList(
    string[] lines,
    int start,
    StringBuilder sb,
    string listTag,
    Func<ReadOnlySpan<char>, bool> isItem,
    Func<ReadOnlySpan<char>, string> extractContent)
  {
    var items = new List<(int Indent, string Content)>();
    var i = start;

    while (i < lines.Length)
    {
      var rawLine = lines[i].AsSpan().TrimEnd('\r');
      if (rawLine.IsEmpty)
        break;

      var trimmed = rawLine.TrimStart();
      if (!isItem(trimmed))
        break;

      var indent = GetIndentLevel(rawLine);
      var content = extractContent(trimmed);
      items.Add((indent, content));
      i++;
    }

    if (items.Count == 0)
      return i;

    sb.Append('<').Append(listTag).Append(">\n");
    var depthStack = new List<int> { items[0].Indent };

    for (var j = 0; j < items.Count; j++)
    {
      var indent = items[j].Indent;

      // Close nested lists when indentation decreases
      var hasDecreasedIndent = false;
      while (depthStack.Count > 1 && indent < depthStack[^1])
      {
        hasDecreasedIndent = true;
        depthStack.RemoveAt(depthStack.Count - 1);
        // Close the deepest li (right after content, same line)
        sb.Append("</li>\n");
        // Close the nested list
        var parentDepth = depthStack.Count;
        for (var k = 0; k < parentDepth; k++)
          sb.Append("  ");
        sb.Append("</").Append(listTag).Append(">\n");
      }

      // After unwinding nested levels, close the exposed root-level li
      if (hasDecreasedIndent && depthStack.Count == 1)
      {
        sb.Append("  ");
        sb.Append("</li>\n");
      }

      // Close previous sibling li when at the same depth
      if (j > 0 && indent == items[j - 1].Indent)
        sb.Append("</li>\n");

      // Open li with proper indent
      var itemDepth = depthStack.Count;
      for (var k = 0; k < itemDepth; k++)
        sb.Append("  ");
      sb.Append("<li>");

      ParseInline(items[j].Content.AsSpan(), sb);

      // Open nested list if next item is deeper
      if (j + 1 < items.Count && items[j + 1].Indent > indent)
      {
        depthStack.Add(items[j + 1].Indent);
        sb.Append('\n');
        var nextDepth = depthStack.Count;
        for (var k = 0; k < nextDepth; k++)
          sb.Append("  ");
        sb.Append('<').Append(listTag).Append(">\n");
      }
    }

    // Close remaining open tags.
    while (depthStack.Count > 0)
    // and its parent item from innermost to outermost.
    while (depthStack.Count > 0)
    {
      depthStack.RemoveAt(depthStack.Count - 1);
      sb.Append("</li>\n");
      if (depthStack.Count > 0)
      {
        var nestDepth = depthStack.Count;
        for (var k = 0; k < nestDepth; k++)
          sb.Append("  ");
        sb.Append("</").Append(listTag).Append(">\n");
      }
    }
    sb.Append("</").Append(listTag).Append(">\n");

    return i;
  }

  private static int ParseOrderedList(string[] lines, int start, StringBuilder sb)
  {
    return ParseList(lines, start, sb, "ol", StartsWithOrderedMarker, ExtractOrderedContent);
  }

  private static int ParseParagraph(string[] lines, int start, StringBuilder sb)
  {
    sb.Append("<p>");
    var i = start;

    while (i < lines.Length)
    {
      var line = lines[i].AsSpan().TrimEnd('\r');

      if (line.IsEmpty)
        break;

      // Whitespace-only lines are treated as blank
      if (line.TrimStart().IsEmpty)
        break;

      var c = line[0];

      if (c == '#' || line.StartsWith("```") || c == '>')
        break;

      if (IsHorizontalRule(line))
        break;

      if (c is '-' or '*' or '+' && IsUnorderedListItem(line))
        break;

      if (char.IsAsciiDigit(c) && StartsWithOrderedMarker(line))
        break;

      if (i > start)
        sb.Append(' ');

      ParseInline(line, sb);
      i++;
    }

    // If nothing was consumed, still advance past this line to avoid OOM
    if (i == start)
    {
      var line = lines[start].AsSpan().TrimEnd('\r');
      ParseInline(line, sb);
      i = start + 1;
    }

    sb.Append("</p>\n");
    return i;
  }

  private static int ParseUnorderedList(string[] lines, int start, StringBuilder sb)
  {
    return ParseList(lines, start, sb, "ul", IsUnorderedListItem, ExtractUnorderedContent);
  }

  private static bool StartsWithOrderedMarker(ReadOnlySpan<char> line)
  {
    var dotPos = line.IndexOf('.');
    if (dotPos < 1 || dotPos > 3)
      return false;

    for (var i = 0; i < dotPos; i++)
    {
      if (!char.IsAsciiDigit(line[i]))
        return false;
    }

    return dotPos + 1 < line.Length && line[dotPos + 1] == ' ';
  }
}
