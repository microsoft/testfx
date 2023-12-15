// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

#pragma warning disable SA1300 // We keep the lower case so we can use it in the command line without the needs of ToLowerInvariant()
public enum Verb
{
    build,
    publish,
}
