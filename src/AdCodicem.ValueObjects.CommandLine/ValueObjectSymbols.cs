using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.CommandLine;

/// <summary>What configuring a symbol came to.</summary>
internal enum ValueObjectSymbolOutcome
{
    /// <summary>The symbol reads its text through the value object's rules.</summary>
    Configured,

    /// <summary>The symbol already has a parser, which was left alone.</summary>
    ParserAlreadySet,

    /// <summary>The symbol holds the value object in a shape that is not read.</summary>
    UnsupportedShape,
}

/// <summary>
/// Configures the options and arguments of one value object, closed over it through the descriptor's visitor, which is
/// not generic, so that a native binary holds one closing per value object rather than one per symbol type.
/// </summary>
internal abstract class ValueObjectSymbol
{
    /// <summary>The collections a symbol may hold a value object in, as the misuse messages list them.</summary>
    private const string Collections = "an array, a List<T>, an IEnumerable<T> or an IReadOnlyList<T>";

    /// <summary>
    /// The reader of each option and argument configured to read a value object, which converts it as <c>GetValue</c>
    /// does, kept for as long as the symbol lives.
    /// </summary>
    private static readonly ConditionalWeakTable<Symbol, Action<CommandResult>> Readers = new();

    /// <summary>
    /// The validator <c>AcceptValueObjects</c> adds to each command it reaches, which, when the command is the innermost
    /// one of a parse, reads the configured options and arguments of the commands above it that System.CommandLine leaves
    /// unread until <c>GetValue</c>, so that a refusal there is a parse error before any action runs.
    /// </summary>
    public static readonly Action<CommandResult> ParentsReader = ReadParents;

    /// <summary>Configures an option of the value object, its nullable type or a collection of it.</summary>
    /// <param name="option">The option.</param>
    /// <param name="settings">The culture and the messages.</param>
    /// <returns>What configuring it came to.</returns>
    public abstract ValueObjectSymbolOutcome Configure(Option option, ValueObjectArgumentSettings settings);

    /// <summary>Configures an argument of the value object, its nullable type or a collection of it.</summary>
    /// <param name="argument">The argument.</param>
    /// <param name="settings">The culture and the messages.</param>
    /// <returns>What configuring it came to.</returns>
    public abstract ValueObjectSymbolOutcome Configure(Argument argument, ValueObjectArgumentSettings settings);

