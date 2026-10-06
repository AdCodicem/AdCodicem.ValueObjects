using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Formatters;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Formatters;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The Newtonsoft.Json input formatter <c>AddValueObjectsNewtonsoftJson()</c> puts in place of MVC's, read from the
/// options an application resolves, and read with beside the framework's own on the same bodies.
/// </summary>
public class NewtonsoftJsonInputFormatterTests
{
    /// <summary>
    /// The formatter takes the place of the framework's, over the application's own settings and options, whichever of
    /// the two calls comes first; the JSON Patch formatter, which derives from the framework's, stays where it is.
    /// </summary>
    /// <param name="newtonsoftFirst">Whether <c>AddNewtonsoftJson()</c> is called before the package's method.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_framework_formatter_is_replaced_in_place_over_the_application_s_own_settings(bool newtonsoftFirst)
    {
        using var provider = Build(mvc =>
        {
            if (newtonsoftFirst)
            {
                mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson();
            }
            else
            {
                mvc.AddValueObjectsNewtonsoftJson().AddNewtonsoftJson();
            }
        });
        using var withoutPackage = Build(static mvc => mvc.AddNewtonsoftJson());
        var framework = withoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        var application = provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value;

        var index = framework.ToList().FindIndex(static formatter => formatter.GetType() == typeof(NewtonsoftJsonInputFormatter));
        var formatter = formatters[index].Should().BeOfType<ValueObjectNewtonsoftJsonInputFormatter>().Subject;
        formatters.Should().HaveSameCount(framework);
        formatters.Should().NotContain(static candidate => candidate.GetType() == typeof(NewtonsoftJsonInputFormatter));
        ValueObjectNewtonsoftJsonInputFormatter.SettingsOf(formatter).Should().BeSameAs(application.SerializerSettings);
        var replaced = framework[index].Should().BeOfType<NewtonsoftJsonInputFormatter>().Subject;
        formatter.SupportedMediaTypes.Should().Equal(replaced.SupportedMediaTypes);
        formatter.SupportedEncodings.Should().Equal(replaced.SupportedEncodings);
        formatter.ExceptionPolicy.Should().Be(replaced.ExceptionPolicy).And.Be(InputFormatterExceptionPolicy.MalformedInputExceptions);
        var patch = framework.ToList().FindIndex(static candidate => candidate is NewtonsoftJsonPatchInputFormatter);
        formatters[patch].Should().BeOfType<NewtonsoftJsonPatchInputFormatter>();
    }

    /// <summary>
    /// One call sets everything up, and an application that also calls the AspNetCore package's method, in either order,
    /// or this one twice, gets one replacement, one setup of each kind and one converter in its settings.
    /// </summary>
    /// <param name="aspNetCoreFirst">Whether the AspNetCore package's <c>AddValueObjects()</c> comes first.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Calling_the_AspNetCore_method_as_well_or_this_one_twice_sets_things_up_once(bool aspNetCoreFirst)
    {
        using var provider = Build(mvc =>
        {
            mvc.AddNewtonsoftJson();
            if (aspNetCoreFirst)
            {
                mvc.AddValueObjects().AddValueObjectsNewtonsoftJson().AddValueObjectsNewtonsoftJson();
            }
            else
            {
                mvc.AddValueObjectsNewtonsoftJson().AddValueObjectsNewtonsoftJson().AddValueObjects();
            }
        });

        provider.GetServices<IPostConfigureOptions<MvcOptions>>().OfType<ValueObjectNewtonsoftJsonInputFormatterSetup>().Should().ContainSingle();
        provider.GetServices<IPostConfigureOptions<MvcNewtonsoftJsonOptions>>().OfType<ValueObjectNewtonsoftJsonOptionsSetup>().Should().ContainSingle();
        provider.GetServices<IPostConfigureOptions<MvcOptions>>().OfType<ValueObjectInputFormatterSetup>().Should().ContainSingle();
        var options = provider.GetRequiredService<IOptions<MvcOptions>>().Value;
        options.InputFormatters.OfType<ValueObjectNewtonsoftJsonInputFormatter>().Should().ContainSingle();
        options.ModelBinderProviders[0].Should().BeOfType<AspNetCore.ModelBinding.ValueObjectModelBinderProvider>();
        provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings.Converters
            .OfType<ValueObjectConverter>().Should().ContainSingle();
    }

