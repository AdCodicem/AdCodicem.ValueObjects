using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// The parameters of a method a model calls that hold value objects, and the check of the arguments it sends for them,
/// read as the method's own binding reads them, through the same contract, so that an argument is refused exactly when
/// binding it would throw.
/// </summary>
/// <remarks>
/// <para>
/// A value-object parameter is read through the contract the serializer options hold for it, which runs the value
/// object's own converter: the form it travels in, a number read from text where the options allow it included, and its
/// rules, through <c>TryCreate</c>, never <c>CreateUnchecked</c>. A refusal carries the code of the rule and the
/// converter's message, which names the value object and the rule, never the value; where the converter leaves the
/// message to System.Text.Json, which names the path of the value, a dictionary's keys included, or where no value
/// object explains the refusal, the message names the value object alone. An absent argument the binding has no default
/// for, and a <see langword="null"/> for a value object that cannot be one, which the binding would pass on as an
/// uninitialized instance, are refused as <see cref="ValueObjectErrorCodes.Required"/>.
/// </para>
/// <para>
/// A parameter that may hold value objects, a collection, a dictionary or an object, is read through its contract too,
/// and refused only for a value object it holds: any other error is left to the binding. A parameter that cannot hold
/// one, text, a number or a <see cref="JsonElement"/>, is never read. An instance of the parameter's type is left to the
/// binding as it is; any other argument that is not JSON, text or a number a host hands over, is read as
/// Microsoft.Extensions.AI's binding converts it, through JSON, and refused only when that conversion fails, which leaves
/// the binding nothing but a value of the wrong type to call the method with.
/// </para>
/// <para>
/// It names no type of Microsoft.Extensions.AI, so that any host of tools that binds the arguments of a method through
/// System.Text.Json can link it: the AI package checks the arguments of an <c>AIFunction</c> with it.
/// </para>
/// </remarks>
internal sealed partial class ValueObjectArguments
{
    /// <summary>
    /// The attribute that renames a parameter in the schema, read by its name: Microsoft.Extensions.AI marks it
    /// experimental, and a renaming or a removal must leave the check working.
    /// </summary>
    private const string ParameterNameAttribute = "Microsoft.Extensions.AI.AIParameterNameAttribute";

    /// <summary>What an absent value is refused with, as the descriptor and the minimal API filter refuse it.</summary>
    private const string RequiredMessage = "A value is required.";

    /// <summary>
    /// The message of a refusal thrown without one and caught before System.Text.Json could write its own: the
    /// exception's default, which names its own type alone.
    /// </summary>
    private static readonly string Unwritten = new ValueObjectJsonException(null, typeof(ValueObjectArguments), ValueObjectErrorCodes.NotParsable).Message;

    private readonly Parameter[] _parameters;

    private ValueObjectArguments(Parameter[] parameters) => _parameters = parameters;

