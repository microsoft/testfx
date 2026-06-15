// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice;

[Embedded]
internal static class TargetFrameworkParser
{
    [return: NotNullIfNotNull(nameof(frameworkDescription))]
    public static string? GetShortTargetFramework(string? frameworkDescription)
    {
        if (frameworkDescription == null)
        {
            return null;
        }

        // https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription
        string netFramework = ".NET Framework";
        if (frameworkDescription.StartsWith(netFramework, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Framework 4.7.2
            if (frameworkDescription.Length < (netFramework.Length + 6))
            {
                return frameworkDescription;
            }

            char major = frameworkDescription[netFramework.Length + 1];
            char minor = frameworkDescription[netFramework.Length + 3];
            char patch = frameworkDescription[netFramework.Length + 5];

            if (major == '4' && minor == '6' && patch == '2')
            {
                return "net462";
            }
            else if (major == '4' && minor == '7' && patch == '1')
            {
                return "net471";
            }
            else if (major == '4' && minor == '7' && patch == '2')
            {
                return "net472";
            }
            else if (major == '4' && minor == '8' && patch == '1')
            {
                return "net481";
            }
            else
            {
                // Just return the first 2 numbers.
                return $"net{major}{minor}";
            }
        }

        string netCore = ".NET Core";
        if (frameworkDescription.StartsWith(netCore, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Core 3.1
            return frameworkDescription.Length >= (netCore.Length + 4)
                ? $"netcoreapp{frameworkDescription[netCore.Length + 1]}.{frameworkDescription[netCore.Length + 3]}"
                : frameworkDescription;
        }

        string net = ".NET";
        if (frameworkDescription.StartsWith(net, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            int firstDotInVersion = frameworkDescription.IndexOf('.', net.Length + 1);
            return firstDotInVersion < 1
                ? frameworkDescription
                : $"net{frameworkDescription.Substring(net.Length + 1, firstDotInVersion - net.Length - 1)}.{frameworkDescription[firstDotInVersion + 1]}";
        }

        return frameworkDescription;
    }

    /// <summary>
    /// Resolves the short target framework moniker of <paramref name="entryAssembly"/>, including the
    /// OS-platform component (e.g. <c>net8.0-windows10.0.18362.0</c>) when the assembly was built for an
    /// OS-specific TFM.
    /// </summary>
    /// <remarks>
    /// A plain <c>net8.0</c> build and a <c>net8.0-windows10.0.18362.0</c> build carry the exact same
    /// <see cref="TargetFrameworkAttribute"/> (<c>.NETCoreApp,Version=v8.0</c>) and produce the same
    /// <see cref="RuntimeInformation.FrameworkDescription"/>, so the short TFM alone cannot tell them apart.
    /// The only runtime-visible signal is <c>System.Runtime.Versioning.TargetPlatformAttribute</c>, which the
    /// SDK emits for any platform-specific TFM (including non-OS / custom platform identifiers such as Uno's
    /// <c>browserwasm</c>). Appending it here keeps report file names unique per build so two modules of the
    /// same assembly no longer overwrite each other's report.
    /// </remarks>
    public static string? GetShortTargetFrameworkIncludingPlatform(Assembly? entryAssembly)
    {
        string? shortTargetFramework = GetShortTargetFramework(entryAssembly?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName);

        // FrameworkDisplayName can be missing, empty, or whitespace for some assemblies (e.g. when the SDK
        // emits a TargetFrameworkAttribute without a display name for a custom TargetFrameworkIdentifier such
        // as Uno's net8.0-browserwasm). In that case GetShortTargetFramework echoes the empty value back rather
        // than returning null, so a plain null-coalesce would not fall back. Treat null/empty/whitespace alike
        // and fall back to the runtime description so the base moniker stays meaningful (e.g. net8.0) and we
        // never produce a dangling "-platform" name.
        if (RoslynString.IsNullOrWhiteSpace(shortTargetFramework))
        {
            shortTargetFramework = GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
        }

        return BuildTargetFrameworkMoniker(shortTargetFramework, GetTargetPlatformName(entryAssembly));
    }

    /// <summary>
    /// Combines a short target framework (e.g. <c>net8.0</c>) with an optional OS-platform name
    /// (e.g. <c>Windows10.0.18362.0</c>) into a full moniker (e.g. <c>net8.0-windows10.0.18362.0</c>).
    /// </summary>
    internal static string? BuildTargetFrameworkMoniker(string? shortTargetFramework, string? targetPlatformName)
        => RoslynString.IsNullOrEmpty(shortTargetFramework) || RoslynString.IsNullOrEmpty(targetPlatformName)
            ? shortTargetFramework
            : $"{shortTargetFramework}-{targetPlatformName!.ToLowerInvariant()}";

    /// <summary>
    /// Reads the OS-platform name from <c>System.Runtime.Versioning.TargetPlatformAttribute</c> on
    /// <paramref name="entryAssembly"/>, or <see langword="null"/> when the assembly targets no specific OS.
    /// </summary>
    internal static string? GetTargetPlatformName(Assembly? entryAssembly)
    {
        if (entryAssembly is null)
        {
            return null;
        }

        // TargetPlatformAttribute only exists in the .NET 5+ BCL, so read it by full type name through
        // CustomAttributeData to keep this method compiling for netstandard2.0 / net462 consumers.
        foreach (CustomAttributeData attribute in entryAssembly.GetCustomAttributesData())
        {
            if (string.Equals(attribute.AttributeType.FullName, "System.Runtime.Versioning.TargetPlatformAttribute", StringComparison.Ordinal)
                && attribute.ConstructorArguments.Count == 1
                && attribute.ConstructorArguments[0].Value is string platformName
                && platformName.Length > 0)
            {
                return platformName;
            }
        }

        return null;
    }
}
