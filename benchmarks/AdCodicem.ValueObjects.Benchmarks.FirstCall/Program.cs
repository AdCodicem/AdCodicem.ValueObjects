using System.Diagnostics;
using System.Globalization;
using AdCodicem.ValueObjects.Benchmarks.Domain;

// Times the first TryCreate of one value object in this process: its type initializer, and so the construction of
// its regular expression, included. first-call.ps1 starts one process per sample.
var variant = args.Length == 1 ? args[0] : throw new ArgumentException("Name one variant.");
const string Account = "FR76 3000 6000 0112 3456 7890 189";
const string Code = "75008";

var start = Stopwatch.GetTimestamp();
var accepted = variant switch
{
    "IbanByRuntimeRegex" => IbanByRuntimeRegex.TryCreate(Account, out _),
    "Iban" => Iban.TryCreate(Account, out _),
    "IbanByHand" => IbanByHand.TryCreate(Account, out _),
    "PostalCodeByRuntimeRegex" => PostalCodeByRuntimeRegex.TryCreate(Code, out _),
    "PostalCode" => PostalCode.TryCreate(Code, out _),
    "PostalCodeByHand" => PostalCodeByHand.TryCreate(Code, out _),
    _ => throw new ArgumentException($"Unknown variant '{variant}'."),
};
var elapsed = Stopwatch.GetElapsedTime(start);

if (!accepted)
{
    throw new InvalidOperationException($"{variant} rejected its input.");
}

Console.WriteLine(elapsed.TotalMicroseconds.ToString("F1", CultureInfo.InvariantCulture));
