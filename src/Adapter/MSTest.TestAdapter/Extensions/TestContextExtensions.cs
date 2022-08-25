﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    internal static class TestContextExtensions
    {
        /// <summary>
        /// Returns diagnostic messages written to test context and clears from this instance.
        /// </summary>
        /// <param name="testContext">The test context instance.</param>
        /// <returns>The diagnostic messages.</returns>
        internal static string GetAndClearDiagnosticMessages(this ITestContext testContext)
        {
            var messages = testContext.GetDiagnosticMessages();

            testContext.ClearDiagnosticMessages();

            return messages;
        }
    }
}
