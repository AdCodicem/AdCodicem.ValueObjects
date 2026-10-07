using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The <c>$jsonSchema</c> validator built from the rules of the value objects a document holds: what each rule becomes,
/// and what is left out because the server could refuse a value the type accepts. The integration suite applies these
/// validators on a server.
/// </summary>
public sealed class MongoDbSchemaTests
{
    private static readonly Lazy<bool> Mappings = new(static () =>
    {
        ConventionRegistry.Register(
            "camel case for MongoCamelOrder",
            new ConventionPack { new CamelCaseElementNameConvention() },
            static type => type == typeof(MongoCamelOrder));

        BsonClassMap.TryRegisterClassMap<MongoConditional>(static map =>
        {
            map.AutoMap();
            map.GetMemberMap(x => x.Written).SetShouldSerializeMethod(static _ => true);
            map.GetMemberMap(x => x.LeftOutWhenNull).SetIgnoreIfNull(true);
        });

        return true;
    });

    public MongoDbSchemaTests()
    {
        MongoDb.EnsureRegistered();
        _ = Mappings.Value;
    }

    /// <summary>
    /// Gets each value object of the domain, with the rules a member holding it is described with, or
    /// <see langword="null"/> where it is left free.
    /// </summary>
    public static TheoryData<string, string?> Members => new()
    {
        { nameof(Consent), """{ "bsonType" : "bool", "description" : "Whether a customer agreed to be contacted." }""" },
        { nameof(Grade), """{ "bsonType" : "int", "minimum" : 65, "maximum" : 70, "description" : "A school grade, from A to F." }""" },
        { nameof(Adjustment), """{ "bsonType" : "int", "minimum" : -10, "maximum" : 10, "description" : "A thermostat adjustment, in degrees." }""" },
        { nameof(Score), """{ "bsonType" : "int", "maximum" : 100, "description" : "A score out of a hundred." }""" },
        { nameof(Port), """{ "bsonType" : "int", "minimum" : 1, "description" : "A TCP port." }""" },
        { nameof(PageNumber), """{ "bsonType" : "int", "minimum" : 1, "description" : "A page number, counted from one, with the first page named in an open value set." }""" },
        { nameof(SequenceNumber), """{ "bsonType" : "int", "description" : "A sequence number, which starts at zero." }""" },
        { nameof(FileSize), """{ "bsonType" : "long", "minimum" : 0, "description" : "The size of a file, in bytes." }""" },
        { nameof(ByteCount), """{ "bsonType" : "long", "description" : "A count of bytes transferred." }""" },
        { nameof(LedgerBalance), null },
        { nameof(Fingerprint), null },
        { nameof(Latitude), """{ "bsonType" : "double", "minimum" : -90.0, "maximum" : 90.0, "description" : "A latitude, in degrees." }""" },
        { nameof(Ratio), """{ "bsonType" : "double", "minimum" : 0.0, "maximum" : 1.0, "description" : "A ratio between zero and one." }""" },
        { nameof(OpeningTime), """{ "bsonType" : "long", "minimum" : 216000000000, "maximum" : 432000000000, "description" : "The time a shop opens." }""" },
        { nameof(RecordedAt), """{ "bsonType" : "date", "description" : "When a record was written." }""" },
        { nameof(OccurredAt), """{ "bsonType" : "object", "description" : "When an event occurred, with the offset it occurred at." }""" },
        { nameof(Duration), """{ "bsonType" : "string", "description" : "How long a task took." }""" },
        { nameof(EffectiveDate), """{ "bsonType" : "date", "description" : "The day a contract takes effect, never before the first day the ledger covers." }""" },
        { nameof(PhoneNumber), """{ "bsonType" : "string", "pattern" : "^\\+[0-9]{6,15}$", "description" : "An international phone number, printed in groups by its own formatter." }""" },
        { nameof(Label), """{ "bsonType" : "string", "maxLength" : 200, "description" : "A free-text label, which may be empty, and whose wide formats outgrow the emitted stack buffer." }""" },
        { nameof(DocumentStatus), """{ "bsonType" : "string", "description" : "The status of a document, from a closed set whose spelling does not matter." }""" },
        { nameof(Iban), """{ "bsonType" : "string", "minLength" : 15, "maxLength" : 34, "pattern" : "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", "description" : "An International Bank Account Number, stored in its electronic form." }""" },
        { nameof(CountryCode), """{ "bsonType" : "string", "minLength" : 1, "maxLength" : 2, "enum" : ["FR", "BE", "LU"], "description" : "An ISO 3166-1 alpha-2 country code restricted to the countries the application serves." }""" },
        { nameof(CurrencyCode), """{ "bsonType" : "string", "pattern" : "^[A-Z]{3}$", "description" : "An ISO 4217 currency code: a pattern and named constants on the same type, so that the constants are created through the pattern while the type initializes." }""" },
        { nameof(EmailAddress), """{ "bsonType" : "string", "maxLength" : 254, "description" : "An email address, normalized to lower case." }""" },
        { nameof(Amount), """{ "bsonType" : "decimal", "minimum" : { "$numberDecimal" : "0" }, "description" : "A monetary amount in the ambient currency, never negative." }""" },
        { nameof(CustomerId), """{ "bsonType" : "binData", "description" : "The identifier of a customer." }""" },
        { nameof(AccountId), """{ "bsonType" : "string", "minLength" : 25, "maxLength" : 25, "pattern" : "^acc_[0123456789abcdefghjkmnpqrstvwxyz]{21}$", "description" : "The public identifier of an account." }""" },
        { nameof(HandWrittenLink), "{ }" },
        { nameof(Reference<PurchaseOrder>), """{ "bsonType" : "string", "maxLength" : 12, "description" : "A reference to a document, whose owner is part of its type: a purchase order's and a sales invoice's are not interchangeable." }""" },
        { "ShapedCode<CaseFree>", """{ "bsonType" : "string", "minLength" : 2 }""" },
        { "ShapedCode<LineByLine>", """{ "bsonType" : "string", "minLength" : 2 }""" },
        { "ShapedCode<SpacedOut>", """{ "bsonType" : "string", "minLength" : 2 }""" },
        { nameof(CulturedStatus), """{ "bsonType" : "string", "description" : "A status from a closed set looked up as the invariant culture compares text." }""" },
    };

