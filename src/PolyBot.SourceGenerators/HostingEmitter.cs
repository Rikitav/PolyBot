using Microsoft.CodeAnalysis;
using System.Reflection;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Emits the embedded static hosting sources gated on the consumer's references; the embedded
/// files are copied from generated output — edit them under <c>Embedded/</c>, not here.
/// </summary>
internal static class HostingEmitter
{
    private const string ResourcePrefix = "PolyBot.SourceGenerators.Embedded.";

    public static string? GenerateHosting(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostedService") is null ||
            compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection") is null)
        {
            return null;
        }

        return ReadResource("PolyBotHosting.g.cs");
    }

    public static string? GenerateWebhook(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions") is null)
        {
            return null;
        }

        // IEndpointRouteBuilder lives in Microsoft.AspNetCore.Builder up to ASP.NET Core 9
        // and moved to Microsoft.AspNetCore.Routing in .NET 10; emit the matching variant.
        if (compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder") is not null)
        {
            return ReadResource("PolyBotWebhook.Routing.g.cs");
        }

        if (compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.IEndpointRouteBuilder") is not null)
        {
            return ReadResource("PolyBotWebhook.Builder.g.cs");
        }

        return null;
    }

    private static string ReadResource(string fileName)
    {
        string fullName = ResourcePrefix + fileName;
        Assembly assembly = typeof(HostingEmitter).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(fullName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{fullName}' is missing from {assembly.GetName().Name}.");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
