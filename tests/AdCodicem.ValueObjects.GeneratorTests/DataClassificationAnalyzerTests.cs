using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using static AdCodicem.ValueObjects.GeneratorTests.LoggingSnippets;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0045</c> and <c>VO0046</c> report: a value object classified on its type that a <c>[LoggerMessage]</c>
/// method Microsoft.Gen.Logging writes logs in clear, and a classified property a <c>[LogProperties]</c> expansion writes
/// as its unclassified <c>Value</c>.
/// </summary>
/// <remarks>
/// Each snippet compiles with the generator and Microsoft.Gen.Logging, against a domain assembly declaring value objects
/// classified on their type (<see cref="LoggingSnippets.Domain"/>), and the tests read the tags the logging generator
/// writes: a diagnostic stands where a tag is written in clear, and none where it goes to a redactor.
/// </remarks>
public sealed class DataClassificationAnalyzerTests
{
    /// <summary>Value objects of the snippet's own assembly, which the logging generator sees no member of.</summary>
    private const string Local = """
        [PersonalData]
        [ValueObject<string>(MaxLength = 254)]
        public readonly partial struct Email;

        [PersonalData]
        [ValueObject<string>(MaxLength = 12)]
        public readonly partial struct Alias<TOwner>;

        public sealed class Customer;

        [PersonalData]
        [EntityId("pat")]
        public readonly partial struct PatientId;

        [ValueObject<string>(MaxLength = 30)]
        public readonly partial struct Nickname;

        """;

    private const string Remedy = "or with [NoDataClassification] to log it in clear on purpose";

    [Theory]
    [InlineData("Email email", "email", "Email", "[PersonalData]")]
    [InlineData("RefEmail email", "email", "RefEmail", "[PersonalData]")]
    [InlineData("RefEmail? email", "email", "RefEmail", "[PersonalData]")]
    [InlineData("Reference<PurchaseOrder> reference", "reference", "Reference<PurchaseOrder>", "[PersonalData]")]
    [InlineData("Alias<Customer> alias", "alias", "Alias<Customer>", "[PersonalData]")]
    [InlineData("Salary salary", "salary", "Salary", "[FinancialData]")]
    [InlineData("HandWrittenSecret secret", "secret", "HandWrittenSecret", "[PersonalData]")]
    [InlineData("RefPatientId patient", "patient", "RefPatientId", "[PersonalData]")]
    [InlineData("PatientId patient", "patient", "PatientId", "[PersonalData]")]
    [InlineData("Unknownish value", "value", "Unknownish", "[UnknownDataClassification]")]
    [InlineData("Both value", "value", "Both", "[PersonalData]")]
    [InlineData("IEnumerable<RefEmail> emails", "emails", "RefEmail", "[PersonalData]")]
    [InlineData("RefEmail[] emails", "emails", "RefEmail", "[PersonalData]")]
    [InlineData("List<RefEmail?> emails", "emails", "RefEmail", "[PersonalData]")]
    [InlineData("IReadOnlyCollection<RefEmail> emails", "emails", "RefEmail", "[PersonalData]")]
    [InlineData("System.Collections.Immutable.ImmutableArray<RefEmail>? emails", "emails", "RefEmail", "[PersonalData]")]
    public async Task A_parameter_logging_a_value_object_classified_on_its_type_is_reported_on_the_parameter(
        string parameter,
        string name,
        string type,
        string shown)
    {
        var (diagnostics, tags) = await AnalyzeAsync(Local + $$"""
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, {{parameter}});
            }
            """);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0045");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        Message(diagnostic).Should().Be(
            $"'{name}' is logged in clear: its type '{type}' is classified {shown}, which the logging generator reads on a "
            + $"parameter or a property only. Classify '{name}' with {shown}, {Remedy}.");
        Located(diagnostic).Text.Should().Be(name);
        diagnostic.Properties[DataClassificationAnalyzer.TargetKey].Should().Be(DataClassificationAnalyzer.ParameterTarget);
        diagnostic.AdditionalLocations.Should().BeEmpty();

