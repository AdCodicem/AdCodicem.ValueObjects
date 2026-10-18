using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Wolverine.Http.CodeGen;

namespace AdCodicem.ValueObjects.Wolverine.Http.Binding;

/// <summary>
/// Writes the statement that reads one value object from the request through <see cref="ValueObjectHttpBinding"/>, its
/// typed helpers closed over the value object in the generated code, so that <c>TSelf.TryParse</c> runs with no visitor
/// and no <c>MakeGenericType</c>.
/// </summary>
internal sealed class ValueObjectReadFrame : SyncFrame
{
    private const string Binding = "global::AdCodicem.ValueObjects.Wolverine.Http.ValueObjectHttpBinding";

    /// <summary>Creates the frame.</summary>
    /// <param name="parameter">What the parameter binds and from where.</param>
    /// <param name="parameterType">The type of the parameter, which the variable has.</param>
    /// <param name="parameterName">The name of the parameter, which the variable takes.</param>
    /// <param name="wolverines">
    /// The variable Wolverine's own factory created for the same source and name, which the frame takes over, or
    /// <see langword="null"/> when Wolverine reads no such value, for a variable of the frame's own.
    /// </param>
    /// <remarks>
    /// Wolverine looks a value up again by its source and name, for an <c>After</c> or <c>Finally</c> method, a route value
    /// a middleware method takes, or the identity a parameter attribute such as <c>[Entity]</c> loads by, in lists of its
    /// own that only its factories fill. The frame takes over the variable those lists hold, re-homed to it as Wolverine's
    /// <c>[AsParameters]</c> binding re-homes the variables it absorbs (GH-3374), so that each lookup finds the package's
    /// read rather than writing a second local of the same name (CS0128). A route value's variable keeps Wolverine's name,
    /// which Wolverine compares to a parameter's name to find it; a query, header or form value's is renamed after the
    /// parameter, since Wolverine finds it by its key and names some of them after that key, which need not be an
    /// identifier.
    /// </remarks>
    public ValueObjectReadFrame(ValueObjectHttpParameter parameter, Type parameterType, string parameterName, HttpElementVariable? wolverines = null)
    {
        Parameter = parameter;
        if (wolverines is null)
        {
            Variable = new Variable(parameterType, parameterName, this);
        }
        else
        {
            ReassignCreator(wolverines, this);
            if (parameter.Source != ValueObjectHttpSource.Route)
            {
                wolverines.OverrideName(parameterName);
            }

            Variable = wolverines;
        }

        // A form is read asynchronously, once, and kept by the request.
        IsAsync = parameter.Source == ValueObjectHttpSource.Form;
    }

    /// <summary>Gets what the parameter binds and from where.</summary>
    public ValueObjectHttpParameter Parameter { get; }

    /// <summary>Gets the variable the handler and its middleware receive.</summary>
    public Variable Variable { get; }

    /// <inheritdoc />
    public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
    {
        var key = Literal(Parameter.Key);
        var types = $"<{Parameter.ValueObjectType.FullNameInCode()}, {Parameter.ValueType.FullNameInCode()}>";
        var many = Parameter.Shape is ValueObjectHttpShape.Array or ValueObjectHttpShape.List or ValueObjectHttpShape.Sequence;
        var text = Parameter.Source switch
        {
            ValueObjectHttpSource.Route => $"{Binding}.Route(httpContext, {key})",
            ValueObjectHttpSource.Header => $"{Binding}.Header(httpContext, {key})",
            ValueObjectHttpSource.Query => many ? $"{Binding}.QueryValues(httpContext, {key})" : $"{Binding}.Query(httpContext, {key})",
            _ => many // A form field.
                ? $"await {Binding}.FormValuesAsync(httpContext, {key}).ConfigureAwait(false)"
                : $"await {Binding}.FormAsync(httpContext, {key}).ConfigureAwait(false)",
        };

        var read = Parameter.Shape switch
        {
            ValueObjectHttpShape.Single => $"{Binding}.Bind{types}(httpContext, {key}, {text}, required: {(Parameter.Required ? "true" : "false")})",
            ValueObjectHttpShape.Nullable => $"{Binding}.BindNullable{types}(httpContext, {key}, {text})",
            ValueObjectHttpShape.List => $"new global::System.Collections.Generic.List<{Parameter.ValueObjectType.FullNameInCode()}>({Binding}.BindArray{types}(httpContext, {key}, {text}))",
            _ => $"{Binding}.BindArray{types}(httpContext, {key}, {text})",
        };

        writer.WriteComment($"Value object {Parameter.Source.ToString().ToLowerInvariant()} value {key}, read through the rules of {Parameter.ValueObjectType.FullNameInCode()}");
        writer.Write($"var {Variable.Usage} = {read};");
        Next?.GenerateCode(method, writer);
    }

    /// <summary>
    /// Makes a frame the creator of a variable Wolverine's factory created, through the method Wolverine keeps internal
    /// (<c>HttpElementVariable.ReassignCreator</c>), which its own <c>[AsParameters]</c> binding calls.
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReassignCreator")]
    private static extern void ReassignCreator(HttpElementVariable variable, Frame creator);

    /// <summary>Writes a name as a C# string literal, so that an attribute's name holding a quote stays one string.</summary>
    internal static string Literal(string value)
    {
        var literal = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            _ = character switch
            {
                '"' => literal.Append("\\\""),
                '\\' => literal.Append("\\\\"),
                _ when char.IsControl(character) || char.IsSurrogate(character) => literal.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)),
                _ => literal.Append(character),
            };
        }

        return literal.Append('"').ToString();
    }
}
