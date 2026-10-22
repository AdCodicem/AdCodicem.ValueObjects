# AdCodicem.ValueObjects.CommandLine — System.CommandLine options and arguments

User-facing behaviour: `website/docs/how-to/files-and-command-line.md`. Tests: `Cli/` of
`tests/AdCodicem.ValueObjects.UnitTests.Text` (commands parsed and invoked in process), `Cli.cs` of `tests/NativeAot`,
and the island's `CommandLineTests.cs` on System.CommandLine 3.0.

## What System.CommandLine does around the parser

- Validators run before any parser (`ArgumentResult.ValidateAndConvert`): `AcceptOnlyFromAmong` compares the raw token
  ordinally and refuses `de` for a type normalizing it to `DE`, with no code, so the package never calls it and offers
  the known values as completions instead; `ParserAssumptionsTests` pins it.
- Setting `CustomParser` replaces the argument's converter with a wrapper that fails the conversion when the tree's
  error count grew: a refusal is `ArgumentResult.AddError`, never a thrown exception, and `GetValue` on a refused symbol
  throws System.CommandLine's `InvalidOperationException`, built from the message alone, so it carries no code.
- `ParseError` is sealed, with an internal constructor, and holds a message and a `SymbolResult` alone: the code lives
  in `ValueObjectParseErrorExtensions`' `ConditionalWeakTable`, keyed by the last error of the public symbol result
  right after `AddError`, the `OptionResult` for an option's argument (System.CommandLine's private
  `AppliesToPublicSymbolResult`); `ParserAssumptionsTests` and every code assertion of `Cli/` pin where it lands.
- `ParseError`'s constructor throws `ArgumentException` for a white-space message: a blank entry of a resource keeps the
  English message rather than fail the parse.
- System.CommandLine calls no parser for a symbol left out: it is the default instance, `null` for `TSelf?`, an empty
  array or list for `T[]`, `List<T>` and `IEnumerable<T>`, and `null` for `IReadOnlyList<T>`, which its
  `Argument<T>.CreateDefaultValue` does not build. The package changes nothing there; the guide says to declare
  `TSelf?`, `Required` or a `DefaultValueFactory`.
- It calls the `DefaultValueFactory`, not the parser, for an option given without a value whose type is not `bool` or
  `bool?` (`OptionResult.UseDefaultValueFor`): a flag's factory is wrapped when the option is configured, so one set
  afterwards serves the flag given alone (`CliFlagTests` pins it).
- An option of a type other than `bool` defaults to one value, which cannot be told from `ExactlyOne` set on purpose
  (`ArgumentArity.IsNonDefault` is internal): a flag at one value becomes `ZeroOrOne`, any other arity stays.
- `ArgumentResult.OnlyTake` throws `NotSupportedException` for an option's argument: a flag at `ZeroOrOne` cannot give
  back a positional token it took, which it refuses, where System.CommandLine's own `bool` option takes the next token
  only when `bool.TryParse` reads it (internal).
