using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.Json;
using Microsoft.Extensions.AI;

namespace AdCodicem.ValueObjects.AI;

/// <summary>
/// Describes value objects in the schemas Microsoft.Extensions.AI publishes, and answers a model that sends one a value
/// its rules refuse with the rule, not with an exception the model never sees.
/// </summary>
/// <remarks>
/// <para>
/// <c>AIFunctionFactory</c> describes a tool's parameters from their <c>JsonTypeInfo</c>, and a value object, serialized
/// by a converter of its own, has none to read: each value-object parameter is published as <c>true</c>, the schema that
/// accepts anything, and the model has to guess its length, pattern, bounds and allowed values. Binding still validates
/// each argument, through the generated converter, but <c>FunctionInvokingChatClient</c> answers the exception it throws
/// with "Error: Function failed.", which tells the model nothing it can correct. The two calls together close both gaps:
/// </para>
/// <code>
/// AIFunction placeOrder = AIFunctionFactory.Create(
///         tools.PlaceOrder,
///         new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
///     .WithValueObjectValidation();
/// </code>
/// <para>
/// Where reflection-based serialization is disabled, as native AOT disables it, the factory's default options describe
/// no value object at all, and refuse to: hand it <c>SerializerOptions</c> whose resolver is a source-generated context
/// naming <see cref="ValueObjectJsonConverterFactory"/> and the types of the parameters and of the result.
/// </para>
/// </remarks>
public static class ValueObjectAIExtensions
{
    private static readonly Func<AIJsonSchemaCreateContext, JsonNode, JsonNode> Describe
        = static (context, schema) => ValueObjectJsonSchema.Apply(context.TypeInfo, schema, ValueObjectJsonSchemaProfile.LanguageModel);

    /// <summary>
    /// Returns a copy of the options whose <see cref="AIJsonSchemaCreateOptions.TransformSchemaNode"/> describes each
    /// value object with the rules declared on it, after any transform the options already carry.
    /// </summary>
    /// <param name="options">The options to copy, or <see langword="null"/> for <see cref="AIJsonSchemaCreateOptions.Default"/>.</param>
    /// <returns>A copy of the options, every other setting kept; the options given are left as they are.</returns>
    /// <remarks>
    /// <para>
    /// Each value object, alone, nullable, or held by a collection, a dictionary or an object, is described as its
    /// underlying value under <see cref="ValueObjectJsonSchemaProfile.LanguageModel"/>: its JSON type alone, its lengths,
    /// pattern and numeric bounds, a closed set of known values as <c>enum</c>, a date bound as a sentence of the
    /// description, its description after the one a <c>[Description]</c> gives the parameter, and its example; a format
    /// JSON Schema does not define moves into the description. A transform the options already carry runs first, and
    /// this one completes what it wrote.
    /// </para>
    /// <para>
    /// Under OpenAI's strict mode, Microsoft.Extensions.AI.OpenAI moves the lengths, the pattern, the bounds and the
    /// format into the description, and keeps <c>type</c>, <c>enum</c> and <c>examples</c>: the rules still reach the
    /// model.
    /// </para>
    /// </remarks>
    public static AIJsonSchemaCreateOptions WithValueObjects(this AIJsonSchemaCreateOptions? options)
    {
        var source = options ?? AIJsonSchemaCreateOptions.Default;
        var inner = source.TransformSchemaNode;

        return source with
        {
            TransformSchemaNode = inner is null ? Describe : (context, schema) => Describe(context, inner(context, schema)),
        };
    }

    /// <summary>
    /// Wraps a function so that an argument a value object refuses is answered with its rule, not an exception.
    /// </summary>
    /// <param name="function">The function, built from a method by <see cref="AIFunctionFactory"/>.</param>
    /// <returns>The wrapping function, or <paramref name="function"/> itself when it wraps one already.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Before the function binds its arguments, each argument of a value-object parameter is read as the binding reads it,
    /// through the contract the function's serializer options hold for the parameter, which runs the value object's
    /// converter and its rules. The first refused, in the order of the parameters, is returned instead of calling the
    /// function, as a JSON object <c>FunctionInvokingChatClient</c> hands the model as it is:
    /// </para>
    /// <code>
    /// {"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 100."}
    /// </code>
    /// <para>
    /// The message is the converter's, which names the value object and the rule, never the value sent; where the
    /// converter leaves it to System.Text.Json, for a number its underlying type cannot hold, or where a converter of the
    /// application's own refuses the value with an exception of its own, it is <c>The value is not a valid Quantity.</c>,
    /// naming the value object alone. An argument the value object cannot read at all is <c>value_object.not_parsable</c>, unless a
    /// converter of the application's own carries a code; an absent one the parameter has no default for, and a
    /// <c>null</c> for a value object that cannot be <see langword="null"/>, which the binding would pass on as an
    /// uninitialized instance, are <c>value_object.required</c>. A collection, a dictionary or an object is read the same
    /// way, and answered for a value object it holds, under the parameter's name; any other error is left to the
    /// function, which throws it as it would without the wrapper.
    /// </para>
    /// <para>
    /// An instance of the parameter's type is passed on as it is. Text or a number, which the binding converts through a
    /// JSON round trip of its own rather than refuses, is read as the binding converts it: text that may be JSON as JSON
    /// first, then the value written through the contract of its own type and read back; it is refused only when neither
    /// reading takes it.
    /// </para>
    /// <para>
    /// The parameters are read from <see cref="AIFunction.UnderlyingMethod"/>, once: a function no method backs, or whose
    /// parameters cannot hold a value object, is called as it is. A parameter a <c>BindParameter</c> callback binds, while
    /// the schema still lists it, is read as the serializer would read it.
    /// </para>
    /// </remarks>
    public static AIFunction WithValueObjectValidation(this AIFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);

        return function is ValueObjectValidatingFunction ? function : new ValueObjectValidatingFunction(function);
    }
}
