using System.ComponentModel;
using System.Globalization;
using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// A value object its author classifies as personal data, with an attribute derived from
/// <c>DataClassificationAttribute</c>, keeps a rejected value off the exception it throws: neither the message nor
/// <see cref="ValueObjectException.AttemptedValue"/> holds it, so a logger recording the exception and its properties
/// records no personal data.
/// </summary>
public sealed class ClassifiedValueObjectTests
{
    /// <summary>
    /// Every path that throws goes through the generated <c>Create</c> or <c>Parse</c>, or, for a construction of a
    /// generic value object reached through <see cref="TypeDescriptor"/>, through the converter the contracts ship,
    /// which reads the classification off the generic definition.
    /// </summary>
    [Fact]
    public void No_path_hands_the_rejected_value_of_a_classified_value_object_over()
    {
        var paths = new (string Path, string Rejected, Action Act)[]
        {
            ("Parse", "X1", () => PassportNumber.Parse("X1", CultureInfo.InvariantCulture)),
            ("Create", "X1", () => PassportNumber.Create("X1")),
            ("an explicit conversion", "X1", () => _ = (PassportNumber)"X1"),
            ("the type converter, from text", "X1", () => TypeDescriptor.GetConverter(typeof(PassportNumber)).ConvertFromInvariantString("X1")),
            ("Create, over a number", "-4321", () => Salary.Create(-4321m)),
            ("Parse, over a number", "-4321", () => Salary.Parse("-4321", CultureInfo.InvariantCulture)),
            ("the arithmetic", "-4321", () => _ = Salary.Create(1m) - Salary.Create(4322m)),
            ("the type converter, from the underlying value", "-4321", () => TypeDescriptor.GetConverter(typeof(Salary)).ConvertFrom(-4321m)),
            ("Parse, of an identifier", "pat_secret", () => PatientId.Parse("pat_secret", null)),
            ("Create, of an identifier", "pat_secret", () => PatientId.Create("pat_secret")),
            ("Parse, of a generic value object", "Q", () => Pseudonym<PurchaseOrder>.Parse("Q", null)),
            ("the type converter of a generic value object", "Q", () => TypeDescriptor.GetConverter(typeof(Pseudonym<PurchaseOrder>)).ConvertFromInvariantString("Q")),
        };

        foreach (var (path, rejected, act) in paths)
        {
            var thrown = act.Should().Throw<ValueObjectException>(path).Which;

            thrown.AttemptedValue.Should().BeNull("{0} must not hand a classified value over", path);
            thrown.ErrorCode.Should().NotBeNullOrEmpty(path);
            thrown.ValueObjectType.Should().NotBeNull(path);
            thrown.Message.Should().MatchRegex(@"^'(PassportNumber|Salary|PatientId|Pseudonym)' rejected the supplied (text|value): ", path);
            thrown.Message.Should().NotContain(rejected, "{0} names the type and the rule, never the value", path);
        }
    }

    /// <summary>
    /// <c>NoDataClassificationAttribute</c> says the data is not sensitive: the type keeps the rejected value as one
    /// nobody classified does, through the generated code and through the converter that reads the attribute at run
    /// time alike. The message still names the rule only.
    /// </summary>
    [Fact]
    public void A_value_object_classified_as_no_sensitive_data_keeps_the_rejected_value()
    {
        var paths = new (string Path, Action Act)[]
        {
            ("Parse", () => TeamName<PurchaseOrder>.Parse("Q", null)),
            ("Create", () => TeamName<PurchaseOrder>.Create("Q")),
            ("the type converter", () => TypeDescriptor.GetConverter(typeof(TeamName<PurchaseOrder>)).ConvertFromInvariantString("Q")),
        };

        foreach (var (path, act) in paths)
        {
            var thrown = act.Should().Throw<ValueObjectException>(path).Which;

            thrown.AttemptedValue.Should().Be("Q", path);
            thrown.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort, path);
            thrown.Message.Should().Contain("rejected the supplied", path).And.NotContain("Q", path);
        }
    }

    /// <summary>
    /// The classification takes nothing away but the value: the rule that fired, its message and the type all reach
    /// the exception as they do for a value object nobody classified.
    /// </summary>
    [Fact]
    public void A_classified_value_object_reports_the_rule_that_fired()
    {
        PassportNumber.TryParse("X1", CultureInfo.InvariantCulture, out _, out var validation).Should().BeFalse();

        var thrown = FluentActions.Invoking(() => PassportNumber.Parse("X1", CultureInfo.InvariantCulture))
            .Should().Throw<ValueObjectException>().Which;

        thrown.ErrorCode.Should().Be(ValueObjectErrorCodes.TooShort).And.Be(validation.ErrorCode);
        thrown.Message.Should().Be($"'PassportNumber' rejected the supplied text: {validation.ErrorMessage}");
        thrown.ValueObjectType.Should().Be<PassportNumber>();
    }
}

/// <inheritdoc cref="IbanContract" />
public sealed class PassportNumberContract : ValueObjectContract<PassportNumber, string>
{
    protected override IEnumerable<string> AcceptedValues => ["X1234567", " ab123456 "];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "X1", "X1234567890"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class SalaryContract : ValueObjectContract<Salary, decimal>
{
    protected override IEnumerable<decimal> AcceptedValues => [0m, 42_000m];

    protected override IEnumerable<decimal> RejectedValues => [-1m];
}

/// <inheritdoc cref="AccountIdContract" />
public sealed class PatientIdContract : ValueObjectContract<PatientId, string>
{
    private static readonly string[] Minted = [PatientId.New().Value, PatientId.New().Value];

    protected override IEnumerable<string> AcceptedValues => Minted;

    protected override IEnumerable<string> RejectedValues => [string.Empty, "pat_nope", AccountId.New().Value];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PseudonymContract : ValueObjectContract<Pseudonym<PurchaseOrder>, string>
{
    protected override IEnumerable<string> AcceptedValues => ["Ada", "Grace Hopper"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "A", new string('a', 33)];
}

/// <inheritdoc cref="IbanContract" />
public sealed class TeamNameContract : ValueObjectContract<TeamName<PurchaseOrder>, string>
{
    protected override IEnumerable<string> AcceptedValues => ["Core", "Platform"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "C", new string('c', 33)];
}