- System.CommandLine reads its own `decimal`, `double` and date options in the current culture, group separators
  included (`12,5` is 125 under the invariant culture): a value object, read in the invariant culture unless `Culture`
  names another, refuses it; the guide says so (dotnet/command-line-api#1733).
- A `RootCommand` holds a `HelpOption`; a `Command` holds none, and a refusal on it shows no help.
- A parent command is validated with `completeValidation: false` (`CommandResult.ValidateOptionsAndAddDefaultResults`,
  `ValidateArgumentsAndAddDefaultResults`): its options neither `Recursive` nor with a default value, and its arguments
  without one, are converted only at `GetValue`, inside the subcommand's action, where a refusal throws a code-less
  `InvalidOperationException`; its own `Argument<int>` behaves the same. `ParserAssumptionsTests` pins it.
- A command's validators run before its options and arguments, and once one adds an error,
  `CommandResult.Validate` returns without validating them: the errors of a subcommand's own symbols wait until a
  refused parent value is corrected (`ParserAssumptionsTests`, `CliSubcommandTests`).
- An arity the application sets may give a single-value symbol several tokens, where System.CommandLine's own scalar
  converter throws `ArgumentException` from `Parse`.

## What the package does

- The walk reaches the symbols the tree holds when it runs, from the command down, each command once: a symbol added
  later is left to System.CommandLine, and a parent's `Recursive` option is configured by the parent's walk only.
- Each command the walk reaches gets `ValueObjectSymbol.ParentsReader` as a validator, once (`Validators.Contains`):
  when the command is innermost, it reads, through `GetValueOrDefault<T>` as `GetValue` does, the configured symbols of
  the commands above it that the parent's validation skips, so that their refusals are parse errors before any action.
  It reads only those: one System.CommandLine validates as well would be refused twice for its arity, and an argument
  with a default value would skip its validators, which `GetValueOrDefault` does not run. Every configured symbol,
  `AcceptValueObject`'s included, keeps its reader in a `ConditionalWeakTable` (`ValueObjectSymbol.Track`).
- A symbol whose `CustomParser` is set is left alone by the walk and refused by `AcceptValueObject`
  (`ValueObjectSymbolOutcome.ParserAlreadySet`): only the closing over its type reads the parser, the non-generic
  `Option` and `Argument` exposing none, so a value object the registry cannot describe throws from the walk even beside
  a parser of the application's own; the guide and the remarks say to register it, and `WithoutDynamicCodeCliTests`
  pins it.
- One value reads every token it was given, a refused one under its position, the last being the value: none is
  dropped unread whatever arity the application set, before or after the call.
- A refused token among several is counted from one in the English message, `the 2nd value`
  (`ValueObjectSymbol.Ordinal`: `11th`, `21st`, `111th`), and from zero in `{0}`, `--ibans[1]`, as the gRPC package's
  element paths and MVC's keys: two numberings, each for its reader, the maintainer's choice; `CliRefusalTests` and
  `LocalizedCliTests` pin both.
- The value object is found by unwrapping an array, `List<T>`, `IEnumerable<T>` or `IReadOnlyList<T>`, then
  `Nullable<T>`, with `TryGet`, then `TryResolve` where dynamic code runs, and closed once per value object through the
  non-generic `ValueObjectSymbol.Closer`; another collection (`HashSet<T>`) is not detected, since
  `Type.GetInterfaces()` warns under trimming.
- The walk tells a value object it cannot describe from another type with `ValueObjectRegistry.IsValueObject`, and
  refuses it when configured rather than leave its option to throw at `GetValue`.
- Known values become completions only where the application gave none, and an open set's `HelpName` is set only then:
  an argument's `CompletionSources` holds System.CommandLine's own for `bool` and enums alone. Their text is the
  round-trip form (`ValueObjectText.FormatValue`), so a date completed under `th-TH` reads back.
- The text is read through `src/Shared/ValueObjectText.cs` (a date in a Gregorian copy of the culture, a reason-less
  refusal as `not_parsable`), messages through `src/Shared/ValueObjectResourceMessages.cs` (a command line has no
  services), and misuse messages name types through `src/Shared/TypeNames.cs`.
- The options are read once, when the call returns (`ValueObjectArgumentSettings`), so the parsers capture no mutable
  state.

## Versions and names

- Built against 2.0.x; 3.0, whose version follows .NET's, adds three members (`AcceptOnlyFromAmong` with a comparer,
  `CaptureRemainingTokens`, `RootCommand.HelpName`) and runs the package as built: the island pins the 3.0 side, and
  Dependabot ignores the majors.
- The namespace hides a root `CommandLine` namespace (CommandLineParser's), never `System.CommandLine` (root
  `CLAUDE.md`): the folder of `tests/AdCodicem.ValueObjects.UnitTests.Text` is `Cli/` and the native application's
  class `Cli`.
