namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Chooses whom a JSON Schema exported through <see cref="ValueObjectJsonSchema"/> describes a value object for.
/// </summary>
public enum ValueObjectJsonSchemaProfile
{
    /// <summary>
    /// What the serializer reads and writes under the options, with the formats OpenAPI uses: the default.
    /// </summary>
    /// <remarks>
    /// A number the options let be read or written as text is a number or a string held to the form a number is written
    /// in, as System.Text.Json documents the number alone, and a real under
    /// <see cref="System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals"/> lists the named
    /// literals its bounds let through. Every <c>format</c> the type declares is kept: <c>int32</c>, <c>decimal</c> or
    /// <c>iban</c> as well as <c>uuid</c> or <c>date</c>.
    /// </remarks>
    OpenApi,

    /// <summary>
    /// What a language model should send: the underlying type alone, with any format JSON Schema does not define moved
    /// into the description.
    /// </summary>
    /// <remarks>
    /// A number is a number, whatever the options let the serializer read, as a tool's schema describes a bare number,
    /// and its example and known values are written as numbers. A <c>format</c> JSON Schema defines, such as
    /// <c>uuid</c>, <c>date</c> or <c>date-time</c>, stays; any other, such as <c>int32</c>, <c>decimal</c> or
    /// <c>iban</c>, which a model or its provider may not know or may refuse, becomes a sentence of the description. The
    /// description of a closed set also names each of its values, with the description each was declared with.
    /// </remarks>
    LanguageModel,
}
