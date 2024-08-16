// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Services;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IClientInfo
{
    /// <summary>
    /// Gets the client ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the client version.
    /// </summary>
    string Version { get; }
}
