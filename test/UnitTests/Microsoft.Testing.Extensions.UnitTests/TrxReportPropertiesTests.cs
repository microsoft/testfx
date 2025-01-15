// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.UnitTests;

[TestGroup]
public sealed class TrxReportPropertiesTests
{
    public void TrxMessagesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxMessagesProperty { Messages = [TrxMessage { Message = some message }] }",
            new TrxMessagesProperty([new("some message")]).ToString());

    public void TrxCategoriesProperty_ToStringIsCorrect()
        => Assert.AreEqual(
            "TrxCategoriesProperty { Categories = [some category] }",
            new TrxCategoriesProperty(["some category"]).ToString());
}
