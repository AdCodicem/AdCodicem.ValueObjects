using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Marten;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The member source where the runtime supports no dynamic code, as under native AOT, run under the JIT in a child process
/// of its own (<see cref="WithoutDynamicCode"/>): a generated value object is translated with no registration, and a value
/// object the registry can describe only by reflection, a construction of a generic one or one written by hand, is refused
/// with the registrations that teach it, until it is registered or taught to the store.
/// </summary>
public sealed class MartenWithoutDynamicCodeTests
{
    private const string Where = "select d.data from public.mt_doc_martenstaticholder as d where ";

    [Fact]
    public async Task A_value_object_only_reflection_describes_is_refused_until_it_is_registered_or_taught()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(MartenWithoutDynamicCodeTests), nameof(A_value_object_only_reflection_describes_is_refused_until_it_is_registered_or_taught)))
        {
            return;
        }

        using var store = MartenHarness.Offline(static options => options.UseValueObjects());
        MartenHarness.Sql<MartenStaticHolder>(store, static holders => holders.Where(x => x.Page > PageNumber.Create(3))).Text
            .Should().Be(Where + "CAST(d.data ->> 'Page' as integer) > :p0;");

        FluentActions.Invoking(() => MartenHarness.Sql<MartenStaticHolder>(store, static holders => holders.Where(x => x.Code == Declared<MartenStaticDeclaration, string>.Create("ABC"))))
            .Should().Throw<NotSupportedException>().WithMessage(NotRegistered("Declared<MartenStaticDeclaration, string>"));
        FluentActions.Invoking(() => MartenHarness.Sql<MartenStaticHolder>(store, static holders => holders.Where(x => x.Reference == Reference<MartenStaticOwner>.Create("PO-1"))))
            .Should().Throw<NotSupportedException>().WithMessage(NotRegistered("Reference<MartenStaticOwner>"));

        // Registered, or taught to the store one by one, each is translated: registered after the store was built, the
        // first time a query meets it.
        ValueObjectRegistry.Register<Declared<MartenStaticDeclaration, string>, string>(MartenStaticDeclaration.Schema);
        MartenHarness.Sql<MartenStaticHolder>(store, static holders => holders.Where(x => x.Code == Declared<MartenStaticDeclaration, string>.Create("XYZ"))).Text
            .Should().Be(Where + "d.data ->> 'Code' = :p0;");
        using var registered = MartenHarness.Offline(static options => options.UseValueObjects());
        MartenHarness.Sql<MartenStaticHolder>(registered, static holders => holders.Where(x => x.Code == Declared<MartenStaticDeclaration, string>.Create("ABC"))).Text
            .Should().Be(Where + "d.data ->> 'Code' = :p0;");
        using var taught = MartenHarness.Offline(static options => options.UseValueObjects().UseValueObject<Reference<MartenStaticOwner>, string>());
        MartenHarness.Sql<MartenStaticHolder>(taught, static holders => holders.Where(x => x.Reference == Reference<MartenStaticOwner>.Create("PO-1"))).Text
            .Should().Be(Where + "d.data ->> 'Reference' = :p0;");
    }

    private static string NotRegistered(string type)
        => $"'{type}' is a value object nothing registered, and describing it by reflection takes dynamic code, which this "
            + "runtime does not support. Register it at start-up, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema) for "
            + "a value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(static () => new "
            + "TSelf.ValueJsonConverter()) for a construction of a generic one, or teach it to the store with "
            + "UseValueObject<TSelf, TValue>().";
}

/// <summary>A document holding a value object of each kind a runtime without dynamic code meets.</summary>
public sealed class MartenStaticHolder
{
    public Guid Id { get; set; }

    public PageNumber Page { get; set; }

    public Declared<MartenStaticDeclaration, string> Code { get; set; }

    public Reference<MartenStaticOwner> Reference { get; set; }
}

/// <summary>The declaration of the value object written by hand a <see cref="MartenStaticHolder"/> holds.</summary>
public sealed class MartenStaticDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };
}

/// <summary>The owner of the references a <see cref="MartenStaticHolder"/> holds, which no other test closes over.</summary>
public sealed class MartenStaticOwner;
