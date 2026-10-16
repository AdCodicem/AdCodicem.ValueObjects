# AdCodicem.ValueObjects.HotChocolate.Data — filtering

User-facing behaviour: `website/docs/how-to/graphql.md`. Tests: `GraphQLFilteringTests` and `GraphQLFilteringParity` of
the unit suite (EF Core on SQLite and in memory), `HotChocolateFilteringTests` of the integration suite. AOT-compatible
itself, but out of `tests/NativeAot`: HotChocolate.Data closes `FilterExpressionBuilder.CreateAndConvertParameter<T>` and
`Enumerable.Contains<T>` over the member's type at run time, which have no native code for a struct.

## Binding a value object to its operation type

- Unbound, a value-object member gets `FilterInputType<TSelf>`, the struct's public properties (`value`, `countryCode`, a
  hand-written type's `isDefault`), which compares raw text and which EF Core translates only under
  `UseValueObjectQueries()`; a binding of `TSelf` misses a `TSelf?` member, since a binding is looked up by exact type.
- Binding from the registry on the filter convention cannot work: the convention runs before any module initializer of an
  untouched domain assembly, with 0 descriptors. `ValueObjectFilterTypeInterceptor` replaces the inferred reference
  instead, in `OnBeforeRegisterDependencies` (in `OnAfterInitialize` the dependencies are registered already and the schema
  fails), and only an inference (`FilterInputType<TSelf>`, `ListFilterInputType<FilterInputType<TSelf>>`), so an
  application's binding, `[ID]` and a field typed explicitly win. It closes, once per schema build, the operation type of
  each value-object member and the list over it; `BindValueObject<TSelf, TValue>()` is the typed form asking no registry.
- Create the interceptor through a factory: natively `TryAddTypeInterceptor<T>()` finds no constructor. Where the runtime
  supports no dynamic code, a value object nothing registered is refused with a `NotSupportedException`, and
  `BindValueObject` alone does not take its place (the operands are the scalar), so the message names
  `AddValueObjectScalar` beside it.
- `IExtendedType` (`ExtendedTypeReference.Type`) lives in `HotChocolate.Internal`, which a 16.x minor may change, as the
  order of the naming tasks and `From` over a list's configuration: the scope and list tests notice.

## Names and lists

- HotChocolate writes a named convention's `<scope>_` prefix before a type's naming tasks run, and a name function would
  overwrite it, two scopes then colliding: `ValueObjectFilterNames` writes its own `OnBeforeNaming` task, `DependsOn` the
  scalar or the element, which puts the prefix back and leaves alone a name a subclass sets (it compares with the
  conventional name it captured).
- A list over one of the package's operation types is HotChocolate's own `ListFilterInputType<T>`, renamed `List…` by the
  interceptor through `FilterInputTypeDescriptor.From`. No list type of the package's own exists, since a list's element
  must be the text type under `IncludeTextOperations()`.
- HotChocolate gives every field filtering a type the one filter type registered for it, so an application's
  `FilterInputType<T>` filters every field of `T`; a `[UseProjection]` field beside a field filtered through a named
  convention over a row holding an object fails the schema (a type registered twice), with or without the package.

## Text operations (`IncludeTextOperations()`)

- Their handlers come from a `FilterConventionExtension` of the default scope, which pre-empts `AddFiltering()`'s
  `TryAddConvention` when added first, so it is added only when the option is on; a named convention keeps the comparable
  operations. A handler set on the field itself would be skipped silently by another provider's visitor.
- The text type bound by hand without the option, or in a named convention, fails the schema: HotChocolate finds no
  handler for `contains`. Over a value object not over `string` it throws `InvalidOperationException`, inside
  HotChocolate's `SchemaException` at start, with no code.
- HotChocolate's `HasValueAndAlso` wraps `HasValue` in `IsTrue`, which EF Core does not translate: the guard is
  `!= null`, the text read as `IValueObject<string>.Value` through the interface's `PropertyInfo`, which an explicit
  implementation answers and the trimmer sees.
- A `null` fragment goes to the base handler (`CanBeNull = false`), which answers HotChocolate's `HC0026`, with no code.

## Refusals and comparisons

- A refused operand is the scalar's own refusal, at the operation's `inputPath`, quoted by the scalar's name through
  `QuoteScalarNameIn(field => field is IFilterOperationField)`.
- HotChocolate.Data compares through the value object's operators (`Expression.Equal`, `GreaterThan`), which a
  hand-written value object may lack, and its `in` through `Enumerable.Contains`.
- HotChocolate.Data's `build/` targets add `global using HotChocolate.Data` under `ImplicitUsings`, for a project
  referencing it directly only: this package sets `HotChocolateImplicitUsings` to `disable`. Inside an
  `AdCodicem.ValueObjects.*` namespace, `HotChocolate.Data` names this package.
