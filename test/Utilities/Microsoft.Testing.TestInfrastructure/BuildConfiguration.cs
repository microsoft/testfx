// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1602 // Enumeration items should be documented

namespace Microsoft.Testing.TestInfrastructure;

public enum BuildConfiguration
{
    Debug,
    Release,
}

public enum Verb
{
    Build,
    Publish,
}
