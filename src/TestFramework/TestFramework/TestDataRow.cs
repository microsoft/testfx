// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// When this type is returned from <see cref="ITestDataSource.GetData(MethodInfo)" /> (for example, through <see cref="DynamicDataAttribute" />), it
/// determines information related to the specific test case.
/// </summary>
/// <remarks>
/// To use this type from a custom <see cref="ITestDataSource"/>, wrap each instance in a single-element <see cref="object"/> array,
/// for example <c>new object[] { new TestDataRow&lt;int&gt;(42) { DisplayName = "my row" } }</c>. MSTest unwraps the
/// <see cref="Value"/> to obtain the test method arguments and applies the <see cref="DisplayName"/>,
/// <see cref="IgnoreMessage"/>, and <see cref="TestCategories"/> metadata to the generated test case.
/// </remarks>
/// <typeparam name="T">The type parameter corresponding to the type of the value held by this type. It can be a tuple for test methods with more than one parameter.</typeparam>
[DataContract]
public sealed class TestDataRow<T> : ITestDataRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataRow{T}"/> class.
    /// </summary>
    /// <param name="value">The value to be held by this instance, which could be a <c>Tuple</c> or <c>ValueTuple</c> if the test method has more than one parameter.</param>
    public TestDataRow(T value)
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

    /// <summary>
    /// Gets or sets the test categories for the test case.
    /// </summary>
    [DataMember]
    public IList<string>? TestCategories { get; set; }

    /// <inheritdoc cref="Value"/>
    object? ITestDataRow.Value => Value;
}
