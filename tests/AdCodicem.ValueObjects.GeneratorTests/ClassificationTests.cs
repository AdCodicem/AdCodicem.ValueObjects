using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// The rejected value stays out of every message the generated code writes, and out of the <c>AttemptedValue</c> of a
/// value object its author classifies as sensitive data, with an attribute derived from
/// <c>DataClassificationAttribute</c>.
/// </summary>
/// <remarks>
/// The snippets classify with the real Microsoft.Extensions.Compliance.Abstractions, which the harness hands to every
/// snippet, as a consumer's project references it: the generator itself recognizes the attribute by name.
/// </remarks>
public sealed class ClassificationTests
{
    private const string PersonalData = """
        public sealed class PersonalDataAttribute : global::Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute
        {
            public PersonalDataAttribute()
                : base(new global::Microsoft.Extensions.Compliance.Classification.DataClassification("Shop", "Personal"))
            {
            }
        }
        """;

    /// <summary>
    /// <c>Parse</c> names the type and the rule, as <c>Create</c> does, and quotes no text: a message is what a log
    /// records. Both still hand the rejected value to <c>AttemptedValue</c> on a type nobody classified.
    /// </summary>
    [Fact]
    public void Parse_names_the_type_and_the_rule_but_never_the_text()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>(MaxLength = 3)]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var parse = Parse(run.SingleValueObject);
        parse.Should().Contain("\"'Wrapper' rejected the supplied text: \" + validation.ErrorMessage,");
        parse.Should().NotContain("s.ToString()}", "the text is no part of the message");
        AttemptedValues(run.SingleValueObject).Should().Equal("value", "s.ToString()");
    }

    /// <summary>
    /// The type's own name is written as the literal it is, whatever characters C# admits in it: an escaped keyword is
    /// named without its <c>@</c>, as <c>Create</c>, which reads the name off the type, names it.
    /// </summary>
    [Fact]
    public void Parse_names_a_type_named_after_a_keyword_without_its_escape()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<int>]
            public readonly partial struct @event;
            """);

        run.Diagnostics.Should().BeEmpty();
        Parse(run.SingleValueObject).Should().Contain("\"'event' rejected the supplied text: \" + validation.ErrorMessage,");
    }

    /// <summary>
    /// An attribute derived from <c>DataClassificationAttribute</c>, at any depth, classifies the type, and
    /// <c>UnknownDataClassificationAttribute</c> does too, since data nobody has classified yet may be anything:
    /// <c>Create</c> and <c>Parse</c> then pass <see langword="null"/> as the attempted value.
    /// </summary>
    /// <param name="attributes">The attributes on the value object.</param>
    [Theory]
    [InlineData("[PersonalData]")]
    [InlineData("[HealthData]")]
    [InlineData("[global::Microsoft.Extensions.Compliance.Classification.UnknownDataClassification]")]
    [InlineData("[global::Microsoft.Extensions.Compliance.Classification.NoDataClassification, PersonalData]")]
    public void A_classified_value_object_leaves_the_rejected_value_off_its_exceptions(string attributes)
    {
        var run = GeneratorHarness.Run($$"""
            {{PersonalData}}

            public abstract class SensitiveDataAttribute : global::Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute
            {
                protected SensitiveDataAttribute(string name)
                    : base(new global::Microsoft.Extensions.Compliance.Classification.DataClassification("Shop", name))
                {
                }
            }

            public sealed class HealthDataAttribute : SensitiveDataAttribute
            {
                public HealthDataAttribute()
                    : base("Health")
                {
                }
            }

            {{attributes}}
            [ValueObject<string>(MaxLength = 3)]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        AttemptedValues(run.SingleValueObject).Should().Equal("null", "null");
        Parse(run.SingleValueObject).Should().Contain("\"'Wrapper' rejected the supplied text: \" + validation.ErrorMessage,");
    }

    /// <summary>
    /// <c>NoDataClassificationAttribute</c> derives from <c>DataClassificationAttribute</c> to say the data is not
    /// sensitive, and an attribute that only shares the name, in another namespace, is not the classification at all:
    /// neither classifies the type, which keeps the rejected value as every other type does.
    /// </summary>
    /// <param name="attributes">The attributes on the value object.</param>
    [Theory]
    [InlineData("[global::Microsoft.Extensions.Compliance.Classification.NoDataClassification]")]
    [InlineData("[Elsewhere.Classified]")]
    [InlineData("[Elsewhere.DataClassification]")]
    public void A_value_object_nobody_classified_keeps_the_rejected_value(string attributes)
    {
        var run = GeneratorHarness.Run($$"""
            namespace Elsewhere
            {
                public class DataClassificationAttribute : global::System.Attribute
                {
                }

                public sealed class ClassifiedAttribute : DataClassificationAttribute
                {
                }
            }

            namespace Test
            {
                {{attributes}}
                [global::AdCodicem.ValueObjects.Annotations.ValueObject<string>(MaxLength = 3)]
                public readonly partial struct Wrapper;
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        AttemptedValues(run.SingleValueObject).Should().Equal("value", "s.ToString()");
    }

    /// <summary>
    /// An entity identifier and a generic value object are read from their own attributes too, which the declaration of
    /// any of their partial parts may carry.
    /// </summary>
    /// <param name="declaration">The value object, after the classification attribute.</param>
    [Theory]
    [InlineData("[EntityId(\"pat\")] public readonly partial struct PatientId;")]
    [InlineData("[ValueObject<string>] public readonly partial struct Pseudonym<TOwner>;")]
    [InlineData("public readonly partial struct Nickname; [ValueObject<string>] public readonly partial struct Nickname;")]
    public void An_identifier_a_generic_or_a_partial_value_object_is_classified_by_its_attributes(string declaration)
    {
        var run = GeneratorHarness.Run($$"""
            {{PersonalData}}

            [PersonalData]
            {{declaration}}
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        AttemptedValues(run.SingleValueObject).Should().Equal("null", "null");
    }

    /// <summary>
    /// The classification is part of the model, so classifying a type re-runs its generation: cached, the type would keep
    /// handing its rejected value over.
    /// </summary>
    [Fact]
    public void Classifying_a_value_object_does_re_run_the_model()
    {
        var plain = $$"""
            {{PersonalData}}

            [ValueObject<string>]
            public readonly partial struct Wrapper;
            """;
        var classified = plain.Replace("[ValueObject<string>]", "[PersonalData] [ValueObject<string>]", StringComparison.Ordinal);

        GeneratorHarness.RunTwice(plain, classified, "ValueObjects").Should().Contain(IncrementalStepRunReason.Modified);
    }

    /// <summary>
    /// The type converter refuses a number its underlying type cannot hold as <c>Parse</c> refuses text: its message
    /// names the type, never the number, which it hands to <c>AttemptedValue</c> unless the type is classified.
    /// </summary>
    /// <param name="attributes">The attributes on the value object.</param>
    /// <param name="attempted">The attempted value the converter passes.</param>
    [Theory]
    [InlineData("", "value")]
    [InlineData("[PersonalData]", "null")]
    public void The_type_converter_names_no_number_it_refuses_and_hands_over_none_of_a_classified_type(string attributes, string attempted)
    {
        var run = GeneratorHarness.Run($$"""
            {{PersonalData}}

            {{attributes}}
            [ValueObject<int>]
            public readonly partial struct Wrapper;
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();

        var generated = run.SingleValueObject;
        var start = generated.IndexOf("if (!fits)", StringComparison.Ordinal);
        start.Should().BePositive();
        var refusal = generated[start..generated.IndexOf(");", start, StringComparison.Ordinal)];

        refusal.Should().Contain("\"'Wrapper' rejected the supplied number: The number is not a valid int.\",");
        refusal.Should().NotContain("value}", "the number is no part of the message");
        refusal[(refusal.LastIndexOf(',') + 1)..].Trim().Should().Be(attempted);
    }

    /// <summary>Gets the generated <c>Parse</c> that throws, up to the member after it.</summary>
    private static string Parse(string generated)
    {
        var start = generated.IndexOf("Parse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider)", StringComparison.Ordinal);
        start.Should().BePositive();

        return generated[start..generated.IndexOf("public static", start, StringComparison.Ordinal)];
    }

    /// <summary>
    /// Gets the attempted value <c>Create</c> passes, then the one <c>Parse</c> passes, as the generated code writes them.
    /// </summary>
    private static string[] AttemptedValues(string generated)
    {
        var create = generated.IndexOf("validation.ThrowIfInvalid(typeof(", StringComparison.Ordinal);
        create.Should().BePositive();
        var createArgument = generated[(generated.IndexOf("), ", create, StringComparison.Ordinal) + 3)..generated.IndexOf(");", create, StringComparison.Ordinal)];

        var parse = Parse(generated);
        var parseArgument = parse[(parse.IndexOf("validation.ErrorCode,", StringComparison.Ordinal) + "validation.ErrorCode,".Length)..parse.IndexOf(");", StringComparison.Ordinal)];

        return [createArgument, parseArgument.Trim()];
    }
}
