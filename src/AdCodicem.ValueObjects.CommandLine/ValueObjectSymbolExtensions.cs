using System.CommandLine;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.CommandLine;

/// <summary>
/// Reads the options and arguments of value objects through the rules of their type, and reports the rule a value breaks
/// as a parse error carrying its code.
/// </summary>
/// <remarks>
/// <para>
/// System.CommandLine converts text through a closed table of its own, which holds no value object: without these
/// methods, a valid value parses with no error and <c>GetValue</c> then throws. <see cref="AcceptValueObjects"/> sets the
/// parser of every option and argument of a value object, in a command and its subcommands, once they are added;
/// <c>AcceptValueObject</c> sets the parser of one. A symbol holds the value object, its nullable type, or an array, a
/// <see cref="List{T}"/>, an <see cref="IEnumerable{T}"/> or an <see cref="IReadOnlyList{T}"/> of it. Its descriptor is
/// the one registered, or, where the runtime supports dynamic code, one the registry describes by reflection; under native
/// AOT, a value object written by hand and a construction of a generic one are registered first, with
/// <see cref="ValueObjectRegistry.Register{TSelf, TValue}(ValueObjectSchema)"/>.
/// </para>
/// <para>
/// A value reads the last token given, and a collection each token, through <c>TryParse</c>, the overload reporting the
/// rule: the value is the normalized instance. Where an arity of the application's own gives a value several tokens, each
/// one before the last is read through the rules too. A refusal is a parse error, which an invocation writes to standard
/// error before the help, returning 1 without running the action:
/// <c>Option '--quantity': The value must be less than or equal to 100.</c>, <c>Argument 'iban': …</c>, and
/// <c>Option '--ibans', the 2nd value: …</c> for one token of several. It names the option by the alias typed, never
/// quotes the text, and keeps the rule's code beside the error, which
/// <see cref="ValueObjectParseErrorExtensions.TryGetValueObjectErrorCode"/> reads. A parser written by hand that
/// refuses without a reason gives <see cref="ValueObjectErrorCodes.NotParsable"/>. <c>GetValue</c> on a refused symbol
/// throws System.CommandLine's <see cref="InvalidOperationException"/>, carrying the message and no code.
/// </para>
/// <para>
/// System.CommandLine reads an option of a parent command that is neither <c>Recursive</c> nor given a default value, and
/// an argument of a parent command, only when <c>GetValue</c> asks for it, inside the action of the subcommand invoked.
/// <see cref="AcceptValueObjects"/> gives each command it reaches a validator that reads those of the commands above it,
/// so that a refusal there is a parse error too; System.CommandLine then leaves the subcommand's own options and arguments
/// unvalidated, and their errors appear once the refused value is corrected. A symbol configured by
/// <c>AcceptValueObject</c> is read there as well, once a walk reached the subcommand; otherwise its refusal surfaces at
/// <c>GetValue</c>.
/// </para>
/// <para>
/// Empty text is a token like any other, which the type's own rule refuses or accepts, never <see langword="null"/>.
/// System.CommandLine calls no parser for an option left out: it is <see langword="null"/> for the nullable type, and the
/// default instance otherwise, unless the option is <c>Required</c> or has a <c>DefaultValueFactory</c>, whose value,
/// which <c>Create</c> made, is not read again. An option of a value object over <see cref="bool"/> is a flag, as
/// System.CommandLine's own <see cref="bool"/> option is: given alone it reads <see langword="true"/>, and <c>false</c> after
/// it reads <see langword="false"/>; unlike System.CommandLine's, it takes any token after it as its value, so a positional
/// argument right after the flag is refused as one.
/// </para>
/// <para>
/// A symbol without a description takes the value object's summary, and the known values become its completions, each
/// documented by its description, unless the application gave it completions of its own; the help then labels a closed
/// set's option <c>&lt;BE|FR|LU&gt;</c>, and an open set's keeps its name. <c>AcceptOnlyFromAmong</c>, which compares the
/// text before the normalizer runs and answers without a code, is never called: the rule refuses an unknown value.
/// </para>
/// </remarks>
public static class ValueObjectSymbolExtensions
{
    /// <summary>
    /// Reads the option's text through the rules of its value object, the value object's nullable type, or an array or
    /// list of it, and reports the rule a value breaks.
    /// </summary>
    /// <typeparam name="T">The value object, its nullable type, or an array, a list or a sequence of it.</typeparam>
    /// <param name="option">The option.</param>
    /// <param name="configure">Configures the culture the text is read in, and the messages; the defaults when omitted.</param>
    /// <returns>The option, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> holds no value object the registry describes, holds one in a shape this does not read, or the
    /// option already has a <c>CustomParser</c>, which this would replace.
    /// </exception>
    /// <remarks>Set a flag's <c>DefaultValueFactory</c> before calling this: one set afterwards serves the flag given alone.</remarks>
    public static Option<T> AcceptValueObject<T>(this Option<T> option, Action<ValueObjectArgumentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(option);

        var symbol = ValueObjectSymbol.Find(typeof(T)) ?? throw ValueObjectSymbol.NotDescribed(option.Name, typeof(T));
        Check(symbol.Configure(option, ValueObjectArgumentOptions.Read(configure)), option.Name, typeof(T));

        return option;
    }

