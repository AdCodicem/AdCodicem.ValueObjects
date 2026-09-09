# Benchmarks

Measurements behind the design decisions in the root README. Run them yourself with:

```
cd AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter *
```

Every table below comes from a single run, because absolute timings move a lot between runs on this machine —
the same unchanged code measured 248 ns in one run and 152 ns in another. Read the ratios, not the nanoseconds.
Allocation figures are deterministic and can be compared across runs.

```
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.9278)
Intel Core i9-10980HK CPU 2.40GHz, 8 physical cores
.NET 10.0.12, X64 RyuJIT x86-64-v3
```

## Why a struct, even for a string

`StructWrapper` and `ClassWrapper` are the same file twice, differing in one keyword. Anything between their
rows is the type kind and nothing else.

| Operation, N = 100 000  |        Time |   Allocated | Alloc vs raw |
| ----------------------- | ----------: | ----------: | -----------: |
| Hold N raw strings      |    0.810 ms |      800 KB |         1.00 |
| Hold N struct wrappers  |    0.805 ms |  **800 KB** |     **1.00** |
| Hold N class wrappers   |    1.846 ms | **3200 KB** |     **4.00** |
| Read N struct wrappers  |    0.165 ms |           0 |            — |
| Read N class wrappers   |    0.219 ms |           0 |            — |
| Box N struct wrappers   |    0.638 ms |     2400 KB |            — |
| Box N class wrappers    |    0.231 ms |           0 |            — |

Holding a hundred thousand struct wrappers costs **exactly what holding the bare strings costs**, to the byte:
the wrapper is free. The class equivalent costs four times the memory and 2.28x the time. The 2400 KB gap is
24 bytes per instance — object header, method table pointer, field — which is what making a wrapper a reference
type costs.

Reading through the wrapper is 25% faster as a struct, with no pointer to follow.

The last two rows are where a struct gives it back. Crossing a non-generic boundary boxes it: 2.76x slower than
the class doing the same, and it allocates precisely the 24 bytes per instance the class had already paid up
front. A struct value object is therefore only worth it if the hot paths stay generic. The next table is the
check that they do.

## Value objects in collections

| Operation, 10 000 keys   |          Time | Allocated |
| ------------------------ | ------------: | --------: |
| Lookup by raw string     |      242.2 µs |     **0** |
| Lookup by value object   |      393.3 µs |     **0** |
| Lookup by struct wrapper |      336.2 µs |     **0** |
| Lookup by class wrapper  |      361.3 µs |     **0** |
| Sort raw strings         |     1879.9 µs |     80 KB |
| Sort value objects       | **1815.6 µs** |     80 KB |
| Sort class wrappers      |     2172.2 µs |     80 KB |

**Every lookup allocates nothing**, which is the result that matters: the generated equality and hashing mean
nothing boxes and nothing falls back to the reflection-based `ValueType.Equals`. That is what makes the struct
choice above safe.

Among the three wrappers, the lookup differences are not resolved by this benchmark — the spread is ~15% with
error bars to match, so treat them as equivalent. The sort numbers are cleaner and the ordering there is real:
sorting value objects beats sorting the underlying strings, because `Array.Sort` devirtualizes `IComparable<T>`
on a struct where the string overload goes through a comparer, and beats the class by 16%.

Lookups on any wrapper are ~40% slower than a `Dictionary<string, int>` built with `StringComparer.Ordinal`.
That gap is the string hashing strategy, not the wrapper: the specialized string comparer has a non-randomized
fast path a generated `GetHashCode` cannot reach.

## Cost of creating one

| Route                         |         Time |   Allocated |
| ----------------------------- | -----------: | ----------: |
| By hand, no value object      |     152.0 ns |        80 B |
| `Iban.Create`                 |     221.6 ns |    **80 B** |
| `Iban.TryCreate`              |     209.0 ns |    **80 B** |
| `Iban.TryParse(span)`         |     203.9 ns |    **80 B** |
| `Descriptor.TryParse` (boxed) |     220.6 ns |       104 B |
| `TypeDescriptor.ConvertFrom`  |     207.9 ns |       104 B |
| `CreateUnchecked` (EF read)   | **0.007 ns** |     **0 B** |
| Closed set, boxed (shared)    |  **18.6 ns** |     **0 B** |
| Open set, boxed (allocates)   |     276.3 ns |       184 B |

Four things worth stating plainly:

**The boxed descriptor does not beat a cached `TypeConverter`.** 220.6 ns against 207.9 ns, same allocation.
The descriptor route is what model binding, FluentValidation and Dapper take, since they only know a `Type` at
run time, and it is no faster than the reflection-flavoured approach it replaces. Its case is trimming and AOT
friendliness, not raw speed. The framework's speed is in the typed route and in `CreateUnchecked`.

**The 1.46x on `Create` is not pure overhead.** The by-hand row skips the declared `Pattern`, which the
generated `Validate` runs. `Pattern` compiles a `Regex` with `RegexOptions.Compiled` rather than using
`[GeneratedRegex]`, because the regex source generator cannot see our generated code — source generators never
observe each other's output. Declare no `Pattern` and validate in the hook if the last nanoseconds matter.

**Parsing allocates once**, thanks to the span overload of `NormalizeCore`: text is normalized straight from
the span rather than materialized and thrown away first. Without that overload, `TryParse(span)` allocates 168 B
instead of 80 B.

**A closed value set is boxed once.** Members of a `ValueSet = Closed` value object over a reference type are
pre-boxed at registration and shared, so a boxed conversion allocates nothing at all. The open-set row is the
control: it allocates a fresh box every time, as it must.

`CreateUnchecked` at 0.007 ns and no allocation is what makes the EF Core read path free — materializing a row
does not re-validate values this same application already validated on the way in.

## JSON

| Operation                                   |     Time |   Allocated |
| ------------------------------------------- | -------: | ----------: |
| Serialize raw primitives                    | 244.1 ns |       224 B |
| Serialize value objects                     | 266.9 ns |       280 B |
| Serialize value objects, source generated   | 259.8 ns |       280 B |
| Deserialize raw primitives                  | 400.5 ns |   **136 B** |
| Deserialize value objects                   | 676.9 ns |   **136 B** |
| Deserialize value objects, source generated | 673.6 ns |   **136 B** |

**Deserializing a payload typed with value objects allocates exactly what deserializing the same payload of
primitives allocates**, byte for byte, because the JSON reader copies the text into a stack buffer and
normalizes from it. Before the span path it allocated 216 B against the same 136 B.

Serializing costs 9% over primitives and produces byte-identical JSON — the benchmark asserts that in its
setup, so the comparison stays honest.

Deserializing costs 1.69x the primitive version in time, and that is the validation, not the wrapper: every
field is normalized and checked on the way in, including the IBAN's `Pattern` and its MOD-97 check digits.
Buying guaranteed-valid values at the boundary for ~275 ns is the trade the whole library exists to make.

## What the optimization pass changed

Allocations are deterministic, so these are directly comparable:

| Path                          |  Before |   After |
| ----------------------------- | ------: | ------: |
| `Iban.TryParse(span)`         |   168 B |    80 B |
| `Descriptor.TryParse` (boxed) |   192 B |   104 B |
| Deserialize value objects     |   216 B |   136 B |
| Closed set, boxed             |    24 B |     0 B |

Two changes produced all of it: an optional `NormalizeCore(ReadOnlySpan<char>)` overload the generator routes
parsing and JSON reading through, and pre-boxed shared instances for closed value sets over reference types.
Neither changes behaviour, and both are opt-in by construction — a value object that declares no span overload
and no closed set generates exactly what it generated before.
