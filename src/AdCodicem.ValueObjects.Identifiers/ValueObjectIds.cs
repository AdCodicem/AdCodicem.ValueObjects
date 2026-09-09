namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// The clock and the entropy source that <c>New()</c> reads.
/// </summary>
/// <remarks>
/// <para>
/// Two layers. <see cref="Configure"/> sets a process-wide default, which is what an application does once at
/// start-up. <see cref="Use"/> opens a scope bound to the current execution flow, which wins over the default
/// for as long as it lives.
/// </para>
/// <para>
/// The scope is not a convenience: the test suites run in parallel, and a settable static alone would let two
/// tests racing to substitute the clock corrupt one another. The failure that produces is intermittent and
/// lands in whichever test happens to observe it, which is the most expensive kind to diagnose. An
/// <see cref="AsyncLocal{T}"/> scope is bounded by the execution flow, so parallel tests do not interfere and
/// no test collection has to be serialized to stay correct.
/// </para>
/// </remarks>
public static class ValueObjectIds
{
    private static readonly AsyncLocal<Ambient?> Scoped = new();

    private static Ambient _default = new(TimeProvider.System, IdEntropySource.System);

    /// <summary>
    /// Gets the clock in effect, preferring the innermost open scope.
    /// </summary>
    public static TimeProvider TimeProvider => (Scoped.Value ?? _default).TimeProvider;

    /// <summary>
    /// Gets the entropy source in effect, preferring the innermost open scope.
    /// </summary>
    public static IdEntropySource Entropy => (Scoped.Value ?? _default).Entropy;

    /// <summary>
    /// Replaces the process-wide default.
    /// </summary>
    /// <param name="timeProvider">Clock to use, or <see langword="null"/> to keep the current one.</param>
    /// <param name="entropy">Entropy source to use, or <see langword="null"/> to keep the current one.</param>
    /// <remarks>
    /// Intended to be called once, during start-up, before anything generates an identifier. It is not
    /// synchronized against concurrent generation: substituting the clock while requests are in flight is a
    /// test concern, and <see cref="Use"/> is the member for that.
    /// </remarks>
    public static void Configure(TimeProvider? timeProvider = null, IdEntropySource? entropy = null)
        => _default = new Ambient(timeProvider ?? _default.TimeProvider, entropy ?? _default.Entropy);

    /// <summary>
    /// Opens a scope that overrides the default for the current execution flow.
    /// </summary>
    /// <param name="timeProvider">Clock to use, or <see langword="null"/> to inherit the enclosing one.</param>
    /// <param name="entropy">Entropy source to use, or <see langword="null"/> to inherit the enclosing one.</param>
    /// <returns>A handle that restores the enclosing state when disposed.</returns>
    /// <example>
    /// <code>
    /// using (ValueObjectIds.Use(fakeClock, deterministicBytes))
    /// {
    ///     var id = AccountId.New();
    /// }
    /// </code>
    /// </example>
    public static IDisposable Use(TimeProvider? timeProvider = null, IdEntropySource? entropy = null)
    {
        var enclosing = Scoped.Value;
        var inherited = enclosing ?? _default;

        Scoped.Value = new Ambient(timeProvider ?? inherited.TimeProvider, entropy ?? inherited.Entropy);

        return new Scope(enclosing);
    }

    private sealed record Ambient(TimeProvider TimeProvider, IdEntropySource Entropy);

    private sealed class Scope : IDisposable
    {
        private readonly Ambient? _enclosing;
        private bool _disposed;

        public Scope(Ambient? enclosing) => _enclosing = enclosing;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Scoped.Value = _enclosing;
        }
    }
}
