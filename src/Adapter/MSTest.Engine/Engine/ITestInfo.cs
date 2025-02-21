// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework;

public interface ITestInfo
{
    TestNodeUid StableUid { get; }

    string DisplayName { get; }

    IProperty[] Properties { get; }
}