    private static readonly Dictionary<string, Func<BsonDocument>> Validators = new()
    {
        [nameof(Consent)] = ValueOf<Consent>,
        [nameof(Grade)] = ValueOf<Grade>,
        [nameof(Adjustment)] = ValueOf<Adjustment>,
        [nameof(Score)] = ValueOf<Score>,
        [nameof(Port)] = ValueOf<Port>,
        [nameof(PageNumber)] = ValueOf<PageNumber>,
        [nameof(SequenceNumber)] = ValueOf<SequenceNumber>,
        [nameof(FileSize)] = ValueOf<FileSize>,
        [nameof(ByteCount)] = ValueOf<ByteCount>,
        [nameof(LedgerBalance)] = ValueOf<LedgerBalance>,
        [nameof(Fingerprint)] = ValueOf<Fingerprint>,
        [nameof(Latitude)] = ValueOf<Latitude>,
        [nameof(Ratio)] = ValueOf<Ratio>,
        [nameof(OpeningTime)] = ValueOf<OpeningTime>,
        [nameof(RecordedAt)] = ValueOf<RecordedAt>,
        [nameof(OccurredAt)] = ValueOf<OccurredAt>,
        [nameof(Duration)] = ValueOf<Duration>,
        [nameof(EffectiveDate)] = ValueOf<EffectiveDate>,
        [nameof(PhoneNumber)] = ValueOf<PhoneNumber>,
        [nameof(Label)] = ValueOf<Label>,
        [nameof(DocumentStatus)] = ValueOf<DocumentStatus>,
        [nameof(Iban)] = ValueOf<Iban>,
        [nameof(CountryCode)] = ValueOf<CountryCode>,
        [nameof(CurrencyCode)] = ValueOf<CurrencyCode>,
        [nameof(EmailAddress)] = ValueOf<EmailAddress>,
        [nameof(Amount)] = ValueOf<Amount>,
        [nameof(CustomerId)] = ValueOf<CustomerId>,
        [nameof(AccountId)] = ValueOf<AccountId>,
        [nameof(HandWrittenLink)] = ValueOf<HandWrittenLink>,
        [nameof(Reference<PurchaseOrder>)] = ValueOf<Reference<PurchaseOrder>>,
        ["ShapedCode<CaseFree>"] = ValueOf<ShapedCode<CaseFree>>,
        ["ShapedCode<LineByLine>"] = ValueOf<ShapedCode<LineByLine>>,
        ["ShapedCode<SpacedOut>"] = ValueOf<ShapedCode<SpacedOut>>,
        [nameof(CulturedStatus)] = ValueOf<CulturedStatus>,
    };