        // What the warning is for: the tag goes to the logger as it is.
        tags.Plain.Should().Contain(name);
        tags.Classified.Should().BeEmpty();
    }

    /// <summary>
    /// An instance method and an extension-shaped one are read as a static one is, whatever the parameters around the
    /// value object.
    /// </summary>
    [Fact]
    public async Task Instance_and_extension_methods_are_read_too()
    {
        var (diagnostics, _) = await AnalyzeAsync("""
            public sealed partial class Notifier(ILogger<Notifier> logger)
            {
                private readonly ILogger _logger = logger;

                [LoggerMessage(Level = LogLevel.Information, Message = "Sent to {Email}")]
                public partial void Sent(RefEmail email, int attempts);
            }

            public static partial class Extensions
            {
                [LoggerMessage(Level = LogLevel.Warning, Message = "Bounced {Email}")]
                public static partial void Bounced(this ILogger logger, Exception error, RefEmail email);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Located(diagnostic).Line)).Should().Equal(
            ("VO0045", "public partial void Sent(RefEmail email, int attempts);"),
            ("VO0045", "public static partial void Bounced(this ILogger logger, Exception error, RefEmail email);"));
    }

    /// <summary>
    /// A classification of the parameter's own wins, whichever it is: <c>[NoDataClassification]</c> is how a classified
    /// value object is logged in clear on purpose. A tag provider decides what is logged, and nothing but a value object
    /// is read, a classified struct or class included.
    /// </summary>
    [Fact]
    public async Task What_carries_its_own_classification_or_needs_none_is_left_alone()
    {
        var (diagnostics, tags) = await AnalyzeAsync(HandWritten("Code") + """
            [PersonalData]
            public readonly struct Imposter : IValueObject<Code, string>
            {
                public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => Code.Schema;
                public string Value => string.Empty;
                public bool IsDefault => true;
                public static string Normalize(string value) => Code.Normalize(value);
                public static ValidationResult Validate(in string value) => Code.Validate(value);
                public static Code Create(string value) => Code.Create(value);
                public static bool TryCreate(string value, out Code result) => Code.TryCreate(value, out result);
                public static bool TryCreate(string value, out Code result, out ValidationResult validation) => Code.TryCreate(value, out result, out validation);
                public static Code CreateUnchecked(string value) => Code.CreateUnchecked(value);
                public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Code result, out ValidationResult validation) => Code.TryParse(text, provider, out result, out validation);
                public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Code result) => Code.TryParse(s, provider, out result);
                public static bool TryParse(string? s, IFormatProvider? provider, out Code result) => Code.TryParse(s, provider, out result);
                public static Code Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Code.Parse(s, provider);
                public static Code Parse(string s, IFormatProvider? provider) => Code.Parse(s, provider);
                public bool Equals(Code other) => other.IsDefault;
                public int CompareTo(Code other) => other.IsDefault ? 0 : -1;
                public string ToString(string? format, IFormatProvider? formatProvider) => string.Empty;
                public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return true; }
            }

            [PersonalData]
            public sealed class Account
            {
                public override string ToString() => "account";
            }

            public static class Tags
            {
                public static void Add(ITagCollector collector, RefEmail email) => collector.Add("domain", email.Value.Split('@')[^1]);
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(
                    ILogger logger,
                    [PersonalData] RefEmail personal,
                    [FinancialData] RefEmail financial,
                    [PersonalData, FinancialData] RefEmail both,
                    [NoDataClassification] RefEmail inClear,
                    [TagProvider(typeof(Tags), nameof(Tags.Add))] RefEmail provided,
                    Iban iban,
                    Quantity? quantity,
                    PublicCode code,
                    Imposter imposter,
                    Account account,
                    string text,
                    List<Iban> ibans);
            }
            """);

        diagnostics.Should().BeEmpty();
        tags.Classified.Should().Equal(
            "personal <PersonalDataAttribute>",
            "financial <FinancialDataAttribute>",
            "both <PersonalDataAttribute, FinancialDataAttribute>");
        tags.Plain.Should().Contain(["inClear", "iban", "imposter", "account"]);
        tags.Plain.Should().NotContain("provided");
    }

    /// <summary>
    /// A classification is an attribute derived from <c>DataClassificationAttribute</c> of
    /// Microsoft.Extensions.Compliance.Abstractions: one of the same name elsewhere classifies nothing.
    /// </summary>
    [Fact]
    public async Task An_attribute_named_as_the_classification_elsewhere_classifies_nothing()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public static class Elsewhere
            {
                public class DataClassificationAttribute : Attribute;

                public sealed class LookalikeAttribute : DataClassificationAttribute;
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, [Elsewhere.Lookalike] RefEmail email);
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("VO0045");
        tags.Plain.Should().Equal("email");
    }

    /// <summary>
    /// <c>[LogProperties]</c> on a value object of another assembly expands it into its <c>Value</c>, classified as its
    /// type, the one place the logging generator reads a type's classification. On one of the same project, it reports
    /// <c>LOGGEN020</c>. A dictionary of value objects, and a parameter carrying both <c>[LogProperties]</c> and a
    /// classification, which is <c>LOGGEN017</c>, are left to the guide and to the logging generator.
    /// </summary>
    [Fact]
    public async Task LogProperties_on_a_value_object_and_what_stays_out_are_left_alone()
    {
        var (expanded, tags) = await AnalyzeAsync("""
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Expanded")]
                public static partial void Expanded(ILogger logger, [LogProperties] RefEmail email, Dictionary<string, RefEmail> byName);
            }
            """);

        expanded.Should().BeEmpty();
        tags.Classified.Should().Equal("email.Value <PersonalDataAttribute>");

        var run = GeneratorHarness.Run(Header + Local + """
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Local")]
                public static partial void Local(ILogger logger, [LogProperties] Email email);

                [LoggerMessage(Level = LogLevel.Information, Message = "Both")]
                public static partial void Both(ILogger logger, [PersonalData][LogProperties] RefHolder holder);
            }
            """, references: WithDomain, withLoggingGenerator: true);
        run.CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Contain(["LOGGEN020", "LOGGEN017"]);

        var (refused, _) = await AnalyzeAsync(Local + """
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Local")]
                public static partial void Local(ILogger logger, [LogProperties] Email email);

                [LoggerMessage(Level = LogLevel.Information, Message = "Both")]
                public static partial void Both(ILogger logger, [PersonalData][LogProperties] RefHolder holder);
            }
            """, compiles: false);

        refused.Should().BeEmpty();
    }

    /// <summary>
    /// A property <c>[LogProperties]</c> expands is reported on the logging parameter with its path, and the declaration
    /// to classify as its additional location: a record's positional parameter, or the property. A member of another
    /// assembly has none. The logging generator never calls the tag provider of a property, which it logs in clear.
    /// </summary>
    [Fact]
    public async Task A_member_LogProperties_expands_is_reported_with_its_path()
    {
        var (diagnostics, tags) = await AnalyzeAsync(Local + """
            public sealed record Signup(Email Email, RefEmail Contact, [property: PersonalData] RefEmail Classified, Email? Maybe, Iban Iban);

            public static class Tags
            {
                public static void Add(ITagCollector collector, RefEmail email) => collector.Add("domain", email.Value.Split('@')[^1]);
            }

            public class Person
            {
                public RefEmail Inherited { get; init; }

                public virtual RefEmail Overridden { get; init; }
            }

            public sealed class Profile : Person
            {
                public RefEmail Contact { get; init; }

                [NoDataClassification]
                public RefEmail Public { get; init; }

                [LogPropertyIgnore]
                public RefEmail Ignored { get; init; }

                [TagProvider(typeof(Tags), nameof(Tags.Add))]
                public RefEmail Provided { get; init; }

                public static RefEmail Shared { get; set; }

                public RefEmail WriteOnly { set { } }

                public RefEmail Hidden { private get; set; }

                internal RefEmail Internal { get; set; }

                public override RefEmail Overridden { get; init; }

                public RefEmail this[int index] => Contact;
            }

            public sealed record Party(RefEmail Contact, Iban Account);

            public sealed record Payment(Iban Account, [property: LogProperties] Party Payer, Party Payee);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup);

                [LoggerMessage(Level = LogLevel.Information, Message = "Updated")]
                public static partial void Updated(ILogger logger, [LogProperties] Profile profile);

                [LoggerMessage(Level = LogLevel.Information, Message = "Held")]
                public static partial void Held(ILogger logger, [LogProperties] RefHolder holder);

                [LoggerMessage(Level = LogLevel.Information, Message = "Paid")]
                public static partial void Paid(ILogger logger, [LogProperties] Payment payment);
            }
            """);

        diagnostics.Should().AllSatisfy(diagnostic => diagnostic.Id.Should().Be("VO0045"));
        diagnostics.Select(diagnostic => (
                Path: Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)],
                Target: diagnostic.Properties[DataClassificationAnalyzer.TargetKey],
                Declaration: diagnostic.AdditionalLocations.Select(LineOf).SingleOrDefault()))
            .Should().Equal(
                ("signup.Email", "Member", "public sealed record Signup(Email Email, RefEmail Contact, [property: PersonalData] RefEmail Classified, Email? Maybe, Iban Iban);"),
                ("signup.Contact", "Member", "public sealed record Signup(Email Email, RefEmail Contact, [property: PersonalData] RefEmail Classified, Email? Maybe, Iban Iban);"),
                ("signup.Maybe", "Member", "public sealed record Signup(Email Email, RefEmail Contact, [property: PersonalData] RefEmail Classified, Email? Maybe, Iban Iban);"),
                ("profile.Contact", "Member", "public RefEmail Contact { get; init; }"),
                ("profile.Provided", "Member", "public RefEmail Provided { get; init; }"),
                ("profile.Overridden", "Member", "public override RefEmail Overridden { get; init; }"),
                ("profile.Inherited", "Member", "public RefEmail Inherited { get; init; }"),
                ("holder.Email", "None", null),
                ("payment.Payer.Contact", "Member", "public sealed record Party(RefEmail Contact, Iban Account);"));

        diagnostics.Select(diagnostic => Located(diagnostic).Text).Distinct()
            .Should().Equal("signup", "profile", "holder", "payment");
        Message(diagnostics[0]).Should().Be(
            "'signup.Email' is logged in clear: its type 'Email' is classified [PersonalData], which the logging generator "
            + $"reads on a parameter or a property only. Classify 'signup.Email' with [PersonalData], {Remedy}.");
        tags.Plain.Should().Contain(["signup.Email", "signup.Contact", "profile.Contact", "profile.Provided", "holder.Email", "payment.Payer.Contact"]);
        tags.Plain.Should().NotContain(["profile.Ignored", "profile.Shared", "profile.WriteOnly", "profile.Hidden", "profile.Internal"]);
        tags.Classified.Should().Contain("signup.Classified <PersonalDataAttribute>");
    }

    /// <summary>
    /// The logging generator reads a classification on a record's constructor parameter as the classification of the
    /// property of its name, whatever their types, and never on a class's.
    /// </summary>
    [Fact]
    public async Task A_record_constructor_parameter_classifies_the_property_of_its_name_and_a_class_one_does_not()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
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

            public sealed class Primary([PersonalData] RefEmail Contact)
            {
                public RefEmail Contact { get; } = Contact;
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup, [LogProperties] Enrolment enrolment, [LogProperties] Primary primary);
            }
            """);

        diagnostics.Select(diagnostic => (Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)], diagnostic.AdditionalLocations.Select(LineOf).Single()))
            .Should().Equal(
                ("enrolment.Contact", "public RefEmail Contact { get; }"),
                ("primary.Contact", "public RefEmail Contact { get; } = Contact;"));
        tags.Classified.Should().Equal("signup.Contact <PersonalDataAttribute>");
        tags.Plain.Should().Contain(["enrolment.Contact", "primary.Contact"]);
    }

    /// <summary>
    /// A type classified on itself, or through a base type, classifies every property the logging generator logs of it,
    /// <c>[NoDataClassification]</c> on the type aside; an expansion of one of its value objects drops it, as it drops a
    /// property's own.
    /// </summary>
    [Fact]
    public async Task A_type_classified_on_itself_classifies_every_property_an_expansion_logs()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            [PersonalData]
            public sealed record Signup(RefEmail Email, Iban Iban, List<RefEmail> Emails);

            [PersonalData]
            public class Person
            {
                public RefEmail Inherited { get; init; }
            }

            public sealed class Profile : Person
            {
                public RefEmail Contact { get; init; }
            }

            [NoDataClassification]
            public sealed record Open(RefEmail Email);

            [PersonalData]
            public sealed record Transfer(Iban From, RefEmail Contact);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(
                    ILogger logger,
                    [LogProperties] Signup signup,
                    [LogProperties] Profile profile,
                    [LogProperties] Open open,
                    [LogProperties(Transitive = true)] Transfer transfer);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)]))
            .Should().Equal(("VO0045", "open.Email"), ("VO0046", "transfer.From"));
        tags.Classified.Should().Contain([
            "signup.Email <PersonalDataAttribute>",
            "signup.Iban <PersonalDataAttribute>",
            "signup.Emails <PersonalDataAttribute>",
            "profile.Contact <PersonalDataAttribute>",
            "profile.Inherited <PersonalDataAttribute>"]);
        tags.Plain.Should().Contain(["open.Email", "transfer.From.Value"]);
    }

    /// <summary>
    /// A collection the logging generator does not expand is written element by element, in clear: any collection a
    /// property holds without <c>Transitive</c> nor <c>[LogProperties]</c> on the property, and under either, a
    /// collection interface, an array or a collection with no property of its own to log. A collection it expands is
    /// written as its own properties, <c>Count</c> among them, and a nullable struct collection as its text.
    /// </summary>
    [Fact]
    public async Task A_collection_of_value_objects_a_property_holds_is_reported_where_it_is_written_whole()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public sealed class Bag : IEnumerable<RefEmail>
            {
                public IEnumerator<RefEmail> GetEnumerator() => new List<RefEmail>().GetEnumerator();

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public sealed record Signup(
                List<RefEmail> Emails,
                RefEmail[] Array,
                IEnumerable<RefEmail?> Maybe,
                System.Collections.Immutable.ImmutableArray<RefEmail>? Text,
                [property: PersonalData] List<RefEmail> Classified,
                [property: NoDataClassification] List<RefEmail> Public,
                Dictionary<string, RefEmail> ByName,
                [property: LogProperties] List<RefEmail> Expanded,
                [property: LogProperties] Bag Nothing,
                Iban Iban);

            public sealed record Transfer(
                IEnumerable<RefEmail> Emails,
                IReadOnlyCollection<RefEmail> Counted,
                RefEmail[] Array,
                Bag Bag,
                List<RefEmail> List,
                HashSet<RefEmail> Set,
                System.Collections.Immutable.ImmutableArray<RefEmail> Immutable,
                [property: PersonalData] IReadOnlyList<RefEmail> Classified);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup);

                [LoggerMessage(Level = LogLevel.Information, Message = "Transferred")]
                public static partial void Transferred(ILogger logger, [LogProperties(Transitive = true)] Transfer transfer);
            }
            """);

        diagnostics.Select(diagnostic => (
                Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)],
                diagnostic.Properties[DataClassificationAnalyzer.TargetKey]))
            .Should().Equal(
                ("signup.Emails", "Member"),
                ("signup.Array", "Member"),
                ("signup.Maybe", "Member"),
                ("signup.Nothing", "Member"),
                ("transfer.Emails", "Member"),
                ("transfer.Counted", "Member"),
                ("transfer.Array", "Member"),
                ("transfer.Bag", "Member"));
        Message(diagnostics[0]).Should().Be(
            "'signup.Emails' is logged in clear: its type 'RefEmail' is classified [PersonalData], which the logging "
            + $"generator reads on a parameter or a property only. Classify 'signup.Emails' with [PersonalData], {Remedy}.");

        tags.Plain.Should().Contain([
            "signup.Emails", "signup.Array", "signup.Maybe", "signup.Nothing", "signup.Text", "signup.Public", "signup.ByName",
            "signup.Expanded.Count", "transfer.Emails", "transfer.Counted", "transfer.Array", "transfer.Bag",
            "transfer.List.Count", "transfer.Set.Count", "transfer.Immutable.Length"]);
        tags.Classified.Should().Equal("signup.Classified <PersonalDataAttribute>", "transfer.Classified <PersonalDataAttribute>");
    }

    /// <summary>
    /// <c>Transitive</c> expands a value object of another assembly, or one written by hand, into its <c>Value</c>,
    /// classified as its type alone: a classification written on the property holding it is dropped. A value object of
    /// the snippet's own assembly, whose members the logging generator cannot see, is logged whole, with the property's
    /// classification. Every other type is expanded, a collection into its own properties and a classified property
    /// whose classification the expansion drops too, and a type expanded on the way is not expanded again.
    /// </summary>
    [Fact]
    public async Task Transitive_drops_the_classification_of_a_property_it_expands()
    {
        var (diagnostics, tags) = await AnalyzeAsync(Local + HandWritten("LocalSecret") + """
            public sealed record Party([property: PersonalData] Iban Account, RefEmail Contact);

            public sealed record Transfer(
                [property: PersonalData] Iban From,
                Iban To,
                RefEmail Contact,
                Email Local,
                [property: PersonalData] Email LocalClassified,
                [property: PersonalData] Nickname Alias,
                [property: PersonalData] Party Classified,
                Party Payer,
                string Reference,
                List<Iban> Ibans,
                DateTimeOffset At,
                Guid Id);

            public sealed record Vault([property: PersonalData] LocalSecret Secret);

            public sealed class Parties : List<Party>
            {
                public Email Owner { get; init; }
            }

            public sealed record Board(Parties Members, Party Chair);

            public sealed record Order([property: PersonalData] Ticket<PurchaseOrder> Ticket, [property: FinancialData] RefEmail Billing);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Transferred")]
                public static partial void Transferred(ILogger logger, [LogProperties(Transitive = true)] Transfer transfer);

                [LoggerMessage(Level = LogLevel.Information, Message = "Held")]
                public static partial void Held(ILogger logger, [LogProperties(Transitive = true)] RefHolderClassified holder);

                [LoggerMessage(Level = LogLevel.Information, Message = "Visited")]
                public static partial void Visited(ILogger logger, [LogProperties(Transitive = true)] RefVisit visit);

                [LoggerMessage(Level = LogLevel.Information, Message = "Opened")]
                public static partial void Opened(ILogger logger, [LogProperties(Transitive = true)] Vault vault);

                [LoggerMessage(Level = LogLevel.Information, Message = "Met")]
                public static partial void Met(ILogger logger, [LogProperties(Transitive = true)] Board board);

                [LoggerMessage(Level = LogLevel.Information, Message = "Ordered")]
                public static partial void Ordered(ILogger logger, [LogProperties(Transitive = true)] Order order);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)])).Should().Equal(
            ("VO0045", "transfer.Local"),
            ("VO0046", "transfer.From"),
            ("VO0046", "transfer.Classified.Account"),
            ("VO0046", "transfer.Payer.Account"),
            ("VO0046", "holder.Iban"),
            ("VO0046", "visit.Classified"),
            ("VO0046", "vault.Secret"),
            ("VO0045", "board.Members.Owner"),
            ("VO0046", "board.Chair.Account"),
            ("VO0046", "order.Ticket"));

        var dropped = diagnostics[1];
        dropped.Severity.Should().Be(DiagnosticSeverity.Warning);
        Located(dropped).Text.Should().Be("transfer");
        Message(dropped).Should().Be(
            "'transfer.From' is logged in clear as 'transfer.From.Value': [LogProperties(Transitive = true)] expands the "
            + "value object 'Iban' and drops the classification of the property. Classify the type 'Iban' itself, or drop "
            + "Transitive.");
        Message(diagnostics[6]).Should().Contain("expands the value object 'LocalSecret'");
        Message(diagnostics[9]).Should().Be(
            "'order.Ticket' is logged in clear as 'order.Ticket.Value': [LogProperties(Transitive = true)] expands the "
            + "value object 'Ticket<PurchaseOrder>' and drops the classification of the property. Classify the type "
            + "'Ticket<PurchaseOrder>' itself, or drop Transitive.");

        tags.Plain.Should().Contain(["transfer.From.Value", "transfer.Payer.Account.Value", "holder.Iban.Value", "visit.Classified.Value", "visit.Public.Value"]);

        // A classified property of another type is expanded all the same, and a collection into its own properties.
        tags.Plain.Should().Contain(["transfer.Classified.Account.Value", "transfer.Ibans.Count", "board.Members.Owner"]);
        tags.Classified.Should().Contain([
            "transfer.Contact.Value <PersonalDataAttribute>",
            "transfer.LocalClassified <PersonalDataAttribute>",
            "transfer.Alias <PersonalDataAttribute>",
            "transfer.Payer.Contact.Value <PersonalDataAttribute>",
            "visit.Patient.Value <PersonalDataAttribute>"]);

        // A value object classified on its type is expanded into its Value, which carries the type's classification in
        // place of the property's: redacted all the same.
        tags.Plain.Should().Contain("order.Ticket.Value");
        tags.Classified.Should().Contain("order.Billing.Value <PersonalDataAttribute>");
    }

    /// <summary>
    /// The logging generator reads <c>Transitive</c> on the parameter's <c>[LogProperties]</c> alone: written on a
    /// property, <c>[LogProperties(Transitive = true)]</c> expands it one level, as <c>[LogProperties]</c> does, and
    /// <c>Transitive = false</c> on the parameter expands nothing further.
    /// </summary>
    [Fact]
    public async Task Transitive_is_read_on_the_parameter_alone()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public sealed record Party([property: PersonalData] Iban Account, RefEmail Contact);

            public sealed record Payment(Iban Account, [property: LogProperties(Transitive = true)] Party Payer);

            public sealed record Transfer([property: PersonalData] Iban From, Iban To);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Paid")]
                public static partial void Paid(ILogger logger, [LogProperties] Payment payment);

                [LoggerMessage(Level = LogLevel.Information, Message = "Transferred")]
                public static partial void Transferred(ILogger logger, [LogProperties(Transitive = false)] Transfer transfer);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)]))
            .Should().Equal(("VO0045", "payment.Payer.Contact"));
        tags.Plain.Should().Contain(["payment.Payer.Contact", "transfer.To"]);
        tags.Classified.Should().Equal("payment.Payer.Account <PersonalDataAttribute>", "transfer.From <PersonalDataAttribute>");
    }

    /// <summary>
    /// A nullable struct is expanded as the struct it makes nullable, by <c>[LogProperties]</c> on the parameter or on a
    /// property, and under <c>Transitive</c>, with the paths the logging generator writes.
    /// </summary>
    [Fact]
    public async Task A_nullable_struct_is_expanded_as_the_struct_it_makes_nullable()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public readonly record struct Card(RefEmail Holder, [property: PersonalData] Iban Number);

            public sealed record Purse([property: LogProperties] Card? Main);

            public sealed record Wallet(Card? Main);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Carded")]
                public static partial void Carded(
                    ILogger logger,
                    [LogProperties] Card? card,
                    [LogProperties] Purse purse,
                    [LogProperties(Transitive = true)] Wallet wallet);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)])).Should().Equal(
            ("VO0045", "card.Holder"),
            ("VO0045", "purse.Main.Holder"),
            ("VO0046", "wallet.Main.Number"));
        tags.Plain.Should().Contain(["card.Holder", "purse.Main.Holder", "wallet.Main.Number.Value"]);
        tags.Classified.Should().Contain([
            "card.Number <PersonalDataAttribute>",
            "purse.Main.Number <PersonalDataAttribute>",
            "wallet.Main.Holder.Value <PersonalDataAttribute>"]);
    }

    /// <summary>
    /// Under <c>Transitive</c>, <c>[LogPropertyIgnore]</c> stops the expansion of a property but not its logging, and the
    /// logging generator never calls the tag provider of a property: both are logged whole, in clear. A property with no
    /// public getter, or a static one, is skipped.
    /// </summary>
    [Fact]
    public async Task Under_Transitive_an_ignored_or_provided_property_is_logged_whole()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public static class Tags
            {
                public static void Add(ITagCollector collector, RefEmail email) => collector.Add("domain", email.Value.Split('@')[^1]);
            }

            public sealed class Wire
            {
                [LogPropertyIgnore]
                public RefEmail Ignored { get; init; }

                [TagProvider(typeof(Tags), nameof(Tags.Add))]
                public RefEmail Provided { get; init; }

                [LogPropertyIgnore]
                public Iban Account { get; init; }

                public RefEmail Kept { get; init; }

                [PersonalData]
                internal Iban Internal { get; init; }

                [PersonalData]
                public static Iban Shared { get; set; }

                [PersonalData]
                public Iban Hidden { private get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Wired")]
                public static partial void Wired(ILogger logger, [LogProperties(Transitive = true)] Wire wire);
            }
            """);

        diagnostics.Select(diagnostic => (Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)], diagnostic.AdditionalLocations.Select(LineOf).Single()))
            .Should().Equal(
                ("wire.Ignored", "public RefEmail Ignored { get; init; }"),
                ("wire.Provided", "public RefEmail Provided { get; init; }"));
        tags.Plain.Should().Equal("wire.Ignored", "wire.Provided", "wire.Account");
        tags.Classified.Should().Equal("wire.Kept.Value <PersonalDataAttribute>");
    }

    /// <summary>
    /// A type that holds itself ends the walk where it comes back: the logging generator reports the cycle
    /// (<c>LOGGEN021</c>), and the analysis does not loop.
    /// </summary>
    [Fact]
    public async Task A_type_holding_itself_ends_the_walk()
    {
        var (diagnostics, _) = await AnalyzeAsync(Local + """
            public sealed class Node
            {
                public Node? Next { get; init; }

                public Email Contact { get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Walked")]
                public static partial void Walked(ILogger logger, [LogProperties(Transitive = true)] Node node);
            }
            """, compiles: false);

        diagnostics.Select(Message).Should().ContainSingle().Which.Should().StartWith("'node.Contact' is logged in clear");
    }

    /// <summary>
    /// <c>[LogProperties]</c> written on the value-object property itself expands it as <c>Transitive</c> does, with or
    /// without its own <c>Transitive</c>, and the message names the attribute to drop.
    /// </summary>
    [Fact]
    public async Task LogProperties_on_the_property_drops_its_classification_too()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public sealed class Wire
            {
                [PersonalData]
                [LogProperties]
                public Iban Account { get; init; }

                [PersonalData]
                [LogProperties(Transitive = true)]
                public Iban Other { get; init; }

                [PersonalData]
                public Iban Kept { get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Wired")]
                public static partial void Wired(ILogger logger, [LogProperties] Wire wire);
            }
            """);

        diagnostics.Select(Message).Should().Equal(
            "'wire.Account' is logged in clear as 'wire.Account.Value': [LogProperties] on the property expands the value "
            + "object 'Iban' and drops the classification of the property. Classify the type 'Iban' itself, or drop "
            + "[LogProperties] from the property.",
            "'wire.Other' is logged in clear as 'wire.Other.Value': [LogProperties] on the property expands the value "
            + "object 'Iban' and drops the classification of the property. Classify the type 'Iban' itself, or drop "
            + "[LogProperties] from the property.");
        tags.Plain.Should().Contain(["wire.Account.Value", "wire.Other.Value"]);
        tags.Classified.Should().Equal("wire.Kept <PersonalDataAttribute>");
    }

    /// <summary>
    /// A record logged whole is written through its <c>ToString()</c>, every property in clear. Classifying the property
    /// makes the logging generator refuse the method (<c>LOGGEN035</c>), so the message asks for <c>[LogProperties]</c>,
    /// and the code fix has nothing to write; <c>[NoDataClassification]</c> on the property, which the logging generator
    /// accepts, logs it in clear on purpose.
    /// </summary>
    [Fact]
    public async Task A_record_logged_whole_is_reported_with_the_remedy_that_compiles()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public sealed record Contact(RefEmail Email, Iban Iban);

            public sealed record Shared([property: NoDataClassification] RefEmail Email);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Contacted {Contact}")]
                public static partial void Contacted(ILogger logger, Contact contact);

                [LoggerMessage(Level = LogLevel.Information, Message = "Shared {Shared}")]
                public static partial void Shared(ILogger logger, Shared shared);

                [LoggerMessage(Level = LogLevel.Information, Message = "Held {Holder}")]
                public static partial void Held(ILogger logger, RefHolder holder);
            }
            """);

        diagnostics.Select(Message).Should().Equal(
            "'contact.Email' is logged in clear: its type 'RefEmail' is classified [PersonalData], which the logging "
            + "generator reads on a parameter or a property only. Log 'contact' with [LogProperties] and classify "
            + "'contact.Email' with [PersonalData]: the logging generator refuses a record it logs whole whose property is "
            + "classified (LOGGEN035).",
            "'holder.Email' is logged in clear: its type 'RefEmail' is classified [PersonalData], which the logging "
            + "generator reads on a parameter or a property only. Log 'holder' with [LogProperties] and classify "
            + "'holder.Email' with [PersonalData]: the logging generator refuses a record it logs whole whose property is "
            + "classified (LOGGEN035).");
        diagnostics.Should().AllSatisfy(diagnostic =>
        {
            diagnostic.Properties[DataClassificationAnalyzer.TargetKey].Should().Be(DataClassificationAnalyzer.NoTarget);
            diagnostic.AdditionalLocations.Should().BeEmpty();
        });
        tags.Plain.Should().Contain(["Contact", "Holder", "Shared"]);

        // The record whose property is classified is the logging generator's own refusal.
        const string Refused = """
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Held {Holder}")]
                public static partial void Held(ILogger logger, RefHolderClassified holder);
            }
            """;
        GeneratorHarness.Run(Header + Refused, references: WithDomain, withLoggingGenerator: true)
            .CompilationDiagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("LOGGEN035");
        (await AnalyzeAsync(Refused, compiles: false)).Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A record's <c>ToString()</c>, as the compiler writes it, prints the public instance fields and the properties with
    /// a getter, whatever their attributes, of the record and of each base record, an override once: each value object
    /// classified on its type among them is reported. A record, or a base record, that writes its own <c>ToString()</c> or
    /// <c>PrintMembers</c> prints what it likes, and a class logged whole is written through its own <c>ToString()</c>:
    /// neither is read.
    /// </summary>
    [Fact]
    public async Task A_record_logged_whole_is_read_as_its_ToString_prints_it()
    {
        var (diagnostics, tags) = await AnalyzeAsync("""
            public sealed record WithField(Iban Iban)
            {
                public RefEmail Email;

                public static RefEmail Default;
            }

            public sealed record WithIgnored(Iban Iban)
            {
                [LogPropertyIgnore]
                public RefEmail Email { get; init; }

                public RefEmail Hidden { private get; init; }

                internal RefEmail Internal { get; init; }

                public static RefEmail Shared { get; set; }

                public RefEmail WriteOnly { set { } }

                public RefEmail this[int index] => Hidden;
            }

            public sealed record Maybe(RefEmail? Email);

            public record Base(RefEmail Inherited)
            {
                public virtual RefEmail Virtual { get; init; }
            }

            public sealed record Derived(RefEmail Own) : Base(Own)
            {
                public override RefEmail Virtual { get; init; }
            }

            public sealed record Custom(RefEmail Email)
            {
                public override string ToString() => "custom";
            }

            public sealed record Printed(RefEmail Email)
            {
                private bool PrintMembers(System.Text.StringBuilder builder) => builder.Append("printed") is not null;
            }

            public record Sealed(RefEmail Email)
            {
                public sealed override string ToString() => "sealed";
            }

            public sealed record UnderSealed(RefEmail Other) : Sealed(Other);

            public record PrintingBase(RefEmail Email)
            {
                protected virtual bool PrintMembers(System.Text.StringBuilder builder) => builder.Append("base") is not null;
            }

            public sealed record OverPrinting(RefEmail Extra) : PrintingBase(Extra);

            public readonly record struct Pair(RefEmail Email);

            public sealed record Formatted(RefEmail Email)
            {
                public string ToString(string format) => format;
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(
                    ILogger logger,
                    WithField withField,
                    WithIgnored withIgnored,
                    Maybe maybe,
                    Derived derived,
                    Custom custom,
                    Printed printed,
                    UnderSealed underSealed,
                    OverPrinting overPrinting,
                    Pair? pair,
                    Formatted formatted);
            }
            """);

        diagnostics.Select(diagnostic => Message(diagnostic)[1..Message(diagnostic).IndexOf('\'', 1)]).Should().Equal(
            "withField.Email",
            "withIgnored.Email",
            "withIgnored.Hidden",
            "maybe.Email",
            "derived.Own",
            "derived.Inherited",
            "derived.Virtual",
            "overPrinting.Extra",
            "pair.Email",
            "formatted.Email");
        tags.Plain.Should().Equal(
            "withField", "withIgnored", "maybe", "derived", "custom", "printed", "underSealed", "overPrinting", "pair", "formatted");

        var (whole, _) = await AnalyzeAsync("""
            public sealed class Account
            {
                public RefEmail Email { get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Account account);
            }
            """, compiles: false);

        whole.Should().BeEmpty();
    }

    /// <summary>
    /// Only a method Microsoft.Gen.Logging implements is read: without Microsoft.Extensions.Telemetry.Abstractions,
    /// Microsoft.Extensions.Logging's generator writes it and redacts nothing at all, and a method another generator or
    /// the author implements is theirs.
    /// </summary>
    [Fact]
    public async Task Only_methods_Microsoft_Gen_Logging_writes_are_read()
    {
        const string Logged = """
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, RefEmail email);
            }
            """;

        // Microsoft.Extensions.Logging.Abstractions alone, and no LoggerMessageAttribute at all.
        (await GeneratorHarness.RunAnalyzerAsync<DataClassificationAnalyzer>(Header + Logged, PlainWithDomain))
            .Should().BeEmpty();
        (await GeneratorHarness.RunAnalyzerAsync<DataClassificationAnalyzer>(
                Header + Logged,
                GeneratorHarness.LibraryReferences.Add(LogPropertiesAssembly).Add(Image)))
            .Should().BeEmpty();

        // No contracts: a project that declares no value object.
        (await GeneratorHarness.RunAnalyzerAsync<DataClassificationAnalyzer>(
                """
                using Microsoft.Extensions.Logging;

                namespace Snippets;

                public static partial class Log
                {
                    [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                    public static partial void Logged(ILogger logger, string email);
                }
                """,
                [.. GeneratorHarness.FrameworkReferences, LoggingAssembly, LogPropertiesAssembly],
                withLoggingGenerator: true))
            .Should().BeEmpty();

        // Microsoft.Gen.Logging not running: the method has no implementation.
        (await GeneratorHarness.RunAnalyzerAsync<DataClassificationAnalyzer>(Header + Logged, WithDomain)).Should().BeEmpty();

        // Another generator's implementation, in a generated file of its own as a generator writes it, and the author's.
        (await AnalyzeImplementedAsync("[System.CodeDom.Compiler.GeneratedCode(\"Microsoft.Extensions.Logging.Generators\", \"10.0.0.0\")]"))
            .Should().BeEmpty();
        (await AnalyzeImplementedAsync("[System.Obsolete(\"Microsoft.Gen.Logging\")]")).Should().BeEmpty();
        (await AnalyzeImplementedAsync("[System.CodeDom.Compiler.GeneratedCode(\"Microsoft.Gen.Logging\", \"10.11.0.0\")]"))
            .Select(diagnostic => diagnostic.Id).Should().Equal("VO0045");
    }

    /// <summary>
    /// A message names every classification of the type, in the order the type declares them, as written: the suffix
    /// <c>Attribute</c> left out where the class name carries it.
    /// </summary>
    [Fact]
    public async Task The_classifications_are_named_in_their_declared_order()
    {
        var (diagnostics, _) = await AnalyzeAsync("""
            public sealed class Sensitive : DataClassificationAttribute
            {
                public Sensitive() : base(Taxonomy.Personal) { }
            }

            public static class Odd
            {
                public sealed class Attribute : DataClassificationAttribute
                {
                    public Attribute() : base(Taxonomy.Financial) { }
                }
            }

            [FinancialData, PersonalData]
            [Sensitive]
            [Odd.Attribute]
            [ValueObject<string>]
            public readonly partial struct Account;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Account account);
            }
            """);

        Message(diagnostics.Should().ContainSingle().Subject).Should().StartWith(
            "'account' is logged in clear: its type 'Account' is classified [FinancialData], [PersonalData], [Sensitive], "
            + "[Attribute], which");
    }

    /// <summary>
    /// An analyzer's diagnostics honour <c>#pragma</c>, unlike the generator's: a value object logged in clear on purpose
    /// may take one around its method, as <c>[NoDataClassification]</c> on its parameter does.
    /// </summary>
    [Fact]
    public async Task A_pragma_silences_each_diagnostic()
    {
        var (diagnostics, _) = await AnalyzeAsync("""
            public sealed record Transfer([property: PersonalData] Iban From);

            public static partial class Log
            {
            #pragma warning disable VO0045 // Logged in clear on purpose.
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, RefEmail email);
            #pragma warning restore VO0045

            #pragma warning disable VO0046 // The type is classified elsewhere.
                [LoggerMessage(Level = LogLevel.Information, Message = "Transferred")]
                public static partial void Transferred(ILogger logger, [LogProperties(Transitive = true)] Transfer transfer);
            #pragma warning restore VO0046

                [LoggerMessage(Level = LogLevel.Information, Message = "Again")]
                public static partial void Again(ILogger logger, RefEmail email, [LogProperties(Transitive = true)] Transfer transfer);
            }
            """);

        diagnostics.Select(diagnostic => (diagnostic.Id, Located(diagnostic).Line)).Should().Equal(
            ("VO0045", "public static partial void Again(ILogger logger, RefEmail email, [LogProperties(Transitive = true)] Transfer transfer);"),
            ("VO0046", "public static partial void Again(ILogger logger, RefEmail email, [LogProperties(Transitive = true)] Transfer transfer);"));
    }

    [Fact]
    [Trait("Reads", "Documentation")]
    public void The_help_links_land_on_the_data_classification_section_of_the_logging_guide()
    {
        var analyzer = new DataClassificationAnalyzer();

        analyzer.SupportedDiagnostics.Select(descriptor => descriptor.Id).Should().Equal("VO0045", "VO0046");
        analyzer.SupportedDiagnostics.Select(descriptor => descriptor.DefaultSeverity)
            .Should().Equal(DiagnosticSeverity.Warning, DiagnosticSeverity.Warning);

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "how-to", "logging.md"));
        page.Should().Contain("slug: /how-to/logging");

        foreach (var descriptor in analyzer.SupportedDiagnostics)
        {
            var link = new Uri(descriptor.HelpLinkUri);
            link.GetLeftPart(UriPartial.Path).Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/logging");
            link.Fragment.Should().Be("#data-classification");
            page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
        }
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which it
    /// tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var analyzer = new DataClassificationAnalyzer();

        analyzer.Invoking(target => target.Initialize(null!)).Should().NotThrow();
    }

    /// <summary>
    /// What the analysis and the code fix read of a type, on the shapes no logging parameter of a valid program reaches:
    /// a type that is not named, an attribute class with no hierarchy left, an attribute that does not resolve.
    /// </summary>
    [Fact]
    public void The_readers_answer_for_the_shapes_no_logging_parameter_reaches()
    {
        var compilation = CSharpCompilation.Create("Shapes", references: GeneratorHarness.LibraryReferences);
        var array = compilation.CreateArrayTypeSymbol(compilation.GetSpecialType(SpecialType.System_Int32));

        DataClassificationAnalyzer.Classifications(array).Should().BeEmpty();
        DataClassificationAnalyzer.Element(array).Should().Be(compilation.GetSpecialType(SpecialType.System_Int32));
        DataClassificationAnalyzer.Element(compilation.GetSpecialType(SpecialType.System_Int32)).Should().BeNull();
        DataClassificationAnalyzer.AllowsTarget(null, AttributeTargets.Parameter).Should().BeTrue();
        DataClassificationAnalyzer.IsClassification(null).Should().BeFalse();
        AdCodicem.ValueObjects.Generators.ValueObjectGenerator.IsSensitiveClassification(null).Should().BeFalse();
    }

    /// <summary>A value object written by hand in the snippet, unclassified, of the name given.</summary>
    private static string HandWritten(string name) => $$"""
        public readonly struct {{name}} : IValueObject<{{name}}, string>
        {
            private readonly string? _value;
            private {{name}}(string value) => _value = value;
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => AdCodicem.ValueObjects.Metadata.ValueObjectSchema.Unconstrained;
            public string Value => _value ?? string.Empty;
            public bool IsDefault => _value is null;
            public static string Normalize(string value) => value;
            public static ValidationResult Validate(in string value) => ValidationResult.Success;
            public static {{name}} Create(string value) => new(value);
            public static bool TryCreate(string value, out {{name}} result) => TryCreate(value, out result, out _);
            public static bool TryCreate(string value, out {{name}} result, out ValidationResult validation) { validation = default; result = new(value); return true; }
            public static {{name}} CreateUnchecked(string value) => new(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out {{name}} result, out ValidationResult validation) => TryCreate(text.ToString(), out result, out validation);
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {{name}} result) => TryParse(s, provider, out result, out _);
            public static bool TryParse(string? s, IFormatProvider? provider, out {{name}} result) => TryParse(s.AsSpan(), provider, out result, out _);
            public static {{name}} Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());
            public static {{name}} Parse(string s, IFormatProvider? provider) => Create(s);
            public bool Equals({{name}} other) => Value == other.Value;
            public override bool Equals(object? obj) => obj is {{name}} other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
            public int CompareTo({{name}} other) => string.CompareOrdinal(Value, other.Value);
            public override string ToString() => Value;
            public string ToString(string? format, IFormatProvider? formatProvider) => Value;
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return Value.TryCopyTo(destination) && (charsWritten = Value.Length) >= 0; }
            public static bool operator ==({{name}} left, {{name}} right) => left.Equals(right);
            public static bool operator !=({{name}} left, {{name}} right) => !left.Equals(right);
            public static bool operator <({{name}} left, {{name}} right) => left.CompareTo(right) < 0;
            public static bool operator >({{name}} left, {{name}} right) => left.CompareTo(right) > 0;
            public static bool operator <=({{name}} left, {{name}} right) => left.CompareTo(right) <= 0;
            public static bool operator >=({{name}} left, {{name}} right) => left.CompareTo(right) >= 0;
        }

        """;

    /// <summary>
    /// Compiles a logging method whose other half a generated file of its own holds, carrying an attribute, as a generator
    /// writes it, and runs the analyzer over it: the gate's input, with no generator running.
    /// </summary>
    private static Task<ImmutableArray<Diagnostic>> AnalyzeImplementedAsync(string attribute)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "Gate",
            [
                CSharpSyntaxTree.ParseText(Header + """
                    public static partial class Log
                    {
                        [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                        public static partial void Logged(ILogger logger, RefEmail email);
                    }
                    """, parseOptions, path: "Log.cs"),
                CSharpSyntaxTree.ParseText($$"""
                    // <auto-generated/>
                    namespace Snippets;

                    partial class Log
                    {
                        {{attribute}}
                        public static partial void Logged(global::Microsoft.Extensions.Logging.ILogger logger, global::Shop.RefEmail email)
                        {
                        }
                    }
                    """, parseOptions, path: "Log.g.cs"),
            ],
            WithDomain,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return compilation
            .WithAnalyzers([new DataClassificationAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    private static readonly MetadataReference LoggingAssembly =
        MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location);

    private static readonly MetadataReference LogPropertiesAssembly =
        MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Logging.LogPropertiesAttribute).Assembly.Location);

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading of any level: lower case, punctuation dropped, spaces as hyphens. Any
    /// other line gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith('#'))
        {
            return string.Empty;
        }

        var heading = line.TrimStart('#').Trim().ToLowerInvariant();

        return string.Concat(heading.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'))
            .Replace(' ', '-');
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