    /// <summary>
    /// The media types and encodings an application gave the framework formatter, in the configuration of MVC, are the
    /// ones its replacement reads.
    /// </summary>
    [Fact]
    public void The_media_types_and_encodings_the_application_gave_the_framework_formatter_are_kept()
    {
        using var provider = Build(static mvc => mvc
            .AddNewtonsoftJson()
            .AddMvcOptions(static options =>
            {
                var framework = options.InputFormatters.Single(static formatter => formatter.GetType() == typeof(NewtonsoftJsonInputFormatter));
                var json = (NewtonsoftJsonInputFormatter)framework;
                json.SupportedMediaTypes.Add("application/csp-report");
                json.SupportedEncodings.Remove(json.SupportedEncodings.Single(static encoding => encoding.CodePage == Encoding.Unicode.CodePage));
            })
            .AddValueObjectsNewtonsoftJson());

        var formatter = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectNewtonsoftJsonInputFormatter>().Single();

        formatter.SupportedMediaTypes.Should().Contain("application/csp-report");
        formatter.SupportedEncodings.Should().ContainSingle().Which.CodePage.Should().Be(Encoding.UTF8.CodePage);
    }

    /// <summary>
    /// Only the framework's formatter is replaced. One the application built over settings of its own, though of the
    /// exact type, one it derived, though over the application's settings, the JSON Patch formatter and a formatter of
    /// another kind are left where and as they are.
    /// </summary>
    [Fact]
    public void A_formatter_the_application_built_or_derived_itself_is_left_as_it_is()
    {
        var own = new NewtonsoftJsonInputFormatter(
            NullLogger(), new JsonSerializerSettings(), ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), new MvcOptions(), new MvcNewtonsoftJsonOptions());
        DerivedFormatter? derived = null;
        using var provider = Build(mvc =>
        {
            mvc.AddNewtonsoftJson();
            mvc.AddXmlSerializerFormatters();
            mvc.Services.AddOptions<MvcOptions>().Configure<IOptions<MvcNewtonsoftJsonOptions>>((options, json) =>
            {
                derived = new DerivedFormatter(json.Value, options);
                options.InputFormatters.Insert(0, own);
                options.InputFormatters.Insert(1, derived);
            });
            mvc.AddValueObjectsNewtonsoftJson();
        });

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        var settings = provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;

