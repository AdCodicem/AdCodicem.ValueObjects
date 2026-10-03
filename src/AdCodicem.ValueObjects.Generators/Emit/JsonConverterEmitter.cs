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
/// A value object over a number honours <c>JsonNumberHandling</c> as the built-in converter of its underlying type
/// does, since System.Text.Json leaves the number handling of a custom converter to the converter: it reads a number
/// written as text under <c>AllowReadingFromString</c>, writes one as text under <c>WriteAsString</c>, and, over a
/// <see cref="double"/> or a <see cref="float"/>, writes <c>NaN</c> and the infinities as text under
/// <c>AllowNamedFloatingPointLiterals</c> and reads them under either reading option. Text is read as
/// System.Text.Json reads it, unescaped and parsed whole by <c>Utf8Parser</c>, with no white space, no group separator
/// and no culture, and written as a string value, which an indented writer lays out as it lays out any other. A value
/// object crosses the boundary exactly as its underlying type does.
/// </para>
/// <para>
/// <c>null</c> needs no case of its own. System.Text.Json hands a null token to the converter of a value type, and
/// the generated <c>Read</c> refuses it from its default arm with a <c>JsonException</c>; for an optional value
/// object, the nullable wrapper System.Text.Json puts around the converter answers the null itself.
/// </para>
/// <para>
/// The writers refuse what the reader would: an instance equal to the default whose value the type rejects is a
/// <c>JsonException</c> naming the type and the rule, rather than a value that faults the service reading it.
/// </para>
/// </remarks>
internal static class JsonConverterEmitter
{
    private const string Json = "global::System.Text.Json";
    private const string Reader = Json + ".Utf8JsonReader";
    private const string Writer = Json + ".Utf8JsonWriter";
    private const string Options = Json + ".JsonSerializerOptions";
    private const string TokenType = Json + ".JsonTokenType";
    private const string Handling = Json + ".Serialization.JsonNumberHandling";
    private const string JsonException = Json + ".JsonException";
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
    private const string ValidationResult = "global::AdCodicem.ValueObjects.ValidationResult";

    /// <summary>Characters the read path is willing to put on the stack before falling back to a string.</summary>
    private const int StackBufferSize = 512;

    /// <summary>Bytes the read path puts on the stack for a number written as text, far more than any number takes.</summary>
    private const int QuotedNumberBufferSize = 128;

