// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal sealed class EnvironmentWrapper : IEnvironment
{
    public string MachineName => Environment.MachineName;
}
