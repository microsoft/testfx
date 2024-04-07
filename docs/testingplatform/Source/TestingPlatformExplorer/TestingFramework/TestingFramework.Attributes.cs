// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestingPlatformExplorer.TestingFramework;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class TestMethodAttribute : Attribute
{
    public TestMethodAttribute(bool skip = false)
    {
        Skip = skip;
    }

    public bool Skip { get; }
}