    /// <summary>
    /// A value object member is described by the rules of its type, as the serializer of its underlying value stores
    /// it: the BSON type it writes, a bound written as it writes a number, a length in code points, a pattern in PCRE2.
    /// A 128-bit value object, which nothing stores, is left free.
    /// </summary>
    /// <param name="valueObject">The value object.</param>
    /// <param name="expected">The rules of a member holding it, or <see langword="null"/> where it is left free.</param>
    /// <remarks>
    /// <c>CountryCode</c> has no pattern to keep a value in the Basic Multilingual Plane, so its minimum length is halved;
    /// each <c>ShapedCode</c>'s pattern is matched ignoring case, line by line, or with its spaces ignored, which a
    /// pattern alone cannot say, so it is left out and its minimum length halved; <c>DocumentStatus</c> and
    /// <c>CulturedStatus</c> accept other spellings of their known values, which an <c>enum</c> would refuse.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Members))]
    public void A_value_object_member_is_described_by_the_rules_of_its_type(string valueObject, string? expected)
    {
        var validator = Validators[valueObject]();

        if (expected is null)
        {
            validator.ToJson().Should().Be("""{ "$jsonSchema" : { "bsonType" : "object" } }""");
        }
        else
        {
            validator.ToJson().Should().Be($$"""{ "$jsonSchema" : { "bsonType" : "object", "required" : ["Value"], "properties" : { "Value" : {{expected}} } } }""");
        }
    }

    [Fact]
    public void A_document_is_described_from_its_class_map_with_nested_documents_and_arrays()
    {
        ValueObjectBsonSchema.For<MongoOrder>().ToJson(new JsonWriterSettings { Indent = true }).ReplaceLineEndings("\n").Should().Be(
            """
            {
              "$jsonSchema" : {
                "bsonType" : "object",
                "required" : ["Iban", "Quantity", "Page", "Customer", "Status", "Reference"],
                "properties" : {
                  "Iban" : {
                    "bsonType" : "string",
                    "minLength" : 15,
                    "maxLength" : 34,
                    "pattern" : "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
                    "description" : "An International Bank Account Number, stored in its electronic form."
                  },
                  "Quantity" : {
                    "bsonType" : "int",
                    "minimum" : 0,
                    "maximum" : 1000,
                    "description" : "A quantity of items, tested to cover the narrow integer promotion path."
                  },
                  "Page" : {
                    "bsonType" : "int",
                    "minimum" : 1,
                    "description" : "A page number, counted from one, with the first page named in an open value set."
                  },
                  "Customer" : {
                    "bsonType" : "binData",
                    "description" : "The identifier of a customer."
                  },
                  "Backup" : {
                    "bsonType" : ["string", "null"],
                    "minLength" : 15,
                    "maxLength" : 34,
                    "pattern" : "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
                    "description" : "An International Bank Account Number, stored in its electronic form."
                  },
                  "Countries" : {
                    "items" : {
                      "bsonType" : "string",
                      "minLength" : 1,
                      "maxLength" : 2,
                      "enum" : ["FR", "BE", "LU"],
                      "description" : "An ISO 3166-1 alpha-2 country code restricted to the countries the application serves."
                    }
                  },
                  "Status" : {
                    "bsonType" : "string",
                    "description" : "The status of a document, from a closed set whose spelling does not matter."
                  },
                  "FirstLine" : {
                    "required" : ["Quantity"],
                    "properties" : {
                      "Quantity" : {
                        "bsonType" : "int",
                        "minimum" : 0,
                        "maximum" : 1000,
                        "description" : "A quantity of items, tested to cover the narrow integer promotion path."
                      },
                      "Price" : {
                        "bsonType" : ["decimal", "null"],
                        "minimum" : { "$numberDecimal" : "0" },
                        "description" : "A monetary amount in the ambient currency, never negative."
                      }
                    }
                  },
                  "Lines" : {
                    "items" : {
                      "required" : ["Quantity"],
                      "properties" : {
                        "Quantity" : {
                          "bsonType" : "int",
                          "minimum" : 0,
                          "maximum" : 1000,
                          "description" : "A quantity of items, tested to cover the narrow integer promotion path."
                        },
                        "Price" : {
                          "bsonType" : ["decimal", "null"],
                          "minimum" : { "$numberDecimal" : "0" },
                          "description" : "A monetary amount in the ambient currency, never negative."
                        }
                      }
                    }
                  },
                  "Reference" : {
                    "bsonType" : "string",
                    "maxLength" : 12,
                    "description" : "A reference to a document, whose owner is part of its type: a purchase order's and a sales invoice's are not interchangeable."
                  }
                }
              }
            }
            """);
    }

