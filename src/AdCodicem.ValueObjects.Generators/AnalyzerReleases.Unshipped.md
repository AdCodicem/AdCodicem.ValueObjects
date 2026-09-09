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
VO0011 | AdCodicem.ValueObjects | Warning | Member looks like a value object hook
VO0013 | AdCodicem.ValueObjects | Error | Invalid known value
VO0014 | AdCodicem.ValueObjects | Error | Invalid pattern
