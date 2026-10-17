using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Looks up the message of a refused value object in a resource the application names, through a
/// <see cref="ResourceManager"/> over the resource's type, with no service provider.
/// </summary>
/// <remarks>
/// This file is linked into the contracts, whose DataAnnotations attributes <c>[ParsesAs]</c> and <c>[NotDefault]</c> look
/// a message up under the resource their <c>ErrorMessageResourceType</c> names alone, and into the System.CommandLine
/// package, whose options and arguments look one up under the resource <c>UseResource&lt;T&gt;()</c> names: neither has
/// services to resolve an <c>IStringLocalizerFactory</c> from, which <c>ValueObjectLocalizedMessages.cs</c> needs. Each
/// compiles its own copy. It reads no annotation and closes no generic type at run time, so it runs in a native binary.
/// </remarks>
internal static class ValueObjectResourceMessages
{
    /// <summary>The resource managers of the resource types named, one per type, shared by every caller of a copy.</summary>
    private static readonly ConcurrentDictionary<Type, ResourceManager> Managers = new();

    /// <summary>
    /// Looks the message of a refusal up in a resource, under each key in turn, in the current UI culture, formatted in the
    /// current culture.
    /// </summary>
    /// <param name="resource">The resource type the application names.</param>
    /// <param name="keys">The keys, the most specific first.</param>
    /// <param name="arguments">Gets the arguments of the rule, quoted from <c>{1}</c>; asked only for a message found.</param>
    /// <param name="member">The member, quoted as <c>{0}</c>.</param>
    /// <returns>The message, or <see langword="null"/> when the resource holds none for the culture.</returns>
    /// <remarks>
    /// The resource is read through a <see cref="ResourceManager"/> over the type, the base name <c>IStringLocalizerFactory</c>
    /// reads while <c>LocalizationOptions.ResourcesPath</c> is unset, so that the resource an application hands MVC's and
    /// the minimal APIs' <c>UseResource&lt;T&gt;()</c> serves these callers too, with no service provider. A resource
    /// with no neutral culture throws <see cref="MissingManifestResourceException"/> for a key its cultures lack, as it
    /// falls back to the neutral one, which the framework's localizer swallows too: the next key is looked up, and the
    /// English message stands when none is found. A template quoting an argument its code does not supply throws
    /// <see cref="FormatException"/>, the application's error to see.
    /// </remarks>
    public static string? Find(
        Type resource,
        IReadOnlyList<string> keys,
        Func<IReadOnlyList<KeyValuePair<string, object?>>> arguments,
        string member)
    {
        var manager = Managers.GetOrAdd(resource, static type => new ResourceManager(type));
        foreach (var key in keys)
        {
            string? template;
            try
            {
                template = manager.GetString(key, CultureInfo.CurrentUICulture);
            }
            catch (MissingManifestResourceException)
            {
                continue;
            }

            if (template is null)
            {
                continue;
            }

            var supplied = arguments();
            var positional = new object?[supplied.Count + 1];
            positional[0] = member;
            for (var index = 0; index < supplied.Count; index++)
            {
                positional[index + 1] = supplied[index].Value;
            }

            return string.Format(CultureInfo.CurrentCulture, template, positional);
        }

        return null;
    }
}
