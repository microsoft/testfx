// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.UnitTests;

[TestClass]
public sealed class TrxReportPropertiesTests
{
    [TestMethod]
    public void TrxMessagesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxMessagesProperty { Messages = [StandardOutputTrxMessage { Message = some message }] }",
            new TrxMessagesProperty([new StandardOutputTrxMessage("some message")]).ToString());

    [TestMethod]
    public void TrxCategoriesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxCategoriesProperty { Categories = [some category] }",
            new TrxCategoriesProperty(["some category"]).ToString());
}