    /// <summary>
    /// A serialization option on a member reaches the serializer of the underlying value, and the validator describes
    /// what that serializer writes: a bound written as text, or after an overflow, compares nothing and is left out.
    /// </summary>
    [Fact]
    public void A_member_given_a_representation_is_described_in_it()
    {
        var properties = ValueObjectBsonSchema.For<MongoRepresented>()["$jsonSchema"]["properties"];

        properties["PageText"]["bsonType"].Should().Be(new BsonString("string"));
        properties["PageText"].AsBsonDocument.Contains("minimum").Should().BeFalse();
        properties["MaybePageText"]["bsonType"].Should().Be(new BsonArray { "string", "null" });
        properties["PagesText"]["items"]["bsonType"].Should().Be(new BsonString("string"));
        properties["AsText"]["bsonType"].Should().Be(new BsonString("string"));
        properties["Legacy"]["bsonType"].Should().Be(new BsonString("binData"));
        properties["Narrowed"].ToJson().Should().Be("""{ "bsonType" : "int", "description" : "The size of a file, in bytes." }""");
        properties["Day"]["bsonType"].Should().Be(new BsonString("date"));
    }

    [Fact]
    public void What_holds_no_value_object_the_type_rules_is_left_free()
    {
        ValueObjectBsonSchema.For<MongoShapes>().ToJson(new JsonWriterSettings { Indent = true }).ReplaceLineEndings("\n").Should().Be(
            """
            {
              "$jsonSchema" : {
                "bsonType" : "object",
                "required" : ["Required"],
                "properties" : {
                  "Required" : {
                    "bsonType" : "int",
                    "minimum" : 1,
                    "description" : "A page number, counted from one, with the first page named in an open value set."
                  },
                  "Optional" : {
                    "bsonType" : "int",
                    "minimum" : 1,
                    "description" : "A page number, counted from one, with the first page named in an open value set."
                  },
                  "Grid" : {
                    "items" : {
                      "items" : {
                        "bsonType" : "int",
                        "minimum" : 1,
                        "description" : "A page number, counted from one, with the first page named in an open value set."
                      }
                    }
                  },
                  "Maybes" : {
                    "items" : {
                      "bsonType" : ["string", "null"],
                      "minLength" : 1,
                      "maxLength" : 2,
                      "enum" : ["FR", "BE", "LU", null],
                      "description" : "An ISO 3166-1 alpha-2 country code restricted to the countries the application serves."
                    }
                  },
                  "Root" : {
                    "required" : ["Page"],
                    "properties" : {
                      "Page" : {
                        "bsonType" : "int",
                        "minimum" : 1,
                        "description" : "A page number, counted from one, with the first page named in an open value set."
                      }
                    }
                  }
                }
              }
            }
            """);
    }

    [Fact]
    public void Element_names_follow_the_conventions_of_the_class_map()
    {
        var schema = ValueObjectBsonSchema.For<MongoCamelOrder>()["$jsonSchema"];

        schema["required"].Should().Be(new BsonArray { "iban" });
        schema["properties"].AsBsonDocument.Names.Should().Equal("iban", "firstLine");

        // The line's class map has conventions of its own.
        schema["properties"]["firstLine"]["properties"].AsBsonDocument.Names.Should().Equal("Quantity", "Price");
    }

