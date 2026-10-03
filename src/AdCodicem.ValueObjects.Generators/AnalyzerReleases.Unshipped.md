; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------------------|----------|-------------------------------------------------
VO0001 | AdCodicem.ValueObjects | Error | Value object must be partial
VO0002 | AdCodicem.ValueObjects | Error | Value object must be a readonly struct
VO0003 | AdCodicem.ValueObjects | Error | Unsupported underlying type
VO0004 | AdCodicem.ValueObjects | Error | Invalid bound
VO0005 | AdCodicem.ValueObjects | Error | Closed value set declares no value
VO0006 | AdCodicem.ValueObjects | Error | Invalid known value name
VO0007 | AdCodicem.ValueObjects | Error | Arithmetic requires a numeric underlying type
VO0008 | AdCodicem.ValueObjects | Warning | Length constraints only apply to strings
VO0009 | AdCodicem.ValueObjects | Error | Containing type must be partial
VO0010 | AdCodicem.ValueObjects | Error | Uninitialized value object
VO0011 | AdCodicem.ValueObjects | Warning | Value object hook is not declared
VO0013 | AdCodicem.ValueObjects | Error | Invalid known value
VO0014 | AdCodicem.ValueObjects | Error | Invalid pattern
VO0015 | AdCodicem.ValueObjects | Error | Invalid entity identifier prefix
VO0016 | AdCodicem.ValueObjects | Error | Duplicate entity identifier prefix
VO0017 | AdCodicem.ValueObjects | Error | Entity identifier owns its normalization
VO0018 | AdCodicem.ValueObjects | Error | Conflicting value object annotations
VO0019 | AdCodicem.ValueObjects | Error | Unsupported value object declaration
VO0020 | AdCodicem.ValueObjects | Error | Option set to an undefined enum value
VO0022 | AdCodicem.ValueObjects | Error | Pattern declared twice
VO0023 | AdCodicem.ValueObjects | Error | A pattern only applies to strings
VO0024 | AdCodicem.ValueObjects | Error | Entity identifier owns its format
VO0025 | AdCodicem.ValueObjects | Warning | Pattern options are not published
VO0026 | AdCodicem.ValueObjects | Warning | Pattern has no match timeout
VO0027 | AdCodicem.ValueObjects | Error | Entity identifier takes no known value
VO0029 | AdCodicem.ValueObjects | Error | Bound declared twice
VO0030 | AdCodicem.ValueObjects | Error | A bound hook that cannot bound the type
VO0031 | AdCodicem.ValueObjects | Error | Declared value refused by its own type
