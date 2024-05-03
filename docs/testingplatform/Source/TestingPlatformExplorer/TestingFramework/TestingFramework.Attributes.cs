// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestingPlatformExplorer.TestingFramework;

[AttributeUsage(AttributeTargets.Method)]
public class SkipAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public class TestMethodAttribute : Attribute
{
}
