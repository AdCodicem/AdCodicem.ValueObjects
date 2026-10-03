using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Emit;

/// <summary>
/// Emits the <see cref="System.ComponentModel.TypeConverter"/> nested in a value object.
/// </summary>
/// <remarks>
/// <para>
/// A type converter serves the code that converts text, or a number, through <c>TypeDescriptor</c> without knowing
/// the type. Probes found it used by the reflection-based configuration binder, MVC model binding without the
/// package's binder, Newtonsoft.Json without <c>ValueObjectConverter</c>, Spectre.Console.Cli, YamlDotNet's
/// reflection reader, Blazor <c>@bind</c>, Quartz job data, and Avalonia and WPF bindings (WPF read from its source,
/// not run). It is not everywhere, though: the configuration binding source generator, CsvHelper, System.CommandLine
/// 2.0, <c>XmlSerializer</c>, <c>DataContractSerializer</c>, Parquet.Net and the Azure Functions isolated worker
/// never read <c>TypeDescriptor</c>: they reach a value object only through an extension point of their own, so
/// support for them does not belong here. Having the converter costs one small class.
/// </para>
/// <para>
/// A numeric value object converts from and to every numeric type a value object may wrap, not only its own: the
/// callers of a type converter hand it whatever number they hold, Newtonsoft.Json a <see cref="long"/> for every JSON
/// integer, a numeric control a <see cref="decimal"/>. The conversion is checked, through a closed set of typed arms
/// and the bridges of <c>UnderlyingValue</c>, so it involves no reflection: a number the underlying type cannot hold
/// whole is refused as <c>value_object.not_parsable</c>, never truncated, and one that fits goes through
/// <c>Create</c> and the rules of the type.
/// </para>
/// </remarks>
internal static class TypeConverterEmitter
{
    private const string ComponentModel = "global::System.ComponentModel";
    private const string Context = ComponentModel + ".ITypeDescriptorContext";
    private const string Culture = "global::System.Globalization.CultureInfo";
    private const string Invariant = Culture + ".InvariantCulture";
    private const string Bridge = "global::AdCodicem.ValueObjects.UnderlyingValue";

    public static void Emit(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        var numeric = underlying.IsNumeric;

        writer.Line(numeric
            ? $"/// <summary>Converts <see cref=\"{model.CrefName}\"/> to and from text and any number.</summary>"
            : $"/// <summary>Converts <see cref=\"{model.CrefName}\"/> to and from text and its underlying value.</summary>");
        writer.Open($"public sealed class ValueTypeConverter : {ComponentModel}.TypeConverter");

        writer.Line("/// <inheritdoc />");
        writer.Line($"public override bool CanConvertFrom({Context}? context, global::System.Type sourceType)");
        writer.Line(numeric
            ? "    => sourceType == typeof(string) || IsNumber(sourceType) || base.CanConvertFrom(context, sourceType);"
            : underlying.IsString
                ? "    => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);"
                : $"    => sourceType == typeof(string) || sourceType == typeof({value}) || base.CanConvertFrom(context, sourceType);");
        writer.Line();

        EmitConvertFrom(writer, model, underlying, value, self);

        writer.Line("/// <inheritdoc />");
        writer.Line($"public override bool CanConvertTo({Context}? context, global::System.Type? destinationType)");
        writer.Line(numeric
            ? "    => destinationType == typeof(string) || (destinationType is not null && IsNumber(destinationType)) || base.CanConvertTo(context, destinationType);"
            : underlying.IsString
                ? "    => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);"
                : $"    => destinationType == typeof(string) || destinationType == typeof({value}) || base.CanConvertTo(context, destinationType);");
        writer.Line();

        EmitConvertTo(writer, underlying, value, self);

        if (numeric)
        {
            writer.Line();
            writer.Line("/// <summary>Tells a numeric type a value object may wrap, which this converter converts from and to.</summary>");
            writer.Open("private static bool IsNumber(global::System.Type type)");
            writer.Line("return type == " + string.Join("\n    || type == ", Types()) + ";");
            writer.Close();
        }

        writer.Close();
        writer.Line();
    }

