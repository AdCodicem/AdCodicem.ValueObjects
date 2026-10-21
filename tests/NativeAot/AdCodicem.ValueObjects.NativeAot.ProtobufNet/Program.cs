// Runs a fixed script over protobuf-net's compile-time model and writes one line per scenario to the file it is given.
// heavy.yml's native AOT job runs it once under the JIT (dotnet run) and once as the native binary, then diffs the two
// files, as for AdCodicem.ValueObjects.NativeAot: a difference is a behaviour native AOT changes, and fails the job. The
// process also fails on its own when a scenario finds what neither run should.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.NativeAot;
using AdCodicem.ValueObjects.NativeAot.Protobuf;
using ProtoBuf;
using ProtoBuf.Meta;

if (args.Length != 1)
{
    await Console.Error.WriteLineAsync("Usage: AdCodicem.ValueObjects.NativeAot.ProtobufNet <output file>");
    return 2;
}

var report = new Report();
TypeModel model = AppModel.Instance;
report.Line("runtime", $"dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}");

// Every value object, written as the message over the primitives is, and read back.
var written = Bytes(model, Samples.Everything());
var plain = Bytes(model, Samples.Plain());
if (written.AsSpan().SequenceEqual(plain))
{
    report.Line("Everything", $"written as the message over the primitives: {Convert.ToHexString(written)}");
}
else
{
    report.Fail("Everything", $"written as {Convert.ToHexString(written)}, where the primitives write {Convert.ToHexString(plain)}");
}

var expected = Samples.Describe(Samples.Everything());
var back = Samples.Describe(Read<Everything>(model, written));
if (back == expected)
{
    report.Line("Everything", $"read back: {back}");
}
else
{
    report.Fail("Everything", $"read back {back}, where {expected} was written");
}

var lists = Bytes(model, new Lists { Pages = [PageNumber.Create(1), PageNumber.Create(2)], Phones = [PhoneNumber.Create("+33123456789")] });
var plainLists = Bytes(model, new PlainLists { Pages = [1, 2], Phones = ["+33123456789"] });
if (lists.AsSpan().SequenceEqual(plainLists))
{
    var read = Read<Lists>(model, lists);
    report.Line("Lists", $"written as the lists over the primitives, unpacked, and read back: [{string.Join(", ", read.Pages!)}] [{string.Join(", ", read.Phones!)}]");
}
else
{
    report.Fail("Lists", $"written as {Convert.ToHexString(lists)}, where the primitives write {Convert.ToHexString(plainLists)}");
}

// Each value the rules of a value object refuse, written over the primitives and read as the value objects.
foreach (var (subject, spoil) in Samples.Refused)
{
    var spoiled = Samples.Plain();
    spoil(spoiled);
    try
    {
        Read<Everything>(model, Bytes(model, spoiled));
        report.Fail(subject, "a value its rules refuse was read");
    }
    catch (ProtoException exception)
    {
        report.Line(subject, $"{Report.Describe(exception)}: {exception.Message}");
    }
}

// A trusted profile reads what its rules refuse.
var trusted = Samples.Plain();
trusted.Tolerance = 2;
report.Line("Tolerance", $"read without validating under its trusted profile: {Read<Everything>(model, Bytes(model, trusted)).Tolerance.Value.ToString(CultureInfo.InvariantCulture)}");

// What protobuf-net never reads, a field left out, is the default instance, which no rule checked.
var missing = Read<Everything>(model, []);
report.Line("missing", $"a field left out reads as the default instance: PageNumber {missing.PageNumber.Value}, PhoneNumber '{missing.PhoneNumber.Value}'");

// An instance no rule checked is refused on write.
try
{
    Bytes(model, new Everything());
    report.Fail("default", "an instance its rules refuse was written");
}
catch (ProtoException exception)
{
    report.Line("default", $"{Report.Describe(exception)}: {exception.Message}");
}

// A generated model exports no schema.
try
{
    model.GetSchema(typeof(Everything), ProtoSyntax.Proto3);
    report.Fail("GetSchema", "a generated model exported a schema");
}
catch (NotSupportedException exception)
{
    report.Line("GetSchema", Report.Describe(exception));
}

await File.WriteAllLinesAsync(args[0], report.Lines);
if (report.Unexpected.Count == 0)
{
    Console.WriteLine($"{report.Lines.Count} lines written to {args[0]}.");
    return 0;
}

foreach (var line in report.Unexpected)
{
    await Console.Error.WriteLineAsync($"Unexpected: {line}");
}

return 1;

static byte[] Bytes<[DynamicallyAccessedMembers(Samples.Contract)] T>(TypeModel model, T value)
{
    using var stream = new MemoryStream();
    model.Serialize(stream, value);
    return stream.ToArray();
}

static T Read<[DynamicallyAccessedMembers(Samples.Contract)] T>(TypeModel model, byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    return model.Deserialize<T>(stream);
}
