using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.HotChocolate.Data;
using AdCodicem.ValueObjects.Metadata;
using HotChocolate;
using HotChocolate.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// The two HotChocolate packages where the runtime supports no dynamic code, as under native AOT, each test run under the
/// JIT in a child process of its own (<see cref="WithoutDynamicCode"/>): a value object the registry holds is mapped and
/// filtered as anywhere else, and a construction of a generic one nothing registered is refused at start, naming what
/// maps it at compile time, which then does.
/// </summary>
public sealed class WithoutDynamicCodeGraphQLTests
{
    private const string Reflection =
        "' is a value object nothing registered, and describing it by reflection takes dynamic code, which this runtime does "
        + "not support. Register it at start-up, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), or map it to its "
        + "scalar with AddValueObjectScalar<TSelf, TValue>()";

    /// <summary>
    /// The scalar handler maps a value object the registry holds, refuses one nothing registered with a
    /// <see cref="NotSupportedException"/> naming the registry and the typed scalar, which maps it.
    /// </summary>
    [Fact]
    public async Task The_scalars_refuse_a_value_object_nothing_registered_naming_the_typed_scalar()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(WithoutDynamicCodeGraphQLTests), nameof(The_scalars_refuse_a_value_object_nothing_registered_naming_the_typed_scalar)))
        {
            return;
        }

        var registered = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<EchoQuery<Iban>>().AddValueObjects());
        (await registered.ExecuteAsync("""{ echo(value: "fr76 3000 6000 0112 3456 7890 189") }""")).Data()
            .Should().Be("""{"echo":"FR7630006000011234567890189"}""");

        var unregistered = () => GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<EchoQuery<Reference<UnregisteredOwner>>>().AddValueObjects());
        (await Refusal(unregistered)).Should().Be($"'{typeof(Reference<UnregisteredOwner>)}{Reflection}.");

        var typed = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<EchoQuery<Reference<UnregisteredOwner>>>()
            .AddValueObjects().AddValueObjectScalar<Reference<UnregisteredOwner>, string>());
        (await typed.ExecuteAsync("""{ echo(value: " po-1 ") }""")).Data().Should().Be("""{"echo":"PO-1"}""");
    }

    /// <summary>
    /// The filter interceptor replaces the input HotChocolate.Data infers for a value object the registry holds, and
    /// refuses one nothing registered with a <see cref="NotSupportedException"/> naming the registry, the typed scalar and
    /// the typed binding: the binding alone meets the scalar handler's refusal, the two together filter it, and so does a
    /// registration.
    /// </summary>
    [Fact]
    public async Task The_filters_refuse_a_value_object_nothing_registered_naming_the_typed_scalar_and_binding()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(WithoutDynamicCodeGraphQLTests), nameof(The_filters_refuse_a_value_object_nothing_registered_naming_the_typed_scalar_and_binding)))
        {
            return;
        }

        var registered = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<RegisteredRow>>().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows(
                new RegisteredRow { Id = 1, Iban = Iban.Create(FilterRows.French) },
                new RegisteredRow { Id = 2, Iban = Iban.Create(FilterRows.German) }));
        (await registered.ExecuteAsync("""{ rows(where: { iban: { eq: "fr76 3000 6000 0112 3456 7890 189" } }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":1}]}""");

        var unbound = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<UnregisteredRow>>()
                .AddValueObjectScalar<Reference<UnregisteredOwner>, string>().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows<UnregisteredRow>());
        (await Refusal(unbound)).Should().Be(
            $"'{typeof(Reference<UnregisteredOwner>)}{Reflection} and bind it in the filter convention with BindValueObject<TSelf, TValue>().");

        var untyped = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<UnregisteredRow>>()
                .AddFiltering(static convention => convention.AddDefaults().BindValueObject<Reference<UnregisteredOwner>, string>())
                .AddValueObjectFiltering(),
            static services => services.AddRows<UnregisteredRow>());
        (await Refusal(untyped)).Should().Be($"'{typeof(Reference<UnregisteredOwner>)}{Reflection}.", "the binding's operands are the scalar");

        var bound = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<UnregisteredRow>>()
                .AddValueObjectScalar<Reference<UnregisteredOwner>, string>()
                .AddFiltering(static convention => convention.AddDefaults().BindValueObject<Reference<UnregisteredOwner>, string>())
                .AddValueObjectFiltering(),
            static services => services.AddRows(
                new UnregisteredRow { Id = 1, Iban = Iban.Create(FilterRows.French), Ref = Reference<UnregisteredOwner>.Create("PO-1") },
                new UnregisteredRow { Id = 2, Iban = Iban.Create(FilterRows.German), Ref = Reference<UnregisteredOwner>.Create("PO-2") }));
        (await bound.ExecuteAsync("""{ rows(where: { ref: { eq: " po-2 " }, iban: { neq: "FR7630006000011234567890189" } }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":2}]}""");

        ValueObjectRegistry.Register<Reference<RegisteredOwner>, string>(Reference<RegisteredOwner>.Schema);
        var registration = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<RegisteredReferenceRow>>().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows(
                new RegisteredReferenceRow { Id = 1, Ref = Reference<RegisteredOwner>.Create("PO-1") },
                new RegisteredReferenceRow { Id = 2, Ref = Reference<RegisteredOwner>.Create("PO-2") }));
        (await registration.ExecuteAsync("""{ rows(where: { ref: { in: [" po-1 "] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
    }

    /// <summary>Gets the message of the one exception, a <see cref="NotSupportedException"/>, a schema failed at start with.</summary>
    private static async Task<string> Refusal(Func<Task> start)
        => (await start.Should().ThrowAsync<SchemaException>()).Which.Errors.Should().ContainSingle().Which.Exception
            .Should().BeOfType<NotSupportedException>().Which.Message;
}

/// <summary>The owner of the references nothing registers, which nothing else names.</summary>
public sealed class UnregisteredOwner;

/// <summary>The owner of the references a test without dynamic code registers, which nothing else names.</summary>
public sealed class RegisteredOwner;

/// <summary>A row holding a value object the registry holds.</summary>
public sealed class RegisteredRow
{
    public int Id { get; set; }

    public Iban Iban { get; set; }
}

/// <summary>A row holding a value object the registry holds, beside a construction of a generic one nothing registers.</summary>
public sealed class UnregisteredRow
{
    public int Id { get; set; }

    public Iban Iban { get; set; }

    public Reference<UnregisteredOwner> Ref { get; set; }
}

/// <summary>A row holding a construction of a generic value object the test registers.</summary>
public sealed class RegisteredReferenceRow
{
    public int Id { get; set; }

    public Reference<RegisteredOwner> Ref { get; set; }
}
