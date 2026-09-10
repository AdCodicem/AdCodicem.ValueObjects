---
title: Entity Identifiers
sidebar_label: Entity Identifiers
slug: /entity-identifiers
---

# Entity identifiers

Stripe-style public identifiers — `acc_1kcv3ahrz6dmv29gqy5cv` — as value objects, from
`AdCodicem.ValueObjects.Identifiers`.

This page records the design and the reasoning behind it.

## What the type is for

An entity identifier is the identity of an aggregate as the outside world sees it: it appears in URLs, in JSON
payloads, in webhook bodies, in support tickets and in the database. It is *not* a secret, and nothing about it
may be relied upon for authorization.

The prefix is the load-bearing part of the format. `cus_…` cannot be parsed as an `AccountId`, so substituting
one identifier for another in a request parameter fails at the boundary rather than reaching a repository. That
is the reason the prefix is stored in the database rather than reconstructed at read time: a raw-SQL join
between two tables holding bare bodies would succeed silently, whereas prefixed values cannot be confused even
by a query the application never sees.

## Format

```
acc_ TTTT RRRRRRRRRRRRRRRR C
│    │    │                │
│    │    │                └─ 1 check character
│    │    └─ 16 characters = 80 bits from a CSPRNG
│    └─ time bucket, width derived from the granularity
└─ prefix, one or more lowercase segments
```

The alphabet is Crockford Base32 in lower case — `0123456789abcdefghjkmnpqrstvwxyz` — chosen over Base62 for
three reasons:

1. **It is order-preserving under ordinal comparison.** The alphabet is strictly increasing in ASCII, so
   comparing two identifiers as text reproduces the numeric order of their encoded values. The time bucket sits
   at the head of the body, which makes the B-tree ordering of the column chronological with no extra work and
   with `StringComparison.Ordinal`, already this library's default.
2. **It removes the collation trap.** Normalization folds to lower case and maps Crockford's aliases (`i`, `l`
   → `1`; `o` → `0`), so the stored value is canonical. A case-insensitive column collation can no longer
   collapse two distinct identifiers. `Latin1_General_BIN2` / `COLLATE "C"` remains preferable for speed, but is
   no longer a correctness requirement.
3. **It survives being read aloud.** No `l`/`i`/`o`/`0` confusion in a support ticket.

Lower case is the canonical spelling, so an identifier is one unbroken token from prefix to check character —
`acc_1kcv3ahrz6dmv29gqy5cv`, not a lowercase prefix bolted onto a shouting body. Upper case still parses and
folds down, so nothing that quotes an identifier back in the wrong case is turned away.

**Crockford's optional hyphen is not accepted.** The specification allows one anywhere in an encoded value for
readability, and grouping helps someone reading digits off a page. These identifiers are never transcribed by
hand, so the grouped spelling is one nobody produces — and accepting it would mean two texts mapping onto one
identifier, in a format whose whole discipline is that there is exactly one. `Normalize` therefore drops
nothing: a hyphenated candidate keeps its length and is refused as `invalid_length`.