        formatters[0].Should().BeSameAs(own);
        formatters[1].Should().BeSameAs(derived);
        ValueObjectNewtonsoftJsonInputFormatter.SettingsOf(derived!).Should().BeSameAs(settings);
        formatters.OfType<NewtonsoftJsonPatchInputFormatter>().Should().ContainSingle();
        formatters.OfType<XmlSerializerInputFormatter>().Should().ContainSingle();
        formatters.OfType<ValueObjectNewtonsoftJsonInputFormatter>().Should().ContainSingle()
            .Which.Should().Match<ValueObjectNewtonsoftJsonInputFormatter>(formatter => ValueObjectNewtonsoftJsonInputFormatter.SettingsOf(formatter) == settings);
    }

    /// <summary>
    /// An application whose MVC reads with System.Text.Json, never having called <c>AddNewtonsoftJson()</c>, gets the
    /// AspNetCore package's formatter and no Newtonsoft.Json one.
    /// </summary>
    [Fact]
    public void Without_Newtonsoft_Json_MVC_reads_with_the_System_Text_Json_formatter_of_the_AspNetCore_package()
    {
        using var provider = Build(static mvc => mvc.AddValueObjectsNewtonsoftJson());

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

        formatters.OfType<ValueObjectJsonInputFormatter>().Should().ContainSingle();
        formatters.OfType<NewtonsoftJsonInputFormatter>().Should().BeEmpty();
    }

    /// <summary>
    /// A post-configuration registered after the package's method finds the replacement, which
    /// <c>RemoveType&lt;NewtonsoftJsonInputFormatter&gt;()</c>, matching the exact type, leaves in place; removing every
    /// formatter that is a <see cref="NewtonsoftJsonInputFormatter"/> but the JSON Patch formatter, as the documentation
    /// says, takes it out and keeps the JSON Patch formatter, as that call keeps it without the package.
    /// </summary>
    [Fact]
    public void A_removal_registered_after_the_method_takes_the_replacement_and_keeps_the_JSON_Patch_formatter()
    {
        using var exact = Build(static mvc =>
        {
            mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson();
            mvc.Services.PostConfigure<MvcOptions>(static options => options.InputFormatters.RemoveType<NewtonsoftJsonInputFormatter>());
        });
        using var documented = Build(static mvc =>
        {
            mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson();
            mvc.Services.PostConfigure<MvcOptions>(static options =>
            {
                foreach (var formatter in options.InputFormatters
                    .Where(static formatter => formatter is NewtonsoftJsonInputFormatter and not NewtonsoftJsonPatchInputFormatter)
                    .ToList())
                {
                    options.InputFormatters.Remove(formatter);
                }
            });
        });
        using var withoutPackage = Build(static mvc =>
        {
            mvc.AddNewtonsoftJson();
            mvc.Services.PostConfigure<MvcOptions>(static options => options.InputFormatters.RemoveType<NewtonsoftJsonInputFormatter>());
        });

        exact.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectNewtonsoftJsonInputFormatter>().Should().ContainSingle();
        var removed = documented.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        var expected = withoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        removed.OfType<NewtonsoftJsonInputFormatter>().Should().ContainSingle().Which.Should().BeOfType<NewtonsoftJsonPatchInputFormatter>();
        removed.Select(static formatter => formatter.GetType()).Should().Equal(expected.Select(static formatter => formatter.GetType()));
    }

    /// <summary>
    /// MVC's settings get the converter and the two settings it reads value objects best under.
    /// </summary>
    [Fact]
    public void The_settings_get_the_converter_and_the_settings_it_reads_best_under()
    {
        using var provider = Build(static mvc => mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson());

        var settings = provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;

        settings.Converters.OfType<ValueObjectConverter>().Should().ContainSingle();
        settings.DateParseHandling.Should().Be(DateParseHandling.None);
        settings.FloatParseHandling.Should().Be(FloatParseHandling.Decimal);
    }

    /// <summary>
    /// Settings the application configured for value objects itself are left as it left them, its choice of reading
    /// reals as doubles included, whether its configuration is registered before the package's method or after it.
    /// Settings holding other converters only get this one beside them.
    /// </summary>
    /// <param name="applicationFirst">Whether the application's configuration is registered first.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Settings_the_application_configured_for_value_objects_are_left_as_it_left_them(bool applicationFirst)
    {
        static void Application(MvcNewtonsoftJsonOptions options) => options.SerializerSettings.AddValueObjects(decimalReals: false);
        using var provider = Build(mvc =>
        {
            if (applicationFirst)
            {
                mvc.AddNewtonsoftJson(Application).AddValueObjectsNewtonsoftJson();
            }
            else
            {
                mvc.AddValueObjectsNewtonsoftJson().AddNewtonsoftJson(Application);
            }
        });
        using var other = Build(static mvc => mvc
            .AddNewtonsoftJson(static options => options.SerializerSettings.Converters.Add(new StringEnumConverter()))
            .AddValueObjectsNewtonsoftJson());

        var settings = provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;
        var otherSettings = other.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;

        settings.Converters.OfType<ValueObjectConverter>().Should().ContainSingle();
        settings.FloatParseHandling.Should().Be(FloatParseHandling.Double);
        otherSettings.Converters.Should().ContainSingle(static converter => converter is ValueObjectConverter)
            .And.ContainSingle(static converter => converter is StringEnumConverter);
        otherSettings.FloatParseHandling.Should().Be(FloatParseHandling.Decimal);
    }

    /// <summary>
    /// A body read by the replacement and by the framework's formatter, built over the same settings and options, gives
    /// the same result, the same model state and the same logs, whatever the application says of exception messages;
    /// the replacement also records the code of each refusal, under the key of its model state error, quirks of the
    /// framework's keys included.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="model">The type the body is read as.</param>
    /// <param name="refused">Whether the framework refuses the body, so that the row compares what it means to.</param>
    /// <param name="codes">The codes the replacement records, as <c>key=code</c> pairs separated by <c>|</c>.</param>
    [Theory]
    [InlineData("""{"reference":"ABC-1"}""", "order", false, "")]
    [InlineData("""{"reference":"no"}""", "order", true, "reference=" + ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"reference":42}""", "order", true, "reference=" + ValueObjectErrorCodes.NotParsable)]
    [InlineData("""{"reference":null}""", "order", true, "reference=" + ValueObjectErrorCodes.Required)]
    [InlineData("""{"reference":""", "order", true, "")]
    [InlineData("null", "order", false, "")]
    [InlineData("""{"reference":"ABC-1","country":null,"quantity":2,"email":"ada@example.com","purchase":"po-1","code":"ab"}""", "parcel", false, "")]
    [InlineData(
        """{"reference":"no","country":"ZZ","quantity":1001,"email":"nope","purchase":"PO-1042-AND-MORE","code":"a1"}""",
        "parcel",
        true,
        "reference=" + ValueObjectErrorCodes.TooShort
        + "|country=" + ValueObjectErrorCodes.NotAKnownValue
        + "|quantity=" + ValueObjectErrorCodes.OutOfRange
        + "|email=" + ValueObjectErrorCodes.InvalidFormat
        + "|purchase=" + ValueObjectErrorCodes.TooLong
        + "|code=" + ValueObjectErrorCodes.InvalidFormat)]
    [InlineData(
        """{"lines":[{"reference":"no"},{"reference":"ABC-1"},{"reference":"x"}]}""",
        "parcel",
        true,
        "lines[0].reference=" + ValueObjectErrorCodes.TooShort + "|lines[2].reference=" + ValueObjectErrorCodes.TooShort)]
    [InlineData(
        """{"aliases":{"a":"no","a b":"no","[x]":"no","it's":"no","0":"no"}}""",
        "parcel",
        true,
        "aliases.a=" + ValueObjectErrorCodes.TooShort
        + "|aliases['a b']=" + ValueObjectErrorCodes.TooShort
        + "|aliases['[x]'][x]=" + ValueObjectErrorCodes.TooShort
        + "|aliases['it\\'s'].it's=" + ValueObjectErrorCodes.TooShort
        + "|aliases.0=" + ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"order":{"reference":"no"}}""", "parcel", true, "order.reference.order=" + ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"created":"no"}""", "parcel", true, "created=" + ValueObjectErrorCodes.TooShort)]
    [InlineData(
        """{"createdAliases":{"a b":"no","[x]":"no","it's":"no"}}""",
        "parcel",
        true,
        "createdAliases['a b']=" + ValueObjectErrorCodes.TooShort
        + "|createdAliases['[x]']=" + ValueObjectErrorCodes.TooShort
        + "|createdAliases['it\\'s']=" + ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"keyed":{"no":"refused key"}}""", "parcel", true, "")]
    [InlineData("""{"link":42}""", "parcel", true, "link=" + ValueObjectErrorCodes.NotParsable)]
    [InlineData("""{"link":""}""", "parcel", true, "link=" + ValueObjectErrorCodes.Required)]
    [InlineData("""["no","ABC-1","x"]""", "references", true, "[0]=" + ValueObjectErrorCodes.TooShort + "|[2]=" + ValueObjectErrorCodes.TooShort)]
    [InlineData("\"no\"", "reference", true, "=" + ValueObjectErrorCodes.TooShort)]
    public async Task A_body_reads_as_the_framework_reads_it_and_each_refusal_records_its_code(
        string body,
        string model,
        bool refused,
        string codes)
    {
        var type = model switch
        {
            "order" => typeof(ProbeOrder),
            "parcel" => typeof(NewtonsoftParcel),
            "references" => typeof(List<Ordering.OrderReference>),
            _ => typeof(Ordering.OrderReference),
        };
        var expectedCodes = codes.Length == 0
            ? []
            : codes.Split('|').Select(static pair => pair.Split('=')).ToDictionary(static pair => pair[0], static pair => pair[1]);

        foreach (var allowMessages in new[] { true, false })
        {
            var settings = new JsonSerializerSettings().AddValueObjects();
            var json = new MvcNewtonsoftJsonOptions { AllowInputFormatterExceptionMessages = allowMessages };
            var frameworkLogger = new RecordingLogger();
            var replacementLogger = new RecordingLogger();
            var framework = new NewtonsoftJsonInputFormatter(
                frameworkLogger, settings, ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), new MvcOptions(), json);
            var replacement = Replacement(settings, json, replacementLogger);

            var (expected, expectedState, expectedHttp) = await ReadAsync(framework, body, type);
            var (actual, actualState, actualHttp) = await ReadAsync(replacement, body, type);

            expected.HasError.Should().Be(refused);
            actual.HasError.Should().Be(expected.HasError);
            actual.IsModelSet.Should().Be(expected.IsModelSet);
            actual.Model.Should().BeEquivalentTo(expected.Model);
            Describe(actualState).Should().Equal(Describe(expectedState), "the model state of {0}", body);
            replacementLogger.Entries.Should().Equal(frameworkLogger.Entries);
            ValueObjectProblemDetails.GetErrorCodes(expectedHttp).Should().BeEmpty();
            ValueObjectProblemDetails.GetErrorCodes(actualHttp).Should().Equal(expectedCodes);
            expectedCodes.Keys.Should().BeSubsetOf(actualState.Keys, "a code is recorded under the key of its error");
        }
    }

    /// <summary>
    /// A body bound under a model name, as a parameter bound with a prefix is, has its codes recorded under the keys of
    /// its errors, the name included.
    /// </summary>
    [Fact]
    public async Task A_code_is_recorded_under_the_model_name_as_its_error_is()
    {
        var replacement = Replacement(new JsonSerializerSettings().AddValueObjects(), new MvcNewtonsoftJsonOptions(), new RecordingLogger());
        var http = Request("""{"reference":"no"}""");
        var modelState = new ModelStateDictionary();

        await replacement.ReadAsync(Context(http, typeof(NewtonsoftParcel), modelState, "parcel"));

        modelState.Keys.Should().Equal("parcel.reference");
        ValueObjectProblemDetails.GetErrorCodes(http).Should().Equal(new Dictionary<string, string>
        {
            ["parcel.reference"] = ValueObjectErrorCodes.TooShort,
        });
    }

    /// <summary>
    /// Newtonsoft.Json reads on past a refusal, but the model state takes no more errors than it is allowed, less the
    /// one saying so: no code is recorded past them, so that every code has its error.
    /// </summary>
    [Fact]
    public async Task No_code_is_recorded_once_the_model_state_takes_no_more_errors()
    {
        var replacement = Replacement(new JsonSerializerSettings().AddValueObjects(), new MvcNewtonsoftJsonOptions(), new RecordingLogger());
        var http = Request("""{"lines":[{"reference":"no"},{"reference":"no"},{"reference":"no"},{"reference":"no"},{"reference":"no"}]}""");
        var modelState = new ModelStateDictionary(maxAllowedErrors: 3);

        await replacement.ReadAsync(Context(http, typeof(NewtonsoftParcel), modelState, string.Empty));

        modelState.HasReachedMaxErrors.Should().BeTrue();
        ValueObjectProblemDetails.GetErrorCodes(http).Keys.Should().Equal("lines[0].reference", "lines[1].reference");
        modelState.Keys.Should().Contain(ValueObjectProblemDetails.GetErrorCodes(http).Keys);
    }

    /// <summary>
    /// An error the handler of the application's settings marks handled is skipped, as the framework skips it: the body
    /// reads, and records no code.
    /// </summary>
    [Fact]
    public async Task An_error_the_application_handled_records_no_code()
    {
        var settings = new JsonSerializerSettings().AddValueObjects();
        settings.Error = static (_, arguments) => arguments.ErrorContext.Handled = true;
        var replacement = Replacement(settings, new MvcNewtonsoftJsonOptions(), new RecordingLogger());
        var http = Request("""{"reference":"no","quantity":2}""");
        var modelState = new ModelStateDictionary();

        var result = await replacement.ReadAsync(Context(http, typeof(NewtonsoftParcel), modelState, string.Empty));

        result.HasError.Should().BeFalse();
        modelState.IsValid.Should().BeTrue();
        ValueObjectProblemDetails.GetErrorCodes(http).Should().BeEmpty();
    }

    /// <summary>
    /// An exception no JSON explains propagates from either formatter, and the replacement keeps the framework
    /// formatter's exception policy, under which MVC lets it propagate rather than answer it with a 400.
    /// </summary>
    [Fact]
    public async Task An_exception_no_JSON_explains_propagates_as_from_the_framework_s_formatter()
    {
        var settings = new JsonSerializerSettings().AddValueObjects();
        var json = new MvcNewtonsoftJsonOptions();
        var framework = new NewtonsoftJsonInputFormatter(
            NullLogger(), settings, ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), new MvcOptions(), json);
        var replacement = Replacement(settings, json, new RecordingLogger());

        foreach (var formatter in new NewtonsoftJsonInputFormatter[] { framework, replacement })
        {
            var http = Request("""{"name":"anything"}""");

            await FluentActions.Awaiting(() => formatter.ReadAsync(Context(http, typeof(NewtonsoftFaulty), new ModelStateDictionary(), string.Empty)))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("The converter is broken.");
            ValueObjectProblemDetails.GetErrorCodes(http).Should().BeEmpty();
        }

        replacement.ExceptionPolicy.Should().Be(framework.ExceptionPolicy);
    }

    /// <summary>
    /// The serializers are pooled, and one serves read after read. The handler of a read leaves with it: a read served by
    /// the serializer another read used records its codes on its own request alone.
    /// </summary>
    [Fact]
    public async Task A_pooled_serializer_records_the_codes_of_the_read_it_serves_alone()
    {
        var pools = new SingleInstancePoolProvider();
        var replacement = new ValueObjectNewtonsoftJsonInputFormatter(
            NullLogger(), new JsonSerializerSettings().AddValueObjects(), ArrayPool<char>.Shared, pools, new MvcOptions(), new MvcNewtonsoftJsonOptions());
        var first = Request("""{"reference":"no"}""");
        var second = Request("""{"quantity":1001}""");

        await replacement.ReadAsync(Context(first, typeof(NewtonsoftParcel), new ModelStateDictionary(), string.Empty));
        await replacement.ReadAsync(Context(second, typeof(NewtonsoftParcel), new ModelStateDictionary(), string.Empty));

        pools.Created.Should().Be(1, "both reads are served by one serializer");
        pools.Served.Should().Be(2);
        ValueObjectProblemDetails.GetErrorCodes(first).Should().Equal(new Dictionary<string, string>
        {
            ["reference"] = ValueObjectErrorCodes.TooShort,
        });
        ValueObjectProblemDetails.GetErrorCodes(second).Should().Equal(new Dictionary<string, string>
        {
            ["quantity"] = ValueObjectErrorCodes.OutOfRange,
        });
    }

    /// <summary>
    /// A serializer goes back to the pool without the handler of the read it served, so that the pool, which the
    /// formatter keeps for as long as the application runs, keeps nothing of a request once it is done with.
    /// </summary>
    [Fact]
    public async Task A_pooled_serializer_keeps_nothing_of_the_request_it_served()
    {
        var replacement = Replacement(new JsonSerializerSettings().AddValueObjects(), new MvcNewtonsoftJsonOptions(), NullLogger());

        var request = await Task.Run(() => ReadOnceAsync(replacement), TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 10 && request.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        request.IsAlive.Should().BeFalse("the request was read, and nothing the formatter holds refers to it");
        GC.KeepAlive(replacement);
    }

    /// <summary>
    /// The replacement is built over the MVC options being configured, as the framework's formatter is. Under
    /// <see cref="MvcOptions.SuppressInputFormatterBuffering"/>, both read the body as it comes, synchronously, which a
    /// server refusing synchronous reads refuses, where without it both buffer the body first and read it.
    /// </summary>
    [Fact]
    public async Task The_replacement_reads_under_the_application_s_MVC_options()
    {
        static void Unbuffered(MvcOptions options) => options.SuppressInputFormatterBuffering = true;
        using var unbuffered = Build(static mvc => mvc.AddMvcOptions(Unbuffered).AddNewtonsoftJson().AddValueObjectsNewtonsoftJson());
        using var unbufferedWithoutPackage = Build(static mvc => mvc.AddMvcOptions(Unbuffered).AddNewtonsoftJson());
        using var buffered = Build(static mvc => mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson());
        var framework = unbufferedWithoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .Single(static formatter => formatter.GetType() == typeof(NewtonsoftJsonInputFormatter));

        foreach (var formatter in new[] { framework, ReplacementOf(unbuffered) })
        {
            var http = new DefaultHttpContext { Request = { Body = new AsynchronousOnlyStream("""{"reference":"ABC-1"}"""), ContentType = "application/json" } };

            await FluentActions.Awaiting(() => formatter.ReadAsync(Context(http, typeof(NewtonsoftParcel), new ModelStateDictionary(), string.Empty)))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage(AsynchronousOnlyStream.Refusal);
        }

        var read = new DefaultHttpContext { Request = { Body = new AsynchronousOnlyStream("""{"reference":"ABC-1"}"""), ContentType = "application/json" } };
        var result = await ReplacementOf(buffered).ReadAsync(Context(read, typeof(NewtonsoftParcel), new ModelStateDictionary(), string.Empty));
        result.Model.Should().BeOfType<NewtonsoftParcel>().Which.Reference.Should().Be(Ordering.OrderReference.Create("ABC-1"));
    }

    /// <summary>
    /// The replacement logs what the framework's formatter logs, under the framework formatter's category, so that the
    /// application's filters on that category apply to it.
    /// </summary>
    [Fact]
    public async Task The_replacement_logs_under_the_framework_formatter_s_category()
    {
        var logs = new RecordingLoggerProvider();
        var frameworkLogs = new RecordingLoggerProvider();
        using var provider = Build(static mvc => mvc.AddNewtonsoftJson().AddValueObjectsNewtonsoftJson(), logs);
        using var withoutPackage = Build(static mvc => mvc.AddNewtonsoftJson(), frameworkLogs);
        var framework = withoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .Single(static formatter => formatter.GetType() == typeof(NewtonsoftJsonInputFormatter));

        await ReadAsync(framework, """{"reference":"no"}""", typeof(NewtonsoftParcel));
        await ReadAsync(ReplacementOf(provider), """{"reference":"no"}""", typeof(NewtonsoftParcel));

        logs.Entries.Should().Equal(frameworkLogs.Entries)
            .And.Equal($"{typeof(NewtonsoftJsonInputFormatter).FullName} Debug JsonInputException");
    }

    /// <summary>
    /// The value objects of the domain, one per underlying type and per hook, each record the code the converter gives
    /// a refusal of theirs.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    /// <returns>The check.</returns>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public Task A_refusal_of_every_value_object_records_its_code(string type)
        => Samples.All[type].RecordsTheCodeOfARefusedNewtonsoftBodyAsync();

    [Fact]
    public void Nothing_is_set_up_without_a_builder_or_options()
    {
        using var provider = Build(static mvc => mvc.AddNewtonsoftJson());

        FluentActions.Invoking(static () => ValueObjectNewtonsoftJsonMvcExtensions.AddValueObjectsNewtonsoftJson(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(() => new ValueObjectNewtonsoftJsonInputFormatterSetup(
                provider.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>(),
                provider.GetRequiredService<ILoggerFactory>(),
                ArrayPool<char>.Shared,
                new DefaultObjectPoolProvider())
            .PostConfigure(null, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(static () => new ValueObjectNewtonsoftJsonOptionsSetup().PostConfigure(null, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    /// <summary>
    /// Reads a body through a replacement over the settings the package gives MVC, and gives the codes it recorded.
    /// </summary>
    /// <param name="type">The type the body is read as.</param>
    /// <param name="body">The body.</param>
    /// <returns>The codes.</returns>
    internal static async Task<IReadOnlyDictionary<string, string>> ReadCodesAsync(Type type, string body)
    {
        var replacement = Replacement(new JsonSerializerSettings().AddValueObjects(), new MvcNewtonsoftJsonOptions(), new RecordingLogger());
        var (_, _, http) = await ReadAsync(replacement, body, type);

        return ValueObjectProblemDetails.GetErrorCodes(http);
    }

    private static ValueObjectNewtonsoftJsonInputFormatter Replacement(JsonSerializerSettings settings, MvcNewtonsoftJsonOptions json, ILogger logger)
        => new(logger, settings, ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), new MvcOptions(), json);

    private static ServiceProvider Build(Action<IMvcBuilder> configure, ILoggerProvider? logs = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            if (logs is not null)
            {
                logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs);
            }
        });
        configure(services.AddControllers());

        return services.BuildServiceProvider();
    }

    private static ValueObjectNewtonsoftJsonInputFormatter ReplacementOf(ServiceProvider provider)
        => provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters.OfType<ValueObjectNewtonsoftJsonInputFormatter>().Single();

    /// <summary>Reads a refused body, and gives a reference to its request, which the caller no longer holds.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> ReadOnceAsync(ValueObjectNewtonsoftJsonInputFormatter formatter)
    {
        var http = Request("""{"reference":"no"}""");

        await formatter.ReadAsync(Context(http, typeof(NewtonsoftParcel), new ModelStateDictionary(), string.Empty));
        ValueObjectProblemDetails.GetErrorCodes(http).Should().ContainKey("reference");

        return new WeakReference(http);
    }

    private static Microsoft.Extensions.Logging.Abstractions.NullLogger<NewtonsoftJsonInputFormatter> NullLogger()
        => Microsoft.Extensions.Logging.Abstractions.NullLogger<NewtonsoftJsonInputFormatter>.Instance;

    private static async Task<(InputFormatterResult Result, ModelStateDictionary ModelState, HttpContext Http)> ReadAsync(
        IInputFormatter formatter,
        string body,
        Type type)
    {
        var http = Request(body);
        var modelState = new ModelStateDictionary();

        var result = await formatter.ReadAsync(Context(http, type, modelState, string.Empty));

        return (result, modelState, http);
    }

    private static DefaultHttpContext Request(string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);

        return new DefaultHttpContext
        {
            Request =
            {
                Body = new MemoryStream(bytes),
                ContentLength = bytes.Length,
                ContentType = "application/json; charset=utf-8",
            },
        };
    }

    private static InputFormatterContext Context(HttpContext http, Type type, ModelStateDictionary modelState, string modelName)
        => new(
            http,
            modelName,
            modelState,
            new EmptyModelMetadataProvider().GetMetadataForType(type),
            static (stream, encoding) => new StreamReader(stream, encoding),
            treatEmptyInputAsDefaultValue: false);

    /// <summary>Every error of a model state, as text a client could tell apart.</summary>
    private static List<string> Describe(ModelStateDictionary modelState)
        => [.. modelState
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(static entry => entry.Value!.Errors.Select(error =>
                $"{entry.Key} | {error.ErrorMessage} | {error.Exception?.GetType().FullName} | {error.Exception?.Message}"))];

    private sealed class DerivedFormatter(MvcNewtonsoftJsonOptions json, MvcOptions options)
        : NewtonsoftJsonInputFormatter(NullLogger(), json.SerializerSettings, ArrayPool<char>.Shared, new DefaultObjectPoolProvider(), options, json);

    /// <summary>Pools that keep a single instance, and count how many they created and served.</summary>
    private sealed class SingleInstancePoolProvider : ObjectPoolProvider
    {
        public int Created { get; private set; }

        public int Served { get; private set; }

        public override ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy) => new Pool<T>(this, policy);

        private sealed class Pool<T>(SingleInstancePoolProvider provider, IPooledObjectPolicy<T> policy) : ObjectPool<T>
            where T : class
        {
            private T? _instance;

            public override T Get()
            {
                provider.Served++;
                if (_instance is { } instance)
                {
                    _instance = null;
                    return instance;
                }

                provider.Created++;
                return policy.Create();
            }

            public override void Return(T obj)
            {
                if (policy.Return(obj))
                {
                    _instance = obj;
                }
            }
        }
    }

    /// <summary>
    /// A request body that, as a server refusing synchronous reads, Kestrel's default, serves asynchronous reads only.
    /// </summary>
    /// <param name="body">The body.</param>
    private sealed class AsynchronousOnlyStream(string body) : MemoryStream(Encoding.UTF8.GetBytes(body))
    {
        public const string Refusal = "Synchronous operations are disallowed.";

        public override bool CanSeek => false;

        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException(Refusal);

        public override int Read(Span<byte> buffer) => throw new InvalidOperationException(Refusal);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(base.Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // The span overload of a derived memory stream calls the array overload, which refuses.
            var bytes = new byte[buffer.Length];
            var read = base.Read(bytes, 0, bytes.Length);
            bytes.AsSpan(0, read).CopyTo(buffer.Span);

            return ValueTask.FromResult(read);
        }
    }

    /// <summary>Records the category, the level and the event of everything logged.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<string> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Logger(RecordingLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => provider.Entries.Add($"{category} {logLevel} {eventId.Name}");
        }
    }

    /// <summary>Records what a formatter logs, as event and message.</summary>
    private sealed class RecordingLogger : ILogger
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add($"{logLevel} {eventId.Id} {eventId.Name}: {formatter(state, exception)} {exception?.GetType().Name}");
    }
}
