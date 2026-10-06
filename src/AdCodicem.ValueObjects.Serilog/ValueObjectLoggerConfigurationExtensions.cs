using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using Serilog;
using Serilog.Configuration;

namespace AdCodicem.ValueObjects.Serilog;

/// <summary>
/// Wires value objects into Serilog's capture of log event properties.
/// </summary>
public static class ValueObjectLoggerConfigurationExtensions
{
    /// <summary>
    /// Destructures value objects as their underlying value.
    /// </summary>
    /// <param name="destructure">Serilog's destructuring configuration, <c>LoggerConfiguration.Destructure</c>.</param>
    /// <returns>The logger configuration, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// A value object logged with <c>@</c>, or met inside an object, a collection or a dictionary logged with <c>@</c>, is
    /// captured as the scalar its underlying value is, <c>"Qty": 42</c> rather than <c>"Qty": {"Value": 42}</c>, and
    /// Serilog writes it as it writes that type. Every type implementing <see cref="IValueObject"/> is captured so,
    /// through <see cref="IValueObject.GetBoxedValue"/>: a value object written by hand and a construction of a generic
    /// value object included, with nothing registered.
    /// </para>
    /// <para>
    /// Without <c>@</c>, Serilog captures a type it does not know through <c>ToString()</c>, and consults no
    /// destructuring policy: <c>{Qty}</c> stays the text <c>"42"</c>.
    /// <see cref="ValueObjectLoggingOptions.CaptureAsUnderlyingValue"/> captures it as its underlying value too.
    /// </para>
    /// <para>
    /// Serilog runs destructuring policies in the order they were declared: an application's own destructuring of a
    /// value object type, a mask written with <c>Destructure.ByTransforming&lt;T&gt;()</c> included, applies only when
    /// it is declared before this call.
    /// </para>
    /// </remarks>
    public static LoggerConfiguration ValueObjects(this LoggerDestructuringConfiguration destructure)
    {
        ArgumentNullException.ThrowIfNull(destructure);

        return destructure.With(ValueObjectDestructuringPolicy.Instance);
    }

    /// <summary>
    /// Destructures value objects as their underlying value, with the options given.
    /// </summary>
    /// <param name="destructure">Serilog's destructuring configuration, <c>LoggerConfiguration.Destructure</c>.</param>
    /// <param name="configure">Sets the options.</param>
    /// <returns>The logger configuration, so that calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The assemblies of <see cref="ValueObjectLoggingOptions.Assemblies"/> are registered first. With
    /// <see cref="ValueObjectLoggingOptions.CaptureAsUnderlyingValue"/>, Serilog then treats every value object the
    /// <see cref="ValueObjectRegistry"/> holds as a scalar, which it captures as it is rather than through
    /// <c>ToString()</c>, and an enricher replaces each one an event holds, in its properties and in the structures,
    /// sequences and dictionaries they hold, with its underlying value.
    /// </para>
    /// <para>
    /// The registry is read when this method runs. A construction of a generic value object is in it once it was
    /// registered by hand, as native AOT asks, or resolved by another integration before the call, and is then made a
    /// scalar as any other; one that was not, and a value object written by hand that nothing registered, is captured as
    /// its text until the application makes it a scalar, with <c>Destructure.AsScalar&lt;T&gt;()</c>, and the enricher
    /// then replaces it too. The enricher runs where this call places it among the enrichers: an enricher added after
    /// it, <c>Enrich.FromLogContext()</c> included, adds a value object the enricher no longer sees, written as its text.
    /// </para>
    /// <para>
    /// Serilog consults its scalar types before any destructuring policy. Without the option, the application should
    /// therefore make no value object a scalar, or it is written as its text with <c>@</c> too. With it, no destructuring
    /// of the application's runs for a value object the registry holds, whatever the order of the calls and wherever it
    /// is met: a mask written with <c>Destructure.ByTransforming&lt;T&gt;()</c> is lost, and the masked text is what to
    /// log. Without it, an application's own destructuring of a value object type applies when it is declared before
    /// this call, Serilog running destructuring policies in the order they were declared.
    /// </para>
    /// </remarks>
    public static LoggerConfiguration ValueObjects(
        this LoggerDestructuringConfiguration destructure,
        Action<ValueObjectLoggingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(destructure);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ValueObjectLoggingOptions();
        configure(options);
        foreach (var assembly in options.Assemblies)
        {
            Register(assembly);
        }

        var configuration = destructure.With(ValueObjectDestructuringPolicy.Instance);
        if (!options.CaptureAsUnderlyingValue)
        {
            return configuration;
        }

        // Serilog reads its scalar types when the logger is built, and captures one as it is: the struct rather than its
        // text, which the enricher then replaces with the underlying value.
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            destructure.AsScalar(descriptor.ValueObjectType);
        }

        return configuration.Enrich.With(ValueObjectEnricher.Instance);
    }

    /// <summary>Runs the generated registration of an assembly.</summary>
    /// <param name="assembly">Assembly declaring value objects.</param>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The generated registration is the module initializer of its assembly, which trimming keeps and "
                        + "native AOT runs at start-up: a lookup that finds nothing leaves nothing unregistered.")]
    private static void Register(Assembly assembly) => ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
}
