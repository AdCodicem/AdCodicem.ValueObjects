using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Emit;

/// <summary>
/// Emits the strongly typed <c>System.Text.Json</c> converter nested in a value object.
/// </summary>
/// <remarks>
/// <para>
/// The converter is attached to the type through <c>[JsonConverter]</c>, so a value object serializes as its
/// bare underlying value with no registration step, no converter factory, and no reflection. That also makes it
/// work inside a <c>JsonSerializerContext</c>, which is what keeps the whole chain compatible with native AOT.
/// </para>
/// <para>
/// <c>null</c> is deliberately not handled: System.Text.Json rejects a null token for a non-nullable struct
/// before the converter is reached, and routes it to the nullable wrapper for an optional value object.
/// </para>
/// </remarks>
internal static class JsonConverterEmitter
{
    private const string Json = "global::System.Text.Json";
    private const string Reader = Json + ".Utf8JsonReader";
    private const string Writer = Json + ".Utf8JsonWriter";
    private const string Options = Json + ".JsonSerializerOptions";
    private const string TokenType = Json + ".JsonTokenType";
    private const string JsonException = Json + ".JsonException";
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>Characters the read path is willing to put on the stack before falling back to a string.</summary>
    private const int StackBufferSize = 512;

    public static void Emit(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        writer.Line($"/// <summary>Serializes <see cref=\"{model.TypeName}\"/> as its bare underlying value.</summary>");
        writer.Open($"public sealed class ValueJsonConverter : {Json}.Serialization.JsonConverter<{self}>");

        EmitRead(writer, model, underlying, value, self);
        EmitWrite(writer, underlying, self);
        EmitPropertyName(writer, underlying, self);
        EmitHelpers(writer, underlying, value);

        writer.Close();
        writer.Line();
    }

    private static void EmitRead(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public override {self} Read(ref {Reader} reader, global::System.Type typeToConvert, {Options} options)");

        if (model.HasSpanNormalizeHook)
        {
            // Copy the text into a stack buffer and normalize straight from it, so the normalized string is the
            // only allocation this read makes. CopyString unescapes, and a UTF-8 byte count is always an upper
            // bound on the char count, so the byte length is a safe size for the destination.
            writer.Open($"if (reader.TokenType == {TokenType}.String)");
            writer.Line("var length = reader.HasValueSequence");
            writer.Line("    ? checked((int)reader.ValueSequence.Length)");
            writer.Line("    : reader.ValueSpan.Length;");
            writer.Line();
            writer.Open($"if (length <= {StackBufferSize})");
            writer.Line($"global::System.Span<char> buffer = stackalloc char[{StackBufferSize}];");
            writer.Line("var written = reader.CopyString(buffer);");
            writer.Open($"if (!{self}.TryCreateFrom(buffer[..written], out var scoped, out var scopedValidation))");
            writer.Line($"throw new {JsonException}($\"The value is not a valid {model.TypeName}: {{scopedValidation.ErrorMessage}}\");");
            writer.Close();
            writer.Line();
            writer.Line("return scoped;");
            writer.Close();
            writer.Close();
            writer.Line();
        }

        writer.Line($"{value} raw;");
        writer.Open("switch (reader.TokenType)");

        if (underlying.IsJsonBoolean)
        {
            writer.Line($"case {TokenType}.True:");
            writer.Line($"case {TokenType}.False:");
            writer.Indent().Line($"raw = {underlying.JsonReadExpression};").Line("break;").Unindent();
        }
        else if (underlying.IsJsonNumber)
        {
            writer.Line($"case {TokenType}.Number:");
            writer.Indent().Line($"raw = {underlying.JsonReadExpression};").Line("break;").Unindent();
            writer.Line();
            writer.Line($"// Honour JsonNumberHandling.AllowReadingFromString, which many APIs turn on for interop.");
            writer.Line($"case {TokenType}.String when (options.NumberHandling & {Json}.Serialization.JsonNumberHandling.AllowReadingFromString) != 0:");
            writer.Indent();
            writer.Open($"if (!global::AdCodicem.ValueObjects.UnderlyingValue.TryParse<{value}>(reader.GetString(), {Invariant}, out raw))");
            writer.Line($"throw new {JsonException}($\"The value could not be read as {model.TypeName}.\");");
            writer.Close();
            writer.Line();
            writer.Line("break;");
            writer.Unindent();
        }
        else
        {
            writer.Line($"case {TokenType}.String:");
            writer.Indent().Line($"raw = {underlying.JsonReadExpression};").Line("break;").Unindent();
        }

        writer.Line();
        writer.Line("default:");
        writer.Indent();
        writer.Line($"throw new {JsonException}($\"Expected a JSON {DescribeJson(underlying)} for {model.TypeName} but found {{reader.TokenType}}.\");");
        writer.Unindent();
        writer.Close();
        writer.Line();

        writer.Open($"if (!{self}.TryCreate(raw, out var result, out var validation))");
        writer.Line($"throw new {JsonException}($\"The value is not a valid {model.TypeName}: {{validation.ErrorMessage}}\");");
        writer.Close();
        writer.Line();
        writer.Line("return result;");

        writer.Close();
        writer.Line();
    }

