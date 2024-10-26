// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    [TestClass]
    public class ExceptionHelperTests
    {
        [TestMethod]
        public void GetFormattedExceptionMessage_WithCyclicExceptions_ShouldNotLoopInfinitely()
        {
            // Arrange
            var exceptionA = new Exception("Exception A");
            var exceptionB = new Exception("Exception B", exceptionA);
            
            // Use reflection to set exceptionA's InnerException to exceptionB, creating a cycle
            var innerExceptionField = typeof(Exception).GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic);
            innerExceptionField.SetValue(exceptionA, exceptionB);

            // Act
            string formattedMessage = ExceptionHelper.GetFormattedExceptionMessage(exceptionA);

            // Assert
            StringAssert.Contains(formattedMessage, "[Cyclic Exception Reference]");
        }

        [TestMethod]
        public void GetFormattedExceptionMessage_WithNoCyclicExceptions_ShouldFormatNormally()
        {
            // Arrange
            var exceptionA = new Exception("Exception A");
            var exceptionB = new Exception("Exception B", exceptionA);

            // Act  
            string formattedMessage = ExceptionHelper.GetFormattedExceptionMessage(exceptionB);

            // Assert
            StringAssert.Contains(formattedMessage, "Exception A");
            StringAssert.Contains(formattedMessage, "Exception B");
            StringAssert.DoesNotMatch(formattedMessage, new Regex(@"\[Cyclic Exception Reference\]"));
        }
    }
}
