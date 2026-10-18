using System.Runtime.CompilerServices;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolverine.Http;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.Wolverine.Http.Binding;

/// <summary>
/// Puts the refusal frame first in the middleware of each endpoint the strategy read a value object for, and holds the
/// messages <c>UseValueObjects</c> named.
/// </summary>
internal sealed class ValueObjectRefusalPolicy : IHttpPolicy
{
    private const string Unwritable =
        "UseValueObjects() answers a refused value object with problem details the HTTP JSON options of this application "
        + "cannot serialize: no resolver of theirs knows HttpValidationProblemDetails or JsonElement, as in a native "
        + "binary, where reflection-based serialization is off. Call builder.Services.AddProblemDetails(), which chains "
        + "the framework's serializer context for problem details into them, or list both types in a "
        + "JsonSerializerContext chained into ConfigureHttpJsonOptions.";

    private readonly ConditionalWeakTable<HttpChain, List<ValueObjectReadFrame>> _reads = [];

    private bool _applied;

    private bool _checkedOptions;

    /// <summary>
    /// Gets or sets the messages the refusals are answered with, or <see langword="null"/> for the English ones of the
    /// rules.
    /// </summary>
    public ValueObjectMessageOptions? Messages { get; set; }

    /// <summary>Gets the reads of an endpoint, which the strategy adds to.</summary>
    /// <param name="chain">The endpoint.</param>
    /// <returns>Its reads, in the order the strategy created them.</returns>
    public List<ValueObjectReadFrame> ReadsOf(HttpChain chain) => _reads.GetOrCreateValue(chain);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// An endpoint reads a value object, and the HTTP JSON options cannot serialize the problem details a refusal is
    /// answered with.
    /// </exception>
    public void Apply(IReadOnlyList<HttpChain> chains, GenerationRules rules, IServiceContainer container)
    {
        _applied = true;
        foreach (var chain in chains)
        {
            Answer(chain, container);
        }
    }

    /// <summary>
    /// Puts the refusal frame first in the middleware of an endpoint the strategy read a value object for, once this
    /// policy runs: for every endpoint then, and for one a later policy gives its first read, through a middleware method
    /// it adds and matches the parameters of.
    /// </summary>
    /// <param name="chain">The endpoint.</param>
    /// <param name="container">The application's services.</param>
    /// <remarks>
    /// The frame holds the endpoint's list of reads, not a copy: JasperFx asks it for its variables when it writes the
    /// code, after every policy has run, so that a read a later policy adds is written before the answer too.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The HTTP JSON options cannot serialize the problem details a refusal is answered with.
    /// </exception>
    public void Answer(HttpChain chain, IServiceContainer container)
    {
        if (!_applied
            || !_reads.TryGetValue(chain, out var reads)
            || reads.Count == 0
            || chain.Middleware.OfType<ValueObjectRefusalFrame>().Any())
        {
            return;
        }

        if (!_checkedOptions)
        {
            EnsureProblemsCanBeWritten(container.Services);
            _checkedOptions = true;
        }

        chain.Middleware.Insert(0, new ValueObjectRefusalFrame(reads));
    }

    /// <summary>
    /// Fails the mapping of the endpoints, as the minimal API package fails the build of an endpoint, when the HTTP JSON
    /// options cannot serialize the validation problem and the <see cref="JsonElement"/> of its codes, rather than answer
    /// each refusal with a 500.
    /// </summary>
    private static void EnsureProblemsCanBeWritten(IServiceProvider services)
    {
        var options = (services.GetService<IOptions<HttpJsonOptions>>()?.Value ?? new HttpJsonOptions()).SerializerOptions;
        if (!options.TryGetTypeInfo(typeof(HttpValidationProblemDetails), out _) || !options.TryGetTypeInfo(typeof(JsonElement), out _))
        {
            throw new InvalidOperationException(Unwritable);
        }
    }
}
