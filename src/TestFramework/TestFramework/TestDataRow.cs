// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// When this type is returned from <see cref="ITestDataSource.GetData(MethodInfo)" /> (for example, through <see cref="DynamicDataAttribute" />), it
/// determines information related to the specific test case.
/// </summary>
/// <typeparam name="T">The type parameter corresponding to the type of the value held by this type. It can be a tuple for test methods with more than one parameter.</typeparam>
[DataContract]
public sealed class TestDataRow<T> : ITestDataRow
{
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataRow{T}"/> class.
    /// </summary>
    /// <param name="value">The value to be held by this instance, which could be a <see cref="Tuple"/> or <see cref="ValueTuple"/> if the test method has more than one parameter.</param>
    public TestDataRow(T value)
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved
        => Value = value;

    /// <summary>
    /// Gets the value held by this instance.
    /// </summary>
    [DataMember]
    public T Value { get; }

    /// <summary>
    /// Gets or sets the ignore message. A non-null value means the test case is ignored with the message provided.
    /// </summary>
    [DataMember]
    public string? IgnoreMessage { get; set; }

    /// <summary>
    /// Gets or sets the display name for the test case.
    /// </summary>
    [DataMember]
    public string? DisplayName { get; set; }

    /// <inheritdoc cref="Value"/>
    object? ITestDataRow.Value => Value;
}
