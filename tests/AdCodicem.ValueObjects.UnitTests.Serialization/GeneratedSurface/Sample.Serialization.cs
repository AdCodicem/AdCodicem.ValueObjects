using System.Text.Json;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.BinarySerialization;
using AdCodicem.ValueObjects.UnitTests.SchemaGeneration;
using AdCodicem.ValueObjects.UnitTests.Web;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The serialization family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to
// Newtonsoft.Json, JSON Patch, MessagePack, protobuf-net and NJsonSchema.

public abstract partial class Sample
{
    /// <summary>
    /// Reads a body holding the refused text, as the token Newtonsoft.Json reads it from, through the formatter
    /// <c>AddValueObjectsNewtonsoftJson()</c> puts in MVC, and checks that it records the code the converter gives the
    /// refusal under the key of its error.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task RecordsTheCodeOfARefusedNewtonsoftBodyAsync();

    /// <summary>
    /// Patches a holder of the value object through <c>TryApplyTo</c> with the text the Newtonsoft.Json converter writes,
    /// by every operation and in every shape a member takes, and checks that it is read as a body reads it, that the
    /// refused text is refused with the code the converter gives it, and that removing the value object is refused.
    /// </summary>
    public abstract void PatchesThroughItsRules();

    /// <summary>
    /// Writes the value object through MessagePack, with the resolver <c>WithValueObjects</c> puts in front of the
    /// options' own, and checks that it writes and reads back what the bare value it carries writes and reads, under the
    /// standard options and under the <c>Native*</c> resolvers.
    /// </summary>
    public abstract void RoundTripsThroughMessagePackAsItsUnderlyingValue();

    /// <summary>
    /// Writes the value object through protobuf-net, on a model it is registered on with <c>AddValueObject</c>, and checks
    /// that it writes, reads back and is named in the <c>.proto</c> export as the bare value it carries is, alone,
    /// nullable and in a list, at each compatibility level, strict and trusted; or, over an underlying type protobuf-net
    /// cannot write, that it is refused by name.
    /// </summary>
    public abstract void RoundTripsThroughProtobufAsItsUnderlyingValue();

    /// <summary>
    /// Describes the value object with NJsonSchema's System.Text.Json settings, under the default options, the web
    /// defaults and the named literals of a real, and its Newtonsoft.Json settings, with <c>ValueObjectConverter</c> and without it where its type
    /// converter's text reads back, once <c>AddValueObjects()</c> is called on them; and checks that its definition is its
    /// underlying type on each, and that the payload each serializer writes of its instances, of the values at the edges
    /// of its rules and of values drawn from them, validates as Confluent's serializer validates a message.
    /// </summary>
    public abstract void DescribesItselfInEveryNJsonSchemaWire();
}

public partial class Sample<TSelf, TValue>
{
    public override async Task RecordsTheCodeOfARefusedNewtonsoftBodyAsync()
    {
        var written = JsonSerializer.Serialize(Small);
        var isNumber = !written.StartsWith('"') && written is not ("true" or "false");
        var token = isNumber && IsJsonNumber(Refused) ? Refused : JsonSerializer.Serialize(Refused);
        var body = $$"""{"value":{{token}}}""";

        var refusal = FluentActions.Invoking(() => Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftHolder<TSelf>>(
                body, new Newtonsoft.Json.JsonSerializerSettings().AddValueObjects()))
            .Should().Throw<Newtonsoft.Json.JsonSerializationException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue("the converter refuses {0} with a code", body);

        (await NewtonsoftJsonInputFormatterTests.ReadCodesAsync(typeof(NewtonsoftHolder<TSelf>), body))
            .Should().Equal(new Dictionary<string, string> { ["value"] = code! });
    }

    public override void PatchesThroughItsRules()
    {
        var written = JsonSerializer.Serialize(Small);
        var isNumber = !written.StartsWith('"') && written is not ("true" or "false");
        var token = isNumber && IsJsonNumber(Refused) ? Refused : JsonSerializer.Serialize(Refused);

        JsonPatchParity.Check<TSelf, TValue>(Small, Large, token);
    }

    public override void RoundTripsThroughMessagePackAsItsUnderlyingValue()
    {
        MessagePackParity.Check<TSelf, TValue>(Small);
        MessagePackParity.Check<TSelf, TValue>(Large);
    }

    public override void RoundTripsThroughProtobufAsItsUnderlyingValue()
    {
        ProtoParity.Check<TSelf, TValue>(Small);
        ProtoParity.Check<TSelf, TValue>(Large);
    }

    public override void DescribesItselfInEveryNJsonSchemaWire() => NJsonSchemaWires.Check<TSelf, TValue>(Small, Large);
}

/// <summary>An object holding a value object as a property Newtonsoft.Json sets, so that a refusal has a member to name.</summary>
/// <typeparam name="TSelf">The value object.</typeparam>
public sealed class NewtonsoftHolder<TSelf>
    where TSelf : struct
{
    /// <summary>Gets or sets the value object.</summary>
    public TSelf Value { get; set; }
}
