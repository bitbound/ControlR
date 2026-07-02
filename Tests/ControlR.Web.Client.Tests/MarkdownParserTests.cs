#pragma warning disable BB0001 // Member order is incorrect
using ControlR.Web.Client.Services;

namespace ControlR.Web.Client.Tests;

public class MarkdownParserTests
{
  private readonly MarkdownParser _parser = new();

  #region Headings

  [Fact]
  public void ToHtml_H1Heading_ReturnsH1()
  {
    var result = _parser.ToHtml("# Heading 1");
    Assert.Contains("<h1>", result);
    Assert.Contains("Heading 1", result);
    Assert.Contains("</h1>", result);
  }

  [Fact]
  public void ToHtml_H2Heading_ReturnsH2()
  {
    var result = _parser.ToHtml("## Heading 2");
    Assert.Contains("<h2>", result);
    Assert.Contains("Heading 2", result);
    Assert.Contains("</h2>", result);
  }

  [Fact]
  public void ToHtml_H3Heading_ReturnsH3()
  {
    var result = _parser.ToHtml("### Heading 3");
    Assert.Contains("<h3>", result);
    Assert.Contains("Heading 3", result);
    Assert.Contains("</h3>", result);
  }

  [Fact]
  public void ToHtml_H4Heading_ReturnsH4()
  {
    var result = _parser.ToHtml("#### Heading 4");
    Assert.Contains("<h4>", result);
    Assert.Contains("Heading 4", result);
    Assert.Contains("</h4>", result);
  }

  [Fact]
  public void ToHtml_H5Heading_ReturnsH5()
  {
    var result = _parser.ToHtml("##### Heading 5");
    Assert.Contains("<h5>", result);
    Assert.Contains("Heading 5", result);
    Assert.Contains("</h5>", result);
  }

  [Fact]
  public void ToHtml_H6Heading_ReturnsH6()
  {
    var result = _parser.ToHtml("###### Heading 6");
    Assert.Contains("<h6>", result);
    Assert.Contains("Heading 6", result);
    Assert.Contains("</h6>", result);
  }

  [Fact]
  public void ToHtml_HeadingWithTrailingHashes_StripsTrailingHashes()
  {
    var result = _parser.ToHtml("# Title #");
    Assert.Contains("<h1>Title</h1>", result);
  }

  [Fact]
  public void ToHtml_HeadingWithTrailingMultipleHashes_StripsThem()
  {
    var result = _parser.ToHtml("# Title ####");
    Assert.Contains("<h1>Title</h1>", result);
  }

  [Fact]
  public void ToHtml_SevenHashes_TreatedAsParagraph()
  {
    var result = _parser.ToHtml("####### Not a heading");
    Assert.Contains("<p>", result);
  }

  [Fact]
  public void ToHtml_HeadingContainsInline_EmitsNestedHtml()

