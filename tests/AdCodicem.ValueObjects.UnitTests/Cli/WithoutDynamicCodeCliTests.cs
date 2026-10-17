using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// The System.CommandLine symbols where the runtime supports no dynamic code, as under native AOT, run under the JIT in a
/// child process of its own (<see cref="WithoutDynamicCode"/>): a generated value object is read with no registration, and
/// a construction of a generic one nothing registered is refused when its symbol is configured, even beside a parser of
/// the application's own, until it is registered.
/// </summary>
public sealed class WithoutDynamicCodeCliTests
{
    private const string NotDescribed =
        "holds 'Reference<UnregisteredCliOwner>', which is not a value object the registry describes, its nullable type, or an "
        + "array, a List<T>, an IEnumerable<T> or an IReadOnlyList<T> of one. Under native AOT, a value object written by hand, "
        + "ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction of a generic one, "
        + "ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), have to be registered "
        + "before AcceptValueObject or AcceptValueObjects is called.";

    [Fact]
    public async Task A_value_object_nothing_registered_is_refused_when_configured_until_it_is_registered()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(WithoutDynamicCodeCliTests), nameof(A_value_object_nothing_registered_is_refused_when_configured_until_it_is_registered)))
        {
            return;
        }

        var quantity = new Option<Quantity>("--quantity").AcceptValueObject();
        new RootCommand { quantity }.Parse(["--quantity", "7"]).GetValue(quantity).Should().Be(Quantity.Create(7));

        FluentActions.Invoking(static () => new Option<Reference<UnregisteredCliOwner>>("--reference").AcceptValueObject())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--reference' {NotDescribed}");
        FluentActions.Invoking(static () => new RootCommand { new Command("sub") { new Argument<Reference<UnregisteredCliOwner>[]>("references") } }.AcceptValueObjects())
            .Should().Throw<InvalidOperationException>().WithMessage($"'references' {NotDescribed.Replace("'Reference<UnregisteredCliOwner>'", "'Reference<UnregisteredCliOwner>[]'", StringComparison.Ordinal)}");

        // The value object is found before the parser is read, which only the closing over its type can do.
        var parsed = new Option<Reference<UnregisteredCliOwner>>("--parsed") { CustomParser = static _ => Reference<UnregisteredCliOwner>.Create("PO-1") };
        FluentActions.Invoking(() => new RootCommand { parsed }.AcceptValueObjects())
            .Should().Throw<InvalidOperationException>().WithMessage($"'--parsed' {NotDescribed}");

        ValueObjectRegistry.Register<Reference<UnregisteredCliOwner>, string>(static () => new Reference<UnregisteredCliOwner>.ValueJsonConverter());
        var reference = new Option<Reference<UnregisteredCliOwner>>("--reference");
        var root = new RootCommand { reference, parsed }.AcceptValueObjects();
        root.Parse(["--reference", " po-7 "]).GetValue(reference).Should().Be(Reference<UnregisteredCliOwner>.Create("PO-7"));
        root.Parse(["--parsed", "ignored"]).GetValue(parsed).Should().Be(Reference<UnregisteredCliOwner>.Create("PO-1"));
    }
}