    /// <summary>
    /// Reads the argument's text through the rules of its value object, the value object's nullable type, or an array or
    /// list of it, and reports the rule a value breaks.
    /// </summary>
    /// <typeparam name="T">The value object, its nullable type, or an array, a list or a sequence of it.</typeparam>
    /// <param name="argument">The argument.</param>
    /// <param name="configure">Configures the culture the text is read in, and the messages; the defaults when omitted.</param>
    /// <returns>The argument, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> holds no value object the registry describes, holds one in a shape this does not read, or the
    /// argument already has a <c>CustomParser</c>, which this would replace.
    /// </exception>
    public static Argument<T> AcceptValueObject<T>(this Argument<T> argument, Action<ValueObjectArgumentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(argument);

        var symbol = ValueObjectSymbol.Find(typeof(T)) ?? throw ValueObjectSymbol.NotDescribed(argument.Name, typeof(T));
        Check(symbol.Configure(argument, ValueObjectArgumentOptions.Read(configure)), argument.Name, typeof(T));

        return argument;
    }

    /// <summary>
    /// Applies <c>AcceptValueObject</c> to every option and argument of a value object, in the command and its
    /// subcommands, leaving alone a symbol whose <c>CustomParser</c> the application set once the registry describes its
    /// value object.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command, a <see cref="RootCommand"/> or any other.</typeparam>
    /// <param name="command">The command.</param>
    /// <param name="configure">Configures the culture the text is read in, and the messages; the defaults when omitted.</param>
    /// <returns>The command, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// A symbol holds a value object the registry cannot describe, or holds one in a shape this does not read.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Call it once every option, argument and subcommand is added: it reaches the symbols the tree holds when it runs,
    /// from the command down, so a parent's <c>Recursive</c> option is read through its rules by the parent's call, not a
    /// subcommand's. A symbol already configured, by an earlier call or by <c>AcceptValueObject</c>, keeps its settings.
    /// Each command reached reads, when it is invoked, the configured options and arguments of the commands above it,
    /// which System.CommandLine would read only at <c>GetValue</c>.
    /// </para>
    /// <para>
    /// The value object behind a symbol is found before its <c>CustomParser</c> is read, which only the closing over its
    /// type can do: where the runtime supports no dynamic code, a value object written by hand or a construction of a
    /// generic one that nothing registered throws even beside a parser of the application's own. Register it.
    /// </para>
    /// </remarks>
    public static TCommand AcceptValueObjects<TCommand>(this TCommand command, Action<ValueObjectArgumentOptions>? configure = null)
        where TCommand : Command
    {
        ArgumentNullException.ThrowIfNull(command);

        Walk(command, ValueObjectArgumentOptions.Read(configure), []);

        return command;
    }

    /// <summary>Configures the symbols of a command and of its subcommands, each command once.</summary>
    private static void Walk(Command command, ValueObjectArgumentSettings settings, HashSet<Command> walked)
    {
        if (!walked.Add(command))
        {
            return;
        }

        // System.CommandLine reads a parent command's option or argument only when the action asks for it, unless it is
        // recursive or has a default value: the command, when it is the innermost one, reads them while it is validated.
        if (!command.Validators.Contains(ValueObjectSymbol.ParentsReader))
        {
            command.Validators.Add(ValueObjectSymbol.ParentsReader);
        }

        foreach (var option in command.Options)
        {
            if (Find(option.Name, option.ValueType)?.Configure(option, settings) == ValueObjectSymbolOutcome.UnsupportedShape)
            {
                throw ValueObjectSymbol.Unsupported(option.Name, option.ValueType);
            }
        }

        foreach (var argument in command.Arguments)
        {
            if (Find(argument.Name, argument.ValueType)?.Configure(argument, settings) == ValueObjectSymbolOutcome.UnsupportedShape)
            {
                throw ValueObjectSymbol.Unsupported(argument.Name, argument.ValueType);
            }
        }

        foreach (var subcommand in command.Subcommands)
        {
            Walk(subcommand, settings, walked);
        }
    }

    /// <summary>
    /// Finds the symbols of the value object a symbol of the walk holds, or nothing for a type that holds none, and refuses
    /// one the registry cannot describe rather than leave it to throw at <c>GetValue</c>.
    /// </summary>
    private static ValueObjectSymbol? Find(string name, Type type)
        => ValueObjectSymbol.Find(type)
           ?? (ValueObjectSymbol.HoldsValueObject(type) ? throw ValueObjectSymbol.NotDescribed(name, type) : null);

    /// <summary>Throws for a symbol the call cannot configure.</summary>
    private static void Check(ValueObjectSymbolOutcome outcome, string name, Type type)
    {
        if (outcome == ValueObjectSymbolOutcome.ParserAlreadySet)
        {
            throw ValueObjectSymbol.AlreadyParsed(name);
        }

        if (outcome == ValueObjectSymbolOutcome.UnsupportedShape)
        {
            throw ValueObjectSymbol.Unsupported(name, type);
        }
    }
}
