using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Emit;

/// <summary>
/// Emits the <see cref="System.ComponentModel.TypeConverter"/> nested in a value object.
/// </summary>
/// <remarks>
/// A type converter is what makes a value object usable everywhere the framework converts text without knowing
/// the type: <c>IConfiguration</c> binding, <c>[FromQuery]</c> properties of a complex model, dictionary keys in
/// route data, and designers. The ASP.NET Core model binder does not need it — it has a dedicated binder — but
/// having it costs one small class and removes a whole family of surprises.
/// </remarks>
internal static class TypeConverterEmitter
{
    private const string ComponentModel = "global::System.ComponentModel";
    private const string Context = ComponentModel + ".ITypeDescriptorContext";
    private const string Culture = "global::System.Globalization.CultureInfo";
    private const string Invariant = Culture + ".InvariantCulture";

    public static void Emit(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        var underlyingIsString = underlying.IsString;

        writer.Line($"/// <summary>Converts <see cref=\"{model.TypeName}\"/> to and from text and its underlying value.</summary>");
        writer.Open($"public sealed class ValueTypeConverter : {ComponentModel}.TypeConverter");

        writer.Line("/// <inheritdoc />");
        writer.Line($"public override bool CanConvertFrom({Context}? context, global::System.Type sourceType)");
        writer.Line(underlyingIsString
            ? "    => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);"
            : $"    => sourceType == typeof(string) || sourceType == typeof({value}) || base.CanConvertFrom(context, sourceType);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Open($"public override object? ConvertFrom({Context}? context, {Culture}? culture, object value)");
        writer.Open("switch (value)");
        writer.Line("case string text:");
        writer.Indent().Line($"return {self}.Parse(text, culture ?? {Invariant});").Unindent();
        if (!underlyingIsString)
        {
            writer.Line($"case {value} underlying:");
            writer.Indent().Line($"return {self}.Create(underlying);").Unindent();
        }

        writer.Line("default:");
        writer.Indent().Line("return base.ConvertFrom(context, culture, value);").Unindent();
        writer.Close();
        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"public override bool CanConvertTo({Context}? context, global::System.Type? destinationType)");
        writer.Line(underlyingIsString
            ? "    => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);"
            : $"    => destinationType == typeof(string) || destinationType == typeof({value}) || base.CanConvertTo(context, destinationType);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Open($"public override object? ConvertTo({Context}? context, {Culture}? culture, object? value, global::System.Type destinationType)");
        writer.Open($"if (value is {self} typed)");
        writer.Open("if (destinationType == typeof(string))");
        writer.Line($"return typed.ToString(null, culture ?? {Invariant});");
        writer.Close();
        if (!underlyingIsString)
        {
            writer.Line();
            writer.Open($"if (destinationType == typeof({value}))");
            writer.Line("return typed.Value;");
            writer.Close();
        }

        writer.Close();
        writer.Line();
        writer.Line("return base.ConvertTo(context, culture, value, destinationType);");
        writer.Close();

        writer.Close();
        writer.Line();
    }
}
