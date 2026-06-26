using System.Text;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace FoundryStudio.Core.Chat;

/// <summary>One fenced code block's exact raw code and language, for the UI Copy control (US1.4).</summary>
public readonly record struct CodeBlock(string Language, string Code);

/// <summary>Sanitized HTML plus the raw code blocks extracted from the source markdown.</summary>
public sealed record RenderedMarkdown(string Html, IReadOnlyList<CodeBlock> CodeBlocks);

/// <summary>
/// Renders assistant markdown to <b>sanitized</b> HTML and extracts fenced code blocks (US1, R1). Model
/// output is UNTRUSTED. Two defenses: (1) <c>.DisableHtml()</c> encodes raw HTML; (2) link/image URLs are
/// scheme-filtered so <c>javascript:</c>/<c>vbscript:</c>/<c>file:</c> etc. cannot execute in the
/// BlazorWebView interop context (FR-005). Pure; deterministic; dylib-free.
/// </summary>
public static class ChatMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAdvancedExtensions()
        .Build();

    private static readonly HashSet<string> SafeSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto"
    };

    public static RenderedMarkdown Render(string markdown)
    {
        markdown ??= string.Empty;

        var document = Markdown.Parse(markdown, Pipeline);

        // Neutralize dangerous URL schemes on every link/image before rendering (XSS defense).
        foreach (var node in document.Descendants())
        {
            if (node is LinkInline link && !IsSafeUrl(link.Url, link.IsImage))
            {
                link.Url = string.Empty;
            }
        }

        var blocks = new List<CodeBlock>();
        foreach (var node in document.Descendants())
        {
            if (node is FencedCodeBlock fenced)
            {
                blocks.Add(new CodeBlock(
                    Language: fenced.Info ?? string.Empty,
                    Code: ExtractCode(fenced)));
            }
        }

        // Render the (sanitized) document — NOT the raw markdown — so the scheme filtering takes effect.
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        return new RenderedMarkdown(writer.ToString(), blocks);
    }

    /// <summary>
    /// Allow only safe schemes. Relative URLs (no scheme) are safe; <c>data:</c> is allowed for images only.
    /// Control chars are stripped first so obfuscated schemes (e.g. "java\tscript:") can't slip through.
    /// </summary>
    private static bool IsSafeUrl(string? url, bool isImage)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        var cleaned = new string(url.Where(c => !char.IsControl(c)).ToArray()).Trim();
        var colon = cleaned.IndexOf(':');
        if (colon < 0)
        {
            return true; // relative, no scheme
        }

        var firstDelim = cleaned.IndexOfAny(new[] { '/', '?', '#' });
        if (firstDelim >= 0 && firstDelim < colon)
        {
            return true; // ':' is in a path/query, not a scheme (e.g. "/a:b")
        }

        var scheme = cleaned[..colon];
        if (isImage && scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SafeSchemes.Contains(scheme);
    }

    private static string ExtractCode(FencedCodeBlock block)
    {
        var sb = new StringBuilder();
        var lines = block.Lines.Lines;
        for (var i = 0; i < block.Lines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            sb.Append(lines[i].Slice.ToString());
        }

        return sb.ToString();
    }
}

