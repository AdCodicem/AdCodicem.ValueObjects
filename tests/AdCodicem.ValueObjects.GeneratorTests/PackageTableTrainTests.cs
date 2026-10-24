using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Holds the support tables of <c>README.md</c> and <c>website/docs/packages.md</c> to the packages that ship: one row
/// for each, whose Train cell names the release train <c>src/release-trains.json</c> gives it.
/// </summary>
/// <remarks>
/// <para>
/// A consumer references every package of a train at that train's version
/// (<c>docs/adr/0012-version-the-packages-in-two-trains-independently-of-the-frameworks.md</c>), and the Train column is
/// where they read which train a package rides. A project moved into the core's list, or a package added under
/// <c>src/</c>, changes neither table by itself: these tests make both follow.
/// </para>
/// <para>
/// A package is a project of <c>src/*/</c> that does not set <c>IsPackable</c> to <c>false</c> over the default
/// <c>src/Directory.Build.props</c> gives it, as the generator and the code fixes do, which the main package carries
/// inside it. <c>.github/scripts/package-ids.sh</c> lists the same set through MSBuild; this reads the project files,
/// so that a pull request changing only the documentation runs it without evaluating a project.
/// </para>
/// </remarks>
[Trait("Reads", "Documentation")]
public sealed partial class PackageTableTrainTests
{
    /// <summary>The files holding a support table, under their <c>## Supported frameworks</c> heading.</summary>
    public static TheoryData<string> Tables => ["README.md", "website/docs/packages.md"];

    [Theory]
    [MemberData(nameof(Tables))]
    public void Every_row_names_the_train_its_package_rides(string table)
    {
        var packages = Packages();

        var wrong = Rows(table)
            .Where(row => packages.TryGetValue(row.Package, out var train) && !string.Equals(row.Train, train, StringComparison.Ordinal))
            .Select(row => $"{row.Package} (`{row.Train}`, where src/release-trains.json puts it in `{packages[row.Package]}`)")
            .ToList();

        wrong.Should().BeEmpty("the Train column of {0} must give each package the train it is released in: {1}", table, string.Join(", ", wrong));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void Every_package_has_exactly_one_row(string table)
    {
        var rows = Rows(table);

        var miscounted = Packages().Keys
            .Select(package => (Package: package, Count: rows.Count(row => string.Equals(row.Package, package, StringComparison.Ordinal))))
            .Where(static found => found.Count != 1)
            .Select(static found => $"{found.Package} ({found.Count} rows)")
            .ToList();

        miscounted.Should().BeEmpty("the support table of {0} must list each package once: {1}", table, string.Join(", ", miscounted));
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void No_row_names_a_package_that_does_not_ship(string table)
    {
        var packages = Packages();

        var unknown = Rows(table)
            .Select(static row => row.Package)
            .Where(package => !packages.ContainsKey(package))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unknown.Should().BeEmpty("no project under src/ packs these packages the support table of {0} lists: {1}", table, string.Join(", ", unknown));
    }

    /// <summary>The rows of the file's support table, as (package, train), read off the columns its header names.</summary>
    private static IReadOnlyList<(string Package, string Train)> Rows(string table)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), table));

        var section = Array.IndexOf(lines, "## Supported frameworks");
        section.Should().BeGreaterThanOrEqualTo(0, "{0} holds the support table under its '## Supported frameworks' heading", table);

        var header = Enumerable.Range(section + 1, lines.Length - section - 1)
            .TakeWhile(index => !lines[index].StartsWith("## ", StringComparison.Ordinal))
            .Where(index => lines[index].StartsWith("| Package |", StringComparison.Ordinal))
            .DefaultIfEmpty(-1)
            .First();
        header.Should().BeGreaterThanOrEqualTo(0, "the '## Supported frameworks' section of {0} holds a table whose first column is Package", table);

        var columns = Cells(lines[header]);
        var package = Array.IndexOf(columns, "Package");
        var train = Array.IndexOf(columns, "Train");
        train.Should().BeGreaterThanOrEqualTo(0, "the support table of {0} has a Train column", table);

        // The header, the separator under it, then every row up to the first line that is not one.
        return [.. lines.Skip(header + 2)
            .TakeWhile(static line => line.StartsWith('|'))
            .Select(Cells)
            .Select(cells =>
            {
                cells.Should().HaveCount(
                    columns.Length, "each row of the support table of {0} has a cell per column: {1}", table, string.Join(" | ", cells));
                return (cells[package].Trim('*').Trim('`'), cells[train].Trim('`'));
            })];
    }

    /// <summary>The cells of a Markdown table row, trimmed; a pipe escaped as <c>\|</c> stays inside its cell.</summary>
    private static string[] Cells(string line)
        => [.. CellSeparator().Split(line.Trim().Trim('|')).Select(static cell => cell.Trim())];

    /// <summary>Each package that ships, by its id, with the release train <c>src/release-trains.json</c> gives its project.</summary>
    private static Dictionary<string, string> Packages()
    {
        var source = Path.Combine(RepositoryRoot(), "src");

        IsPackable(XDocument.Load(Path.Combine(source, "Directory.Build.props")))
            .Should().Be("true", "src/Directory.Build.props makes every project under src/ a package unless the project opts out");

        var (named, others) = Trains(Path.Combine(source, "release-trains.json"));
        var packages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateDirectories(source).SelectMany(static directory => Directory.EnumerateFiles(directory, "*.csproj")))
        {
            var project = XDocument.Load(path);
            if (string.Equals(IsPackable(project), "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            var id = project.Descendants("PackageId").Select(static element => element.Value.Trim()).LastOrDefault() ?? name;
            packages.Add(id, named.GetValueOrDefault(name, others));
        }

        packages.Should().HaveCountGreaterThan(30, "the walk of src/ must keep finding the packages");
        return packages;
    }

    /// <summary>The value a project file gives <c>IsPackable</c>, the last one MSBuild reads, if it gives one.</summary>
    private static string? IsPackable(XDocument project)
        => project.Descendants("IsPackable").Select(static element => element.Value.Trim()).LastOrDefault();

    /// <summary>
    /// The trains of <c>src/release-trains.json</c>: the train of each project a train names, and the one train whose
    /// projects are <c>"*"</c>, which takes every other project.
    /// </summary>
    private static (Dictionary<string, string> Named, string Others) Trains(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        var others = new List<string>();
        foreach (var train in document.RootElement.EnumerateObject().Where(static property => property.Value.ValueKind == JsonValueKind.Object))
        {
            var projects = train.Value.GetProperty("projects");
            if (projects.ValueKind == JsonValueKind.String)
            {
                projects.GetString().Should().Be("*", "a train of src/release-trains.json names its projects in an array, or takes the rest with \"*\"");
                others.Add(train.Name);
                continue;
            }

            foreach (var project in projects.EnumerateArray())
            {
                named.Add(project.GetString()!, train.Name);
            }
        }

        others.Should().ContainSingle("exactly one train of src/release-trains.json takes every project no train names");
        return (named, others[0]);
    }

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

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex CellSeparator();
}