    public static void Emit(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        writer.Line($"/// <summary>Serializes <see cref=\"{model.CrefName}\"/> as its bare underlying value.</summary>");
        writer.Open($"public sealed class ValueJsonConverter : {Json}.Serialization.JsonConverter<{self}>");

        EmitRead(writer, model, underlying, value, self);
        EmitWrite(writer, underlying, value, self);
        EmitPropertyName(writer, model, underlying, self);
        EmitWriteCheck(writer, model, underlying, value, self);
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
            writer.Line("int length = reader.HasValueSequence");
            writer.Line("    ? checked((int)reader.ValueSequence.Length)");
            writer.Line("    : reader.ValueSpan.Length;");
            writer.Line();
            writer.Open($"if (length <= {StackBufferSize})");
            writer.Line($"global::System.Span<char> buffer = stackalloc char[{StackBufferSize}];");
            writer.Line("int written = reader.CopyString(buffer);");
            writer.Open($"if (!{self}.TryCreateFrom(buffer[..written], out {self} scoped, out {ValidationResult} scopedValidation))");
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
        else if (underlying.IsJsonNumber && IsFloatingPoint(underlying))
        {
            writer.Line($"case {TokenType}.Number:");
            writer.Indent().Line($"raw = {underlying.JsonReadExpression};").Line("break;").Unindent();
            writer.Line();
            writer.Line("// Honour JsonNumberHandling as the built-in converter of the underlying type does: either option reads NaN");
            writer.Line("// and the infinities, spelled exactly so, and AllowReadingFromString a finite number written as text.");
            writer.Line($"case {TokenType}.String when (options.NumberHandling & ({Handling}.AllowReadingFromString | {Handling}.AllowNamedFloatingPointLiterals)) != 0:");
            writer.Indent();
            writer.Open("if (reader.ValueTextEquals(\"NaN\"))");
            writer.Line($"raw = {value}.NaN;");
            writer.Close();
            writer.Open("else if (reader.ValueTextEquals(\"Infinity\"))");
            writer.Line($"raw = {value}.PositiveInfinity;");
            writer.Close();
            writer.Open("else if (reader.ValueTextEquals(\"-Infinity\"))");
            writer.Line($"raw = {value}.NegativeInfinity;");
            writer.Close();
            writer.Line("// The parser reads an overflow as an infinity, which System.Text.Json refuses.");
            writer.Open(
                $"else if ((options.NumberHandling & {Handling}.AllowReadingFromString) == 0\n"
                + "    || !TryReadQuoted(ref reader, out raw)\n"
                + $"    || !{value}.IsFinite(raw))");
            writer.Line($"throw new {JsonException}($\"The value could not be read as {model.TypeName}.\");");
            writer.Close();
            writer.Line();
            writer.Line("break;");
            writer.Unindent();
        }
        else if (underlying.IsJsonNumber)
        {
            writer.Line($"case {TokenType}.Number:");
            writer.Indent().Line($"raw = {underlying.JsonReadExpression};").Line("break;").Unindent();
            writer.Line();
            writer.Line($"// Honour JsonNumberHandling.AllowReadingFromString, which many APIs turn on for interop.");
            writer.Line($"case {TokenType}.String when (options.NumberHandling & {Handling}.AllowReadingFromString) != 0:");
            writer.Indent();
            writer.Open("if (!TryReadQuoted(ref reader, out raw))");
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

        writer.Open($"if (!{self}.TryCreate(raw, out {self} result, out {ValidationResult} validation))");
        writer.Line($"throw new {JsonException}($\"The value is not a valid {model.TypeName}: {{validation.ErrorMessage}}\");");
        writer.Close();
        writer.Line();
        writer.Line("return result;");

        writer.Close();
        writer.Line();
    }

    private static void EmitWrite(CodeWriter writer, UnderlyingType underlying, string value, string self)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public override void Write({Writer} writer, {self} value, {Options} options)");
        writer.Line("ThrowIfRefused(in value);");
        writer.Line();

        if (underlying.IsJsonNumber)
        {
            // Honour JsonNumberHandling as the built-in converter of the underlying type does: every number as text
            // under WriteAsString, and NaN or an infinity as text under AllowNamedFloatingPointLiterals, in the
            // invariant form the reader above accepts back, as a string value, so that an indented writer lays it out.
            var condition = $"(options.NumberHandling & {Handling}.WriteAsString) != 0";
            if (IsFloatingPoint(underlying))
            {
                condition += $" || ((options.NumberHandling & {Handling}.AllowNamedFloatingPointLiterals) != 0 && !{value}.IsFinite(value.Value))";
            }

            writer.Open($"if ({condition})");
            writer.Line($"global::System.Span<char> buffer = stackalloc char[{underlying.FormatBufferSize}];");
            writer.Line($"{value} number = value.Value;");
            writer.Line($"global::AdCodicem.ValueObjects.UnderlyingValue.TryFormat(in number, buffer, out int length, default, {Invariant});");
            if (IsFloatingPoint(underlying))
            {
                // The built-in converter writes the + of an exponent as is, where the default encoder would escape it.
                writer.Line("global::System.ReadOnlySpan<char> text = buffer[..length];");
                writer.Open("if (global::System.MemoryExtensions.Contains(text, '+'))");
                writer.Line(
                    $"writer.WriteStringValue({Json}.JsonEncodedText.Encode(text, "
                    + "global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping));");
                writer.Close();
                writer.Open("else");
                writer.Line("writer.WriteStringValue(text);");
                writer.Close();
            }
            else
            {
                writer.Line("writer.WriteStringValue(buffer[..length]);");
            }

            writer.Line();
            writer.Line("return;");
            writer.Close();
            writer.Line();
        }

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
                writer.Line($"{value} current = value.Value;");
                writer.Line($"global::AdCodicem.ValueObjects.UnderlyingValue.TryFormat(in current, buffer, out int written, {FormatArgument(underlying)}, {Invariant});");
                writer.Line("writer.WriteStringValue(buffer[..written]);");
                break;
        }