    private static void EmitConvertFrom(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        var numeric = underlying.IsNumeric;

        writer.Line("/// <inheritdoc />");
        if (numeric)
        {
            writer.Line("/// <remarks>");
            writer.Line("/// A number of another numeric type is converted with a checked conversion, then created through the rules of the");
            writer.Line(underlying.IsReal
                ? "/// type. One beyond the range of the underlying type is refused as <c>value_object.not_parsable</c>."
                : "/// type. One the underlying type cannot hold whole, out of its range or with a fraction, is refused as");
            writer.LineIf(!underlying.IsReal, "/// <c>value_object.not_parsable</c>, never truncated.");
            writer.Line("/// </remarks>");
        }

        writer.Open($"public override object? ConvertFrom({Context}? context, {Culture}? culture, object value)");

        if (numeric)
        {
            writer.Line($"{value} raw;");
            writer.Line("bool fits;");
        }

        writer.Open("switch (value)");
        writer.Line("case string text:");
        writer.Indent().Line($"return {self}.Parse(text, culture ?? {Invariant});").Unindent();
        if (!underlying.IsString)
        {
            writer.Line($"case {value} underlying:");
            writer.Indent().Line($"return {self}.Create(underlying);").Unindent();
        }

        if (numeric)
        {
            var convert = underlying.IsReal ? "TryConvertToReal" : "TryConvertToInteger";
            foreach (var source in UnderlyingType.Numbers)
            {
                if (source.Kind == underlying.Kind)
                {
                    continue;
                }

                writer.Line($"case {source.FullName} number:");
                writer.Indent().Line($"fits = {Bridge}.{convert}(number, out raw);").Line("break;").Unindent();
            }
        }

        writer.Line("default:");
        writer.Indent().Line("return base.ConvertFrom(context, culture, value);").Unindent();
        writer.Close();

        if (numeric)
        {
            // As Parse: the message names the type, never the number, and AttemptedValue keeps it unless the type is
            // classified as sensitive data.
            writer.Line();
            writer.Open("if (!fits)");
            writer.Line("throw new global::AdCodicem.ValueObjects.ValueObjectException(");
            writer.Line($"    \"'{model.TypeName}' rejected the supplied number: The number is not a valid {underlying.Keyword}.\",");
            writer.Line($"    typeof({self}),");
            writer.Line("    global::AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable,");
            writer.Line($"    {ValueObjectEmitter.AttemptedValue(model, "value")});");
            writer.Close();
            writer.Line();
            writer.Line($"return {self}.Create(raw);");
        }

        writer.Close();
        writer.Line();
    }

    private static void EmitConvertTo(CodeWriter writer, UnderlyingType underlying, string value, string self)
    {
        writer.Line("/// <inheritdoc />");
        if (underlying.IsNumeric)
        {
            writer.Line("/// <remarks>");
            writer.Line("/// The value is handed to another numeric type with a checked conversion. A type that cannot hold it whole, since");
            writer.Line("/// the value is out of its range or has a fraction an integer type drops, throws");
            writer.Line("/// <see cref=\"global::System.NotSupportedException\"/>, as a conversion this converter does not perform does.");
            writer.Line("/// </remarks>");
        }

        writer.Open($"public override object? ConvertTo({Context}? context, {Culture}? culture, object? value, global::System.Type destinationType)");
        writer.Open($"if (value is {self} typed)");
        writer.Open("if (destinationType == typeof(string))");
        writer.Line($"return typed.ToString(null, culture ?? {Invariant});");
        writer.Close();
        if (!underlying.IsString)
        {
            writer.Line();
            writer.Open($"if (destinationType == typeof({value}))");
            writer.Line("return typed.Value;");
            writer.Close();
        }

        if (underlying.IsNumeric)
        {
            foreach (var destination in UnderlyingType.Numbers)
            {
                if (destination.Kind == underlying.Kind)
                {
                    continue;
                }

                // A destination that cannot hold the value falls through to the base, which throws NotSupportedException.
                var convert = destination.IsReal ? "TryConvertToReal" : "TryConvertToInteger";
                writer.Line();
                writer.Open(
                    $"if (destinationType == typeof({destination.FullName})\n"
                    + $"    && {Bridge}.{convert}(typed.Value, out {destination.FullName} to{destination.Kind}))");
                writer.Line($"return to{destination.Kind};");
                writer.Close();
            }
        }

        writer.Close();
        writer.Line();
        writer.Line("return base.ConvertTo(context, culture, value, destinationType);");
        writer.Close();
    }

    private static string[] Types()
    {
        var types = new string[UnderlyingType.Numbers.Count];
        for (var i = 0; i < types.Length; i++)
        {
            types[i] = $"typeof({UnderlyingType.Numbers[i].FullName})";
        }

        return types;
    }
}