    [Fact]
    public void A_member_the_class_map_may_leave_out_is_not_required()
    {
        var schema = ValueObjectBsonSchema.For<MongoConditional>()["$jsonSchema"];

        schema["required"].Should().Be(new BsonArray { "Always" });
        schema["properties"].AsBsonDocument.Names.Should().Equal("Always", "Written", "LeftOutWhenNull");
    }

    /// <summary>
    /// A document whose value object members may all be left out lists none as <c>required</c>, which the server refuses
    /// as an empty array, failing the whole validator. The integration suite creates a collection with it.
    /// </summary>
    [Fact]
    public void A_document_whose_value_objects_may_all_be_left_out_requires_nothing()
        => ValueObjectBsonSchema.For<MongoLeftOut>().ToJson().Should().Be(
            """{ "$jsonSchema" : { "bsonType" : "object", "properties" : { "Maybe" : { "bsonType" : ["int", "null"], "minimum" : 1, "description" : "A page number, counted from one, with the first page named in an open value set." }, "Omitted" : { "bsonType" : "int", "minimum" : 1, "description" : "A page number, counted from one, with the first page named in an open value set." } } } }""");

    [Fact]
    public void A_document_the_driver_serializes_without_a_class_map_is_refused()
        => FluentActions.Invoking(ValueObjectBsonSchema.For<Iban>).Should().Throw<InvalidOperationException>()
            .WithMessage("MongoDB.Driver serializes Iban through ValueObjectBsonSerializer`2, not through a class map, so the validator cannot tell what it writes.");

    /// <summary>
    /// A bound is written as the serializer writes a number, when it keeps the order of the values it stores; a bound
    /// that does not read, does not fit, or would be compared out of order, is left out.
    /// </summary>
    [Fact]
    public void A_bound_is_published_only_where_the_server_compares_it_as_the_type_does()
    {
        Describe<PageNumber, int>(PageNumber.Schema with { Minimum = "soon", Description = null }, new Int32Serializer())
            .Should().Be("""{ "bsonType" : "int" }""");
        Describe<ByteCount, ulong>(ByteCount.Schema with { Minimum = "0", Maximum = "18446744073709551615", Description = null }, new UInt64Serializer())
            .Should().Be("""{ "bsonType" : "long", "minimum" : 0 }""");
        Describe<PageNumber, int>(PageNumber.Schema with { Description = null }, new OwnInt32Serializer())
            .Should().Be("{ }", "a serializer of the application's own may write numbers in any order");
        Describe<OpeningTime, TimeOnly>(OpeningTime.Schema with { Description = null }, new TimeOnlySerializer(BsonType.Int32))
            .Should().Be("""{ "bsonType" : "int" }""", "ticks as an Int32 wrap around");
        Describe<OpeningTime, TimeOnly>(OpeningTime.Schema with { Description = null }, new TimeOnlySerializer(BsonType.Double, TimeOnlyUnits.Hours))
            .Should().Be("""{ "bsonType" : "double", "minimum" : 6.0, "maximum" : 12.0 }""");
        Describe<Duration, TimeSpan>(Duration.Schema with { Description = null }, new TimeSpanSerializer(BsonType.Int64))
            .Should().Be("""{ "bsonType" : "long", "minimum" : 0, "maximum" : 864000000000 }""");
        Describe<Duration, TimeSpan>(Duration.Schema with { Description = null }, new TimeSpanSerializer(BsonType.Int32, TimeSpanUnits.Seconds))
            .Should().Be("""{ "bsonType" : "int" }""", "a duration as an Int32 wraps around");
        Describe<Latitude, double>(Latitude.Schema with { Description = null }, new DoubleSerializer(BsonType.Int32))
            .Should().Be("""{ "bsonType" : "int", "minimum" : -90, "maximum" : 90 }""");
        Describe<Tolerance, double>(Tolerance.Schema with { Maximum = "0.5", Description = null }, new DoubleSerializer(BsonType.Int32))
            .Should().Be("""{ "bsonType" : "int" }""", "0.5 would be truncated, which the serializer refuses");
        Describe<Amount, decimal>(Amount.Schema with { Description = null }, new DecimalSerializer(BsonType.String))
            .Should().Be("""{ "bsonType" : "string" }""", "text compares in another order than numbers");
        Describe<RecordedAt, DateTime>(RecordedAt.Schema with { Description = null }, new DateTimeSerializer())
            .Should().Be("""{ "bsonType" : "date" }""", "minimum and maximum compare numbers only");
    }

