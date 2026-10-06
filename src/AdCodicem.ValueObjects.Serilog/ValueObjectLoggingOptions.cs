using System.Reflection;

namespace AdCodicem.ValueObjects.Serilog;

/// <summary>
/// Options of <see cref="ValueObjectLoggerConfigurationExtensions.ValueObjects(global::Serilog.Configuration.LoggerDestructuringConfiguration, Action{ValueObjectLoggingOptions})"/>.
/// </summary>
public sealed class ValueObjectLoggingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether a value object logged without <c>@</c> is captured as its underlying value
    /// too. Off by default.
    /// </summary>
    /// <remarks>
    /// It costs one pass over the properties of every event, and a numeric value object then reaches a filter, a query and
    /// a format in the message template as the number it holds.
    /// </remarks>
    public bool CaptureAsUnderlyingValue { get; set; }

    /// <summary>
    /// Gets the assemblies whose value objects are registered before the logger is built.
    /// </summary>
    /// <remarks>
    /// Under the JIT, the registration the generator emits for an assembly runs once its code is first used, which a
    /// logger configured at the top of <c>Program.cs</c> comes before: name the assemblies declaring value objects there.
    /// A native binary has registered all of them at start-up.
    /// </remarks>
    public ICollection<Assembly> Assemblies { get; } = new List<Assembly>();
}
