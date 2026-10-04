namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The lines the script writes, one per scenario, each led by the name of what it exercises.
/// </summary>
internal sealed class Report
{
    private readonly List<string> _lines = [];

    private readonly List<string> _unexpected = [];

    /// <summary>Gets the lines written, in order.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Gets the scenarios that threw where nothing should, or found something missing.</summary>
    public IReadOnlyList<string> Unexpected => _unexpected;

    /// <summary>Writes the outcome of a scenario.</summary>
    /// <param name="subject">What the scenario exercises.</param>
    /// <param name="outcome">What it observed.</param>
    public void Line(string subject, string outcome) => _lines.Add($"{subject} | {outcome}");

    /// <summary>Writes a scenario that went wrong in a way neither run should, and counts it.</summary>
    /// <param name="subject">What the scenario exercises.</param>
    /// <param name="outcome">What went wrong.</param>
    public void Fail(string subject, string outcome)
    {
        Line(subject, $"UNEXPECTED {outcome}");
        _unexpected.Add($"{subject} | {outcome}");
    }

    /// <summary>Describes an exception by its type and, where it carries one, the code of the rule it reports.</summary>
    /// <param name="exception">Exception to describe.</param>
    /// <returns>The description.</returns>
    public static string Describe(Exception exception)
        => ValueObjectErrors.TryGetCode(exception, out var code)
            ? $"{exception.GetType().Name} {code}"
            : exception.GetType().Name;
}
