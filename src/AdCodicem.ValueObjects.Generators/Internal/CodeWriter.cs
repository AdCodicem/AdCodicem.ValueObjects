using System;
using System.Text;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// A minimal indentation-aware text writer used to emit generated sources.
/// </summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new(capacity: 8 * 1024);
    private int _indent;
    private bool _atLineStart = true;

    public CodeWriter Line()
    {
        _builder.Append('\n');
        _atLineStart = true;
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

            WriteIndent();
            _builder.Append(segment).Append('\n');
            _atLineStart = true;

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

    public IDisposable Block(string text)
    {
        Open(text);
        return new Closer(this);
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

    private void WriteIndent()
    {
        if (!_atLineStart)
        {
            return;
        }

        _builder.Append(' ', _indent * 4);
        _atLineStart = false;
    }

    private sealed class Closer : IDisposable
    {
        private readonly CodeWriter _writer;

        public Closer(CodeWriter writer) => _writer = writer;

        public void Dispose() => _writer.Close();
    }
}