  {
    var result = _parser.ToHtml("# **Bold** heading");
    Assert.Contains("<h1><b>Bold</b> heading</h1>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_HeadingWithTrailingWhitespace_Trimmed()
  {
    var result = StripNewlines(_parser.ToHtml("# Title   "));
    Assert.Equal("<h1>Title</h1>", result);
  }

  [Fact]
  public void ToHtml_HeadingDashCombination_ProducesHeading()
  {
    var result = _parser.ToHtml("##---");
    Assert.Contains("<h2>", result);
    Assert.DoesNotContain("<hr />", result);
  }

  #endregion

  #region Paragraphs

  [Fact]
  public void ToHtml_PlainText_ReturnsParagraph()
  {
    var result = _parser.ToHtml("Hello world");
    Assert.Contains("<p>Hello world</p>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_MultiLineParagraph_JoinsWithSpaces()
  {
    var result = _parser.ToHtml("Line one\nLine two");
    Assert.Contains("<p>Line one Line two</p>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_MultipleParagraphs_SeparatedByBlankLine()
  {
    var result = _parser.ToHtml("First paragraph.\n\nSecond paragraph.");
    Assert.Contains("<p>First paragraph.</p>", result);
    Assert.Contains("<p>Second paragraph.</p>", result);
  }

  [Fact]
  public void ToHtml_ParagraphWithInlineBold_EmitsCorrectHtml()
  {
    var result = _parser.ToHtml("This is **bold** text.");
    Assert.Contains("<p>This is <b>bold</b> text.</p>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_EmptyString_ReturnsEmpty()
  {
    var result = _parser.ToHtml("");
    Assert.Equal("", result);
  }

  [Fact]
  public void ToHtml_NullString_ReturnsEmpty()
  {
    var result = _parser.ToHtml(null!);
    Assert.Equal("", result);
  }

  [Fact]
  public void ToHtml_WhitespaceOnly_ReturnsEmpty()
  {
    var result = _parser.ToHtml("   \n  \n  ");
    Assert.Equal("", result);
  }

  [Fact]
  public void ToHtml_StandaloneCarriageReturn_Normalized()
  {
    var result = _parser.ToHtml("Line one\rLine two");
    Assert.Contains("<p>Line one Line two</p>", StripNewlines(result));
  }

  #endregion

  #region Bold

  [Fact]
  public void ToHtml_BoldText_ReturnsBoldTag()
  {
    var result = _parser.ToHtml("This is **bold** text.");
    Assert.Contains("<b>bold</b>", result);
  }

  [Fact]
  public void ToHtml_BoldWithUnderscores_ReturnsBoldTag()
  {
    var result = _parser.ToHtml("This is __bold__ text.");
    Assert.Contains("<b>bold</b>", result);
  }

  [Fact]
  public void ToHtml_BoldNestedInsideParagraph_EmitsCorrectStructure()
  {
    var result = StripNewlines(_parser.ToHtml("Some **bold words** here."));
    Assert.Equal("<p>Some <b>bold words</b> here.</p>", result);
  }

  [Fact]
  public void ToHtml_UnmatchedBold_EmitsPlainText()
  {
    var result = _parser.ToHtml("Unmatched **bold");
    Assert.Contains("**bold", result);
  }

  #endregion

  #region Italic

  [Fact]
  public void ToHtml_ItalicText_ReturnsItalicTag()
  {
    var result = _parser.ToHtml("This is *italic* text.");
    Assert.Contains("<i>italic</i>", result);
  }

  [Fact]
  public void ToHtml_ItalicWithUnderscore_ReturnsItalicTag()
  {
    var result = _parser.ToHtml("This is _italic_ text.");
    Assert.Contains("<i>italic</i>", result);
  }

  [Fact]
  public void ToHtml_UnmatchedItalic_EmitsPlainText()
  {
    var result = _parser.ToHtml("Unmatched *italic");
    Assert.Contains("*italic", result);
  }

  #endregion

  #region Bold-Italic

  [Fact]
  public void ToHtml_BoldItalic_ReturnsBoldItalicTags()
  {
    var result = _parser.ToHtml("This is ***bold italic*** text.");
    Assert.Contains("<b><i>bold italic</i></b>", result);
  }

  [Fact]
  public void ToHtml_BoldItalicWithUnderscores_ReturnsBoldItalicTags()
  {
    var result = _parser.ToHtml("This is ___bold italic___ text.");
    Assert.Contains("<b><i>bold italic</i></b>", result);
  }

  [Fact]
  public void ToHtml_BoldItalicAdjacentToWord_ParsesCorrectly()
  {
    var result = _parser.ToHtml("word***bold italic***word");
    Assert.Contains("<b><i>bold italic</i></b>", result);
  }

  #endregion

  #region Mixed Nested Inline

  [Fact]
  public void ToHtml_BoldContainsItalic_ParsesNested()
  {
    var result = _parser.ToHtml("**bold _and italic_**");
    Assert.Contains("<b>bold <i>and italic</i></b>", result);
  }

  [Fact]
  public void ToHtml_ItalicContainsBold_ParsesNested()
  {
    var result = _parser.ToHtml("*italic **and bold***");
    Assert.Contains("<i>italic <b>and bold</b></i>", result);
  }

  [Fact]
  public void ToHtml_MultipleInlineElements_ParsesEach()
  {
    var result = StripNewlines(_parser.ToHtml("**bold**, *italic*, and `code`."));
    Assert.Equal("<p><b>bold</b>, <i>italic</i>, and <code>code</code>.</p>", result);
  }

  #endregion

  #region Inline Code

  [Fact]
  public void ToHtml_InlineCode_ReturnsCodeTag()
  {
    var result = _parser.ToHtml("Use `code` here.");
    Assert.Contains("<code>code</code>", result);
  }

  [Fact]
  public void ToHtml_InlineCodeWithSpecialChars_EncodesContent()
  {
    var result = _parser.ToHtml("`<div> & \"quote\"`");
    Assert.Contains("<code>&lt;div&gt; &amp; &quot;quote&quot;</code>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_UnmatchedInlineCode_EmitsPartialCode()
  {
    var result = _parser.ToHtml("Unmatched `code");
    Assert.Contains("Unmatched `code", result);
  }

  #endregion

  #region Links

  [Fact]
  public void ToHtml_Link_ReturnsAnchorTag()
  {
    var result = _parser.ToHtml("[click me](https://example.com)");
    Assert.Contains("<a href=\"https://example.com\">click me</a>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_LinkWithNestedBold_ParsesNestedText()
  {
    var result = _parser.ToHtml("[**bold link**](https://example.com)");
    Assert.Contains("<a href=\"https://example.com\"><b>bold link</b></a>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_LinkWithInlineCode_ParsesNestedText()
  {
    var result = _parser.ToHtml("[`code` link](https://example.com)");
    Assert.Contains("<a href=\"https://example.com\"><code>code</code> link</a>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_LinkWithHtmlSpecialCharsInUrl_EncodesUrl()
  {
    var result = _parser.ToHtml("[link](https://example.com?a=1&b=2)");
    Assert.Contains("<a href=\"https://example.com?a=1&amp;b=2\">link</a>", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_BrokenLinkMissingParen_EmitsPlainText()
  {
    var result = _parser.ToHtml("[link](https://example.com");
    Assert.Contains("[link](https://example.com", result);
  }

  [Fact]
  public void ToHtml_JavascriptSchemeLink_NotRenderedAsLink()
  {
    var result = StripNewlines(_parser.ToHtml("[click](javascript:alert(1))"));
    Assert.DoesNotContain("<a", result);
    Assert.Contains("[click](javascript:alert(1))", result);
  }

  [Fact]
  public void ToHtml_DataSchemeLink_NotRenderedAsLink()
  {
    var result = StripNewlines(_parser.ToHtml("[click](data:text/html,<script>alert(1)</script>)"));
    Assert.DoesNotContain("<a", result);
    Assert.Contains("[click]", result);
  }

  [Fact]
  public void ToHtml_HttpAndHttpsLinks_StillRendered()
  {
    var httpResult = StripNewlines(_parser.ToHtml("[http](http://example.com)"));
    Assert.Contains("<a href=\"http://example.com\">http</a>", httpResult);
    var httpsResult = StripNewlines(_parser.ToHtml("[https](https://example.com)"));
    Assert.Contains("<a href=\"https://example.com\">https</a>", httpsResult);
  }

  [Fact]
  public void ToHtml_RelativeLink_StillRendered()
  {
    var result = StripNewlines(_parser.ToHtml("[relative](/path/page)"));
    Assert.Contains("<a href=\"/path/page\">relative</a>", result);
  }

  [Fact]
  public void ToHtml_NestedBrackets_DoesNotCrash()
  {
    var result = _parser.ToHtml("[[text](url)](url2)");
    Assert.NotNull(result);
    Assert.NotEqual(string.Empty, result);
  }

  #endregion

  #region Images

  [Fact]
  public void ToHtml_Image_ReturnsImgTag()
  {
    var result = _parser.ToHtml("![alt text](image.png)");
    Assert.Contains("<img src=\"image.png\" alt=\"alt text\" />", StripNewlines(result));
  }

  [Fact]
  public void ToHtml_ImageInParagraph_EmitsImgInline()
  {
    var result = StripNewlines(_parser.ToHtml("Look at ![this](img.png) picture."));
    Assert.Equal("<p>Look at <img src=\"img.png\" alt=\"this\" /> picture.</p>", result);
  }

  [Fact]
  public void ToHtml_JavascriptSchemeImage_NotRenderedAsImage()
  {
    var result = StripNewlines(_parser.ToHtml("![alt](javascript:alert(1))"));
    Assert.DoesNotContain("<img", result);
    Assert.Contains("![alt](javascript:alert(1))", result);
  }

  #endregion

  #region Escape Sequences

  [Fact]
  public void ToHtml_BackslashEscapesAsterisk_EmitsLiteralAsterisk()
  {
    var result = _parser.ToHtml("\\*not italic*");
    Assert.Contains("*not italic*", result);
    Assert.DoesNotContain("<i>", result);
  }

  [Fact]
  public void ToHtml_BackslashEscapesBacktick_EmitsLiteral()
  {
    var result = _parser.ToHtml("\\`not code`");
    Assert.Contains("`not code`", result);
    Assert.DoesNotContain("<code>", result);
  }

  [Fact]
  public void ToHtml_BackslashEscapesBracket_EmitsLiteral()
  {
    var result = _parser.ToHtml("\\[not a link](url)");
    Assert.Contains("[not a link](url)", result);
  }

  [Fact]
  public void ToHtml_BackslashEscapesExclamation_EmitsLiteral()
  {
    var result = _parser.ToHtml("\\!escaped");
    Assert.Contains("!escaped", result);
    Assert.DoesNotContain("<img", result);
  }

  [Fact]
  public void ToHtml_BackslashEscapesExclamationBeforeBracket_EmitsExclamationAndLink()
  {
    var result = _parser.ToHtml("\\![alt](url)");
    Assert.Contains("!<a href=\"url\">alt</a>", StripNewlines(result));
  }

  #endregion

  #region Unordered Lists

  [Fact]
  public void ToHtml_UnorderedList_ReturnsUlWithLi()
  {
    var result = _parser.ToHtml("- Item 1\n- Item 2\n- Item 3");
    Assert.Contains("<ul>", result);
    Assert.Contains("<li>Item 1</li>", result);
    Assert.Contains("<li>Item 2</li>", result);
    Assert.Contains("<li>Item 3</li>", result);
    Assert.Contains("</ul>", result);
  }

  [Fact]
  public void ToHtml_UnorderedListWithAsterisk_ReturnsUl()
  {
    var result = _parser.ToHtml("* Item 1\n* Item 2");
    Assert.Contains("<ul>", result);
    Assert.Contains("<li>Item 1</li>", result);
    Assert.Contains("<li>Item 2</li>", result);
  }

  [Fact]
  public void ToHtml_UnorderedListWithPlus_ReturnsUl()
  {
    var result = _parser.ToHtml("+ Item 1\n+ Item 2");
    Assert.Contains("<ul>", result);
    Assert.Contains("<li>Item 1</li>", result);
  }

  [Fact]
  public void ToHtml_UnorderedListWithInlineBold_EmitsNestedHtml()
  {
    var result = _parser.ToHtml("- **bold item**");
    Assert.Contains("<li><b>bold item</b></li>", result);
  }

  [Fact]
  public void ToHtml_ListThenParagraph_ProperlyTerminates()
  {
    var result = _parser.ToHtml("- Item 1\n- Item 2\n\nParagraph text");
    Assert.Contains("</ul>", result);
    Assert.Contains("<p>Paragraph text</p>", result);
  }

  [Fact]
  public void ToHtml_NestedUnorderedList_ParsesIndentedItems()
  {
    var md = """
        - Top level item
          - Nested item
        - Another top item
        """;
    var result = _parser.ToHtml(md);
    Assert.Contains("<ul>", result);
    Assert.Contains("<li>Top level item", result);
    Assert.Contains("<li>Nested item", result);
    Assert.Contains("<li>Another top item", result);
    Assert.Contains("</ul>", result);
  }

  [Fact]
  public void ToHtml_NestedUnorderedList_MatchingLiCount()
  {
    var md = """
        - A
          - B
            - C
        """;
    var result = _parser.ToHtml(md);
    var openCount = CountOccurrences(result, "<li");
    var closeCount = CountOccurrences(result, "</li>");
    Assert.Equal(openCount, closeCount);
  }

  [Fact]
  public void ToHtml_TwoLevelNestedList_MatchingLiCount()
  {
    var md = """
        - A
          - B
        """;
    var result = _parser.ToHtml(md);
    var openCount = CountOccurrences(result, "<li");
    var closeCount = CountOccurrences(result, "</li>");
    Assert.Equal(openCount, closeCount);
  }

  [Fact]
  public void ToHtml_DeepNestedListWithSibling_MatchingLiCount()
  {
    var md = """
        - A
          - B
            - C
        - D
        """;
    var result = _parser.ToHtml(md);
    var openCount = CountOccurrences(result, "<li");
    var closeCount = CountOccurrences(result, "</li>");
    Assert.Equal(openCount, closeCount);
  }

  #endregion

  #region Ordered Lists

  [Fact]
  public void ToHtml_OrderedList_ReturnsOlWithLi()
  {
    var result = _parser.ToHtml("1. First\n2. Second\n3. Third");
    Assert.Contains("<ol>", result);
    Assert.Contains("<li>First</li>", result);
    Assert.Contains("<li>Second</li>", result);
    Assert.Contains("<li>Third</li>", result);
    Assert.Contains("</ol>", result);
  }

  [Fact]
  public void ToHtml_OrderedListWithInlineItalic_EmitsNestedHtml()
  {
    var result = _parser.ToHtml("1. *italic item*");
    Assert.Contains("<li><i>italic item</i></li>", result);
  }

  [Fact]
  public void ToHtml_OrderedListThenParagraph_ProperlyTerminates()
  {
    var result = _parser.ToHtml("1. Item\n\nParagraph");
    Assert.Contains("</ol>", result);
    Assert.Contains("<p>Paragraph</p>", result);
  }

  [Fact]
  public void ToHtml_OrderedList_NonOneStart_ParsesCorrectly()
  {
    var result = _parser.ToHtml("3. Third\n4. Fourth");
    Assert.Contains("<ol>", result);
    Assert.Contains("<li>Third</li>", result);
    Assert.Contains("<li>Fourth</li>", result);
    Assert.Contains("</ol>", result);
  }

  #endregion

  #region Fenced Code Blocks

  [Fact]
  public void ToHtml_FencedCodeBlock_ReturnsPreCode()
  {
    var result = _parser.ToHtml("```\ncode block\n```");
    Assert.Contains("<pre><code>code block\n</code></pre>", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlockWithLanguage_AddsLanguageClass()
  {
    var result = _parser.ToHtml("```csharp\nvar x = 1;\n```");
    Assert.Contains("<pre><code class=\"language-csharp\">var x = 1;\n</code></pre>", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlock_ContentNotInlineParsed()
  {
    var result = _parser.ToHtml("```\n**not bold**\n```");
    Assert.Contains("<pre><code>**not bold**\n</code></pre>", result);
    Assert.DoesNotContain("<b>", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlockWithLeadingSpaces_ParsesCorrectly()
  {
    var result = _parser.ToHtml("```\n  indented code\n```");
    Assert.Contains("  indented code", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlock_MultiLine()
  {
    var result = _parser.ToHtml("```\nline1\nline2\nline3\n```");
    Assert.Contains("line1\nline2\nline3", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlock_LanguageWithSpecialChars_EncodesHtml()
  {
    var result = _parser.ToHtml("```javascript\\\"><script>alert(1)</script>\ncode\n```");
    Assert.DoesNotContain("<script>", result);
    Assert.DoesNotContain("onclick", result);
    Assert.Contains("&quot;", result);
    Assert.Contains("&gt;", result);
    Assert.Contains("&lt;", result);
  }

  [Fact]
  public void ToHtml_FencedCodeBlock_IndentedClosingFence_DoesNotClose()
  {
    var result = _parser.ToHtml("```\ncode\n  ```");
    Assert.Contains("  ```", result);
  }

  #endregion

  #region Blockquotes

  [Fact]
  public void ToHtml_Blockquote_ReturnsBlockquote()
  {
    var result = _parser.ToHtml("> quoted text");
    Assert.Contains("<blockquote>quoted text", result);
    Assert.Contains("</blockquote>", result);
  }

  [Fact]
  public void ToHtml_BlockquoteWithInlineBold_EmitsNestedHtml()
  {
    var result = _parser.ToHtml("> **bold quote**");
    Assert.Contains("<blockquote><b>bold quote</b>", result);
  }

  [Fact]
  public void ToHtml_MultiLineBlockquote_JoinsContent()
  {
    var result = StripNewlines(_parser.ToHtml("> Line one\n> Line two"));
    Assert.Contains("<blockquote>Line one Line two", result);
  }

  #endregion

  #region Horizontal Rules

  [Fact]
  public void ToHtml_HorizontalRuleDashes_ReturnsHr()
  {
    var result = _parser.ToHtml("---");
    Assert.Contains("<hr />", result);
  }

  [Fact]
  public void ToHtml_HorizontalRuleAsterisks_ReturnsHr()
  {
    var result = _parser.ToHtml("***");
    Assert.Contains("<hr />", result);
  }

  [Fact]
  public void ToHtml_HorizontalRuleUnderscores_ReturnsHr()
  {
    var result = _parser.ToHtml("___");
    Assert.Contains("<hr />", result);
  }

  [Fact]
  public void ToHtml_HorizontalRuleWithSpaces_ReturnsHr()
  {
    var result = _parser.ToHtml("- - -");
    Assert.Contains("<hr />", result);
  }

  [Fact]
  public void ToHtml_TwoDashes_NotHorizontalRule()
  {
    var result = _parser.ToHtml("--");
    Assert.DoesNotContain("<hr />", result);
  }

  #endregion

  #region Composite Document

  [Fact]
  public void ToHtml_FullDocument_EmitsCompleteHtml()
  {
    var md = """
        # Release Notes

        ## Version 2.0

        Here are the **highlights** of this *release*:

        - New feature ***alpha***
        - Bug fixes for `critical` issues
        - [Learn more](https://example.com)

        ### Details

        > This is a quote about the release.

        ```csharp
        Console.WriteLine("Hello");
        ```

        1. First step
        2. Second step

        ---

        That's all!
        """;

    var result = _parser.ToHtml(md);

    Assert.Contains("<h1>Release Notes</h1>", result);
    Assert.Contains("<h2>Version 2.0</h2>", result);
    Assert.Contains("<h3>Details</h3>", result);
    Assert.Contains("<p>Here are the <b>highlights</b> of this <i>release</i>:</p>", StripNewlines(result));
    Assert.Contains("<li>New feature <b><i>alpha</i></b></li>", result);
    Assert.Contains("<li>Bug fixes for <code>critical</code> issues</li>", result);
    Assert.Contains("<a href=\"https://example.com\">Learn more</a>", result);
    Assert.Contains("<blockquote>This is a quote about the release.</blockquote>", StripNewlines(result));
    Assert.Contains("<pre><code class=\"language-csharp\">", result);
    Assert.Contains("<li>First step</li>", result);
    Assert.Contains("<li>Second step</li>", result);
    Assert.Contains("<hr />", result);
    Assert.Contains("<p>That&#39;s all!</p>", result);
  }

  #endregion

  #region HTML Encoding

  [Fact]
  public void ToHtml_LessThan_GetsEncoded()
  {
    var result = _parser.ToHtml("a < b");
    Assert.Contains("a &lt; b", result);
  }

  [Fact]
  public void ToHtml_GreaterThan_GetsEncoded()
  {
    var result = _parser.ToHtml("a > b");
    Assert.Contains("a &gt; b", result);
  }

  [Fact]
  public void ToHtml_Ampersand_GetsEncoded()
  {
    var result = _parser.ToHtml("a & b");
    Assert.Contains("a &amp; b", result);
  }

  [Fact]
  public void ToHtml_Quote_GetsEncoded()
  {
    var result = _parser.ToHtml("\"quote\"");
    Assert.Contains("&quot;quote&quot;", result);
  }

  #endregion

  #region Determinism

  [Fact]
  public void ToHtml_RepeatedCalls_ProduceSameResult()
  {
    var first = _parser.ToHtml("# Hello\n\nWorld");
    var second = _parser.ToHtml("# Hello\n\nWorld");
    Assert.Equal(first, second);
  }

  #endregion

  private static string StripNewlines(string s) => s.Replace("\n", "");
  private static int CountOccurrences(string text, string value) => (text.Length - text.Replace(value, "").Length) / value.Length;
}
