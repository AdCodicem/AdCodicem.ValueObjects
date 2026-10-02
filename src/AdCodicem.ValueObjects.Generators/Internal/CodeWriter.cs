using System.Text;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// A minimal indentation-aware text writer used to emit generated sources.
/// </summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new(capacity: 8 * 1024);
    private int _indent;

    public CodeWriter Line()
    {
        _builder.Append('\n');
        return this;
    }

    public CodeWriter Line(string text)
    {
        // Multi-line literals keep their own relative indentation, so split and re-indent each line.
        var start = 0;
        while (start <= text.Length)
        {
            var end = text.IndexOf('\n', start);
            var segment = end < 0 ? text.Substring(start) : text.Substring(start, end - start).TrimEnd('\r');

            _builder.Append(' ', _indent * 4).Append(segment).Append('\n');

            if (end < 0)
            {
                break;
            }

            start = end + 1;
        }

        return this;
    }

    public CodeWriter LineIf(bool condition, string text) => condition ? Line(text) : this;

    public CodeWriter Open(string text)
    {
        Line(text);
        Line("{");
        _indent++;
        return this;
    }

    public CodeWriter Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
        return this;
    }

    public CodeWriter Indent()
    {
        _indent++;
        return this;
    }

    public CodeWriter Unindent()
    {
        _indent--;
        return this;
    }

    public override string ToString() => _builder.ToString();
}
