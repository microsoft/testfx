// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SampleFrameworkExtensions;

public sealed class DurationAttribute : TestPropertyAttribute
{
    public DurationAttribute(string duration)
        : base("Duration", duration) => Duration = duration;

    public string Duration { get; private set; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class CategoryArrayAttribute : Attribute
{
    public CategoryArrayAttribute(params string[] value) => Value = value;

    public string[] Value { get; private set; }
}
