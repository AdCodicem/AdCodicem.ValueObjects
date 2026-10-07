namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// A value the schema of a value object rules out, with the rule it breaks.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <param name="Value">The value, which the type is expected to refuse.</param>
/// <param name="Rule">
/// The rule it breaks, as the schema declares it: <c>MinLength (15)</c>, <c>MaxLength (34)</c>, <c>Minimum (0)</c>,
/// <c>Maximum (1000)</c> or <c>a closed value set</c>.
/// </param>
public readonly record struct SchemaViolation<TValue>(TValue Value, string Rule);
