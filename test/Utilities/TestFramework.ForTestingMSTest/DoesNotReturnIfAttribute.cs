// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETSTANDARD2_0 || NETFRAMEWORK

#pragma warning disable SA1623 // Remove ridiculous stylecop documentation warning
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
///   Specifies that the method will not return if the associated <see cref="bool"/>
///   parameter is passed the specified value.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DoesNotReturnIfAttribute :
    Attribute
{
    /// <summary>
    ///   Gets the condition parameter value.
    ///   Code after the method is considered unreachable by diagnostics if the argument
    ///   to the associated parameter matches this value.
    /// </summary>
    public bool ParameterValue { get; }

    /// <summary>
    ///   Initializes a new instance of the <see cref="DoesNotReturnIfAttribute"/>
    ///   class with the specified parameter value.
    /// </summary>
    /// <param name="parameterValue">
    ///   The condition parameter value.
    ///   Code after the method is considered unreachable by diagnostics if the argument
    ///   to the associated parameter matches this value.
    /// </param>
    public DoesNotReturnIfAttribute(bool parameterValue) =>
        ParameterValue = parameterValue;
}

#endif