    /// <summary>
    /// Finds the symbols of the value object a symbol's type holds: the descriptor registered, or, where the runtime
    /// supports dynamic code, one the registry describes by reflection, as <c>[ParsesAs(typeof(T))]</c> does.
    /// </summary>
    /// <param name="type">The type of the symbol's value.</param>
    /// <returns>The symbols, or <see langword="null"/> when the type holds no value object the registry describes.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Only reached for a type a symbol's type argument names, which keeps it, and which nothing registered: "
                        + "a value object written by hand or a construction of a generic one, or a type that is no value "
                        + "object. Under native AOT, where dynamic code is not supported, the registered descriptor is the "
                        + "only one.")]
    public static ValueObjectSymbol? Find(Type type)
    {
        var element = Element(type);
        if (ValueObjectRegistry.TryGet(element, out var descriptor)
            || (RuntimeFeature.IsDynamicCodeSupported && ValueObjectRegistry.TryResolve(element, out descriptor)))
        {
            return descriptor.Accept(Closer.Instance);
        }

        return null;
    }

    /// <summary>
    /// Tells whether a symbol's type holds a value object, described by the registry or not, as its nullable type or as an
    /// element of a collection.
    /// </summary>
    /// <param name="type">The type of the symbol's value.</param>
    /// <returns><see langword="true"/> when it holds one.</returns>
    public static bool HoldsValueObject(Type type) => ValueObjectRegistry.IsValueObject(Element(type));

    /// <summary>
    /// Gets the name a refusal names an option by, as System.CommandLine's own messages do: the alias typed, or the
    /// option's name where nothing was typed.
    /// </summary>
    /// <param name="option">The option's result.</param>
    /// <returns>The name.</returns>
    public static string NameOf(OptionResult option) => option.IdentifierToken?.Value ?? option.Option.Name;

    /// <summary>The misuse of a symbol whose type holds no value object the registry describes.</summary>
    /// <param name="name">The symbol's name.</param>
    /// <param name="type">The type of its value.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException NotDescribed(string name, Type type)
        => new(
            $"'{name}' holds '{TypeNames.Of(type)}', which is not a value object the registry describes, its nullable type, or "
            + $"{Collections} of one. Under "
            + "native AOT, a value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a "
            + "construction of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => new "
            + "TSelf.ValueJsonConverter()), have to be registered before AcceptValueObject or AcceptValueObjects is called.");

    /// <summary>The misuse of a symbol holding a value object in a shape that is not read.</summary>
    /// <param name="name">The symbol's name.</param>
    /// <param name="type">The type of its value.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException Unsupported(string name, Type type)
        => new(
            $"'{name}' holds '{TypeNames.Of(type)}', a value object in a shape AcceptValueObject does not read: it reads the value "
            + $"object, its nullable type, or {Collections} of it.");

    /// <summary>The misuse of a symbol whose parser the application set.</summary>
    /// <param name="name">The symbol's name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException AlreadyParsed(string name)
        => new($"'{name}' already has a CustomParser, which AcceptValueObject would replace. AcceptValueObjects leaves such a symbol alone.");

    /// <summary>Keeps the reader of an option or an argument just configured.</summary>
    /// <param name="symbol">The option or the argument.</param>
    /// <param name="read">Converts it, in the parse of the command result it is given, as <c>GetValue</c> does.</param>
    protected static void Track(Symbol symbol, Action<CommandResult> read) => Readers.AddOrUpdate(symbol, read);

    /// <summary>
    /// Writes the position of a token among several as a person counts it, an English ordinal from one: <c>1st</c>,
    /// <c>2nd</c>, <c>3rd</c>, <c>4th</c>, <c>11th</c>, <c>21st</c>, <c>111th</c>.
    /// </summary>
    /// <param name="index">The position from zero, as the key of a localized message writes it.</param>
    /// <returns>The ordinal.</returns>
    protected static string Ordinal(int index)
    {
        var count = index + 1;
        var suffix = (count % 100) is 11 or 12 or 13
            ? "th"
            : (count % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        return string.Create(CultureInfo.InvariantCulture, $"{count}{suffix}");
    }

    /// <summary>
    /// Reads the configured options and arguments of the commands above the innermost one that System.CommandLine does not
    /// read while it parses: a parent command is validated with <c>completeValidation: false</c>, which skips an option
    /// neither <c>Recursive</c> nor with a default value, and an argument without one
    /// (<c>CommandResult.ValidateOptionsAndAddDefaultResults</c>, <c>ValidateArgumentsAndAddDefaultResults</c>).
    /// </summary>
    private static void ReadParents(CommandResult result)
    {
        for (var parent = result.Parent as CommandResult; parent is not null; parent = parent.Parent as CommandResult)
        {
            foreach (var option in parent.Command.Options)
            {
                if (!option.Recursive && !option.HasDefaultValue && Readers.TryGetValue(option, out var read))
                {
                    read(result);
                }
            }

            foreach (var argument in parent.Command.Arguments)
            {
                if (!argument.HasDefaultValue && Readers.TryGetValue(argument, out var read))
                {
                    read(result);
                }
            }
        }
    }

    /// <summary>
    /// Gets the type a symbol holds a value object as: the element of an array, a <see cref="List{T}"/>, an
    /// <see cref="IEnumerable{T}"/> or an <see cref="IReadOnlyList{T}"/>, or the type itself, its nullable type included.
    /// </summary>
    private static Type Element(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return type;
    }

    /// <summary>Closes the symbols over the type arguments a descriptor hands back.</summary>
    private sealed class Closer : IValueObjectVisitor<ValueObjectSymbol>
    {
        public static readonly Closer Instance = new();

        /// <summary>Closes the symbols.</summary>
        /// <typeparam name="TSelf">The value object type.</typeparam>
        /// <typeparam name="TValue">Its underlying type.</typeparam>
        /// <returns>The symbols of the value object.</returns>
        public ValueObjectSymbol Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => ValueObjectSymbol<TSelf, TValue>.Instance;
    }
}

