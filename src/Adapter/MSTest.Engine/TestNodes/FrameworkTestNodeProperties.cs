// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework;

internal readonly struct FrameworkEngineMetadataProperty() : IProperty
{
    public bool PreventArgumentsExpansion { get; init; }

    public string[] UsedFixtureIds { get; init; } = [];
}
