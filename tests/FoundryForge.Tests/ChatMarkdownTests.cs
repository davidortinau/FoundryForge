using FoundryForge.Core.Chat;
using Xunit;

namespace FoundryForge.Tests;

public class ChatMarkdownTests
{
    [Fact]
    public void Headings_lists_emphasis_render()
    {
        var r = ChatMarkdown.Render("# Title\n\n- one\n- two\n\n**bold** and `code`");
        Assert.Contains("<h1", r.Html);
        Assert.Contains("<li>one</li>", r.Html);
        Assert.Contains("<strong>bold</strong>", r.Html);
        Assert.Contains("<code>code</code>", r.Html);
    }

    [Fact]
    public void Fenced_block_extracted_with_exact_code_and_language()
    {
        var md = "Here:\n\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```\n";
        var r = ChatMarkdown.Render(md);

        Assert.Single(r.CodeBlocks);
        Assert.Equal("csharp", r.CodeBlocks[0].Language);
        Assert.Equal("var x = 1;\nConsole.WriteLine(x);", r.CodeBlocks[0].Code);
    }

    [Fact]
    public void Raw_html_is_encoded_not_executed()
    {
        var r = ChatMarkdown.Render("hello <script>alert('x')</script> world");
        // DisableHtml => the raw tag is encoded, never emitted as a live <script> element.
        Assert.DoesNotContain("<script>", r.Html);
        Assert.Contains("&lt;script&gt;", r.Html);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JavaScript:alert(1))")]
    [InlineData("![x](javascript:alert(2))")]
    [InlineData("[click](vbscript:msgbox(1))")]
    [InlineData("[click](file:///etc/passwd)")]
    public void Dangerous_url_schemes_are_neutralized(string md)
    {
        var r = ChatMarkdown.Render(md);
        Assert.DoesNotContain("javascript:", r.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vbscript:", r.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file:", r.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safe_schemes_and_relative_urls_are_preserved()
    {
        Assert.Contains("https://ok.com", ChatMarkdown.Render("[a](https://ok.com)").Html);
        Assert.Contains("mailto:a@b.com", ChatMarkdown.Render("[a](mailto:a@b.com)").Html);
        Assert.Contains("/docs/page", ChatMarkdown.Render("[a](/docs/page)").Html);
    }
}
