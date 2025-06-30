// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Constants used throughout.
/// </summary>
internal static class FrameworkConstants
{
    internal const string PublicTypeObsoleteMessage = "We will remove or hide this type starting with v4. If you are using this type, reach out to our team on https://github.com/microsoft/testfx.";
    internal const string ThatPropertyObsoleteMessage = "The 'That' property is obsolete and will be removed in a future version. Use the 'Instance' property instead.";
    internal const string DoNotUseAssertEquals = "Assert.Equals should not be used for Assertions. Please use Assert.AreEqual & overloads instead.";
    internal const string DoNotUseAssertReferenceEquals = "Assert.ReferenceEquals should not be used for Assertions. Please use Assert.AreSame & overloads instead.";
    internal const string DoNotUseStringAssertEquals = "StringAssert.Equals should not be used for Assertions. Please use StringAssert methods or Assert.AreEqual & overloads instead.";
    internal const string DoNotUseStringAssertReferenceEquals = "StringAssert.ReferenceEquals should not be used for Assertions. Please use StringAssert methods or Assert.AreSame & overloads instead.";
    internal const string DoNotUseCollectionAssertEquals = "CollectionAssert.Equals should not be used for Assertions. Please use CollectionAssert.AreEqual & overloads instead.";
    internal const string DoNotUseCollectionAssertReferenceEquals = "CollectionAssert.ReferenceEquals should not be used for Assertions. Please use CollectionAssert methods or Assert.AreSame & overloads instead.";
}
