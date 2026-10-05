# Measures the first call to a value object in a fresh process, under the JIT and under native AOT: the cost of
# building its regular expression, which a warmed-up BenchmarkDotNet run never sees.
#
#   pwsh benchmarks/first-call.ps1 [-Samples 41]
#
# Publishes AdCodicem.ValueObjects.Benchmarks.FirstCall twice, then starts one process per sample, interleaving the
# variants round robin so that drift on the machine spreads evenly over them, and prints the median and quartiles of
# each. Native AOT needs the C++ build tools; on Windows, vswhere must be reachable, which this script arranges.
param([int] $Samples = 41)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'AdCodicem.ValueObjects.Benchmarks.FirstCall'
$output = Join-Path $PSScriptRoot '..' 'artifacts' 'first-call'
$variants = 'IbanByRuntimeRegex', 'Iban', 'IbanByHand', 'PostalCodeByRuntimeRegex', 'PostalCode', 'PostalCodeByHand'

if ($IsWindows) {
    $env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
}

dotnet publish $project -c Release -o (Join-Path $output 'jit') -p:FirstCallAot=false --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'The JIT build failed.' }
dotnet publish $project -c Release -o (Join-Path $output 'aot') -p:FirstCallAot=true --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'The native AOT build failed.' }

$executable = if ($IsWindows) { 'AdCodicem.ValueObjects.Benchmarks.FirstCall.exe' } else { 'AdCodicem.ValueObjects.Benchmarks.FirstCall' }
$modes = [ordered]@{
    'JIT' = @{ File = 'dotnet'; Arguments = @(Join-Path $output 'jit' 'AdCodicem.ValueObjects.Benchmarks.FirstCall.dll') }
    'AOT' = @{ File = Join-Path $output 'aot' $executable; Arguments = @() }
}

foreach ($mode in $modes.Keys) {
    $times = @{}
    foreach ($variant in $variants) { $times[$variant] = [System.Collections.Generic.List[double]]::new() }

    # Two rounds discarded: the first processes pay for cold disk caches, which is not what is being measured.
    for ($round = -2; $round -lt $Samples; $round++) {
        foreach ($variant in $variants) {
            $spec = $modes[$mode]
            $text = & $spec.File @($spec.Arguments + $variant)
            if ($LASTEXITCODE -ne 0) { throw "$mode $variant failed: $text" }
            if ($round -ge 0) { $times[$variant].Add([double]::Parse($text, [Globalization.CultureInfo]::InvariantCulture)) }
        }
    }

    foreach ($variant in $variants) {
        $sorted = @($times[$variant] | Sort-Object)
        $n = $sorted.Count
        [string]::Format(
            [Globalization.CultureInfo]::InvariantCulture,
            '{0} {1,-20} median {2,9:F1} us   quartiles {3,9:F1} - {4,9:F1}',
            $mode, $variant, $sorted[[int]($n / 2)], $sorted[[int]($n / 4)], $sorted[[int](3 * $n / 4)])
    }
}
