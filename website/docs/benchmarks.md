---
title: Benchmarks
sidebar_label: Benchmarks
slug: /benchmarks
---

# Benchmarks

The measurements behind the design decisions on the previous page. Absolute timings move a lot between runs on
any one machine — the same unchanged code measured 248 ns in one run and 152 ns in another — so read the
ratios, not the nanoseconds. Allocation figures are deterministic and comparable across runs.

The full tables, including JSON round-tripping and the cost of each creation route, live in
[`benchmarks/README.md`](https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/benchmarks/README.md)
in the repository, alongside the exact hardware and BenchmarkDotNet version each run used. Run them yourself
with:

```bash
cd benchmarks/AdCodicem.ValueObjects.Benchmarks
dotnet run -c Release -- --filter '*WrapperCost*'  # just the struct-vs-class comparison
dotnet run -c Release -- --filter '*'               # everything
```

They want a quiet machine — background load is the biggest source of the run-to-run variance above.

## Why a struct, even for a string

`StructWrapper` and `ClassWrapper` are the same file twice, differing in one keyword. Anything between their
rows below is the type kind and nothing else.

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
the wrapper is free. The class equivalent costs four times the memory and 2.28x the time — 24 bytes per
instance for the object header, method table pointer and field, which is what making a wrapper a reference type
costs.

The last two rows are where a struct gives it back. Crossing a non-generic boundary boxes it: 2.76x slower than
the class doing the same, and it allocates precisely the 24 bytes per instance the class had already paid up
front. A struct value object is therefore only worth it if the hot paths stay generic — which the next table is
the check for.

## Value objects in collections

| Operation, 10 000 keys   |          Time | Allocated |
| ------------------------ | ------------: | --------: |
| Lookup by raw string     |      242.2 µs |     **0** |
| Lookup by value object   |      393.3 µs |     **0** |
| Sort raw strings         |     1879.9 µs |     80 KB |
| Sort value objects       | **1815.6 µs** |     80 KB |

**Every lookup allocates nothing**, which is the result that matters: the generated equality and hashing mean
nothing boxes and nothing falls back to reflection-based equality. That is what makes the struct choice above
safe. Sorting value objects even beats sorting the underlying strings, because `Array.Sort` devirtualizes
`IComparable<T>` on a struct where the string overload goes through a comparer.

## What buying validation at the boundary costs

Deserializing a payload typed with value objects allocates exactly what deserializing the same payload of
primitives allocates, byte for byte — the JSON reader copies text into a stack buffer and normalizes from it
directly. It costs about 1.7x the primitive version in *time*, and that is the validation, not the wrapper:
every field is normalized and checked on the way in. Buying guaranteed-valid values at the boundary for a few
hundred nanoseconds is the trade the whole library exists to make.
