using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Compiles every C# snippet shipped in the agent skill (<c>skills/value-objects/</c>).
/// </summary>
/// <remarks>
/// <para>
/// The skill is a fourth place where the authoring surface is written down, after the README, CLAUDE.md and the
/// documentation site, and the only one whose reader cannot notice that it has gone stale: an agent will write
/// exactly what it is told. So the snippets are compiled here, through the same harness the generator tests
/// use, and a snippet that no longer generates — or that trips an analyzer — fails the build.
/// </para>
/// <para>
/// A fenced block tagged <c>csharp</c> is compiled. One tagged <c>csharp skip</c> is not: wiring examples name
/// packages this test project does not reference (ASP.NET Core, EF Core, Dapper, FluentValidation), and they
/// are prose about a composition root rather than a value object declaration.
/// </para>
/// </remarks>
public sealed partial class SkillDocumentationTests
{
    /// <summary>Snippets that must compile, as (file relative to the skill directory, index within it).</summary>
    public static TheoryData<string, int> CompilableSnippets()
    {
        var data = new TheoryData<string, int>();

        foreach (var file in SkillFiles())
        {
            for (var index = 0; index < Snippets(file).Count; index++)
            {
                data.Add(file, index);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CompilableSnippets))]
    public async Task Every_snippet_in_the_skill_generates_and_compiles(string file, int index)
    {
        var snippet = Snippets(file)[index];

        var run = GeneratorHarness.Run(snippet);

        run.Diagnostics.Should().BeEmpty(
            "the skill must not teach a declaration the generator rejects ({0}, snippet {1})", file, index);
        run.CompilationDiagnostics.Should().BeEmpty(
            "the skill must not teach code that does not compile ({0}, snippet {1})", file, index);

        var hooks = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>(snippet);
        var uninitialized = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(snippet);

        // VO0011 is the one that matters here: a rule written without its interface compiles, never runs, and
        // is only a warning in a consumer's project. The skill is exactly where that mistake would be copied.
        hooks.Select(diagnostic => diagnostic.Id).Should().BeEmpty("{0}, snippet {1}", file, index);
        uninitialized.Select(diagnostic => diagnostic.Id).Should().BeEmpty("{0}, snippet {1}", file, index);
    }

    [Fact]
    public void The_skill_ships_the_files_it_advertises()
    {
        var directory = SkillDirectory();

        File.Exists(Path.Combine(directory, "SKILL.md")).Should().BeTrue();

        foreach (var reference in new[] { "authoring.md", "integrations.md", "identifiers.md", "diagnostics.md" })
        {
            File.Exists(Path.Combine(directory, "references", reference))
                .Should().BeTrue("SKILL.md points readers at references/{0}", reference);
        }
    }

    [Fact]
    public void The_skill_declares_a_name_and_a_description()
    {
        var text = File.ReadAllText(Path.Combine(SkillDirectory(), "SKILL.md"));

        text.Should().StartWith("---", "a skill is recognized by its YAML front matter");
        text.Should().Contain("\nname: value-objects\n");
        text.Should().Contain("\ndescription: ");

        // The front matter drives when an agent loads the skill at all, so it has to name the library and the
        // diagnostics someone would be searching with.
        text.Should().Contain("AdCodicem.ValueObjects");
    }

    /// <summary>
    /// Keeps the entry point short enough to be worth loading, as the skill format expects.
    /// </summary>
    [Fact]
    public void The_entry_point_stays_under_the_line_budget()
    {
        var lines = File.ReadAllLines(Path.Combine(SkillDirectory(), "SKILL.md"));

        lines.Length.Should().BeLessThan(500, "detail belongs in references/, which is loaded on demand");
    }

    /// <summary>
    /// Guards the extraction itself: a regular expression that silently stops matching would turn every other
    /// test in this file green while checking nothing.
    /// </summary>
    [Fact]
    public void The_skill_carries_snippets_to_compile()
    {
        CompilableSnippets().Should().HaveCountGreaterThan(5);
    }

    private static IReadOnlyList<string> SkillFiles()
    {
        var directory = SkillDirectory();

        return [.. Directory
            .EnumerateFiles(directory, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static IReadOnlyList<string> Snippets(string file)
    {
        var text = File.ReadAllText(Path.Combine(SkillDirectory(), file));

        return [.. FencedBlock()
            .Matches(text)
            .Where(match => match.Groups["meta"].Value.Trim().Length == 0)
            .Select(match => match.Groups["code"].Value)];
    }

    private static string SkillDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");

        return Path.Combine(directory!.FullName, "skills", "value-objects");
    }

    [GeneratedRegex(@"^```csharp(?<meta>[^\r\n]*)\r?\n(?<code>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedBlock();
}
