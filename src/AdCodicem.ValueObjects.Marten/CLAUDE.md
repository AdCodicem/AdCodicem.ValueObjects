# AdCodicem.ValueObjects.Marten

User-facing behaviour: `website/docs/how-to/marten.md`. Tests: `Persistence/Marten*.cs` of
`tests/AdCodicem.ValueObjects.UnitTests.Data` (offline: SQL, compiled-query previews, DDL) and
`tests/AdCodicem.ValueObjects.IntegrationTests/MartenTests.cs` (PostgreSQL); natively,
`tests/NativeAot/AdCodicem.ValueObjects.NativeAot.Marten`.

## Marten's internals

- `StoreOptions.ValueTypes` is internal: reached through `[UnsafeAccessor]` `get_ValueTypes` (`MartenValueTypes.Of`).
  `MartenInternalsTests` pins it, the duplicated field finding a value type by its member's `MemberType`, Marten's
  refusal of a compiled query over a value type the list does not hold, the
  `ValueTypeSelectClause<,>(string, Func<TInner, TOuter>)` constructor, each step of the compiled-query planning the
  binding relies on, and Marten's stale value-type and identity parameters, so that a Marten release changing one fails
  CI first: adapt the accessor or the binding, never silence the test.
- Marten reads the list without a lock; the package adds under its own, what the registry holds when `UseValueObjects()`
  runs and the rest when Marten first resolves the member: a lazy add can race with a reader (`List<T>` grows its array
  and bumps its count before storing the item).
- Each value object takes one entry, `TSelf?`, never `TSelf`: `QueryCompiler.assertValidityOfQueryType` accepts a query
  type holding a value type the list holds, and Marten binds none, so a `TSelf` entry made every compiled query taking
  one answer with its first call's value, whatever the query did with it; Marten refuses a nullable member whatever the
  list holds. `DocumentMapping.DuplicateField` looks the value type up by the member's `MemberType` (without an entry,
  `DocumentDuplicatedFieldBinder` throws `NullReferenceException`): `ValueObjectMember` and `ValueObjectStringMember`
  report `TSelf?`, where Marten's members report `TSelf` (`GetMemberType` unwraps a nullable member) or the declared
  type (`StringValueTypeMember`), and re-raise Marten's refusal of a member it does not read under that declared type's
  name.
- `StoreOptions.CreateQueryableMember` builds a `ValueCollectionMember` for an enumerable whose element type the list
  holds, a `ChildCollectionMember` (a collection of documents) otherwise: the member source claims a collection of a
  value object (`MartenValueObject.IsHeldBy`) and builds Marten's `ValueCollectionMember` itself, as Marten would.
- Weasel's `PostgresqlProvider.Instance` mappings are process-wide, as `RegisterValueType`'s: once any store taught a
  value object, a store without the package sees it as a column type and fails at execution (Npgsql's
  `InvalidCastException`) instead of at parsing (`BadLinqExpressionException`).
- Marten maps no `ushort`, `ulong`, `Int128` or `UInt128` (`HasTypeMapping` false; `TryGetDbType` throws): a value
  object over one is declined, so Marten refuses it as it refuses the primitive.
- Marten's member closes `ValueTypeSelectClause<TSelf, TValue>` with `Activator.CreateInstance`: `CreateMember` keeps
  its constructor, or a native image throws `MissingMethodException`.
- Marten bundles `JasperFx.Events.SourceGenerator` in its `analyzers/`, which reaches this project through its direct
  reference (never a consumer: the nuspec excludes `Analyzers`) and would stamp
  `[assembly: JasperFxSourceGeneratorApplied]` into the package: `LeaveJasperFxGeneratorsOut` removes it after
  `ResolveReferences`.

## LINQ

- `ValueObjectMember` reads a date or a time through Marten's `mt_immutable_date`, `_time`, `_timestamp` and
  `_timestamptz`, as Marten's own members of those types do: PostgreSQL's cast from text to a date depends on the
  session's settings, so a computed index (`Index(x => x.Effective)`) refuses it, and a query must read the value the
  index's way for the index to serve it.
- A `DateTimeOffset` parameter goes to Npgsql in UTC (`MartenValueObject.Parameter`), as Marten's own member writes one:
  Npgsql writes no other offset to a `timestamp with time zone`. Marten's own `Contains` over `DateTimeOffset` values
  does not convert them, and fails there.
