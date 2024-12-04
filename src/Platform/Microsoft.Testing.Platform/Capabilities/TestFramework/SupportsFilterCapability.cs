// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

public class SupportsFilterCapability<TExecutionFilter> : ISupportsFilterCapability
{
    public Type FilterType { get; } = typeof(TExecutionFilter);
}
