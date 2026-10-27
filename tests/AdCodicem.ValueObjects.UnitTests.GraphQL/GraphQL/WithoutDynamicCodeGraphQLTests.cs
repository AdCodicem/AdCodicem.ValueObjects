using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.Metadata;
using HotChocolate;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// The HotChocolate package where the runtime supports no dynamic code, as under native AOT, each test run under the
/// JIT in a child process of its own (<see cref="WithoutDynamicCode"/>): a value object the registry holds is mapped as
/// anywhere else, and a construction of a generic one nothing registered is refused at start, naming what
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

    /// <summary>Gets the message of the one exception, a <see cref="NotSupportedException"/>, a schema failed at start with.</summary>
    private static async Task<string> Refusal(Func<Task> start)
        => (await start.Should().ThrowAsync<SchemaException>()).Which.Errors.Should().ContainSingle().Which.Exception
            .Should().BeOfType<NotSupportedException>().Which.Message;
}

/// <summary>The owner of the references nothing registers, which nothing else names.</summary>
public sealed class UnregisteredOwner;