A mixed-case alphabet would shorten an identifier by two characters. Why that is not worth taking is set out
in [Rejected: a mixed-case alphabet](#rejected-a-mixed-case-alphabet).

### Widths

The epoch is **2020-01-01T00:00:00Z**. Bucket width follows the granularity, sized to keep roughly a century of
horizon:

| Granularity | Bucket chars | Bits | Horizon | Body | Example total for `acc_` |
| --- | --- | --- | --- | --- | --- |
| `Minute` | 6 | 30 | year 4062 | 23 | `char(27)` |
| `Hour` (default) | 4 | 20 | year 2139 | 21 | `char(25)` |
| `Day` | 3 | 15 | year 2109 | 20 | `char(24)` |

The random part is 80 bits at every granularity, giving a birthday bound of ≈ 1.1 × 10¹² identifiers *per
bucket*.

**Per bucket is the number that matters, and it is why this is 80 bits rather than 128.** Randomness is
redrawn on every bucket, so what has to stay out of reach is the mint rate within one bucket width — and the
bucket is deliberately sized at 10⁴–10⁵ rows, seven to eight orders of magnitude below the bound. One without
a time bucket would have to budget against the lifetime row count of the table and would need its 128 bits;
this one does not, and spending them would buy nothing but five characters on every row and every foreign key.

Length is fixed per type, so the column is `char(n)`, not `varchar(n)`.

### The check character

```
check = ( seed(prefix) + Σᵢ wᵢ · vᵢ ) mod 32,   wᵢ = 2·(i mod 16) + 1
```

where `vᵢ` is the Crockford value of the i-th data character of the body (time bucket and random part, check
character excluded) and `seed(prefix)` is a folded FNV-1a hash of the prefix reduced to five bits.

The guarantee, stated exactly because a checksum that promises more than it delivers is worse than none:

- **every single-character substitution in the body is detected.** The weights are odd, hence invertible
  modulo 32, and a non-zero difference of Crockford values can never be ≡ 0 (mod 32);
- **every adjacent transposition is detected** unless the two characters' values differ by exactly 16. The
  weight difference between adjacent positions is ≡ 2 (mod 32) everywhere, wrap included;
- **changing the prefix changes the expected check character** for 31 prefixes out of 32, so a body copied
  between two identifier types is caught even by a validator that does not know which prefix to expect — which
  is what `AnyEntityId` needs before it has resolved anything;
- random corruption slips through with probability 1/32. That is the information-theoretic limit of one check
  character over 32 symbols, and no scheme does better.

## Why a monotonic prefix, and what it costs

A purely random primary key inserts into the middle of a B-tree. On SQL Server the primary key is clustered by
default, so the table itself fragments; on PostgreSQL the heap is unordered but the index still touches random
leaf pages on every insert, so the dirty working set is the whole index rather than its tail.

Putting a coarse time bucket at the head of the body clusters insertions at the right edge of the tree. The
security cost is bounded and explicit:

- **enumeration is unaffected.** The random part keeps its full 80 bits. An attacker who knows the exact
  creation instant still faces 2⁸⁰. Conflating "partially time-derived" with "partially guessable" is the
  usual mistake; it does not apply here;
- **the leak is temporal only**, at exactly the granularity chosen. An hourly bucket reveals the hour and
  nothing finer. Identifiers within one bucket are unordered relative to each other, so holding a handful of
  them does not yield a creation rate.

**Choose the granularity from the insert rate, not from taste.** Aim for a bucket holding roughly 10⁴–10⁵ rows:
at ~200 keys per leaf page that is a few hundred pages, which stays cached. Too wide a bucket and writes
scatter again; too narrow and the identifier leaks more precisely than it needs to.

Two complementary knobs, neither of which this library sets for you:

- on SQL Server, `PRIMARY KEY NONCLUSTERED` confines fragmentation to the ~25-byte index rather than the whole
  row;
- with a monotonic head, insertions are effectively append-only, so `FILLFACTOR` can go back up towards 95–100.

## Rejected: a mixed-case alphabet

Base58 or Base62 would encode the same 80 bits in 14 characters instead of 16, taking `acc_` from 25 down to
23. Base64url encodes no shorter than either at these widths and would collide with both separators — `_`
opens the body, `-` is Crockford's readability separator — so it was never in the running.

Two characters do not pay for what they cost:

- **the check character loses its proof.** The weights `2·(i mod 16) + 1` run 1, 3, …, 31. Modulo 58 the
  weight 29 is not invertible, so a substitution differing by two at that position slips through undetected;
  modulo 62 the weight 31 fails the same way. Each alphabet would need its own weight sequence and its own
  restated transposition bound;
- **the encoder loses its uniformity for free.** 32 divides 256, so the low five bits of a uniform byte are a
  uniform symbol. Neither 58 nor 62 divides anything convenient, which forces either rejection sampling — an
  unbounded draw against a fixed `stackalloc` — or `UInt128` division;
- **the collation stops being a performance choice.** Everything above rests on normalization folding an
  identifier to a single canonical spelling. A case-sensitive alphabet cannot fold, so a case-insensitive
  column collation — the default on SQL Server — can once again return the wrong row, and nothing enforces the
  binary collation for raw SQL, a second application, or a database restored elsewhere.

It was considered as a per-type option too, and rejected on a sharper line. `Granularity` trades index
locality against temporal leakage and every setting of it is correct. An alphabet switch is not that kind of
knob: it would make correctness depend on an ambient database setting the library cannot verify, chosen per
type, and baked into every persisted row.

## Rejected: the two-key pattern

An internal sequential surrogate key alongside the public identifier was considered and dropped. It is only
worth its cost when foreign keys carry the internal key — and then every serialization of a referencing entity
needs a join to materialize the public identifier, trading write locality for read amplification, while adding
an enumerable value that must never leak. With foreign keys carrying the public identifier, the internal key
would fund nothing but the physical ordering of its own table, which the time bucket already delivers for free.

One identity, visible everywhere.

## Authoring surface

```csharp
[EntityId("acc")]
public readonly partial struct AccountId;

[EntityId("evt", Granularity = IdGranularity.Minute)]
public readonly partial struct EventId;
```

Everything `[ValueObject<string>]` generates is generated here too — construction, parsing, formatting,
equality, ordering, the JSON converter, the `TypeConverter`, the registry entry — plus:

| Member | Purpose |
| --- | --- |
| `AccountId.New()` | A fresh identifier from the ambient clock and entropy source. |
| `AccountId.New(TimeProvider, IdEntropySource)` | The same, with both sources supplied explicitly. |
| `AccountId.Prefix` | The declared prefix. |
| `AccountId.Granularity` | The declared granularity. |

`MinLength`, `MaxLength` and the OpenAPI `pattern` are **derived** from the profile and flow into the EF Core
column and the OpenAPI schema exactly as they do for any other value object — the rule is still declared once.
The pattern is published as schema text but never compiled: at fixed length over a fixed alphabet, validation
is a span scan, so an entity identifier costs no `Regex` at start-up, unlike a `Pattern`-constrained value
object.

The generated `Normalize` trims surrounding whitespace, folds the body to lower case, applies the alias
mapping, and canonicalizes the prefix's case. It drops nothing, so it preserves length. It never rejects
either: a text without the expected prefix comes back unchanged and is refused by `Validate`.

### Error codes

Rejections carry a code precise enough to act on, rather than a generic `not_parsable`:

| Code | Meaning |
| --- | --- |
| `value_object.id.invalid_length` | Wrong total length for the declared profile. |
| `value_object.id.invalid_prefix` | The text does not carry the declared prefix. |
| `value_object.id.invalid_character` | A character outside the Crockford alphabet. |
| `value_object.id.invalid_checksum` | The check character does not match. |

## The ambient provider

`New()` reads a `TimeProvider` and an `IdEntropySource`. Both resolve through `ValueObjectIds`, which layers an
`AsyncLocal` scope over a process-wide default:

```csharp
ValueObjectIds.Configure(timeProvider, entropy);          // once, at start-up

using (ValueObjectIds.Use(fakeClock, deterministicBytes)) // scoped, wins over the default
{
    var id = AccountId.New();
}
```

The scope exists because the test suites run in parallel. A settable static alone would let two tests racing to
substitute the clock corrupt each other, and the symptom would be an intermittent failure — the most expensive
kind to diagnose. The scope is bounded by the execution flow, so parallel tests do not interfere and no test
collection has to be serialized.

`IdEntropySource.System` wraps `RandomNumberGenerator`. It is a CSPRNG, deliberately: `Random.Shared` would
make identifiers predictable from a handful of samples.

## Polymorphic references

`AnyEntityId` parses any registered prefix and reports which type it belongs to. It serves webhooks, deep
links, audit logs and heterogeneous references.

It deliberately does **not** implement `IValueObject`, which is what makes it non-persistable by construction:
the EF Core convention keys off that interface, so `AnyEntityId` is invisible to it and no one can accidentally
map a polymorphic column. It is a transport and resolution type, nothing more.

Its OpenAPI schema is a plain `string` with a description. A `oneOf` over every registered pattern would be
faithful and unreadable, growing with every identifier type in the application.

## Packages

| Package | Contents |
| --- | --- |
| `AdCodicem.ValueObjects` | The existing generator gains the `[EntityId]` emission path. |
| `AdCodicem.ValueObjects.Identifiers` | `[EntityId]`, the Crockford codec, the check character, the layout, the ambient provider, the prefix registry, `AnyEntityId`. |
| `AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore` | Fixed-width column, binary collation, index guidance. |
| `AdCodicem.ValueObjects.Secrets` | Bearer secrets. Specified below, not yet implemented. |

**`[EntityId]` lives in `.Identifiers`, not in `Abstractions`.** If the attribute shipped with the core package
while its runtime did not, a consumer could annotate a type and receive a compile error inside generated code
they cannot edit. Placing the attribute in the package that carries its runtime makes that state unreachable:
without the package, the attribute does not exist.

## Diagnostics

| Id | Severity | Meaning |
| --- | --- | --- |
| `VO0015` | Error | Malformed prefix: empty, wrong characters, or an over-long segment. |
| `VO0016` | Error | Two types in the compilation declare the same prefix. |
| `VO0017` | Error | An option that `[EntityId]` derives or forbids was set by hand. |
| `VO0018` | Error | Both `[EntityId]` and `[ValueObject<T>]` on one type. |

Cross-assembly prefix collisions are beyond a generator's reach and surface at start-up, when the second
registration for a prefix is refused.

## Bearer secrets — specified, not implemented

`sk_live_…` style secrets are a sibling of entity identifiers, not a mode of them, because they conflict with
`IValueObject<TSelf, TValue>` on four points:

- the interface requires `ISpanFormattable`, and a secret whose `ToString()` is masked cannot round-trip, so
  the implementation would be a lie;
- the generated JSON converter writes `Value`, which would leak every secret that reaches a response body;
- `IComparable` reopens a non-constant-time comparison, exactly what the secret mode exists to close;
- `ValueObjectContract` asserts text and JSON round-tripping, and would have to be holed for every other type
  to accommodate secrets.

They will therefore get their own `ISecretValueObject<TSelf>`, which implements none of those, and their own
contract kit. Storage is `SHA-256` of the token with a unique index, and lookup is by hash. A slow KDF is
**not** appropriate here: Argon2 and bcrypt exist to make low-entropy passwords expensive to guess, and a
128-bit random token is already beyond guessing — a slow hash would only add latency to every authenticated
request. A displayable fragment (the last four characters) is stored alongside for the UI.

Issuance, rotation, revocation and expiry stay with the consumer, symmetrically with the decision not to own
the entity model for identifiers.

Next: [Testing](./testing.md).
