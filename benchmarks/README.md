# Benchmarks

Measurements behind the design decisions in the root README. Run them yourself with:

```
cd AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter *
```

All figures below come from one machine and are meant to be read as ratios, not as absolutes:

```
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.9278)
Intel Core i9-10980HK CPU 2.40GHz, 8 physical cores
.NET 10.0.12, X64 RyuJIT x86-64-v3
```

## Why a struct, even for a string

`StructWrapper` and `ClassWrapper` are the same file twice, differing in one keyword. Anything between their
rows is the type kind and nothing else.

| Operation, N = 100 000  | Time       | Allocated   | vs raw |
| ----------------------- | ---------: | ----------: | -----: |
| Hold N raw strings      |   1.045 ms |      800 KB |   1.00 |
| Hold N struct wrappers  |   0.990 ms |  **800 KB** | **1.00** |
| Hold N class wrappers   |   2.263 ms |  **3200 KB** | **4.00** |
| Read N struct wrappers  |   0.210 ms |           0 |      — |
| Read N class wrappers   |   0.251 ms |           0 |      — |
| Box N struct wrappers   |   0.835 ms |     2400 KB |      — |
| Box N class wrappers    |   0.293 ms |           0 |      — |

Holding a hundred thousand struct wrappers costs **exactly what holding the bare strings costs**: the wrapper
is free, to the byte. The class equivalent costs four times the memory and a bit over twice the time. The
2400 KB difference is 24 bytes per instance — an object header, a method table pointer, and the field — which
is the price of making a wrapper a reference type.

Reading through the wrapper is 16% faster as a struct, because there is no pointer to follow.

The last two rows are where a struct gives it all back. Crossing a non-generic boundary boxes it: 2.85 times
slower, and it allocates precisely the 24 bytes per instance that the class had already paid up front. A struct
value object is therefore only worth it if the hot paths stay generic. The collection numbers below are the
check that they do.

## Value objects in collections

| Operation, 10 000 keys | Time      | Allocated |
| ---------------------- | --------: | --------: |
| Lookup by raw string   |  271.6 µs |         0 |
| Lookup by value object |  392.8 µs |     **0** |
| Lookup by struct wrapper | 385.7 µs |       0 |
| Lookup by class wrapper |  410.1 µs |         0 |
| Sort raw strings       | 2380.5 µs |     80 KB |
| Sort value objects     | **2153.7 µs** |  80 KB |
| Sort class wrappers    | 2458.4 µs |     80 KB |

Zero allocation on every lookup, which is the point: equality and hashing are generated, so nothing boxes and
nothing falls back to the reflection based `ValueType.Equals`. Sorting value objects is faster than sorting the
underlying strings, because `Array.Sort` devirtualizes `IComparable<T>` on a struct where the string overload
goes through a comparer.

Lookups are ~45% slower than a `Dictionary<string, int>` built with `StringComparer.Ordinal`. That gap is the
string hashing strategy, not the wrapper: the specialized string comparer has a non-randomized fast path that a
generated `GetHashCode` cannot reach.

## Cost of creating one

| Route                          | Time       | Allocated |
| ------------------------------ | ---------: | --------: |
| By hand, no value object       |   248.0 ns |      80 B |
| `Iban.Create`                  |   318.9 ns |      80 B |
| `Iban.TryCreate`               |   323.5 ns |      80 B |
| `Iban.TryParse(span)`          |   319.1 ns |     168 B |
| `Descriptor.TryParse` (boxed)  |   346.4 ns |     192 B |
| `TypeDescriptor.ConvertFrom`   |   328.7 ns |     192 B |
| `CreateUnchecked` (EF read)    | **0.07 ns** | **0 B** |

Three things worth stating plainly:

**The boxed descriptor does not beat a cached `TypeConverter`.** 346 ns against 329 ns. The descriptor route is
what model binding, FluentValidation and Dapper take, since they only know a `Type` at run time, and it is no
faster than the reflection-flavoured approach it replaces. Its advantages are trimming and AOT friendliness and
not needing a converter lookup, not raw speed. The framework's speed comes from the typed route and from
`CreateUnchecked`.

**The 1.29x on `Create` is not pure overhead.** The by-hand row skips the declared `Pattern`, which the
generated `Validate` runs. `Pattern` compiles a `Regex` with `RegexOptions.Compiled` rather than using
`[GeneratedRegex]`, because the regex source generator cannot see our generated code — source generators never
observe each other's output. Declare no `Pattern` and validate in the hook if the last nanoseconds matter.

**`TryParse(span)` allocates twice.** It materializes the span into a string, then the normalize hook allocates
the normalized one. This only affects value objects that declare a normalize hook: without one, `Normalize` is
emitted as the identity and the second allocation disappears. Removing it would mean a hook overload taking a
`ReadOnlySpan<char>`.

`CreateUnchecked` at 0.07 ns and no allocation is what makes the EF Core read path free — materializing a row
does not re-validate values this same application already validated on the way in.

## JSON

| Operation                                   | Time     | Allocated |
| ------------------------------------------- | -------: | --------: |
| Serialize raw primitives                    | 309.0 ns |     224 B |
| Serialize value objects                     | 327.3 ns |     280 B |
| Serialize value objects, source generated   | 336.1 ns |     280 B |
| Deserialize raw primitives                  | 493.7 ns |     136 B |
| Deserialize value objects                   | 906.8 ns |     216 B |
| Deserialize value objects, source generated | 878.6 ns |     216 B |

Serializing a payload typed with value objects costs 6% over the same payload typed with primitives, and
produces byte-identical JSON — the benchmark asserts that in its setup, so the comparison stays honest.

Deserializing costs 1.84x, and that is the validation, not the wrapper: every field is normalized and checked
on the way in, including the IBAN's `Pattern` and its MOD-97 check digits. Buying guaranteed-valid values at the
boundary for ~400 ns is the trade the whole library exists to make.
