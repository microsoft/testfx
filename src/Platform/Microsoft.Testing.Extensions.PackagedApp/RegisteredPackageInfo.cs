// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed class RegisteredPackageInfo(string fullName, string installedPath, bool isDevelopmentMode)
{
    public string FullName { get; } = fullName;

    public string InstalledPath { get; } = installedPath;

    public bool IsDevelopmentMode { get; } = isDevelopmentMode;
}
