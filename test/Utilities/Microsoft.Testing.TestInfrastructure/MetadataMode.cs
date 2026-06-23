// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// The mechanism a generated acceptance-test asset uses to obtain test metadata at runtime.
/// Acceptance assets are built (and exercised) once per mode so the same behavioral assertions
/// validate both the runtime reflection path and the <c>MSTest.SourceGeneration</c> path.
/// </summary>
public enum MetadataMode
{
    /// <summary>
    /// The default build: metadata is discovered through runtime reflection.
    /// Output lands under <c>bin/&lt;config&gt;/&lt;tfm&gt;</c>.
    /// </summary>
    Reflection,

    /// <summary>
    /// A second build with the <c>MSTest.SourceGeneration</c> package injected so the
    /// source-generated <c>ReflectionMetadataHook</c> provides metadata.
    /// Output lands under <c>bin/SourceGen/&lt;config&gt;/&lt;tfm&gt;</c>.
    /// </summary>
    SourceGeneration,
}