    private static void EmitWrite(CodeWriter writer, UnderlyingType underlying, string self)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public override void Write({Writer} writer, {self} value, {Options} options)");

        switch (underlying.Kind)
        {
            case UnderlyingKind.Boolean:
                writer.Line("writer.WriteBooleanValue(value.Value);");
                break;

            case UnderlyingKind.Char:
                writer.Line("global::System.Span<char> buffer = stackalloc char[1];");
                writer.Line("buffer[0] = value.Value;");
                writer.Line("writer.WriteStringValue(buffer);");
                break;

            case UnderlyingKind.SByte or UnderlyingKind.Byte or UnderlyingKind.Int16 or UnderlyingKind.UInt16:
                writer.Line("writer.WriteNumberValue((int)value.Value);");
                break;

            case UnderlyingKind.String or UnderlyingKind.Guid or UnderlyingKind.DateTime or UnderlyingKind.DateTimeOffset:
                writer.Line("writer.WriteStringValue(value.Value);");
                break;

            case UnderlyingKind.Int32 or UnderlyingKind.UInt32 or UnderlyingKind.Int64
                or UnderlyingKind.UInt64 or UnderlyingKind.Decimal or UnderlyingKind.Double or UnderlyingKind.Single:
                writer.Line("writer.WriteNumberValue(value.Value);");
                break;

            default:
                // Formatted into a stack buffer, so a date or a 128-bit integer costs no intermediate string.
                writer.Line($"global::System.Span<char> buffer = stackalloc char[{underlying.FormatBufferSize}];");
                writer.Line("var current = value.Value;");
                writer.Line($"global::AdCodicem.ValueObjects.UnderlyingValue.TryFormat(in current, buffer, out var written, {FormatArgument(underlying)}, {Invariant});");
                writer.Line("writer.WriteStringValue(buffer[..written]);");
                break;
        }

        writer.Close();
        writer.Line();
    }

    private static void EmitPropertyName(CodeWriter writer, UnderlyingType underlying, string self)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public override {self} ReadAsPropertyName(ref {Reader} reader, global::System.Type typeToConvert, {Options} options)");
        writer.Open($"if (!{self}.TryParse(reader.GetString(), {Invariant}, out var result))");
        writer.Line($"throw new {JsonException}(\"The dictionary key is not a valid value.\");");
        writer.Close();
        writer.Line();
        writer.Line("return result;");
        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Open($"public override void WriteAsPropertyName({Writer} writer, {self} value, {Options} options)");

        if (underlying.IsString)
        {
            writer.Line("writer.WritePropertyName(value.Value);");
        }
        else
        {
            writer.Line($"global::System.Span<char> buffer = stackalloc char[{underlying.FormatBufferSize}];");
            writer.Open($"if (value.TryFormat(buffer, out var written, {FormatArgument(underlying)}, {Invariant}))");
            writer.Line("writer.WritePropertyName(buffer[..written]);");
            writer.Close();
            writer.Open("else");
            writer.Line("writer.WritePropertyName(value.ToString());");
            writer.Close();
        }

        writer.Close();
        writer.Line();
    }

    private static void EmitHelpers(CodeWriter writer, UnderlyingType underlying, string value)
    {
        switch (underlying.Kind)
        {
            case UnderlyingKind.Char:
                writer.Open($"private static char ReadChar(ref {Reader} reader)");
                writer.Line("var text = reader.GetString();");
                writer.Line($"return text is {{ Length: 1 }} ? text[0] : throw new {JsonException}(\"Expected a single character.\");");
                writer.Close();
                writer.Line();
                break;

            case UnderlyingKind.DateOnly or UnderlyingKind.TimeOnly or UnderlyingKind.TimeSpan
                or UnderlyingKind.Int128 or UnderlyingKind.UInt128:
                writer.Open($"private static {value} {HelperName(underlying)}(ref {Reader} reader)");
                writer.Open($"if (!global::AdCodicem.ValueObjects.UnderlyingValue.TryParse<{value}>(reader.GetString(), {Invariant}, out var parsed))");
                writer.Line($"throw new {JsonException}(\"The value is not in the expected format.\");");
                writer.Close();
                writer.Line();
                writer.Line("return parsed;");
                writer.Close();
                writer.Line();
                break;

            default:
                break;
        }
    }

    private static string HelperName(UnderlyingType underlying) => underlying.Kind switch
    {
        UnderlyingKind.DateOnly => "ReadDateOnly",
        UnderlyingKind.TimeOnly => "ReadTimeOnly",
        UnderlyingKind.TimeSpan => "ReadTimeSpan",
        UnderlyingKind.Int128 => "ReadInt128",
        UnderlyingKind.UInt128 => "ReadUInt128",
        _ => "Read",
    };

    private static string FormatArgument(UnderlyingType underlying)
        => underlying.RoundTripFormat is null ? "default" : LiteralFactory.Quote(underlying.RoundTripFormat);

    private static string DescribeJson(UnderlyingType underlying)
        => underlying switch
        {
            { IsJsonBoolean: true } => "boolean",
            { IsJsonNumber: true } => "number",
            _ => "string",
        };
}