/// <summary>The options and arguments of one value object.</summary>
/// <typeparam name="TSelf">The value object type.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
internal sealed class ValueObjectSymbol<TSelf, TValue> : ValueObjectSymbol
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>The symbols of the value object, which hold no state.</summary>
    public static readonly ValueObjectSymbol<TSelf, TValue> Instance = new();

    /// <summary>
    /// Whether the value object holds a <see cref="bool"/>, whose option is a flag, as System.CommandLine's own
    /// <see cref="bool"/> option is: given alone, it reads <see langword="true"/>.
    /// </summary>
    private static readonly bool Flag = typeof(TValue) == typeof(bool);

    private ValueObjectSymbol()
    {
    }

    /// <inheritdoc />
    public override ValueObjectSymbolOutcome Configure(Option option, ValueObjectArgumentSettings settings) => option switch
    {
        Option<TSelf> one => Install(one, Single(settings), settings, Flag),
        Option<TSelf?> optional => Install(optional, Optional(settings), settings, Flag),
        Option<TSelf[]> array => Install(array, result => [.. Many(result, settings)], settings, flag: false),
        Option<List<TSelf>> list => Install(list, result => Many(result, settings), settings, flag: false),
        Option<IEnumerable<TSelf>> sequence => Install(sequence, result => Many(result, settings), settings, flag: false),
        Option<IReadOnlyList<TSelf>> readOnlyList => Install(readOnlyList, result => Many(result, settings), settings, flag: false),
        _ => ValueObjectSymbolOutcome.UnsupportedShape,
    };

    /// <inheritdoc />
    public override ValueObjectSymbolOutcome Configure(Argument argument, ValueObjectArgumentSettings settings) => argument switch
    {
        Argument<TSelf> one => Install(one, Single(settings), settings),
        Argument<TSelf?> optional => Install(optional, Optional(settings), settings),
        Argument<TSelf[]> array => Install(array, result => [.. Many(result, settings)], settings),
        Argument<List<TSelf>> list => Install(list, result => Many(result, settings), settings),
        Argument<IEnumerable<TSelf>> sequence => Install(sequence, result => Many(result, settings), settings),
        Argument<IReadOnlyList<TSelf>> readOnlyList => Install(readOnlyList, result => Many(result, settings), settings),
        _ => ValueObjectSymbolOutcome.UnsupportedShape,
    };

    /// <summary>
    /// Sets an option's parser, unless the application set one, and describes the option from the value object's rules.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="parser">Its parser.</param>
    /// <param name="settings">The culture and the messages.</param>
    /// <param name="flag">Whether the option is a flag, which reads <see langword="true"/> given alone.</param>
    private static ValueObjectSymbolOutcome Install<T>(Option<T> option, Func<ArgumentResult, T?> parser, ValueObjectArgumentSettings settings, bool flag)
    {
        if (option.CustomParser is not null)
        {
            return ValueObjectSymbolOutcome.ParserAlreadySet;
        }

        option.CustomParser = parser;
        Track(option, result =>
        {
            try
            {
                if (result.GetResult(option) is { } given)
                {
                    _ = given.GetValueOrDefault<T>();
                }
            }
            catch (InvalidOperationException)
            {
                // A refusal, which the parser added to the parse's errors before System.CommandLine threw.
            }
        });

        if (flag)
        {
            // An arity the application set stays; System.CommandLine's default for a type that is not bool, one value,
            // cannot be told from ExactlyOne set on purpose.
            if (option.Arity is { MinimumNumberOfValues: 1, MaximumNumberOfValues: 1 })
            {
                option.Arity = ArgumentArity.ZeroOrOne;
            }

            // System.CommandLine calls the default value factory, not the parser, for an option given without a value
            // whose type is not bool: the flag given alone reads true, and its factory serves it absent and in the help.
            if (option.DefaultValueFactory is { } factory)
            {
                option.DefaultValueFactory = result => result.Parent is OptionResult { Implicit: false } ? parser(result)! : factory(result);
            }
        }

        option.Description ??= TSelf.Schema.Description;
        if (option.CompletionSources.Count == 0 && Completions(settings.Culture) is { } completions)
        {
            option.CompletionSources.Add(_ => completions);

            // The known values of an open set are suggestions, which the help would list as the only ones.
            if (!TSelf.Schema.IsClosedValueSet)
            {
                option.HelpName ??= option.Name.TrimStart('-', '/');
            }
        }

        return ValueObjectSymbolOutcome.Configured;
    }

    /// <summary>
    /// Sets an argument's parser, unless the application set one, and describes the argument from the value object's
    /// rules.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <param name="parser">Its parser.</param>
    /// <param name="settings">The culture and the messages.</param>
    private static ValueObjectSymbolOutcome Install<T>(Argument<T> argument, Func<ArgumentResult, T?> parser, ValueObjectArgumentSettings settings)
    {
        if (argument.CustomParser is not null)
        {
            return ValueObjectSymbolOutcome.ParserAlreadySet;
        }

        argument.CustomParser = parser;
        Track(argument, result =>
        {
            try
            {
                if (result.GetResult(argument) is { } given)
                {
                    _ = given.GetValueOrDefault<T>();
                }
            }
            catch (InvalidOperationException)
            {
                // A refusal, which the parser added to the parse's errors before System.CommandLine threw.
            }
        });

        argument.Description ??= TSelf.Schema.Description;
        if (argument.CompletionSources.Count == 0 && Completions(settings.Culture) is { } completions)
        {
            argument.CompletionSources.Add(_ => completions);

            // The known values of an open set are suggestions, which the help would list as the only ones.
            if (!TSelf.Schema.IsClosedValueSet)
            {
                argument.HelpName ??= argument.Name;
            }
        }

        return ValueObjectSymbolOutcome.Configured;
    }

    /// <summary>The parser of one value, which a refusal leaves at the default instance.</summary>
    private static Func<ArgumentResult, TSelf> Single(ValueObjectArgumentSettings settings)
        => result => One(result, settings, out var value) ? value : default;

    /// <summary>The parser of one value of the nullable type, which a refusal leaves <see langword="null"/>.</summary>
    private static Func<ArgumentResult, TSelf?> Optional(ValueObjectArgumentSettings settings)
        => result => One(result, settings, out var value) ? value : null;

    /// <summary>
    /// Reads one value: the last token, as System.CommandLine's own converters do, every token before it read through the
    /// rules too where the application's arity allows several; no token, a flag's <see langword="true"/>, or nothing.
    /// </summary>
    private static bool One(ArgumentResult result, ValueObjectArgumentSettings settings, out TSelf value)
    {
        var tokens = result.Tokens;
        if (tokens.Count > 0)
        {
            // No token is dropped unread: a refused one is an error under its position, and the last is the value.
            var read = true;
            value = default;
            for (var index = 0; index < tokens.Count; index++)
            {
                read &= Read(result, tokens[index].Value, settings, tokens.Count > 1 ? index : null, out value);
            }

            return read;
        }

        if (Flag)
        {
            if (TSelf.TryCreate((TValue)(object)true, out value, out var validation))
            {
                return true;
            }

            Refuse(result, validation, settings, position: null);
            return false;
        }

        // An option given alone, at an arity the application set to allow it: nothing was read, as for one left out.
        value = default;
        return false;
    }

    /// <summary>Reads each token, refusing each one its rules refuse, under its position when there are several.</summary>
    private static List<TSelf> Many(ArgumentResult result, ValueObjectArgumentSettings settings)
    {
        var tokens = result.Tokens;
        var values = new List<TSelf>(tokens.Count);
        for (var index = 0; index < tokens.Count; index++)
        {
            if (Read(result, tokens[index].Value, settings, tokens.Count > 1 ? index : null, out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    /// <summary>Reads a token through the value object's rules, and refuses it with the rule it breaks.</summary>
    private static bool Read(ArgumentResult result, string text, ValueObjectArgumentSettings settings, int? position, out TSelf value)
    {
        if (ValueObjectText<TSelf, TValue>.TryParse(text, settings.Culture, out value, out var validation))
        {
            return true;
        }

        Refuse(result, validation, settings, position);
        return false;
    }

    /// <summary>
    /// Adds the parse error of a refusal, naming the symbol and never the text, and keeps the rule's code beside it. A
    /// token among several is counted from one in the English message, which a person reads (<c>the 2nd value</c>), and
    /// from zero in the <c>{0}</c> of a localized one (<c>--ibans[1]</c>), as the gRPC package's element paths and MVC's
    /// keys are.
    /// </summary>
    private static void Refuse(ArgumentResult result, ValidationResult validation, ValueObjectArgumentSettings settings, int? position)
    {
        var code = validation.ErrorCode!;
        var option = result.Parent as OptionResult;
        var name = option is null ? result.Argument.Name : NameOf(option);
        var subject = (option is null ? $"Argument '{name}'" : $"Option '{name}'") + (position is { } at ? $", the {Ordinal(at)} value" : string.Empty);

        // An empty rule message would read as a sentence cut short.
        var english = validation.ErrorMessage is { Length: > 0 } rule
            ? $"{subject}: {rule}"
            : $"{subject}{(position is null ? string.Empty : ",")} is invalid.";

        var localized = settings.ResourceType is { } resource
            ? ValueObjectResourceMessages.Find(
                resource,
                ValueObjectMessages.KeysFor(TSelf.Schema, code),
                () => ValueObjectMessages.ArgumentsFor<TSelf, TValue>(code, CultureInfo.CurrentCulture),
                position is { } index ? $"{name}[{index}]" : name)
            : null;

        // A parse error refuses a blank message, which a resource may hold: the English one stands.
        result.AddError(string.IsNullOrWhiteSpace(localized) ? english : localized);
        ValueObjectParseErrorExtensions.Record((SymbolResult?)option ?? result, code, typeof(TSelf));
    }

    /// <summary>
    /// Gets the completions of the known values, each written as its parser reads it back in the culture and documented by
    /// the description it was declared with; <see langword="null"/> for a type that declares none.
    /// </summary>
    private static CompletionItem[]? Completions(CultureInfo culture)
    {
        var schema = TSelf.Schema;
        var known = schema.KnownValues;
        if (known.IsEmpty)
        {
            return null;
        }

        var details = schema.KnownValueDetails;
        var completions = new CompletionItem[known.Length];
        for (var index = 0; index < known.Length; index++)
        {
            // A schema written by hand may list a value of another type, or no details.
            var label = known[index] is TValue value
                ? ValueObjectText<TSelf, TValue>.FormatValue(value, culture)
                : string.Create(culture, $"{known[index]}");
            completions[index] = new CompletionItem(label, documentation: index < details.Length ? details[index].Description : null);
        }

        return completions;
    }
}
