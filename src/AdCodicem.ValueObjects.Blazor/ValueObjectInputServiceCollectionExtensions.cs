using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// Registers the options of the inputs of value objects.
/// </summary>
public static class ValueObjectInputServiceCollectionExtensions
{
    /// <summary>
    /// Configures the messages the inputs of value objects answer a refusal with, and registers the localization services
    /// they are read through.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <param name="configure">Configures the options: <c>options =&gt; options.UseResource&lt;Messages&gt;()</c>.</param>
    /// <returns>The services, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An input needs no registration to read text through its value object's rules, and answers a refusal with the
    /// English message of the rule without one. <c>AddLocalization()</c> keeps the localizer factory the application
    /// registered first, and its <c>ResourcesPath</c>.
    /// </remarks>
    public static IServiceCollection AddValueObjectInputs(this IServiceCollection services, Action<ValueObjectInputOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddLocalization();

        return services;
    }
}
