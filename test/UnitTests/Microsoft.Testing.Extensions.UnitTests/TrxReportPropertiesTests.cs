// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.UnitTests;

[TestClass]
public sealed class TrxReportPropertiesTests
{
    [TestMethod]
    public void TrxMessagesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxMessagesProperty { Messages = [StandardOutputTrxMessage { Message = first message }, StandardErrorTrxMessage { Message = second message }] }",
            new TrxMessagesProperty(
                [
                    new StandardOutputTrxMessage("first message"),
                    new StandardErrorTrxMessage("second message"),
                ]).ToString());

    [TestMethod]
    public void TrxCategoriesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxCategoriesProperty { Categories = [first category, second category] }",
            new TrxCategoriesProperty(["first category", "second category"]).ToString());

    [TestMethod]
    public void TrxWorkItemsProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxWorkItemsProperty { WorkItemIds = [123, 456] }",
            new TrxWorkItemsProperty(["123", "456"]).ToString());
}
