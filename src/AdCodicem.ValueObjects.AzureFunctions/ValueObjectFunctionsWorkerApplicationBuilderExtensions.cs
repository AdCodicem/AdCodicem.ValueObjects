using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.AzureFunctions;

/// <summary>
/// Registers the value object converter with the Azure Functions isolated worker.
/// </summary>
public static class ValueObjectFunctionsWorkerApplicationBuilderExtensions
{
    /// <summary>
    /// Puts the value object converter first among the worker's input converters, or right behind the ASP.NET Core
    /// integration's own converter when called before <c>ConfigureFunctionsWebApplication()</c>, so that a route value, a
    /// query value and other binding data bind to a value object through its <c>TryParse</c>.
    /// </summary>
    /// <param name="builder">The worker's builder: the one <c>FunctionsApplication.CreateBuilder(args)</c> returns, or the
    /// one <c>ConfigureFunctionsWebApplication(worker =&gt; …)</c> and <c>ConfigureFunctionsWorkerDefaults(worker =&gt; …)</c>
    /// hand over.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// The worker tries the converter a parameter or its type names with <c>[InputConverter]</c> first, then those its
    /// binding attribute advertises, then the registered ones in order: the value object converter goes first among the
    /// last, where it reads only value objects, and only from text. Payloads stay with their bindings and the worker's
    /// serializer.
    /// </para>
    /// <para>
    /// The ASP.NET Core integration puts its own converter first too, from a configuration of its own, which runs after
    /// this one when <c>ConfigureFunctionsWebApplication()</c> is called later: the value object converter then comes
    /// right behind it. That converter binds an <c>HttpRequest</c> and an <c>HttpRequestData</c> alone, so either order
    /// binds the same.
    /// </para>
    /// <para>Calling it twice registers the converter once.</para>
    /// </remarks>
    public static IFunctionsWorkerApplicationBuilder UseValueObjects(this IFunctionsWorkerApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.Configure<WorkerOptions>(static options =>
        {
            if (!options.InputConverters.Contains(typeof(ValueObjectInputConverter)))
            {
                options.InputConverters.RegisterAt<ValueObjectInputConverter>(0);
            }
        });

        return builder;
    }
}
