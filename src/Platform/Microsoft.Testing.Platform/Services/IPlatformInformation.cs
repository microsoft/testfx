// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

namespace Microsoft.Testing.Platform.Services;

public interface IPlatformInformation
{
    /// <summary>
    /// Gets the platform's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the platform's build date.
    /// </summary>
    DateTimeOffset? BuildDate { get; }

    /// <summary>
    /// Gets the platform's version.
    /// </summary>
    string? Version { get; }

    /// <summary>
    /// Gets the platform's commit hash.
    /// </summary>
    string? CommitHash { get; }
}

internal sealed class PlatformInformation : IPlatformInformation
{
    private const char PlusSign = '+';
    private const string BuildTimeAttributeName = "Microsoft.Testing.Platform.Application.BuildTimeUTC";

    public PlatformInformation()
    {
        var assembly = Assembly.GetExecutingAssembly();
        if (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() is { } version)
        {
            string informationalVersion = version.InformationalVersion;
            int index = informationalVersion.LastIndexOf(PlusSign);
            if (index != -1)
            {
                Version = informationalVersion[..index];
                CommitHash = informationalVersion[(index + 1)..];
            }
            else
            {
                Version = informationalVersion;
            }
        }

        AssemblyMetadataAttribute? buildTime = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(x => x.Key == BuildTimeAttributeName);

        if (!RoslynString.IsNullOrEmpty(buildTime?.Value)
            && DateTimeOffset.TryParse(buildTime.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date))
        {
            BuildDate = date;
        }
    }

    public string Name { get; } = ".NET Testing Platform";

    public DateTimeOffset? BuildDate { get; }

    public string? Version { get; }

    public string? CommitHash { get; }
}
