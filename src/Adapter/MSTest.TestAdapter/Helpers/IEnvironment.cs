// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

/// <summary>
/// Interface to abstract environment related information.
/// </summary>
internal interface IEnvironment
{
    /// <summary>
    /// Gets the machine name.
    /// </summary>
    string MachineName { get; }
}
