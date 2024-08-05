// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal sealed class VSTestProperty(TestProperty property, TestCase testCase) : IProperty
{
    public TestProperty Property { get; } = property;

    public TestCase TestCase { get; } = testCase;

    // We don't want to pay the allocation if we're not printing it for instance inside the logs.
    // So we go to get the property and ToString() it only if needed.
    public override string ToString()
    {
        object? value = TestCase.GetPropertyValue(Property);
        return $"VSTestProperty [Id: {Property.Id}] [Description: {Property.Description}] [ValueType: {Property.ValueType}] [Value: {value ?? "null"}]";
    }
}
