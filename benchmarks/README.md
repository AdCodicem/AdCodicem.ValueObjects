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

Among the three wrappers, the lookup differences are small and do not keep their order from one run to the next:
the spread is 17% in this table, with the value object slowest, and was 4–6% when the suite was rerun on the same
CPU under BenchmarkDotNet 0.15.8 (#63), with the class wrapper slowest. Treat them as equivalent. The sort numbers
are cleaner and the ordering there is real: sorting value objects beats sorting the underlying strings, because
`Array.Sort` devirtualizes `IComparable<T>` on a struct where the string overload goes through a comparer, and
beats the class by 16%.

Lookups on any wrapper are roughly 40–65% slower than a `Dictionary<string, int>` built with
`StringComparer.Ordinal`. That gap is the string hashing strategy, not the wrapper: the specialized string
comparer has a non-randomized fast path a generated `GetHashCode` cannot reach.

## Cost of creating one

Measured again when the IBAN moved from the `Pattern` option to the pattern hook, on the same CPU:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9550)
Intel Core i9-10980HK CPU 2.40GHz, 8 physical cores
.NET 10.0.12, X64 RyuJIT x86-64-v3
```

| Route                         |         Time |   Allocated |
| ----------------------------- | -----------: | ----------: |
| By hand, no value object      |     134.6 ns |        80 B |
| `Iban.Create`                 |     192.1 ns |    **80 B** |
| `Iban.TryCreate`              |     193.6 ns |    **80 B** |
| `Iban.TryParse(span)`         |     185.5 ns |    **80 B** |
| `Descriptor.TryParse` (boxed) |     201.7 ns |       104 B |
| `TypeDescriptor.ConvertFrom`  |     196.8 ns |       104 B |
| `CreateUnchecked` (EF read)   | **0.006 ns** |     **0 B** |
| Closed set, boxed (shared)    |  **15.8 ns** |     **0 B** |
| Open set, boxed (allocates)   |     259.1 ns |       184 B |

Four things worth stating plainly:

**The boxed descriptor does not beat a cached `TypeConverter`.** 201.7 ns against 196.8 ns, same allocation.
The descriptor route is what model binding, FluentValidation and Dapper take, since they only know a `Type` at
run time, and it is no faster than the reflection-flavoured approach it replaces. Its case is trimming and AOT
friendliness, not raw speed. The framework's speed is in the typed route and in `CreateUnchecked`.

**The 1.43x on `Create` is not pure overhead.** The by-hand row skips the shape check, which the generated
`Validate` runs through the IBAN's `[GeneratedRegex]`. [Checking a pattern](#checking-a-pattern) measures what
that costs, against the deprecated `Pattern` option and against the same shape checked by hand.

**Parsing allocates once**, thanks to the span overload of `NormalizeValue`: text is normalized straight from
the span rather than materialized and thrown away first. Without that overload, `TryParse(span)` allocates 168 B
instead of 80 B.

**A closed value set is boxed once.** Members of a `ValueSet = Closed` value object over a reference type are
pre-boxed at registration and shared, so a boxed conversion allocates nothing at all. The open-set row is the
control: it allocates a fresh box every time, as it must.

`CreateUnchecked` at 0.006 ns and no allocation is what makes the EF Core read path free — materializing a row
does not re-validate values this same application already validated on the way in.

## JSON

Measured in the same run as the table above.

| Operation                                   |     Time |   Allocated |
| ------------------------------------------- | -------: | ----------: |
| Serialize raw primitives                    | 236.4 ns |       224 B |
| Serialize value objects                     | 234.1 ns |       280 B |
| Serialize value objects, source generated   | 231.8 ns |       280 B |
| Deserialize raw primitives                  | 339.2 ns |   **136 B** |
| Deserialize value objects                   | 576.6 ns |   **136 B** |
| Deserialize value objects, source generated | 575.9 ns |   **136 B** |

**Deserializing a payload typed with value objects allocates exactly what deserializing the same payload of
primitives allocates**, byte for byte, because the JSON reader copies the text into a stack buffer and
normalizes from it. Before the span path it allocated 216 B against the same 136 B.

Serializing costs no time this run can tell apart from the primitives' and produces byte-identical JSON — the
benchmark asserts that in its setup, so the comparison stays honest. It allocates 56 B more.

Deserializing costs 1.70x the primitive version in time, and that is the validation, not the wrapper: every
field is normalized and checked on the way in, including the IBAN's pattern and its MOD-97 check digits.
Buying guaranteed-valid values at the boundary for ~240 ns is the trade the whole library exists to make.

## Checking a pattern

`PatternBenchmarks` checks the shape of an IBAN and of a five-digit postal code four ways: through the deprecated
`Pattern` option, which compiles a `Regex` at run time; through the pattern hook, whose `[GeneratedRegex]` is
compiled with the type; by hand, in the validator; and not at all, the floor. The setup asserts that every
variant accepts and rejects the same input. Each table comes from one run, under the JIT, then under native AOT:

```
dotnet run -c Release -- --filter '*PatternBenchmarks*' --runtimes nativeaot10.0
```

On Windows, native AOT links with `link.exe`, which cannot open a path longer than 260 characters, and the project
BenchmarkDotNet generates sits deep under the artifacts folder. Point the build at a short one,
`dotnet run -c Release -p:ArtifactsPath=C:\temp\vo -- ...`, run from this directory so that BenchmarkDotNet still
finds the project.

| `TryCreate`, JIT              | `Pattern` option | Pattern hook |    By hand | No shape check |
| ----------------------------- | ---------------: | -----------: | ---------: | -------------: |
| IBAN                          |         193.5 ns |     184.7 ns |   162.9 ns |       132.8 ns |
| Postal code                   |          30.1 ns |      23.1 ns | **3.6 ns** |         0.0 ns |
| IBAN shape alone, `IsMatch`   |          53.0 ns |      41.3 ns |    25.0 ns |              — |

| `TryCreate`, native AOT       | `Pattern` option | Pattern hook |    By hand | No shape check |
| ----------------------------- | ---------------: | -----------: | ---------: | -------------: |
| IBAN                          |         290.5 ns | **213.3 ns** |   181.1 ns |       145.2 ns |
| Postal code                   |          72.4 ns |  **37.2 ns** | **3.2 ns** |         0.0 ns |
| IBAN shape alone, `IsMatch`   |         123.6 ns |  **62.0 ns** |    31.2 ns |              — |

**Under native AOT the option's regular expression is interpreted**, and the hook halves what it costs: the
shape alone drops from 124 to 62 ns, and creating an IBAN from 290 to 213 ns. Under the JIT, which does compile
the option's `Regex`, the hook saves 7 to 9 ns. No variant allocates anything the others do not.

**A shape checked by hand is cheaper than any regular expression**: 1.6 to 2 times as fast as the hook's for the
IBAN shape, and 6 to 12 times for the postal code, where the regular expression is the whole cost. For a value
object whose pattern is on the hot path, validating in `IValueObjectValidator<T>` remains the faster choice; the
pattern hook is the one that also publishes the rule to the OpenAPI schema.

A warmed-up run never sees what building the regular expression costs, so `first-call.ps1` times the first
`TryCreate` in a fresh process instead, publishing `AdCodicem.ValueObjects.Benchmarks.FirstCall` for each mode and
taking the median of 41 processes per variant:

| First `TryCreate` | `Pattern` option | Pattern hook | By hand |
| ----------------- | ---------------: | -----------: | ------: |
| IBAN, JIT         |          2.67 ms |      2.36 ms | 1.78 ms |
| Postal code, JIT  |          0.95 ms |      0.80 ms | 0.34 ms |
| IBAN, AOT         |          29.7 µs |      22.9 µs |  0.6 µs |
| Postal code, AOT  |          30.8 µs |      24.5 µs |  2.1 µs |

Under the JIT most of that first call is the regular expression machinery itself, which the hook pays too; the
option adds 0.15 to 0.3 ms of compilation on top. Under native AOT both are tens of microseconds, once per type.

## What the optimization pass changed

Allocations are deterministic, so these are directly comparable:

| Path                          |  Before |   After |
| ----------------------------- | ------: | ------: |
| `Iban.TryParse(span)`         |   168 B |    80 B |
| `Descriptor.TryParse` (boxed) |   192 B |   104 B |
| Deserialize value objects     |   216 B |   136 B |
| Closed set, boxed             |    24 B |     0 B |

Two changes produced all of it: an optional `NormalizeValue(ReadOnlySpan<char>)` overload the generator routes
parsing and JSON reading through, and pre-boxed shared instances for closed value sets over reference types.
Neither changes behaviour, and both are opt-in by construction — a value object that declares no span overload
and no closed set generates exactly what it generated before.
