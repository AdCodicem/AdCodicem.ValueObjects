using System.Globalization;
using AdCodicem.ValueObjects.Blazor;
using AdCodicem.ValueObjects.Fixtures.Blazor;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>
/// <see cref="Blazor.InputValueObject{TValue}"/> in a form, through bUnit: the text a person types read through the value
/// object's rules, the normalized instance bound, a refusal shown under the field with its rule's message and kept on the
/// form with its code, and the text an instance is written as, in the culture the renderer calls for.
/// </summary>
public sealed class InputValueObjectTests
{
    private const string PrintedIban = "fr76 3000 6000 0112 3456 7890 189";

    private static readonly Iban Electronic = Iban.Create("FR7630006000011234567890189");

    [Fact]
    public void Accepted_text_binds_the_normalized_instance_which_the_input_then_shows()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban>());

        field.Type(PrintedIban);

        field.Model.Should().Be(Electronic);
        field.Text.Should().Be("FR7630006000011234567890189");
        field.Classes.Should().Be("modified valid");
        field.Invalid.Should().BeFalse();
        field.Messages.Should().BeEmpty();
        field.Code.Should().BeNull();
    }

    [Fact]
    public void Refused_text_keeps_the_model_stays_in_the_field_and_is_answered_with_the_rule_and_its_code()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban> { Value = Electronic });

        field.Type("XX");

        field.Model.Should().Be(Electronic);
        field.Text.Should().Be("XX");
        field.Classes.Should().Be("modified invalid");
        field.Invalid.Should().BeTrue();
        field.Messages.Should().Equal("The value must be at least 15 characters long.");
        field.Shown.Should().Equal("The value must be at least 15 characters long.");
        field.Summary.Should().Equal("The value must be at least 15 characters long.");
        field.Code.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Theory]
    [InlineData("1001", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("abc", ValueObjectErrorCodes.NotParsable)]
    public void A_refusal_carries_the_message_and_the_code_of_the_rule_TryParse_reports_and_never_the_text(string text, string code)
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Quantity> { Value = Quantity.Create(3) });
        Quantity.TryParse(text, CultureInfo.InvariantCulture, out _, out var expected).Should().BeFalse();

        field.Type(text);

        field.Model.Should().Be(Quantity.Create(3));
        field.Code.Should().Be(code);
        field.Messages.Should().Equal(expected.ErrorMessage);
        field.Messages.Single().Should().NotContain(text);
    }

    [Fact]
    public void A_closed_set_refuses_an_unknown_value_and_binds_a_known_one_normalized()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<CountryCode>());

        field.Type("XX");
        field.Code.Should().Be(ValueObjectErrorCodes.NotAKnownValue);
        field.Messages.Should().Equal("The value is not one of the accepted values.");

        field.Type(" be ");
        field.Model.Should().Be(CountryCode.Belgium);
        field.Text.Should().Be("BE");
        field.Code.Should().BeNull();
    }

    [Fact]
    public void The_code_of_a_refusal_is_cleared_by_the_next_text_accepted()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban>());

        field.Type("XX");
        field.Code.Should().Be(ValueObjectErrorCodes.TooShort);
        field.Type(PrintedIban);

        field.Code.Should().BeNull();
        field.Messages.Should().BeEmpty();
    }

    [Fact]
    public void The_code_of_a_refusal_is_cleared_when_a_nullable_field_is_emptied()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban?>());

        field.Type("XX");
        field.Code.Should().Be(ValueObjectErrorCodes.TooShort);
        field.Type(string.Empty);

        field.Model.Should().BeNull();
        field.Text.Should().BeNull();
        field.Code.Should().BeNull();
        field.Messages.Should().BeEmpty();
    }

    [Fact]
    public void The_code_of_a_refusal_is_cleared_when_the_input_is_disposed()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban>());
        field.Type("XX");

        field.Rendered.Render(parameters => parameters.Add(static form => form.ShowInput, false));

        field.Code.Should().BeNull();
        field.Messages.Should().BeEmpty();
    }

    [Fact]
    public void The_code_of_a_refusal_is_cleared_once_the_input_shows_the_model_again_whichever_way_the_text_was_set()
    {
        using var context = FormsHarness.Context();
        var model = new Holder<Iban?>();
        var (form, input, rendered) = context.Typed(model);
        var field = new FieldIdentifier(model, nameof(Holder<Iban?>.Value));

        rendered.InvokeAsync(() => input.Type("XX"));
        form.TryGetValueObjectErrorCode(field, out var refused).Should().BeTrue();
        refused.Should().Be(ValueObjectErrorCodes.TooShort);

        // Empty text bound to a nullable value object is never parsed: the input formats the model again as it re-renders.
        rendered.InvokeAsync(() => input.Type(string.Empty));

        form.TryGetValueObjectErrorCode(field, out _).Should().BeFalse();
        rendered.Find("input").GetAttribute("value").Should().BeNull();
    }

    /// <summary>
    /// A handler of <see cref="EditContext.OnFieldChanged"/>, a telemetry hook, reads the code of the text just read while
    /// the field notifies its change, never the code of the text before: whether the text comes from the change event,
    /// which empties a nullable field without parsing, or from a subclass setting text it parses. A subclass emptying a
    /// nullable field is the one case <see cref="Microsoft.AspNetCore.Components.Forms.InputBase{TValue}"/> calls no
    /// member of the input for before notifying.
    /// </summary>
    [Fact]
    public void A_handler_of_the_form_s_field_changes_reads_the_code_of_the_text_just_read()
    {
        using var context = FormsHarness.Context();

        var field = context.Form(new Holder<Iban?>());
        var changed = Watch(field.Rendered.Instance.Context);
        field.Type("XX");
        field.Type(PrintedIban);
        field.Type("XX");
        field.Type(string.Empty);
        changed.Should().Equal(ValueObjectErrorCodes.TooShort, null, ValueObjectErrorCodes.TooShort, null);

        var model = new Holder<Iban?>();
        var (form, input, rendered) = context.Typed(model);
        var typed = Watch(form);
        rendered.InvokeAsync(() => input.Type("XX"));
        rendered.InvokeAsync(() => input.Type(PrintedIban));
        typed.Should().Equal(ValueObjectErrorCodes.TooShort, null);

        // Empty text a subclass sets on a nullable field reaches no member of the input before the form notifies the
        // change: the earlier code is read then, and cleared once the input shows the model again.
        rendered.InvokeAsync(() => input.Type("XX"));
        rendered.InvokeAsync(() => input.Type(string.Empty));
        typed.Should().Equal(ValueObjectErrorCodes.TooShort, null, ValueObjectErrorCodes.TooShort, ValueObjectErrorCodes.TooShort);
        form.TryGetValueObjectErrorCode(new FieldIdentifier(model, nameof(Holder<Iban?>.Value)), out _).Should().BeFalse();
    }

    [Fact]
    public void An_input_outside_a_form_reads_its_text_and_keeps_no_code()
    {
        using var context = FormsHarness.Context();
        var model = new Holder<Iban>();
        var rendered = context.Render<ValueObjectForm<Iban>>(parameters => parameters
            .Add(static form => form.Model, model)
            .Add(static form => form.WithoutForm, true));

        rendered.Find("input").Change(PrintedIban);
        model.Value.Should().Be(Electronic);
        rendered.Find("input").Change("XX");

        model.Value.Should().Be(Electronic);
        rendered.Find("input").GetAttribute("value").Should().Be("XX");
        rendered.Find("input").GetAttribute("class").Should().BeNull();
    }

    [Theory]
    [InlineData("IBAN", "The IBAN field is required.")]
    [InlineData(null, "The Value field is required.")]
    public void Empty_text_a_type_refuses_is_answered_as_a_required_field_and_keeps_the_model(string? displayName, string message)
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban> { Value = Electronic }, parameters => parameters.Add(static form => form.DisplayName, displayName));

        field.Type(string.Empty);

        field.Model.Should().Be(Electronic);
        field.Text.Should().BeEmpty();
        field.Invalid.Should().BeTrue();
        field.Messages.Should().Equal(message);
        field.Code.Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void Empty_text_is_required_even_where_the_parser_would_call_it_unreadable()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<CustomerId>(), parameters => parameters.Add(static form => form.DisplayName, "Customer"));

        field.Type(string.Empty);

        field.Code.Should().Be(ValueObjectErrorCodes.Required);
        field.Messages.Should().Equal("The Customer field is required.");
    }

    [Fact]
    public void Empty_text_binds_null_to_a_nullable_value_object_with_no_message()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban?> { Value = Electronic });

        field.Type(string.Empty);

        field.Model.Should().BeNull();
        field.Messages.Should().BeEmpty();
        field.Code.Should().BeNull();
    }

    [Fact]
    public void Empty_text_binds_the_empty_instance_of_a_type_that_accepts_it_not_its_default()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Label> { Value = Label.Create("draft") });

        field.Type(string.Empty);

        field.Model.Should().Be(Label.Create(string.Empty));
        ((IValueObject<Label, string>)field.Model).IsDefault.Should().BeFalse();
        field.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Statically_rendered_text_is_read_and_written_in_the_invariant_culture()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var context = FormsHarness.Context(FormsHarness.Static);
            var field = context.Form(new Holder<Amount> { Value = Amount.Create(12.5m) });

            field.Text.Should().Be("12.50");
            field.Type("13,5");
            field.Code.Should().Be(ValueObjectErrorCodes.NotParsable);
            field.Type("13.5");
            field.Model.Should().Be(Amount.Create(13.5m));
        }
    }

    [Fact]
    public void Interactive_text_is_read_and_written_in_the_current_culture_prerendered_or_not()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var interactive = FormsHarness.Context(FormsHarness.Interactive);
            var field = interactive.Form(new Holder<Amount> { Value = Amount.Create(12.5m) });
            field.Text.Should().Be("12,50");
            field.Type("13,5");
            field.Model.Should().Be(Amount.Create(13.5m));

            // A component an interactive page holds is prerendered statically first, with its render mode assigned.
            using var prerendered = FormsHarness.Context(FormsHarness.Static);
            var model = new Holder<Amount> { Value = Amount.Create(12.5m) };
            var rendered = prerendered.Render<ValueObjectForm<Amount>>(parameters => parameters
                .Add(static form => form.Model, model)
                .SetAssignedRenderMode(RenderMode.InteractiveServer));
            rendered.Find("input").GetAttribute("value").Should().Be("12,50");
            rendered.Find("input").Change("14,5");
            model.Value.Should().Be(Amount.Create(14.5m));
        }
    }

    [Fact]
    public void The_culture_given_wins_over_the_renderer()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            using var interactive = FormsHarness.Context(FormsHarness.Interactive);
            var invariant = interactive.Form(
                new Holder<Amount> { Value = Amount.Create(12.5m) },
                static parameters => parameters.Add(static form => form.Culture, CultureInfo.InvariantCulture));
            invariant.Text.Should().Be("12.50");
            invariant.Type("13,5");
            invariant.Code.Should().Be(ValueObjectErrorCodes.NotParsable);

            using var statically = FormsHarness.Context(FormsHarness.Static);
            var french = statically.Form(
                new Holder<Amount> { Value = Amount.Create(12.5m) },
                static parameters => parameters.Add(static form => form.Culture, new CultureInfo("fr-FR")));
            french.Type("13,5");
            french.Model.Should().Be(Amount.Create(13.5m));
        }
    }

    /// <summary>
    /// A date is read in the Gregorian calendar its round-trip text is written in, whatever the culture's own: the Thai
    /// Buddhist calendar of <c>th-TH</c> would read <c>2000-01-01</c> 543 years earlier, the Persian one of <c>fa-IR</c> 621
    /// years later, and the Um al-Qura one of <c>ar-SA</c> not at all. The culture's own patterns stay, a year typed being
    /// a Gregorian one, and a date alone typed into a <see cref="DateTime"/> field is read the same way.
    /// </summary>
    [Theory]
    [InlineData("th-TH")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    public void A_date_is_read_in_the_Gregorian_calendar_it_is_written_in_whatever_the_culture_s_own(string culture)
    {
        using (FormsHarness.Culture(culture))
        {
            CultureInfo.CurrentCulture.Calendar.Should().NotBeOfType<GregorianCalendar>();
            using var context = FormsHarness.Context(FormsHarness.Interactive);

            RoundTrip(context, BirthDate.Create(new DateOnly(1990, 10, 14)), "1990-10-14");
            RoundTrip(context, EffectiveDate.Create(new DateOnly(2000, 1, 1)), "2000-01-01");
            RoundTrip(context, RecordedAt.Create(new DateTime(2026, 10, 14, 10, 30, 15, 250, DateTimeKind.Utc)), "2026-10-14T10:30:15.2500000Z");
            RoundTrip(context, OccurredAt.Create(new DateTimeOffset(2026, 10, 14, 10, 30, 15, 250, TimeSpan.FromHours(2))), "2026-10-14T10:30:15.2500000+02:00");
            RoundTrip(context, Holiday.Create(new DateOnly(2026, 10, 14)), "2026-10-14");

            var gregorian = (CultureInfo)CultureInfo.CurrentCulture.Clone();
            gregorian.DateTimeFormat.Calendar = new GregorianCalendar();
            var effective = context.Form(new Holder<EffectiveDate>());
            effective.Type(new DateOnly(2000, 1, 2).ToString("d", gregorian));
            effective.Model.Should().Be(EffectiveDate.Create(new DateOnly(2000, 1, 2)));

            var recorded = context.Form(new Holder<RecordedAt>());
            recorded.Type("2000-01-01");
            recorded.Model.Should().Be(RecordedAt.Create(new DateTime(2000, 1, 1)));
            recorded.Code.Should().BeNull();

            var occurred = context.Form(new Holder<OccurredAt>());
            occurred.Type("2026-10-14");
            occurred.Model.Value.Date.Should().Be(new DateTime(2026, 10, 14));
            occurred.Code.Should().BeNull();
        }
    }

    /// <summary>
    /// In <c>th-TH</c>, the input reads the text it shows and the culture's day-first pattern as the same Gregorian date,
    /// and a Buddhist year typed as a Gregorian one, which a type bounded to plausible birth dates refuses, as the guide
    /// says.
    /// </summary>
    [Fact]
    public void A_Thai_form_reads_a_Gregorian_year_in_the_culture_s_own_pattern_and_a_Buddhist_year_as_a_Gregorian_one()
    {
        using (FormsHarness.Culture("th-TH"))
        {
            using var context = FormsHarness.Context(FormsHarness.Interactive);
            var born = context.Form(new Holder<BirthDate>());

            born.Type("14/10/1990");
            born.Model.Should().Be(BirthDate.Create(new DateOnly(1990, 10, 14)));
            born.Text.Should().Be("1990-10-14");

            born.Type("14/10/2533");
            born.Code.Should().Be(ValueObjectErrorCodes.OutOfRange);
            born.Model.Should().Be(BirthDate.Create(new DateOnly(1990, 10, 14)));
        }
    }

    [Fact]
    public void An_instance_nobody_filled_in_shows_as_empty_text_and_a_zero_its_rules_accept_as_itself()
    {
        using var context = FormsHarness.Context();

        context.Form(new Holder<Iban>()).Text.Should().BeEmpty();
        context.Form(new Holder<PageNumber>()).Text.Should().BeEmpty();
        context.Form(new Holder<Quantity>()).Text.Should().Be("0");
        context.Form(new Holder<Celsius>()).Text.Should().Be("0");
        context.Form(new Holder<PageNumber?>()).Text.Should().BeNull();
    }

    [Fact]
    public void A_type_whose_formatting_hook_writes_other_text_shows_its_underlying_value_which_reads_back()
    {
        using var context = FormsHarness.Context();

        context.Form(new Holder<Celsius> { Value = Celsius.Create(21) }).Text.Should().Be("21");
        context.Form(new Holder<Floor> { Value = Floor.Create(3) }).Text.Should().Be("3");
        context.Form(new Holder<PhoneNumber> { Value = PhoneNumber.Create("+33123456789") }).Text.Should().Be("+33123456789");
        context.Form(new Holder<Iban?> { Value = Electronic }).Text.Should().Be("FR7630006000011234567890189");

        var temperature = context.Form(new Holder<Celsius> { Value = Celsius.Create(21) });
        temperature.Type(temperature.Text!);
        temperature.Model.Should().Be(Celsius.Create(21));
        temperature.Messages.Should().BeEmpty();
    }

    /// <summary>
    /// A type whose string formatter alone writes other text, and a date, a time, a duration whose hook drops the seconds,
    /// the fraction, the kind or the offset, show their bare underlying value in its round-trip form, which reads back to
    /// the same instance in a static form and in an interactive one.
    /// </summary>
    [Fact]
    public void A_hooked_type_shows_its_underlying_value_in_the_form_its_parser_reads_back_in_a_static_and_an_interactive_form()
    {
        using (FormsHarness.Culture("fr-FR"))
        {
            foreach (var renderer in new[] { FormsHarness.Static, FormsHarness.Interactive })
            {
                using var context = FormsHarness.Context(renderer);

                Hooked(context, Storey.Create(3), "storey 3", "3");
                Hooked(context, Holiday.Create(new DateOnly(2026, 10, 14)), "14 October 2026", "2026-10-14");
                Hooked(context, Departure.Create(new TimeOnly(10, 30, 15, 250)), "10:30", "10:30:15.2500000");
                Hooked(context, StampedAt.Create(new DateTime(2026, 10, 14, 10, 30, 15, 250, DateTimeKind.Utc)), "2026-10-14 10:30", "2026-10-14T10:30:15.2500000Z");
                Hooked(context, SignedAt.Create(new DateTimeOffset(2026, 10, 14, 10, 30, 15, 250, TimeSpan.FromHours(2))), "2026-10-14 10:30 +02:00", "2026-10-14T10:30:15.2500000+02:00");
                Hooked(context, Lapse.Create(new TimeSpan(0, 1, 1, 1, 1)), "1:01", "01:01:01.0010000");

                var stamped = context.Form(new Holder<StampedAt>());
                stamped.Type("2026-10-14T10:30:15.2500000Z");
                stamped.Model.Value.Kind.Should().Be(DateTimeKind.Utc);
            }
        }
    }

    /// <summary>
    /// A date, a time and a duration are written in the round-trip form of their type, which the framework's form mapping
    /// reads back in a static form, the fraction of a second and the kind of a <see cref="DateTime"/> included.
    /// </summary>
    [Fact]
    public void Dates_times_and_durations_are_written_in_their_round_trip_form_and_read_back_exactly()
    {
        using var context = FormsHarness.Context();

        RoundTrip(context, BirthDate.Create(new DateOnly(1990, 10, 14)), "1990-10-14");
        RoundTrip(context, OpeningTime.Create(new TimeOnly(10, 30, 15, 250)), "10:30:15.2500000");
        RoundTrip(context, RecordedAt.Create(new DateTime(2026, 10, 14, 10, 30, 15, 250, DateTimeKind.Utc)), "2026-10-14T10:30:15.2500000Z");
        RoundTrip(context, OccurredAt.Create(new DateTimeOffset(2026, 10, 14, 10, 30, 15, 250, TimeSpan.FromHours(2))), "2026-10-14T10:30:15.2500000+02:00");
        RoundTrip(context, Duration.Create(new TimeSpan(0, 1, 1, 1, 1)), "01:01:01.0010000");
    }

    [Fact]
    public void A_submit_is_refused_while_refused_text_shows_and_accepted_once_it_is_fixed()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<Iban> { Value = Electronic });

        field.Type("XX");
        field.Submit();
        field.Rendered.Instance.Invalid.Should().Be(1);
        field.Summary.Should().Equal("The value must be at least 15 characters long.");

        field.Type(PrintedIban);
        field.Submit();
        field.Rendered.Instance.Valid.Should().Be(1);
        field.Summary.Should().BeEmpty();
    }

    [Fact]
    public void A_parser_refusing_without_a_reason_or_with_an_empty_message_is_answered_as_ParsesAs_answers_it()
    {
        using var context = FormsHarness.Context();
        var field = context.Form(new Holder<UnexplainedCode>(), static parameters => parameters.Add(static form => form.DisplayName, "Code"));

        field.Type("unexplained");
        field.Code.Should().Be(ValueObjectErrorCodes.NotParsable);
        field.Messages.Should().Equal("The text could not be parsed as UnexplainedCode.");

        field.Type("quiet");
        field.Code.Should().Be(UnexplainedCode.QuietCode);
        field.Messages.Should().Equal("The field Code is invalid.");

        field.Type("loud");
        field.Model.Should().Be(UnexplainedCode.Create("loud"));
    }

    [Fact]
    public void A_construction_of_a_generic_value_object_and_one_written_by_hand_are_described_by_the_registry()
    {
        using var context = FormsHarness.Context();

        var reference = context.Form(new Holder<Reference<FormsOwner>>());
        reference.Type(" po-1042 ");
        reference.Model.Should().Be(Reference<FormsOwner>.Create("PO-1042"));
        reference.Type("ABCDEFGHIJKLMNOP");
        reference.Code.Should().Be(ValueObjectErrorCodes.TooLong);

        var ticket = context.Form(new Holder<Declared<FormsTicket, string>?>());
        ticket.Type("T-12");
        ticket.Model.Should().Be(Declared<FormsTicket, string>.Create("T-12"));
        ticket.Type("T-1234567");
        ticket.Code.Should().Be(ValueObjectErrorCodes.TooLong);
        ticket.Type("12");
        ticket.Code.Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_type_the_registry_does_not_describe_fails_the_first_render_naming_the_registration()
    {
        using var context = FormsHarness.Context();

        var render = () => context.Form(new Holder<string>());

        render.Should().Throw<InvalidOperationException>()
            .WithMessage("InputValueObject binds 'String', which is not a value object the registry describes.*ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema)*before the input renders.");
    }

    [Fact]
    public void The_attributes_of_the_rules_are_written_only_on_request()
    {
        using var context = FormsHarness.Context();

        context.Form(new Holder<Iban>()).Attributes.Keys.Should().NotContain(["maxlength", "minlength", "pattern"]);

        var attributes = context.Form(new Holder<Iban>(), static parameters => parameters.Add(static form => form.SchemaAttributes, true)).Attributes;
        attributes.Should().Contain("maxlength", "34").And.Contain("minlength", "15").And.Contain("pattern", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
    }

    [Fact]
    public void The_attributes_of_the_rules_are_written_the_first_time_an_input_asks_for_them()
    {
        using var context = FormsHarness.Context();
        context.Form(new Holder<Declared<CountedTicket, string>>()).Type("T-1");
        var reads = CountedTicket.Reads;

        var attributes = context.Form(new Holder<Declared<CountedTicket, string>>(), static parameters => parameters.Add(static form => form.SchemaAttributes, true)).Attributes;

        CountedTicket.Reads.Should().BeGreaterThan(reads);
        attributes.Should().Contain("maxlength", "8");
    }

    [Fact]
    public void An_attribute_the_input_is_given_wins_over_the_one_its_rules_give()
    {
        using var context = FormsHarness.Context();

        var field = context.Form(new Holder<Iban?>(), static parameters => parameters
            .Add(static form => form.SchemaAttributes, true)
            .Add(static form => form.InputAttributes, new Dictionary<string, object> { ["maxlength"] = "40", ["id"] = "iban" }));

        field.Attributes.Should().Contain("maxlength", "40").And.Contain("minlength", "15").And.Contain("id", "iban");
    }

    [Fact]
    public void A_type_with_no_rule_an_attribute_holds_gets_none()
    {
        using var context = FormsHarness.Context();

        var field = context.Form(new Holder<Quantity>(), static parameters => parameters.Add(static form => form.SchemaAttributes, true));

        field.Attributes.Keys.Should().BeEquivalentTo("name", "class", "value");
    }

    [Fact]
    public void The_input_captures_its_element()
    {
        using var context = FormsHarness.Context();
        var model = new Holder<Iban?>();
        var (_, input, _) = context.Typed(model);

        input.Element.Should().NotBeNull();
    }

    private static void Hooked<TSelf>(BunitContext context, TSelf value, string printed, string text)
        where TSelf : struct
    {
        value.ToString().Should().Be(printed);
        RoundTrip(context, value, text);
    }

    /// <summary>Records the code the form keeps for each field whose change it notifies, when it notifies it.</summary>
    private static List<string?> Watch(EditContext form)
    {
        List<string?> codes = [];
        form.OnFieldChanged += (_, changed) => codes.Add(form.TryGetValueObjectErrorCode(changed.FieldIdentifier, out var code) ? code : null);
        return codes;
    }

    private static void RoundTrip<TSelf>(BunitContext context, TSelf value, string text)
        where TSelf : struct
    {
        var field = context.Form(new Holder<TSelf> { Value = value });
        field.Text.Should().Be(text);

        var other = context.Form(new Holder<TSelf>());
        other.Type(text);
        other.Model.Should().Be(value);
        other.Messages.Should().BeEmpty();
    }
}
