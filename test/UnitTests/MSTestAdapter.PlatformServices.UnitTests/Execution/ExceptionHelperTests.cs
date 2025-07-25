// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.Tests.Execution;

/// <summary>
/// Tests for the ExceptionHelper class methods.
/// </summary>
public class ExceptionHelperTests : TestContainer
{
    public void TrimStackTraceShouldReturnEmptyStringForEmptyInput()
    {
        string result = ExceptionHelper.TrimStackTrace("");
        Verify(result == "");
    }

    public void TrimStackTraceShouldKeepFramesNotRelatedToUTF()
    {
        string stackTrace = "   at MyMethod()\r\n   at AnotherMethod()";
        string result = ExceptionHelper.TrimStackTrace(stackTrace);
        
        Verify(result.Contains("MyMethod"));
        Verify(result.Contains("AnotherMethod"));
    }

    public void TrimStackTraceShouldFilterUnitTestingFrameworkFrames()
    {
        string stackTrace = "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual()\r\n   at MyTest()";
        string result = ExceptionHelper.TrimStackTrace(stackTrace);
        
        Verify(!result.Contains("Microsoft.VisualStudio.TestTools.UnitTesting"));
        Verify(result.Contains("MyTest"));
    }

    public void TrimStackTraceShouldFilterTestAdapterFrames()
    {
        string stackTrace = "   at MyTest()\r\n   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SomeMethod()";
        string result = ExceptionHelper.TrimStackTrace(stackTrace);
        
        Verify(result.Contains("MyTest"));
        Verify(!result.Contains("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter"));
    }

    public void TrimStackTraceShouldHandleSingleLineWithoutNewline()
    {
        string stackTrace = "Single line without newline";
        string result = ExceptionHelper.TrimStackTrace(stackTrace);
        
        Verify(result.Contains("Single line without newline"));
    }

    public void TrimStackTraceShouldHandleEmptyLinesCorrectly()
    {
        string stackTrace = "   at Method1()\r\n\r\n   at Method2()";
        string result = ExceptionHelper.TrimStackTrace(stackTrace);
        
        Verify(result.Contains("Method1"));
        Verify(result.Contains("Method2"));
    }

    public void HasReferenceToUTFShouldReturnTrueForUnitTestingFramework()
    {
        string frame = "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual()";
        bool result = ExceptionHelper.HasReferenceToUTF(frame);
        
        Verify(result);
    }

    public void HasReferenceToUTFShouldReturnTrueForTestAdapter()
    {
        string frame = "   at Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.SomeMethod()";
        bool result = ExceptionHelper.HasReferenceToUTF(frame);
        
        Verify(result);
    }

    public void HasReferenceToUTFShouldReturnFalseForRegularFrames()
    {
        string frame = "   at MyNamespace.MyClass.MyMethod()";
        bool result = ExceptionHelper.HasReferenceToUTF(frame);
        
        Verify(!result);
    }

    public void HasReferenceToUTFSpanVersionShouldReturnTrueForUnitTestingFramework()
    {
        ReadOnlySpan<char> frame = "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual()".AsSpan();
        bool result = ExceptionHelper.HasReferenceToUTF(frame);
        
        Verify(result);
    }

    public void HasReferenceToUTFSpanVersionShouldReturnFalseForRegularFrames()
    {
        ReadOnlySpan<char> frame = "   at MyNamespace.MyClass.MyMethod()".AsSpan();
        bool result = ExceptionHelper.HasReferenceToUTF(frame);
        
        Verify(!result);
    }
}