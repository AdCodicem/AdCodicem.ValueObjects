using System.Text.Json;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Blazor;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// The <c>pattern</c> attribute the Blazor package writes for a .NET pattern, pinned in <c>EcmaScriptPatterns.json</c>,
/// whose texts .NET is held to here and a JavaScript engine in ci.yml's documentation site job
/// (<c>.github/scripts/ecmascript-patterns.mjs</c>): a pattern is written only where a browser reads it as .NET does.
/// </summary>
public sealed class EcmaScriptPatternTests
{
    private static readonly IReadOnlyList<Entry> Table = JsonSerializer.Deserialize<List<Entry>>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Forms", "EcmaScriptPatterns.json")),
        JsonSerializerOptions.Web)!;

    public static TheoryData<string> Names => [.. Table.Select(static entry => entry.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void A_pattern_is_written_as_the_table_pins_it_and_dotnet_reads_its_texts_as_the_table_says(string name)
    {
        var entry = Table.Single(entry => entry.Name == name);

        EcmaScriptPattern.TryWrite(entry.Pattern, out var written).Should().Be(entry.Written is not null);
        written.Should().Be(entry.Written);

        var regex = new Regex(entry.Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        entry.Accepted.Should().OnlyContain(text => regex.IsMatch(text), "{0} accepts them", entry.Pattern);
        entry.Refused.Should().NotContain(text => regex.IsMatch(text), "{0} refuses them", entry.Pattern);
    }

    /// <summary>A letter or a word character is listed as the characters .NET matches it with, too many to pin.</summary>
    [Theory]
    [InlineData(@"^\w+$", "_")]
    [InlineData(@"^\p{L}+$", @"\u{3A3}")]
    [InlineData(@"^[\p{Lu}\d]+$", @"\u{FF10}-\u{FF19}")]
    public void A_large_category_is_listed_too(string pattern, string listed)
    {
        EcmaScriptPattern.TryWrite(pattern, out var written).Should().BeTrue();

        written.Should().StartWith("^[").And.Contain(listed).And.NotContain(@"\p").And.NotContain(@"\w").And.NotContain(@"\d");
        EcmaScriptPattern.TryWrite(pattern, out var again).Should().BeTrue();
        again.Should().Be(written);
    }

    /// <summary>An entry of the table.</summary>
    /// <param name="Name">What the entry shows.</param>
    /// <param name="Pattern">The .NET pattern.</param>
    /// <param name="Written">The attribute written for it, or <see langword="null"/> for none.</param>
    /// <param name="Accepted">Texts .NET accepts, which a browser must accept too.</param>
    /// <param name="Refused">Texts .NET refuses, which a browser must refuse too.</param>
    private sealed record Entry(string Name, string Pattern, string? Written, string[] Accepted, string[] Refused);
}