    /// <summary>
    /// Finds the parameters of a method that hold value objects, among those its schema lists.
    /// </summary>
    /// <param name="method">The method, or <see langword="null"/> for a tool no method backs.</param>
    /// <param name="schema">The schema of its parameters, whose <c>properties</c> name those a model supplies.</param>
    /// <param name="options">The options its arguments are bound with.</param>
    /// <returns>The parameters to check, or <see langword="null"/> when none may hold a value object.</returns>
    public static ValueObjectArguments? For(MethodInfo? method, JsonElement schema, JsonSerializerOptions options)
    {
        if (method is null
            || schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties"u8, out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parameters = new List<Parameter>();
        foreach (var parameter in method.GetParameters())
        {
            if (parameter.Name is null)
            {
                // No binding supplies a parameter without a name.
                continue;
            }

            // A parameter the schema leaves out, a cancellation token or a service, is bound from no argument, and one the
            // options have no contract for fails the binding itself.
            var name = NameOf(parameter);
            if (!properties.TryGetProperty(name, out _) || !options.TryGetTypeInfo(parameter.ParameterType, out var contract))
            {
                continue;
            }

            var type = parameter.ParameterType;
            var valueObject = ValueObjectRegistry.IsValueObject(type);
            if (!valueObject && contract.Kind == JsonTypeInfoKind.None)
            {
                // Text, a number, a JsonElement: nothing it binds is a value object.
                continue;
            }

            var underlying = Nullable.GetUnderlyingType(type);
            parameters.Add(new Parameter(
                name,
                contract,
                valueObject,
                Nullable: underlying is not null,
                Required: !HasDefault(parameter),
                TypeName: (underlying ?? type).Name));
        }

        return parameters.Count == 0 ? null : new ValueObjectArguments([.. parameters]);
    }

    /// <summary>
    /// Checks the arguments in the order of the parameters, and stops at the first refused.
    /// </summary>
    /// <param name="arguments">The arguments, by the name the schema gives each parameter.</param>
    /// <returns>The refusal of the first argument refused, or <see langword="null"/> when none is.</returns>
    public ValueObjectArgumentRejection? FirstRejection(IReadOnlyDictionary<string, object?> arguments)
    {
        foreach (var parameter in _parameters)
        {
            var present = arguments.TryGetValue(parameter.Name, out var value);
            if (parameter.Check(present, value) is { } rejection)
            {
                return rejection;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the name a parameter is supplied under: the one its <c>AIParameterNameAttribute</c> gives, or its own.
    /// </summary>
    private static string NameOf(ParameterInfo parameter)
    {
        foreach (var attribute in parameter.GetCustomAttributesData())
        {
            if (attribute.ConstructorArguments is [{ Value: string renamed }] && attribute.AttributeType.FullName == ParameterNameAttribute)
            {
                return renamed;
            }
        }

        return parameter.Name!;
    }

    /// <summary>
    /// Tells a parameter the binding fills in when its argument is absent, as Microsoft.Extensions.AI tells one.
    /// </summary>
    private static bool HasDefault(ParameterInfo parameter)
        => parameter.HasDefaultValue
           || parameter.IsOptional
           || parameter.GetCustomAttribute<DefaultValueAttribute>(inherit: true) is not null;

    /// <summary>
    /// Finds the refusal of a value object in an exception or in one it wraps.
    /// </summary>
    private static ValueObjectJsonException? RefusalIn(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ValueObjectJsonException refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the message of a value object's refusal: the converter's, which names the value object and the rule, or,
    /// where the converter left it to System.Text.Json, for a token the underlying type cannot hold, one naming the value
    /// object alone, since System.Text.Json's names the path of the value, which holds a dictionary's keys.
    /// </summary>
    private static string MessageOf(ValueObjectJsonException refusal)
    {
        var message = refusal.Message;
        var written = message != Unwritten
            && (refusal.Path is not { } path
                || !message.EndsWith($" Path: {path} | LineNumber: {refusal.LineNumber} | BytePositionInLine: {refusal.BytePositionInLine}.", StringComparison.Ordinal));

        return written ? message : NotValid(refusal.ValueObjectType.Name);
    }

    /// <summary>Writes the message of a refusal that names the value object alone.</summary>
    private static string NotValid(string typeName) => $"The value is not a valid {typeName}.";

    /// <summary>
    /// Matches text that may be JSON, as Microsoft.Extensions.AI's binding tells it before reading it as JSON: a literal,
    /// a number, a string, an array, an object or a comment, after any white space.
    /// </summary>
    [GeneratedRegex(@"^\s*(?:null|false|true|-?[0-9]|""|\[|\{|//|/\*)")]
    private static partial Regex PotentiallyJson();

    /// <summary>
    /// A parameter that may hold value objects: the name a model supplies it under, and how it is bound.
    /// </summary>
    /// <param name="Name">The name of its argument.</param>
    /// <param name="Contract">The contract it is bound through.</param>
    /// <param name="ValueObject">Whether it is a value object, rather than a type that may hold some.</param>
    /// <param name="Nullable">Whether it is a nullable value object.</param>
    /// <param name="Required">Whether the binding has no value to fill it with when its argument is absent.</param>
    /// <param name="TypeName">The name of its type, for a refusal no value object explained.</param>
    private sealed record Parameter(string Name, JsonTypeInfo Contract, bool ValueObject, bool Nullable, bool Required, string TypeName)
    {
        /// <summary>
        /// Checks the argument sent for the parameter.
        /// </summary>
        /// <param name="present">Whether an argument was sent under its name.</param>
        /// <param name="value">The argument sent, as the host hands it over.</param>
        /// <returns>The refusal, or <see langword="null"/> when the binding takes the argument.</returns>
        public ValueObjectArgumentRejection? Check(bool present, object? value)
        {
            if (!present || value is null)
            {
                // The binding fills an absent argument with the parameter's default, and hands a value object that
                // cannot be null its uninitialized instance for a null.
                return ValueObject && (present ? !Nullable : Required)
                    ? new ValueObjectArgumentRejection(Name, ValueObjectErrorCodes.Required, RequiredMessage)
                    : null;
            }

            if (Contract.Type.IsInstanceOfType(value))
            {
                // An instance, which the binding passes on as it is.
                return null;
            }

            try
            {
                switch (value)
                {
                    case JsonElement element:
                        _ = element.Deserialize(Contract);
                        return null;

                    case JsonDocument document:
                        _ = document.Deserialize(Contract);
                        return null;

                    case JsonNode node:
                        _ = node.Deserialize(Contract);
                        return null;
                }
            }
            catch (JsonException exception)
            {
                return Refused(exception);
            }

            return Converted(value);
        }

        /// <summary>
        /// Reads an argument that is not JSON as the binding converts it: text that may be JSON is read as JSON first,
        /// then the value is written through the contract of its own type and read back through the parameter's. The
        /// binding calls the method with the value as it is when neither reading takes it, and the call fails, so only
        /// then is the argument refused: with what reading the text as JSON told, unless it told only that the value was
        /// not of the value object's kind.
        /// </summary>
        private ValueObjectArgumentRejection? Converted(object value)
        {
            ValueObjectArgumentRejection? asJson = null;
            if (value is string text && PotentiallyJson().IsMatch(text))
            {
                try
                {
                    _ = JsonSerializer.Deserialize(text, Contract);
                    return null;
                }
                catch (JsonException exception)
                {
                    asJson = Refused(exception);
                }
            }

            if (!Contract.Options.TryGetTypeInfo(value.GetType(), out var own))
            {
                // The binding cannot write it either, and passes it on as it is.
                return asJson;
            }

            try
            {
                _ = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(value, own), Contract);
                return null;
            }
            catch (JsonException exception)
            {
                return asJson is { Code: not ValueObjectErrorCodes.NotParsable } ? asJson : Refused(exception) ?? asJson;
            }
        }

        /// <summary>
        /// Answers an argument the contract refused: with the value object's code and message, or, for a container
        /// or an object, only when a value object it holds refused it.
        /// </summary>
        private ValueObjectArgumentRejection? Refused(JsonException exception)
        {
            var refusal = RefusalIn(exception);
            if (!ValueObject)
            {
                return refusal is null ? null : new ValueObjectArgumentRejection(Name, refusal.ErrorCode, MessageOf(refusal));
            }

            // A value object's converter of the application's own may refuse a value without a code.
            return new ValueObjectArgumentRejection(
                Name,
                ValueObjectErrors.TryGetCode(exception, out var code) ? code : ValueObjectErrorCodes.NotParsable,
                refusal is null ? NotValid(TypeName) : MessageOf(refusal));
        }
    }
}
