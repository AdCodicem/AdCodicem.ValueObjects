using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.ModelContextProtocol;

/// <summary>
/// Creates the instance an instance tool method runs on, once per call, as the SDK's own registrations do.
/// </summary>
internal static class ToolTargets
{
    /// <summary>
    /// Creates an instance of a tool type, its constructor's parameters resolved from the services of the call.
    /// </summary>
    /// <param name="services">The services of the call, which the SDK sets before it asks for the instance.</param>
    /// <param name="type">The tool type.</param>
    /// <returns>The instance, which the SDK disposes after the call when it is disposable.</returns>
    /// <remarks>
    /// The SDK falls back to <see cref="Activator"/> without services, which calls a parameterless constructor alone;
    /// this goes through <see cref="ActivatorUtilities"/> and a provider that resolves nothing, which calls that
    /// constructor too, and also one whose parameters all have defaults, which <see cref="Activator"/> refuses.
    /// </remarks>
    public static object Create(IServiceProvider? services, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
        => ActivatorUtilities.CreateInstance(services ?? NoServices.Instance, type);

    /// <summary>A provider that resolves nothing.</summary>
    private sealed class NoServices : IServiceProvider
    {
        public static readonly NoServices Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