        writer.Close();
        writer.Line();
    }

    private static void EmitPropertyName(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string self)
    {
        var value = underlying.FullName;

        writer.Line("/// <inheritdoc />");
        writer.Open($"public override {self} ReadAsPropertyName(ref {Reader} reader, global::System.Type typeToConvert, {Options} options)");
        writer.Open($"if (!{self}.TryParse(reader.GetString(), {Invariant}, out {self} result, out {ValidationResult} validation))");
        writer.Line($"throw new {JsonException}($\"The dictionary key is not a valid {model.TypeName}: {{validation.ErrorMessage}}\");");
        writer.Close();
        writer.Line();
        writer.Line("return result;");
        writer.Close();
        writer.Line();

        // A key carries the underlying value, in the form the JSON value is written in, as Write does. Formatting
        // the value object instead would hand the key to a formatting hook, whose text TryParse cannot read back.
        writer.Line("/// <inheritdoc />");
        writer.Open($"public override void WriteAsPropertyName({Writer} writer, {self} value, {Options} options)");
        writer.Line("ThrowIfRefused(in value);");
        writer.Line();

        switch (underlying.Kind)
        {
            case UnderlyingKind.String:
                writer.Line("writer.WritePropertyName(value.Value);");
                break;

            case UnderlyingKind.Boolean:
                writer.Line($"writer.WritePropertyName(value.Value.ToString({Invariant}));");
                break;

            case UnderlyingKind.Char:
                writer.Line("global::System.Span<char> buffer = stackalloc char[1];");
                writer.Line("buffer[0] = value.Value;");
                writer.Line("writer.WritePropertyName(buffer);");
                break;

            default:
                // The buffer holds the longest text of the type in its round-trip form, as in Write.
                writer.Line($"global::System.Span<char> buffer = stackalloc char[{underlying.FormatBufferSize}];");
                writer.Line($"{value} current = value.Value;");
                writer.Line($"global::AdCodicem.ValueObjects.UnderlyingValue.TryFormat(in current, buffer, out int written, {FormatArgument(underlying)}, {Invariant});");
                writer.Line("writer.WritePropertyName(buffer[..written]);");
                break;
        }

        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// Emits the check both writers run first, which refuses a value the type rejects rather than put it on the wire.
    /// </summary>
    /// <remarks>
    /// Only an instance equal to the default can hold one: every other went through <c>Create</c>, whose value is valid
    /// by construction, and costs the comparison <c>IsDefault</c> makes. Over a value type, a constructed zero equals the
    /// default too, so validation decides between a valid zero, which is written, and a refused one. The message names
    /// the type and the rule, never the value.
    /// </remarks>
    private static void EmitWriteCheck(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        writer.Line("/// <summary>Refuses to write a value the type rejects, which only an instance equal to the default can hold.</summary>");
        writer.Open($"private static void ThrowIfRefused(in {self} value)");
        writer.Open(underlying.IsReferenceType ? "if (value._value is null)" : $"if (value._value.Equals(default({value})))");
        writer.Line($"{value} current = value.Value;");
        writer.Line($"{ValidationResult} validation = {self}.Validate(in current);");
        writer.Open("if (!validation.IsValid)");
        writer.Line($"throw new {JsonException}($\"The value to write is not a valid {model.TypeName}: {{validation.ErrorMessage}}\");");
        writer.Close();
        writer.Close();
        writer.Close();
        writer.Line();
    }

    private static void EmitHelpers(CodeWriter writer, UnderlyingType underlying, string value)
    {
        switch (underlying.Kind)
        {
            case UnderlyingKind.Char:
                writer.Open($"private static char ReadChar(ref {Reader} reader)");
                writer.Line("string? text = reader.GetString();");
                writer.Line($"return text is {{ Length: 1 }} ? text[0] : throw new {JsonException}(\"Expected a single character.\");");
                writer.Close();
                writer.Line();
                break;

            case UnderlyingKind.DateOnly or UnderlyingKind.TimeOnly or UnderlyingKind.TimeSpan
                or UnderlyingKind.Int128 or UnderlyingKind.UInt128:
                writer.Open($"private static {value} {HelperName(underlying)}(ref {Reader} reader)");
                writer.Open($"if (!global::AdCodicem.ValueObjects.UnderlyingValue.TryParse<{value}>(reader.GetString(), {Invariant}, out {value} parsed))");
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

        if (underlying.IsJsonNumber)
        {
            // System.Text.Json reads a number written as text through Utf8Parser, from the unescaped text, which it
            // has to consume whole: no white space, no group separator, no culture. Text too long for the stack can
            // still be a number, padded with zeros, so the buffer then comes from the heap.
            writer.Line("/// <summary>Reads a number written as a JSON string, as System.Text.Json reads one.</summary>");
            writer.Open($"private static bool TryReadQuoted(ref {Reader} reader, out {value} value)");
            writer.Line("int length = reader.HasValueSequence ? checked((int)reader.ValueSequence.Length) : reader.ValueSpan.Length;");
            writer.Line($"global::System.Span<byte> text = length <= {QuotedNumberBufferSize} ? stackalloc byte[{QuotedNumberBufferSize}] : new byte[length];");
            writer.Line("int written = reader.CopyString(text);");
            writer.Line("return global::System.Buffers.Text.Utf8Parser.TryParse(text[..written], out value, out int consumed) && consumed == written;");
            writer.Close();
            writer.Line();
        }
    }

    private static bool IsFloatingPoint(UnderlyingType underlying)
        => underlying.Kind is UnderlyingKind.Double or UnderlyingKind.Single;

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