- C# compares the `.Value` of a value object over `short`, `byte`, `sbyte` or `char` as an `int`: `ValueObjectMember`
  compares that constant as it stands, as Marten compares the member of the underlying type, where Marten's
  `ValueTypeMember` would cast it to the value object (`InvalidCastException`). Over `char`, the `int` meets a `text`
  column, which PostgreSQL refuses, as for Marten's own `char` member.
- A child collection's filter places that comparison's parameter into a containment document
  (`MemberComparisonFilter.PlaceIntoContainmentFilter` calls `PlaceValueInDictionaryForContainment`), where Marten's
  `ValueTypeMember` drops a value neither `TSelf` nor `TValue`: `@> '[{}]'` matched every document, and a `DeleteWhere`
  or a `Patch` through it reached them all. `ValueObjectMember` places the `int` as it stands, a `char` as the character
  it stands for, which is what the document holds (Marten's own `char` member places the number, which matches nothing).
- `ValueObjectStringMember` takes text on the other side of a comparison (`x.Code.Value == "ABC"`, which Marten's member
  would cast to the value object) and places a containment constant as it stands: Marten hands a child collection's
  filter the text, which its member drops, and the filter then matches every document.
- Marten keeps the parser it found per `MethodInfo` (`LinqParsing.FindMethodParser`): `List<T>.Contains` is one method
  for `codes.Contains(x.Country)` and `x.Countries.Contains(code)`, so the parser claims both and hands the second back
  to the collection member, throwing Marten's own `BadLinqExpressionException` where it is none; declining one would
  cache Marten's own `EnumerableContains`, which fails at execution. A `Contains` taking a comparer is left to Marten.
- The parser unwraps for whatever member Marten resolves: an identity is Marten's `StrongTypedIdMember` and a duplicated
  field a `DuplicatedValueTypeField`; requiring the package's member broke `x.Id.IsOneOf(ids)`.
- Npgsql writes a `char[]` as text and a `byte[]` as bytea: `Contains` hands a `List<TValue>` with the database type
  Weasel gives it (`new CommandParameter(list)`), as Marten does for primitives.
- Marten's `DuplicatedValueTypeField` is not one of its value-type members: `Select(x => x.Quantity)` over a duplicated
  field takes `SelectClauseResolvers.Data(field.MemberType, …)`, which reads the column as JSON (`JsonException:
  '0x00'` while the members reported `TSelf`); with `TSelf?`, Marten refuses the result type under the JIT
  (`NotSupportedException`) and a native image has no `DataSelectClause<TSelf?>` (`MissingMethodException`), both before
  a row is read, which the native application writes as one line. Out of the package's reach; the select of a member
  that is not duplicated works.

## Compiled queries

- Marten binds no value-type parameter in a compiled query (`QueryCompiler.Finders` take none), and refuses a query type
  holding a value type `ValueTypes` does not hold (`InvalidCompiledQueryException`) before any extension of Marten runs:
  `TSelf` stays out of the list, so that Marten refuses a single value object whatever the query does with it, most
  shapes reaching no package code (its `.Value` compared with another member or handed to a string method, a child
  collection's containment or JSON path, a document collection's `Contains`). What the package's planned fragments see
  is a collection, or a value object Marten listed itself for an identity. A fragment knows it is being planned by
  `builder is CompiledQueryPlan` in `ISqlFragment.Apply` (`QueryCompiler.BuildDatabaseCommand` writes the statement into
  the plan itself; `ComparisonFilter.Apply` calls its `Right` through the interface); the parameter stays a
  `CommandParameter` so containment and JSON path filters read its `Value`.
- A collection is bound, a single value refused: the parser sees `Constant(query).Member` before Marten reduces a
  `Contains`, so `ValueObjectInFilter` knows the member; a comparison only ever gets the reduced value, and Marten tells
  parameters apart by value against the query it planned from, which no package code sees. Never bind a single value by
  value at the first bind: Marten publishes the plan before the planning call binds (a batch binds at `Execute`), so
  another call resolves the wrong member for good (scout-marten's `compiled-race.out`).
- `CompiledQueryBinding.MemberOf` reads the query type's members as `CompiledQueryPlan.findMembers` does, a member marked
  `[MartenIgnore]` left out (`IsDefined(…, inherit: true)`, JasperFx's `HasAttribute`): read, an ignored value object
  made the package refuse the `.Value` form and a constant collection beside it, which Marten accepts.
- The binding's member goes at index 0 of `CompiledQueryPlan.QueryMembers`: `MatchParameters` runs after `Apply`, offers
  each parameter to the members in order and stops at the first match, and Marten's own members match by value. It
  claims its parameter by reference (`AppendParameter` returns the `ParameterUsage`'s own `NpgsqlParameter`) and answers
  with itself as the `ICompiledQueryAwareFilter`, whose `BuildSetter` Marten stores on the usage and calls before any
  descriptor's `BindParameter`.
- Marten's runtime descriptor builds a binder for every member of `QueryMembers`, keyed by `Member.Name`, typed by
  `ToParameterType(Type)`: the member's `Member` is the query type's member the values come from, its `Type` the
  underlying type. The binder is never called (the setter wins); two package members over one member share a key, which
  the dictionary's indexer accepts.
- `CompiledQueryPlan.sortMembers` puts an array or a list member into `IncludeMembers` (it closes `IList<>`), and the
  runtime descriptor builds an include reader for it at execution, which throws for value objects: `Bind` removes the
  member it binds, before the descriptor is built (after `BuildQueryPlan` returns).
- A value object Marten takes for a document's identity joins `ValueTypes` by Marten's own doing
  (`StoreOptions.DetermineIdStrategy`, when Marten builds that document's mapping, `UseValueObjectId` or not): before,
  Marten refuses a compiled query holding one; after, it accepts it, the identity compared replays the first call's
  identity (Marten's internal `StrongTypedIdMember` writes the parameter, no package code runs), the package refuses its
  own comparisons and `IsOneOf` over single members, and the shapes no package code sees replay too. Documented with a
  warning; `MartenInternalsTests` and the integration suite pin the replay.
- `VO0048`, an analyzer of the generator (`src/AdCodicem.ValueObjects.Generators/CLAUDE.md`), reports a value-object
  member of a compiled query at build time, the identity case included, which no package code reaches. A test declaring
  one on purpose takes a targeted `#pragma warning disable VO0048`; the collection query types stay unsuppressed, so the
  unit, integration and native builds hold `VO0048` silent on every shape the package binds. Its remedy rests on
  `MartenInternalsTests`' pins of the member types Marten binds and of its refusal of an identity's `.Value`: when one
  fails, adapt the analyzer's message.
- Marten.SourceGenerator's compiled queries are not covered: a generated descriptor lists an array or a list member as
  an include at compile time. It discovers the same query types, and Marten runs `QueryCompiler.BuildQueryPlan`, its
  check included, before any descriptor it generated (offline previews identical with its handlers registered, never
  executed on PostgreSQL), so `VO0048` reports them the same way.

## Identities, serializer, reads

- Marten takes a struct for an identity when exactly one of its public properties, static ones counted, has an identity
  type (`ValueTypeIdGeneration.tryMatch`): the sample's `Iban` (its own `CountryCode`), an `[EntityId]` (`Prefix`,
  `Length`) and an integer identifier with a bound hook are none; `UseValueObjectId` changes nothing to that.
- An identity's value is built by Marten's `ValueTypeIdGeneration` through the type's own `Create`, found by reflection:
  `Select(x => x.Id)` of a refused identity throws `ValueObjectException`, not the package's `ValueObjectJsonException`.
- Without dynamic code, JasperFx's `ValueTypeInfo.CreateWrapper` calls the builder through `MethodInfo.Invoke` with no
  `DoNotWrapExceptions`: a projected value's refusal comes inside a `TargetInvocationException` (the native application
  pins it), which `ValueObjectErrors.TryGetCode` unwraps; seeding its private `_converter` would be another internal.
- `UseSystemTextJsonForSerialization` replaces the serializer: on bare `StoreOptions` `UseValueObjects()` comes after
  it; `AddMarten(…).UseValueObjects()` registers through `ConfigureMarten`, applied after `AddMarten`'s lambda (which
  `AddMarten(Action<StoreOptions>)` runs at registration) and in registration order among the others: a
  `ConfigureMarten` registered after it that replaces the serializer drops the factory (`MartenStoreOptionsTests` pins
  it; the XML docs and the guide say so).
- The namespace hides the root `Marten` namespace (root `CLAUDE.md`): name Marten's types through usings at the top of a
  file, `global::Marten.…` in a body; `JasperFx` and `Marten.Exceptions` both declare a `BadLinqExpressionException`.