    /// <summary>
    /// A closed set becomes an <c>enum</c> only where every value the type accepts is stored as one of its known values;
    /// every known value is written, or none is.
    /// </summary>
    [Fact]
    public void A_closed_set_is_published_only_where_every_value_the_type_accepts_is_stored_as_a_known_value()
    {
        var closed = ValueObjectSchema.Unconstrained with { IsClosedValueSet = true };

        Describe<Ratio, float>(closed with { KnownValues = [0.25f, 0.5f] }, new SingleSerializer())
            .Should().Be("""{ "bsonType" : "double", "enum" : [0.25, 0.5] }""");
        Describe<Ratio, float>(closed with { KnownValues = [0.25f, 0.5f] }, new SingleSerializer(BsonType.String))
            .Should().Be("""{ "bsonType" : "string" }""", "-0 and 0 are equal and written apart as text");
        Describe<Amount, decimal>(closed with { KnownValues = [1.5m] }, new DecimalSerializer())
            .Should().Be("""{ "bsonType" : "decimal", "enum" : [{ "$numberDecimal" : "1.5" }] }""");
        Describe<CustomerId, Guid>(closed with { KnownValues = [Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")] }, new GuidSerializer(GuidRepresentation.Standard))
            .Should().Be("""{ "bsonType" : "binData", "enum" : [{ "$binary" : { "base64" : "b5YZ/4uG0BG0LQDAT8lk/w==", "subType" : "04" } }] }""");
        Describe<PageNumber, int>(closed with { KnownValues = [1, 2] }, new OwnInt32Serializer())
            .Should().Be("""{ "enum" : [1, 2] }""");
        Describe<RecordedAt, DateTime>(closed with { KnownValues = [new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)] }, new DateTimeSerializer())
            .Should().Be("""{ "bsonType" : "date" }""", "two instants equal whatever their kind are written apart");
        Describe<SequenceNumber, uint>(closed with { KnownValues = [1u, uint.MaxValue] }, new UInt32Serializer())
            .Should().Be("""{ "bsonType" : "int" }""", "the serializer cannot write the second value");
        Describe<PageNumber, int>(closed with { KnownValues = [1, "two"] }, new Int32Serializer())
            .Should().Be("""{ "bsonType" : "int" }""", "a known value of another type cannot be written");
        Describe<PageNumber, int>(closed, new Int32Serializer())
            .Should().Be("""{ "bsonType" : "int" }""", "no value is known");
        Describe<Label, string>(closed with { KnownValues = ["a"] }, new StringSerializer())
            .Should().Be("""{ "bsonType" : "string" }""", "Label accepts A as well, and stores it as spelled");
        Describe<DocumentStatus, string>(closed with { KnownValues = ["DRAFT", "FINAL"] }, new StringSerializer())
            .Should().Be("""{ "bsonType" : "string" }""", "DocumentStatus accepts draft as well, and stores it as spelled");
        Describe<OccurredAt, DateTimeOffset>(closed with { KnownValues = [new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)] }, new DateTimeOffsetSerializer())
            .Should().Be("""{ "bsonType" : "object" }""", "two instants equal whatever their offset are written apart");
        Describe<HandWrittenLink, Uri>(closed with { KnownValues = [new Uri("https://example.com/a")] }, new UriSerializer())
            .Should().Be("{ }", "two addresses equal whatever the case of their host are written apart");
    }

    [Fact]
    public void Lengths_and_a_pattern_are_published_for_text_stored_as_text_only()
    {
        Describe<Iban, string>(Iban.Schema with { Description = null }, new StringSerializer(BsonType.ObjectId))
            .Should().Be("""{ "bsonType" : "objectId" }""");
        Describe<OccurredAt, DateTimeOffset>(OccurredAt.Schema with { Description = null }, new DateTimeOffsetSerializer(BsonType.Array))
            .Should().Be("""{ "bsonType" : "array" }""");
        Describe<PageNumber, int>(ValueObjectSchema.Unconstrained with { MaxLength = 1, Pattern = "^1$" }, new Int32Serializer(BsonType.String))
            .Should().Be("""{ "bsonType" : "string" }""", "the text of a number is not the text the rules are about");
    }

    [Fact]
    public void A_pattern_that_cannot_keep_a_value_in_the_basic_plane_halves_the_minimum_length()
    {
        Describe<Label, string>(ValueObjectSchema.Unconstrained with { MinLength = 5, Pattern = "[a-z]" }, new StringSerializer())
            .Should().Be("""{ "bsonType" : "string", "minLength" : 3, "pattern" : "[a-z]" }""");
        Describe<Label, string>(ValueObjectSchema.Unconstrained with { MinLength = int.MaxValue, Pattern = "^.+$" }, new StringSerializer())
            .Should().Be("""{ "bsonType" : "string", "minLength" : 1073741824 }""");
    }

    private static BsonDocument ValueOf<T>() => ValueObjectBsonSchema.For<MongoHolder<T>>();

    private static string Describe<TSelf, TValue>(ValueObjectSchema schema, IBsonSerializer<TValue> serializer)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => ValueObjectBsonRules.Describe<TSelf, TValue>(schema, serializer).ToJson();

    /// <summary>A serializer of the application's own, which writes an <see cref="int"/> as MongoDB.Bson does.</summary>
    private sealed class OwnInt32Serializer : SerializerBase<int>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, int value)
            => context.Writer.WriteInt32(value);

