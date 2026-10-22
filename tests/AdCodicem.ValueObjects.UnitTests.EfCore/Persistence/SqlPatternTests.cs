using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The writer of a .NET pattern in the dialects of PostgreSQL's <c>~</c> and SQL Server 2025's <c>REGEXP_LIKE</c>, which
/// a CHECK constraint matches a value with: what it writes for each, and what it refuses because the database would
/// read it apart from .NET. The integration suite runs the written patterns against both databases, on the same values
/// as .NET.
/// </summary>
public sealed class SqlPatternTests
{
    /// <summary>Gets the patterns the writer writes, with what it writes for PostgreSQL and for SQL Server 2025.</summary>
    public static TheoryData<string, string, string> Written => new()
    {
        { "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", @"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\n?$", @"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\n?$" },
        { @"^\+[0-9]{6,15}$", @"^\+[0-9]{6,15}\n?$", @"^\+[0-9]{6,15}\n?$" },
        { "^[a-z0-9]+(?:-[a-z0-9]+)*$", @"^[a-z0-9]+(?:-[a-z0-9]+)*\n?$", @"^[a-z0-9]+(?:-[a-z0-9]+)*\n?$" },
        { "^a|b$", @"^a|b\n?$", @"^a|b\n?$" },
        { "^(?:a|b)$", @"^(?:a|b)\n?$", @"^(?:a|b)\n?$" },
        { @"\Aab\z|^cd\Z", @"^ab$|^cd\n?$", @"^ab$|^cd\n?$" },
        { @"^[\]\[\\\-\^]$", @"^[\]\[\\\-\^]\n?$", @"^[\]\[\\\-\^]\n?$" },
        { @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\$", @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\\n?$", @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\\n?$" },
        { "^ !\"#%&',/:;<=>@_`~}$", "^ !\"#%&',/:;<=>@_`~\\}\\n?$", "^ !\"#%&',/:;<=>@_`~\\}\\n?$" },
        { @"^\u00E9\x41\t$", @"^\u00E9A\u0009\n?$", @"^\x{E9}A\x{9}\n?$" },
        { "^é[à-ÿ]+$", @"^\u00E9[\u00E0-\u00FF]+\n?$", @"^\x{E9}[\x{E0}-\x{FF}]+\n?$" },
        { "^a+?b*?c??d{2,3}?e{2,}?f{4}$", @"^a+b*c?d{2,3}e{2,}f{4}\n?$", @"^a+b*c?d{2,3}e{2,}f{4}\n?$" },
        { "^(?<year>[0-9]{4})-(?:[0-9]{2})(x|y)$", @"^(?:[0-9]{4})-(?:[0-9]{2})(?:x|y)\n?$", @"^(?:[0-9]{4})-(?:[0-9]{2})(?:x|y)\n?$" },
        { "^x{0,255}$", @"^x{0,255}\n?$", @"^x{0,255}\n?$" },
        { "^$", @"^\n?$", @"^\n?$" },
        { "abc", "abc", "abc" },
        { "^(?:a{0,10}){0,10}$", @"^(?:a{0,10}){0,10}\n?$", @"^(?:a{0,10}){0,10}\n?$" },
    };

    /// <summary>
    /// Gets the patterns the writer refuses in both dialects, each with the start of the construct it names.
    /// </summary>
    public static TheoryData<string, string> Refused => new()
    {
        { @"^\d{3}$", @"\d, which .NET reads over Unicode" },
        { @"^(?:a|\d)$", @"\d, which .NET reads over Unicode" },
        { @"^[\w]+$", @"\w, which .NET reads over Unicode" },
        { @"^\p{Lu}$", @"\p{Lu}, which .NET reads over Unicode" },
        { @"^\S+$", @"\S, which .NET reads over Unicode" },
        { "^.{2}$", ". (any character)" },
        { @"^[^@\s]+@[^@\s]+$", "a negated class" },
        { "^[a-z-[aeiou]]$", "a class subtraction" },
        { @"\bword\b", "a word boundary" },
        { @"\Bx", "a word boundary" },
        { "a$b", "a $ or a \\Z anywhere but at the end of the pattern or of one of its top-level alternatives" },
        { @"(?:ab\Z)c", "a $ or a \\Z anywhere but at the end of the pattern or of one of its top-level alternatives" },
        { "^(?:a$)", "a $ or a \\Z anywhere but at the end of the pattern or of one of its top-level alternatives" },
        { @"^[\uD800-\uDBFF]$", "a range reaching the surrogates" },
        { @"^[a-\uFFFF]$", "a range reaching the surrogates" },
        { @"^\uD83D\uDE00$", "half of a surrogate pair" },
        { @"^[\uDC00]$", "half of a surrogate pair" },
        { "(?=a)a", "a construct only .NET reads" },
        { "^(a", "a construct only .NET reads" },
        { "^x{1001}$", "a count above" },
        { "^(?:a?b?c?d?e?f?g?h?){200}$", "repetitions which, written out, match more than 1000 characters" },
        { "^(?:(?:a){100}){100}$", "repetitions which, written out, match more than 1000 characters" },
        { "^(?:a{10}){101}$", "repetitions which, written out, match more than 1000 characters" },
        { "^(?:a{0,40}){0,30}$", "repetitions which, written out, match more than 1000 characters" },
        { "^(?:ab|cd|ef){0,200}$", "repetitions which, written out, match more than 1000 characters" },
        { "^(?:a{4,}){251}$", "repetitions which, written out, match more than 1000 characters" },
        { "^a{250}b{250}c{250}d{250}e$", "repetitions which, written out, match more than 1000 characters" },
    };

    [Theory]
    [MemberData(nameof(Written))]
    public void A_pattern_the_databases_read_as_dotnet_does_is_written_in_each_dialect(string pattern, string postgreSql, string sqlServer)
    {
        Write(pattern, ValueObjectPatternDialect.PostgreSql).Should().Be(postgreSql);
        Write(pattern, ValueObjectPatternDialect.SqlServer2025).Should().Be(sqlServer);
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_pattern_a_database_would_read_apart_is_refused_with_the_construct_named(string pattern, string construct)
    {
        foreach (var dialect in (ValueObjectPatternDialect[])[ValueObjectPatternDialect.PostgreSql, ValueObjectPatternDialect.SqlServer2025])
        {
            SqlPattern.TryWrite(pattern, dialect, out var written, out var refusal).Should().BeFalse();

            written.Should().BeNull();
            refusal.Should().StartWith(construct);
        }
    }

    /// <summary>
    /// PostgreSQL repeats a node 255 times at most, SQL Server's RE2 1000 times: a count between the two is written for
    /// SQL Server only, each refusal naming its dialect's maximum.
    /// </summary>
    [Fact]
    public void Each_dialect_takes_counts_up_to_its_own_maximum()
    {
        Write("^x{256}$", ValueObjectPatternDialect.SqlServer2025).Should().Be(@"^x{256}\n?$");
        Write("^x{0,1000}$", ValueObjectPatternDialect.SqlServer2025).Should().Be(@"^x{0,1000}\n?$");

        SqlPattern.TryWrite("^x{256}$", ValueObjectPatternDialect.PostgreSql, out _, out var postgreSql).Should().BeFalse();
        postgreSql.Should().Be("a count above 255, the most the database repeats");
        SqlPattern.TryWrite("^x{2,1001}$", ValueObjectPatternDialect.SqlServer2025, out _, out var sqlServer).Should().BeFalse();
        sqlServer.Should().Be("a count above 1000, the most the database repeats");
    }

    /// <summary>
    /// A pattern written means, in .NET, what it meant before, but for <c>$</c> and <c>\Z</c>, which the databases match at
    /// the end alone and which the writer spells <c>\n?$</c>: read as .NET reads it, under the same anchors, the written
    /// pattern accepts the same values.
    /// </summary>
    [Theory]
    [MemberData(nameof(Written))]
    public void A_pattern_written_matches_in_dotnet_what_the_pattern_it_was_written_from_matches(string pattern, string postgreSql, string sqlServer)
    {
        _ = postgreSql;

        // RE2's \x{…} is .NET's \u…, and .NET's $ is the databases' $ once no line feed may follow it.
        var dotnet = new Regex(
            Regex.Replace(sqlServer.Replace("$", @"\z", StringComparison.Ordinal).Replace(@"\\z", @"\$", StringComparison.Ordinal), @"\\x\{([0-9A-F]+)\}", static hex => $@"\u{Convert.ToInt32(hex.Groups[1].Value, 16):X4}"),
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        var original = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        string[] candidates =
        [
            string.Empty, "\n", "a", "b", "ab", "cd", "ab\n", "cd\n", "xab", "FR7630006000011234567890189",
            "FR7630006000011234567890189\n", "+33612345678", "my-slug-1", "my--slug", "]", "[", "\\", "-", "^",
            "$.^|?*+()[]{}\\", " !\"#%&',/:;<=>@_`~}", "\u00E9A\t", "\u00E9\u00E0\u00FF", "abbccdd", "abbccddeffff",
            "aaddeeffff", "2024-01x", "2024-01z", "abc", "xabcx", new string('x', 255), new string('x', 256),
            new string('a', 100), "\uD83D\uDE00",
        ];

        foreach (var candidate in candidates)
        {
            dotnet.IsMatch(candidate).Should().Be(original.IsMatch(candidate), "'{0}' and '{1}' both read '{2}'", pattern, sqlServer, candidate);
        }
    }

    private static string Write(string pattern, ValueObjectPatternDialect dialect)
    {
        SqlPattern.TryWrite(pattern, dialect, out var written, out var refusal).Should().BeTrue(refusal);

        return written!;
    }
}
