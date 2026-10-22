using System.CommandLine;
using AdCodicem.ValueObjects.CommandLine;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>
/// Parses and invokes System.CommandLine commands as an application does, never through the console: standard output and
/// standard error are writers of the test's own.
/// </summary>
internal static class CliHarness
{
    /// <summary>Gets the errors of a parse, each with the code and the type a value object kept beside it.</summary>
    /// <param name="result">The parse.</param>
    /// <returns>The errors, in order.</returns>
    public static IReadOnlyList<CliError> Refusals(this ParseResult result)
        => [.. result.Errors.Select(static error => new CliError(
            error.Message,
            error.TryGetValueObjectErrorCode(out var code) ? code : null,
            error.TryGetValueObjectType(out var type) ? type : null))];

    /// <summary>Invokes a command line, as <c>root.Parse(args).Invoke()</c> does from <c>Main</c>.</summary>
    /// <param name="command">The command.</param>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code, and what the invocation wrote to standard output and standard error.</returns>
    public static (int ExitCode, string Output, string Error) Invoke(this Command command, params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = command.Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });

        return (exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Gets the completions of a command line at its end, each with its documentation.</summary>
    /// <param name="command">The command.</param>
    /// <param name="line">The command line, as typed so far.</param>
    /// <returns>The completions, as <c>FR (France)</c>, or the label alone when undocumented.</returns>
    public static IReadOnlyList<string> Completions(this Command command, string line)
        => [.. command.Parse(line).GetCompletions(line.Length)
            .Select(static item => item.Documentation is null ? item.Label : $"{item.Label} ({item.Documentation})")];
}

/// <summary>A parse error, with what a value object kept beside it.</summary>
/// <param name="Message">The message.</param>
/// <param name="Code">The code of the rule, or <see langword="null"/>.</param>
/// <param name="Type">The value object type that refused the value, or <see langword="null"/>.</param>
internal sealed record CliError(string Message, string? Code, Type? Type);