        public override int Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            => context.Reader.ReadInt32();
    }
}

/// <summary>
/// One value as a document.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public sealed class MongoHolder<T>
{
    public ObjectId Id { get; set; }

    public T Value { get; set; } = default!;
}

/// <summary>
/// The shapes a validator leaves free, beside two value objects it describes.
/// </summary>
public sealed class MongoShapes
{
    public ObjectId Id { get; set; }

    public PageNumber Required { get; set; }

    [BsonIgnoreIfDefault]
    public PageNumber Optional { get; set; }

    public List<List<PageNumber>> Grid { get; set; } = [];

    public List<CountryCode?> Maybes { get; set; } = [];

    public Dictionary<CountryCode, PageNumber> ByCountry { get; set; } = [];

    [BsonSerializer(typeof(OwnIbanSerializer))]
    public Iban Own { get; set; }

    public string Name { get; set; } = string.Empty;

    public MongoUnruled? Unruled { get; set; }

    public MongoNode? Root { get; set; }
}

/// <summary>
/// A node of a tree, which refers to its own type.
/// </summary>
public sealed class MongoNode
{
    public PageNumber Page { get; set; }

    public MongoNode? Parent { get; set; }

    public List<MongoNode> Children { get; set; } = [];
}

/// <summary>
/// A sub-document holding no value object.
/// </summary>
public sealed class MongoUnruled
{
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// An order whose element names are camel case.
/// </summary>
public sealed class MongoCamelOrder
{
    public ObjectId Id { get; set; }

    public Iban Iban { get; set; }

    public MongoLine FirstLine { get; set; } = new();
}

/// <summary>
/// Members the class map writes on a condition: always, when a method says so, and unless it is null.
/// </summary>
public sealed class MongoConditional
{
    public ObjectId Id { get; set; }

    public PageNumber Always { get; set; }

    public PageNumber Written { get; set; }

    public PageNumber LeftOutWhenNull { get; set; }
}

/// <summary>
/// A document whose value object members may all be left out: one nullable, one left out when it is the default.
/// </summary>
public sealed class MongoLeftOut
{
    public ObjectId Id { get; set; }

    public PageNumber? Maybe { get; set; }

    [BsonIgnoreIfDefault]
    public PageNumber Omitted { get; set; }
}

/// <summary>
/// A serializer of the application's own for an IBAN, which the validator leaves free.
/// </summary>
internal sealed class OwnIbanSerializer : SerializerBase<Iban>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Iban value)
        => context.Writer.WriteString(value.Value);

    public override Iban Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => Iban.Create(context.Reader.ReadString());
}

/// <summary>A status from a closed set looked up as the invariant culture compares text.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.InvariantCulture)]
public readonly partial struct CulturedStatus
{
    [KnownValue]
    public static readonly CulturedStatus Open = Known("open");
}
