using System.Globalization;
using AdCodicem.ValueObjects.CsvHelper;
using AdCodicem.ValueObjects.Metadata;
using CsvHelper;

namespace AdCodicem.ValueObjects.UnitTests.Csv;

/// <summary>
/// The factory where the runtime supports no dynamic code, as under native AOT, run under the JIT in a child process of
/// its own (<see cref="WithoutDynamicCode"/>): a generated value object is read with no registration, and a construction
/// of a generic one nothing registered is refused when CsvHelper meets it, rather than read as default instances, until
/// its converter is added or it is registered.
/// </summary>
public sealed class WithoutDynamicCodeCsvTests
{
    private const string NotDescribed =
        "CsvHelper meets 'Reference<UnregisteredCsvOwner>', which is not a value object the registry describes. Under native "
        + "AOT, a value object written by hand, ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema), and a construction "
        + "of a generic one, ValueObjectRegistry.Register<TSelf, TValue>(static () => new TSelf.ValueJsonConverter()), have to "
        + "be registered before CsvHelper meets them, or their converter added with AddValueObject<TSelf, TValue>().";

    [Fact]
    public async Task A_value_object_nothing_registered_is_refused_when_CsvHelper_meets_it_until_it_is_added_or_registered()
    {
        if (await WithoutDynamicCode.RanInAChildAsync(typeof(WithoutDynamicCodeCsvTests), nameof(A_value_object_nothing_registered_is_refused_when_CsvHelper_meets_it_until_it_is_added_or_registered)))
        {
            return;
        }

        CsvHarness.Read<CsvOrder>(CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n").Single().Should().BeEquivalentTo(CsvOrder.Valid);
        CsvHarness.Write([CsvOrder.Valid]).Should().Be(CsvOrder.Header + "\n" + CsvOrder.ValidRow + "\n");

        // With a header, CsvHelper meets the type when it validates the header, and lets the exception through.
        FluentActions.Invoking(static () => CsvHarness.Read<CsvRow<Reference<UnregisteredCsvOwner>>>("Value\nPO-1\n"))
            .Should().Throw<InvalidOperationException>().WithMessage(NotDescribed);

        // Without one, it wraps the exception in a ReaderException for each row, which a handler returning false skips.
        List<Exception> seen = [];
        var headerless = CsvHarness.Configuration() with
        {
            HasHeaderRecord = false,
            ReadingExceptionOccurred = args =>
            {
                seen.Add(args.Exception);
                return false;
            },
        };
        CsvHarness.Read<CsvRow<Reference<UnregisteredCsvOwner>>>("PO-1\nPO-2\n", headerless).Should().BeEmpty();
        seen.Should().HaveCount(2).And.AllSatisfy(static exception =>
        {
            exception.Should().BeOfType<ReaderException>();
            exception.InnerException.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(NotDescribed);
        });

        FluentActions.Invoking(static () => CsvHarness.Write([new CsvRow<Reference<UnregisteredCsvOwner>> { Value = Reference<UnregisteredCsvOwner>.Create("PO-1") }]))
            .Should().Throw<WriterException>().WithInnerException<InvalidOperationException>().WithMessage(NotDescribed);

        // GetField<T> and WriteField<T> let it through as it is.
        using (var fields = new CsvReader(new StringReader("PO-1\n"), CsvHarness.Configuration() with { HasHeaderRecord = false }))
        using (var written = new CsvWriter(new StringWriter(CultureInfo.InvariantCulture), CsvHarness.Configuration()))
        {
            fields.Context.TypeConverterCache.AddValueObjects();
            written.Context.TypeConverterCache.AddValueObjects();
            fields.Read();
            FluentActions.Invoking(() => fields.GetField<Reference<UnregisteredCsvOwner>>(0)).Should().Throw<InvalidOperationException>().WithMessage(NotDescribed);
            FluentActions.Invoking(() => written.WriteField(Reference<UnregisteredCsvOwner>.Create("PO-1"))).Should().Throw<InvalidOperationException>().WithMessage(NotDescribed);
        }

        CsvHarness.Read<CsvRow<Reference<UnregisteredCsvOwner>>>("Value\n po-1 \n", setup: static context => context.TypeConverterCache.AddValueObject<Reference<UnregisteredCsvOwner>, string>())
            .Single().Value.Should().Be(Reference<UnregisteredCsvOwner>.Create("PO-1"));
        ValueObjectRegistry.TryGet(typeof(Reference<UnregisteredCsvOwner>), out _).Should().BeFalse();

        // A cache whose factory refused a type asks it again, so a registration made since is found.
        using var reader = new CsvReader(new StringReader(string.Empty), CultureInfo.InvariantCulture);
        var cache = reader.Context.TypeConverterCache.AddValueObjects();
        FluentActions.Invoking(() => cache.GetConverter<Reference<UnregisteredCsvOwner>>()).Should().Throw<InvalidOperationException>();

        ValueObjectRegistry.Register<Reference<UnregisteredCsvOwner>, string>(static () => new Reference<UnregisteredCsvOwner>.ValueJsonConverter());

        cache.GetConverter<Reference<UnregisteredCsvOwner>>().Should().BeOfType<ValueObjectTypeConverter<Reference<UnregisteredCsvOwner>, string>>();
        CsvHarness.Read<CsvRow<Reference<UnregisteredCsvOwner>>>("Value\n po-2 \n").Single().Value.Should().Be(Reference<UnregisteredCsvOwner>.Create("PO-2"));
    }
}
