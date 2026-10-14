using static AdCodicem.ValueObjects.GeneratorTests.LoggingSnippets;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What Microsoft.Gen.Logging does, at the version <c>Directory.Packages.props</c> pins, that <c>VO0045</c> and
/// <c>VO0046</c> rest on.
/// </summary>
/// <remarks>
/// Each of these pins a behaviour of the logging generator, most of them a defect. When an update of
/// Microsoft.Extensions.Telemetry.Abstractions makes one fail, the logging generator now reads what it did not: the
/// diagnostic resting on it can step back, and the guide's Data classification section changes with it.
/// </remarks>
public sealed class LoggingGeneratorAssumptionsTests
{
    /// <summary>A classification on a parameter sends its tag to a redactor: what the code fix writes.</summary>
    [Fact]
    public void A_classified_parameter_goes_to_a_redactor()
        => Logged("[PersonalData] RefEmail email").Classified.Should().Equal("email <PersonalDataAttribute>");

    /// <summary>
    /// A classification on the type of a value logged whole is never read, the value object of another assembly passed as
    /// it is: when this fails, <c>VO0045</c> can step back.
    /// </summary>
    [Fact]
    public void A_classification_on_the_type_alone_is_not_read()
    {
        var tags = Logged("RefEmail email, Reference<PurchaseOrder> reference, Salary salary");

        tags.Plain.Should().Equal("email", "reference", "salary");
        tags.Classified.Should().BeEmpty();
    }

    /// <summary>
    /// A value object of the project the logging method is declared in is passed through its <c>ToString()</c>: the
    /// logging generator sees none of the members the generator writes.
    /// </summary>
    [Fact]
    public void A_value_object_of_the_same_project_is_logged_through_its_text()
    {
        var run = GeneratorHarness.Run(Header + """
            [PersonalData]
            [ValueObject<string>(MaxLength = 254)]
            public readonly partial struct Email;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Email email);
            }
            """, references: WithDomain, withLoggingGenerator: true);

        run.CompilationDiagnostics.Should().BeEmpty();
        run.LoggingSource.Should().Contain("state.TagArray[0] = new(\"email\", email.ToString());");
    }

    /// <summary>
    /// <c>Transitive</c> expands a value object into its <c>Value</c>, and drops the classification of the property holding
    /// it: when this fails, <c>VO0046</c> can step back.
    /// </summary>
    [Fact]
    public void Transitive_drops_the_classification_of_a_property()
    {
        var tags = Logged("[LogProperties(Transitive = true)] RefHolderClassified holder");

        tags.Plain.Should().Contain("holder.Iban.Value");
        tags.Classified.Should().BeEmpty();
    }

    /// <summary>
    /// The one place the logging generator reads a type's classification: the properties of a type it expands, a value
    /// object of another assembly's <c>Value</c> among them.
    /// </summary>
    [Fact]
    public void An_expanded_value_object_is_classified_as_its_type()
        => Logged("[LogProperties] RefEmail email").Classified.Should().Equal("email.Value <PersonalDataAttribute>");

    /// <summary>
    /// The logging generator builds a classification as <c>new TAttribute().Classification</c>, whatever arguments the
    /// attribute carries, so a copy of one written with arguments does not compile: the code fix offers none.
    /// </summary>
    [Fact]
    public void A_classification_written_with_arguments_does_not_compile_on_a_parameter()
    {
        var run = GeneratorHarness.Run(Header + """
            public sealed class ClassifiedAttribute(string category) : DataClassificationAttribute(new DataClassification("Snippets", category));

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, [Classified("Health")] RefEmail email);
            }
            """, references: WithDomain, withLoggingGenerator: true);

        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("CS7036");
        run.CompilationDiagnostics[0].Location.SourceTree!.FilePath.Should().Contain("Microsoft.Gen.Logging");
    }

