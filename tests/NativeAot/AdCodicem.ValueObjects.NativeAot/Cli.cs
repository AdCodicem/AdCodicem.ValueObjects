using System.Collections;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using AdCodicem.ValueObjects.CommandLine;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// A System.CommandLine command holding an option of every value object the registry describes, of its nullable type and
/// of an array of it, configured by <c>AcceptValueObjects()</c>, whose options are given each text the value object's
/// underlying type is probed with: the value read, or each parse error with the code and the type beside it. The options
/// close their parsers through the descriptor's visitor, which the native binary must hold for every value object, and a
/// value object the registry does not describe is refused, naming the registration.
/// </summary>
internal static class Cli
{
    public static void Run(Report report)
    {
        var command = new Command("probe");
        var probes = ValueObjectRegistry.GetRegistered()
            .OrderBy(static descriptor => Names.Of(descriptor.ValueObjectType), StringComparer.Ordinal)
            .Select((descriptor, index) => descriptor.Accept(new ProbeOf(command, $"--vo{index}")))
            .ToList();
        command.AcceptValueObjects();

        foreach (var probe in probes)
        {
            probe.Run(report, command);
        }

        Localized(report);
        Unregistered(report);
        Invoked(report);
    }

    /// <summary>
    /// The messages of a resource, read through a <see cref="System.Resources.ResourceManager"/>, under the invariant culture
    /// of InvariantGlobalization, which reads the neutral resource; a code it holds no key for keeps English. A token among
    /// several is named by its position from zero, <c>--quantities[2]</c> for the third.
    /// </summary>
    private static void Localized(Report report)
    {
        var quantity = new Option<Quantity>("--quantity");
        var email = new Option<EmailAddress>("--email");
        var quantities = new Option<Quantity[]>("--quantities") { AllowMultipleArgumentsPerToken = true };
        var command = new Command("localized") { quantity, email, quantities }.AcceptValueObjects(static options => options.UseResource<ProblemMessages>());

        var texts = Underlying.Texts(typeof(short));
        foreach (var text in texts)
        {
            report.Line($"cli localized Quantity {Underlying.Quote(text)}", Describe(command.Parse([quantity.Name, text]), quantity));
        }

        foreach (var text in Underlying.Texts(typeof(string)))
        {
            report.Line($"cli localized EmailAddress {Underlying.Quote(text)}", Describe(command.Parse([email.Name, text]), email));
        }

        // Two runs numbering the tokens alike, from one or otherwise, would agree.
        var several = command.Parse([quantities.Name, .. texts]);
        var first = texts.ToList().FindIndex(static text => !Quantity.TryParse(text, CultureInfo.InvariantCulture, out _));
        if (several.Errors.Count == 0 || !several.Errors[0].Message.StartsWith($"{quantities.Name}[{first}] ", StringComparison.Ordinal))
        {
            report.Fail("cli localized Quantity[] every text", Describe(several, quantities));
        }
        else
        {
            report.Line("cli localized Quantity[] every text", Describe(several, quantities));
        }
    }

    /// <summary>A value object nothing registered, refused by the per-symbol call and by the walk alike.</summary>
    private static void Unregistered(Report report)
    {
        try
        {
            new Option<UnregisteredCode>("--code").AcceptValueObject();
            report.Fail("cli UnregisteredCode option", "configured");
        }
        catch (InvalidOperationException exception)
        {
            report.Line("cli UnregisteredCode option", $"threw {Report.Describe(exception)}: {exception.Message}");
        }

        try
        {
            new Command("unregistered") { new Option<UnregisteredCode[]>("--codes") }.AcceptValueObjects();
            report.Fail("cli UnregisteredCode walk", "configured");
        }
        catch (InvalidOperationException exception)
        {
            report.Line("cli UnregisteredCode walk", $"threw {Report.Describe(exception)}: {exception.Message}");
        }
    }

    /// <summary>
    /// An invocation, as <c>Main</c> runs one: a refusal on standard error, the help on standard output, exit code 1 and
    /// the action not run, a refusal of the command's option under its subcommand included; accepted text, exit code 0
    /// and the action given the normalized instances.
    /// </summary>
    private static void Invoked(Report report)
    {
        var email = new Option<EmailAddress>("--email") { Required = true };
        var quantity = new Option<Quantity?>("--quantity");
        var ran = new List<string>();
        var confirm = new Command("confirm");
        var command = new Command("order", "Places an order.") { email, quantity, confirm, new HelpOption() }.AcceptValueObjects();
        command.SetAction(result => ran.Add($"{result.GetValue(email)} {result.GetValue(quantity)}"));
        confirm.SetAction(result => ran.Add($"confirm {result.GetValue(email)} {result.GetValue(quantity)}"));

        string[][] lines =
        [
            ["--email", "not-an-email", "--quantity", "1001"],
            ["--email", "  Ada@Example.COM ", "--quantity", "7"],
            ["--email", "not-an-email", "--quantity", "1001", "confirm"],
            ["--email", "  Ada@Example.COM ", "--quantity", "7", "confirm"],
        ];
        foreach (var line in lines)
        {
            ran.Clear();
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = command.Parse(line).Invoke(new InvocationConfiguration { Output = output, Error = error });
            var errors = error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var outcome = $"exit code {exitCode}, standard error [{string.Join(" | ", errors)}], help {output.ToString().Contains("Usage:", StringComparison.Ordinal)}, "
                          + $"action [{string.Join(" | ", ran)}]";

            // Two runs refusing nothing, or running the action of a refused line, would agree.
            if ((exitCode == 1) != (errors.Length > 0) || (exitCode == 1 && ran.Count > 0))
            {
                report.Fail($"cli invoke {string.Join(' ', line)}", outcome);
            }
            else
            {
                report.Line($"cli invoke {string.Join(' ', line)}", outcome);
            }
        }
    }

