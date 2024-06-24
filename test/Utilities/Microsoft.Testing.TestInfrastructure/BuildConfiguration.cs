// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public enum BuildConfiguration
{
#if FAST_ACCEPTANCE_TEST
#if DEBUG
    Debug,
#else
    Release,
#endif
#else
    Debug,
    Release,
#endif
}
