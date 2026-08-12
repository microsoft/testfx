#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryOrchestratorHelperTests
{
    [TestMethod]
    public void RemoveOption_AllSupportedForms_RemovesOccurrencesAndValues()
    {
        List<string> arguments =
        [
            "before",
            "--target",
            "long-value",
            "-target",
            "short-value",
            "--target=long-equals",
            "long-equals-trailing-value",
            "--target:long-colon",
            "long-colon-trailing-value",
            "-target=short-equals",
            "short-equals-trailing-value",
            "-target:short-colon",
            "short-colon-trailing-value",
            "--other",
            "after",
        ];

        RetryOrchestratorHelper.RemoveOption(arguments, "target");

        Assert.HasCount(3, arguments);
        Assert.AreEqual("before", arguments[0]);
        Assert.AreEqual("--other", arguments[1]);
        Assert.AreEqual("after", arguments[2]);
    }
}
