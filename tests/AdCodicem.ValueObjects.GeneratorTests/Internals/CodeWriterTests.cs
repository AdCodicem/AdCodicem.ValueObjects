using AdCodicem.ValueObjects.Generators.Internal;

namespace AdCodicem.ValueObjects.GeneratorTests.Internals;

/// <summary>
/// The writer the emitters build every generated file with.
/// </summary>
/// <remarks>
/// No emitter hands it text spanning several lines: author text is escaped or folded onto one line before it
/// gets there. Splitting such text is what keeps a block of generated code indented, so it is tested directly.
/// </remarks>
public sealed class CodeWriterTests
{
    [Fact]
    public void Text_spanning_several_lines_is_indented_line_by_line()
    {
        var text = new CodeWriter().Indent().Line("first\r\nsecond\nthird").ToString();

        text.Should().Be("    first\n    second\n    third\n");
    }
}