    /// <summary>
    /// The logging generator refuses a record it logs whole whose property is classified (<c>LOGGEN035</c>), and misses one
    /// whose property's type alone is: when the second is refused too, <c>VO0045</c>'s record remedy changes.
    /// </summary>
    [Fact]
    public void A_record_logged_whole_is_refused_for_a_classified_property_only()
    {
        Run("RefHolderClassified holder").CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("LOGGEN035");
        Run("RefHolder holder").CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The logging generator tells its methods by a <c>[GeneratedCode]</c> naming it, which C# merges onto the author's
    /// definition: the analysis reads a method by it, and must read generated code to see it at all.
    /// </summary>
    [Fact]
    public void Each_method_carries_the_logging_generators_name()
        => Run("RefEmail email").LoggingSource.Should().MatchRegex(
            @"\[global::System\.CodeDom\.Compiler\.GeneratedCodeAttribute\(""Microsoft\.Gen\.Logging"", ""[0-9.]+""\)\]\s+public static partial void Logged\(");

    /// <summary>
    /// <c>Transitive</c> is read on the parameter's <c>[LogProperties]</c> alone: on a property's, it expands the property
    /// one level, as <c>[LogProperties]</c> does, so a classification it holds is kept, and a value object classified on
    /// its type alone is logged whole. When this fails, <c>VO0045</c> and <c>VO0046</c> follow the property's
    /// <c>Transitive</c>.
    /// </summary>
    [Fact]
    public void Transitive_on_a_property_expands_it_one_level()
    {
        const string Declarations = """
            public sealed record Party([property: PersonalData] Iban Account, RefEmail Contact);

            public sealed record Payment([property: LogProperties(Transitive = true)] Party Payer);
            """;

        var tags = Logged(Declarations, "[LogProperties] Payment payment");

        tags.Plain.Should().Equal("payment.Payer.Contact");
        tags.Classified.Should().Equal("payment.Payer.Account <PersonalDataAttribute>");
    }

    /// <summary>
    /// The logging generator never calls the tag provider of a property, and logs the property whole, in clear; under
    /// <c>Transitive</c>, <c>[LogPropertyIgnore]</c> stops the expansion of a property, not its logging. When this fails,
    /// the analysis leaves such a property out again.
    /// </summary>
    [Fact]
    public void A_property_tag_provider_and_LogPropertyIgnore_under_Transitive_log_the_property_whole()
    {
        const string Declarations = """
            public static class Tags
            {
                public static void Add(ITagCollector collector, RefEmail email) => collector.Add("domain", email.Value.Split('@')[^1]);
            }

            public sealed class Wire
            {
                [TagProvider(typeof(Tags), nameof(Tags.Add))]
                public RefEmail Provided { get; init; }

                [LogPropertyIgnore]
                public RefEmail Ignored { get; init; }
            }
            """;

        var run = Run(Declarations, "[LogProperties] Wire wire, [LogProperties(Transitive = true)] Wire again");
        run.CompilationDiagnostics.Should().BeEmpty();

        LoggedTags.Read(run.LoggingSource).Plain.Should().Equal("wire.Provided", "again.Provided", "again.Ignored");
        run.LoggingSource.Should().NotContain("Tags.Add");
    }

    /// <summary>
    /// A record's constructor parameter classifies the property of its name, whatever their types; a class's does not.
    /// A type classified on itself, or through a base type, classifies every property its expansion logs.
    /// </summary>
    [Fact]
    public void Constructor_parameters_and_the_expanded_type_classify_its_properties()
    {
        const string Declarations = """
            public sealed record Signup
            {
                public Signup([PersonalData] string Contact) => this.Contact = RefEmail.Create(Contact);

                public RefEmail Contact { get; init; }
            }

            public sealed class Enrolment
            {
                public Enrolment([PersonalData] RefEmail contact) => Contact = contact;

                public RefEmail Contact { get; }
            }

            [PersonalData]
            public class Person
            {
                public Iban Account { get; init; }
            }

            public sealed class Profile : Person;
            """;

        var tags = Logged(Declarations, "[LogProperties] Signup signup, [LogProperties] Enrolment enrolment, [LogProperties] Profile profile");

        tags.Plain.Should().Equal("enrolment.Contact");
        tags.Classified.Should().Equal("signup.Contact <PersonalDataAttribute>", "profile.Account <PersonalDataAttribute>");
    }

    /// <summary>
    /// Under <c>Transitive</c>, the logging generator expands a collection class or struct into its own properties, and
    /// writes a collection interface, an array or a collection with no property to log element by element, through
    /// <c>LoggerMessageHelper.Stringify</c>; without it, every collection a property holds is written so, but a nullable
    /// struct one, which is written as its text.
    /// </summary>
    [Fact]
    public void Collections_are_written_element_by_element_where_they_are_not_expanded()
    {
        const string Declarations = """
            public sealed record Transfer(IReadOnlyCollection<RefEmail> Counted, RefEmail[] Array, List<RefEmail> List);

            public sealed record Signup(List<RefEmail> List, System.Collections.Immutable.ImmutableArray<RefEmail>? Text);
            """;

        var run = Run(Declarations, "[LogProperties(Transitive = true)] Transfer transfer, [LogProperties] Signup signup");
        run.CompilationDiagnostics.Should().BeEmpty();

        run.LoggingSource.Should().Contain("new(\"transfer.Counted\", global::Microsoft.Extensions.Logging.LoggerMessageHelper.Stringify(transfer?.Counted));")
            .And.Contain("new(\"transfer.Array\", global::Microsoft.Extensions.Logging.LoggerMessageHelper.Stringify(transfer?.Array));")
            .And.Contain("new(\"transfer.List.Count\", transfer?.List?.Count);")
            .And.Contain("new(\"signup.List\", global::Microsoft.Extensions.Logging.LoggerMessageHelper.Stringify(signup?.List));")
            .And.Contain("new(\"signup.Text\", signup?.Text?.ToString());");
    }

    private static GeneratorRun Run(string parameters)
        => GeneratorHarness.Run(Header + $$"""
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, {{parameters}});
            }
            """, references: WithDomain, withLoggingGenerator: true);

    private static GeneratorRun Run(string declarations, string parameters)
        => GeneratorHarness.Run(Header + declarations + $$"""

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, {{parameters}});
            }
            """, references: WithDomain, withLoggingGenerator: true);

    private static LoggedTags Logged(string parameters)
    {
        var run = Run(parameters);
        run.CompilationDiagnostics.Should().BeEmpty();

        return LoggedTags.Read(run.LoggingSource);
    }

    private static LoggedTags Logged(string declarations, string parameters)
    {
        var run = Run(declarations, parameters);
        run.CompilationDiagnostics.Should().BeEmpty();

        return LoggedTags.Read(run.LoggingSource);
    }
}
