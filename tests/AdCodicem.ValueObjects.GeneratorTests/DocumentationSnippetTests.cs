using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Runs the generator and the analyzers over every C# snippet the repository publishes: the agent skill
/// (<c>skills/value-objects/</c>), the README, and the documentation site.
/// </summary>
/// <remarks>
/// <para>
/// The authoring surface is written down in four places, and a reader cannot tell that one of them has gone
/// stale — least of all an agent, which will write exactly what it is told. The README taught
/// <c>private static NormalizeCore</c> for a while after hooks became interfaces: code that compiles, that
/// <c>VO0011</c> flags, and that the generator never calls. These tests are how that stops happening silently.
/// </para>
/// <para>
/// Two contracts, because the two kinds of text promise different things:
/// </para>
/// <list type="bullet">
/// <item>
/// A snippet in the **skill** must generate and compile cleanly: an agent copies it verbatim, so it has to be
/// real code.
/// </item>
/// <item>
/// A snippet in the **README or the site** is prose, and elides bodies on purpose. What is checked there is the
/// declaration the generator and the analyzers see — the attribute, the hook interfaces, the shape of the type
/// — with only the noise an elision makes tolerated.
/// </item>
/// </list>
/// <para>
/// A fenced block tagged <c>csharp</c> is checked. One tagged <c>csharp skip</c> is not: wiring examples name
/// packages this test project does not reference (ASP.NET Core, EF Core, Dapper, FluentValidation, the contract
/// kit), and usage fragments are statements rather than declarations.
/// </para>
/// </remarks>
public sealed partial class DocumentationSnippetTests
{
    /// <summary>
    /// The compiler errors an elided snippet makes: a name the prose left out, and a body written as a comment.
    /// Everything else — a member that does not exist on a generated type, a mismatched argument — still fails.
    /// </summary>
    private static readonly string[] ElisionNoise = ["CS0103", "CS1525"];

    /// <summary>Snippets of the skill, as (repository-relative file, index within it).</summary>
    public static TheoryData<string, int> SkillSnippets() => Enumerate(SkillFiles());

    /// <summary>Snippets of the README and the documentation site.</summary>
    public static TheoryData<string, int> DocumentationSnippets()
        => Enumerate(["README.md", .. DocumentationSite()]);

    [Theory]
    [MemberData(nameof(SkillSnippets))]
    public async Task Every_snippet_in_the_skill_generates_and_compiles(string file, int index)
    {
        var snippet = Snippets(file)[index];

        var run = GeneratorHarness.Run(snippet);

        run.Diagnostics.Should().BeEmpty(
            "the skill must not teach a declaration the generator rejects ({0}, snippet {1})", file, index);
        run.CompilationDiagnostics.Should().BeEmpty(
            "the skill must not teach code that does not compile ({0}, snippet {1})", file, index);

        await AnalyzersShouldBeSilent(snippet, file, index);
    }

    [Theory]
    [MemberData(nameof(DocumentationSnippets))]
    public async Task Every_snippet_in_the_documentation_declares_what_the_generator_accepts(string file, int index)
    {
        var snippet = Snippets(file)[index];

        var run = GeneratorHarness.Run(snippet);

        run.Diagnostics.Should().BeEmpty(
            "the documentation must not show a declaration the generator rejects ({0}, snippet {1})", file, index);

        run.CompilationDiagnostics
            .Where(diagnostic => !ElisionNoise.Contains(diagnostic.Id, StringComparer.Ordinal))
            .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")
            .Should().BeEmpty("{0}, snippet {1}", file, index);

        await AnalyzersShouldBeSilent(snippet, file, index);
    }

    [Fact]
    public void The_skill_ships_the_files_it_advertises()
    {
        File.Exists(Path.Combine(SkillDirectory(), "SKILL.md")).Should().BeTrue();

        foreach (var reference in new[] { "authoring.md", "integrations.md", "identifiers.md", "diagnostics.md" })
        {
            File.Exists(Path.Combine(SkillDirectory(), "references", reference))
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

        // The front matter drives whether an agent loads the skill at all, so it has to name the library.
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
    /// Guards the extraction itself: a regular expression that silently stopped matching would turn every other
    /// test in this file green while checking nothing.
    /// </summary>
    [Fact]
    public void Both_corpora_carry_snippets_to_check()
    {
        SkillSnippets().Should().HaveCountGreaterThan(5);
        DocumentationSnippets().Should().HaveCountGreaterThan(3);
    }

    private static async Task AnalyzersShouldBeSilent(string snippet, string file, int index)
    {
        var hooks = await GeneratorHarness.RunAnalyzerAsync<ValueObjectHookAnalyzer>(snippet);
        var uninitialized = await GeneratorHarness.RunAnalyzerAsync<UninitializedValueObjectAnalyzer>(snippet);

        // VO0011 is the one that matters here: a rule written without its interface compiles, never runs, and is
        // only a warning in a consumer's project. Published prose is exactly where that gets copied from.
        hooks.Select(diagnostic => diagnostic.Id).Should().BeEmpty("{0}, snippet {1}", file, index);
        uninitialized.Select(diagnostic => diagnostic.Id).Should().BeEmpty("{0}, snippet {1}", file, index);
    }

    private static TheoryData<string, int> Enumerate(IEnumerable<string> files)
    {
        var data = new TheoryData<string, int>();

        foreach (var file in files)
        {
            for (var index = 0; index < Snippets(file).Count; index++)
            {
                data.Add(file, index);
            }
        }

        return data;
    }

    private static IReadOnlyList<string> Snippets(string file)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), file));

        return [.. FencedBlock()
            .Matches(text)
            .Where(match => match.Groups["meta"].Value.Trim().Length == 0)
            .Select(match => match.Groups["code"].Value)];
    }

    private static IReadOnlyList<string> SkillFiles() => Relative(SkillDirectory(), "*.md");

    private static IReadOnlyList<string> DocumentationSite()
        => Relative(Path.Combine(RepositoryRoot(), "website", "docs"), "*.md");

    private static IReadOnlyList<string> Relative(string directory, string pattern)
        => [.. Directory
            .EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)];

    private static string SkillDirectory() => Path.Combine(RepositoryRoot(), "skills", "value-objects");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run from inside the repository");

        return directory!.FullName;
    }

    [GeneratedRegex(@"^```csharp(?<meta>[^\r\n]*)\r?\n(?<code>.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex FencedBlock();
}
