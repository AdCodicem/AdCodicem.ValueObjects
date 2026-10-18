using System.Reflection;
using System.Runtime.CompilerServices;
using JasperFx;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Http.CodeGen;

namespace AdCodicem.ValueObjects.Wolverine.Http.Binding;

/// <summary>
/// Claims each value-object parameter of an endpoint, or of its middleware methods, ahead of Wolverine's own strategies,
/// and reads it through the package's frame.
/// </summary>
/// <param name="policy">The policy that answers the refusals, which keeps the reads of each endpoint.</param>
/// <remarks>
/// <para>
/// One read per source and name in an endpoint: Wolverine matches the parameters of a compound handler's <c>Before</c>
/// or <c>Load</c> method too, and a second read of <c>qty</c> would declare the local twice (CS0128).
/// </para>
/// <para>
/// Each read takes over the variable Wolverine's own factory creates for its source and name, re-homed to the package's
/// frame: Wolverine describes a query, header or form value in ApiExplorer only from the variables its factories created,
/// and finds a value again by its source and name, for an <c>After</c> or <c>Finally</c> method or a parameter attribute
/// such as <c>[Entity]</c>, in the lists they fill. Wolverine's frame, which nothing uses any more, is never written.
/// </para>
/// </remarks>
internal sealed class ValueObjectParameterStrategy(ValueObjectRefusalPolicy policy) : IParameterStrategy
{
    /// <inheritdoc />
    public bool TryMatch(HttpChain chain, IServiceContainer container, ParameterInfo parameter, out Variable? variable)
    {
        variable = null;
        if (!ValueObjectHttpParameter.TryDescribe(chain, parameter, out var described))
        {
            return false;
        }

        var reads = policy.ReadsOf(chain);
        if (reads.FirstOrDefault(read => read.Parameter.Source == described.Source && read.Parameter.Key == described.Key) is { } existing)
        {
            if (existing.Variable.VariableType != parameter.ParameterType)
            {
                throw new InvalidOperationException(
                    $"The {described.Source.ToString().ToLowerInvariant()} value '{described.Key}' is bound to both {existing.Variable.VariableType.FullNameInCode()} and {parameter.ParameterType.FullNameInCode()} in {chain}.");
            }

            variable = existing.Variable;
            return true;
        }

        var wolverines = WolverinesVariable(chain, parameter, described);
        if (described.Source == ValueObjectHttpSource.Form)
        {
            // What Wolverine's own [FromForm] strategy sets for a form value, which its endpoint metadata reads:
            // AutoAntiforgeryOnFormEndpoints() requires an antiforgery token of an endpoint marked as form data, and a
            // request type set keeps Wolverine's JSON body strategy from claiming a class parameter after it.
            SetRequestType(chain, typeof(void));
            SetIsFormData(chain, value: true);
        }

        var frame = new ValueObjectReadFrame(described, parameter.ParameterType, parameter.Name!, wolverines);
        reads.Add(frame);
        policy.Answer(chain, container);
        variable = frame.Variable;

        return true;
    }

    /// <summary>Marks an endpoint as reading a form, through the setter Wolverine keeps internal.</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_IsFormData")]
    private static extern void SetIsFormData(HttpChain chain, bool value);

    /// <summary>Sets the request type of an endpoint, through the setter Wolverine keeps internal.</summary>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_RequestType")]
    private static extern void SetRequestType(HttpChain chain, Type? value);

    /// <summary>
    /// Has Wolverine's own factory create, or find, the variable of a value, which Wolverine describes the value from and
    /// looks it up by again: the factory its own strategy for that source calls.
    /// </summary>
    /// <returns>The variable, or <see langword="null"/> where Wolverine reads no such value from that source.</returns>
    private static HttpElementVariable? WolverinesVariable(HttpChain chain, ParameterInfo parameter, ValueObjectHttpParameter described)
    {
        switch (described.Source)
        {
            case ValueObjectHttpSource.Route:
                // By the name [FromRoute] gives, compared ignoring case, or by the parameter, as Wolverine's route strategy.
                var found = parameter.GetCustomAttribute<FromRouteAttribute>(inherit: false) is { Name.Length: > 0 } attribute
                    ? chain.FindRouteVariable(parameter.ParameterType, attribute.Name, out var route)
                    : chain.FindRouteVariable(parameter, out route);
                return found ? route as HttpElementVariable : null;
            case ValueObjectHttpSource.Query:
                return chain.TryFindOrCreateQuerystringValue(parameter.ParameterType, parameter.Name!, described.Key);
            case ValueObjectHttpSource.Header:
                return chain.GetOrCreateHeaderVariable(parameter.GetCustomAttributes(inherit: false).OfType<IFromHeaderMetadata>().First(), parameter);
            default: // A form field.
                return chain.TryFindOrCreateFormValue(parameter.ParameterType, parameter.Name!, described.Key);
        }
    }
}
