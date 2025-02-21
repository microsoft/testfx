// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal sealed class TestArgumentsContext(object arguments, TestNode target)
{
    public object Arguments { get; } = arguments;

    public TestNode Target { get; } = target;
}
