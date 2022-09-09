// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// equality. This object will <b>always</b> throw with Assert.Fail. Please use
    /// Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A </param>
    /// <param name="objB"> Object B </param>
    /// <returns> False, always. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
    public static new bool Equals(object objA, object objB)
    {
        Fail(FrameworkMessages.DoNotUseAssertEquals);
        return false;
    }
}
