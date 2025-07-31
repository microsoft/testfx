// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Extensions;

namespace MSTest.TestAdapter;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestExtension : IExtension
{
    public string Uid { get; } = GetExtensionUid();

    public string DisplayName => "MSTest";

    public string Version => MSTestVersion.SemanticVersion;

    public string Description => "MSTest Framework for Microsoft Testing Platform";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private static string GetExtensionUid()
    {
        IEnumerable<AssemblyMetadataAttribute>? assemblyMetadataAttributes = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>();
        return assemblyMetadataAttributes?.FirstOrDefault(x => x.Key == "MSTest.Extension.Uid")?.Value ?? nameof(MSTestExtension);
    }
}
#endif