    /// <summary>Describes a parse: the value of an option, or each error with the code and the type beside it.</summary>
    private static string Describe<T>(ParseResult result, Option<T> option)
        => result.Errors.Count == 0
            ? $"value {Show(result.GetValue(option))}"
            : "refused " + string.Join(" | ", result.Errors.Select(static error =>
                $"{error.Message} [{(error.TryGetValueObjectErrorCode(out var code) ? code : "no code")}, "
                + $"{(error.TryGetValueObjectType(out var type) ? Names.Of(type) : "no type")}]"));

    /// <summary>Shows a value, the elements of an array between brackets.</summary>
    private static string Show(object? value)
        => value is IEnumerable elements and not string
            ? $"[{string.Join(", ", elements.Cast<object?>().Select(Underlying.Show))}]"
            : Underlying.Show(value);

    /// <summary>What the script does with the options of one value object, whatever its type.</summary>
    private interface ICliProbe
    {
        /// <summary>Gives each option its texts and writes what it read.</summary>
        /// <param name="report">The report.</param>
        /// <param name="command">The command holding the options.</param>
        void Run(Report report, Command command);
    }

    /// <summary>
    /// Adds the options of a value object to the command, through the type arguments its descriptor hands back: a visitor
    /// that is not generic, as the package's own is not.
    /// </summary>
    private sealed class ProbeOf(Command command, string name) : IValueObjectVisitor<ICliProbe>
    {
        public ICliProbe Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new Probe<TSelf, TValue>(command, name);
    }

    /// <summary>The options of one value object: alone, as its nullable type, and as an array.</summary>
    private sealed class Probe<TSelf, TValue> : ICliProbe
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        private readonly Option<TSelf> _one;

        private readonly Option<TSelf?> _optional;

        private readonly Option<TSelf[]> _array;

        public Probe(Command command, string name)
        {
            _one = new Option<TSelf>(name);
            _optional = new Option<TSelf?>($"{name}-optional");
            _array = new Option<TSelf[]>($"{name}-array") { AllowMultipleArgumentsPerToken = true };
            command.Add(_one);
            command.Add(_optional);
            command.Add(_array);
        }

        public void Run(Report report, Command command)
        {
            var type = Names.Of(typeof(TSelf));
            var texts = Underlying.Texts(typeof(TValue));
            foreach (var text in texts)
            {
                var result = command.Parse([_one.Name, text]);
                var line = $"cli {type} {Underlying.Quote(text)}";
                var outcome = Describe(result, _one);

                // Two runs reading nothing as the rules do would agree: an accepted text reads the instance TryParse
                // gives, and a refused one leaves one error carrying the code TryParse gives.
                var accepted = TSelf.TryParse(text, CultureInfo.InvariantCulture, out var expected, out var validation);
                var code = validation.IsValid ? ValueObjectErrorCodes.NotParsable : validation.ErrorCode;
                if (accepted
                        ? result.Errors.Count > 0 || !result.GetValue(_one).Equals(expected)
                        : result.Errors.Count != 1 || !result.Errors[0].TryGetValueObjectErrorCode(out var read) || read != code)
                {
                    report.Fail(line, outcome);
                }
                else
                {
                    report.Line(line, outcome);
                }
            }

            report.Line($"cli {type}? absent", Describe(command.Parse([]), _optional));
            report.Line($"cli {type}? \"\"", Describe(command.Parse([_optional.Name, string.Empty]), _optional));
            if (texts.Count > 0)
            {
                report.Line($"cli {type}[] every text", Describe(command.Parse([_array.Name, .. texts]), _array));
            }

            if (typeof(TValue) == typeof(bool))
            {
                report.Line($"cli {type} flag", Describe(command.Parse([_one.Name]), _one));
            }

            var completions = command.Parse($"{_one.Name} ").GetCompletions(_one.Name.Length + 1)
                .Select(static item => item.Documentation is null ? item.Label : $"{item.Label} ({item.Documentation})")
                .ToList();
            if (completions.Count > 0)
            {
                report.Line($"cli {type} completions", string.Join(", ", completions));
            }
        }
    }
}
