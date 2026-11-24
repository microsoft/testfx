// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public sealed class StackTraceInformationMutationTests : TestContainer
{
    public void Constructor_WithOnlyAStackTrace_HasNoSourceLocation()
    {
        var information = new StackTraceInformation("at Ns.Cls.Method()");

        information.ErrorStackTrace.Should().Be("at Ns.Cls.Method()");
        information.ErrorFilePath.Should().BeNull();
        information.ErrorLineNumber.Should().Be(0);
        information.ErrorColumnNumber.Should().Be(0);
    }

    public void Constructor_WithASourceLocation_KeepsEveryPart()
    {
        var information = new StackTraceInformation("at Ns.Cls.Method()", "C:\\src\\Cls.cs", 42, 7);

        information.ErrorStackTrace.Should().Be("at Ns.Cls.Method()");
        information.ErrorFilePath.Should().Be("C:\\src\\Cls.cs");
        information.ErrorLineNumber.Should().Be(42);
        information.ErrorColumnNumber.Should().Be(7);
    }

#if DEBUG && !NETFRAMEWORK
    public void Constructor_WithValidArguments_DoesNotAssert()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new StackTraceInformation("at Ns.Cls.Method()", "C:\\src\\Cls.cs", 0, 0);
        _ = new StackTraceInformation("at Ns.Cls.Method()");

        assertions.Should().BeEmpty();
    }

    public void Constructor_WithAnEmptyStackTrace_ReportsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new StackTraceInformation(string.Empty);

        assertions.Should().Equal("StackTrace message should not be empty");
    }

    public void Constructor_WithANegativeLineNumber_ReportsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new StackTraceInformation("at Ns.Cls.Method()", "C:\\src\\Cls.cs", -1, 0);

        assertions.Should().Equal("Line number should be greater than or equal to 0");
    }

    public void Constructor_WithANegativeColumnNumber_ReportsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new StackTraceInformation("at Ns.Cls.Method()", "C:\\src\\Cls.cs", 0, -1);

        assertions.Should().Equal("Column number should be greater than or equal to 0");
    }

    public void Constructor_WithEverythingInvalid_ReportsEachProblemInOrder()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new StackTraceInformation(string.Empty, null, -1, -1);

        assertions.Should().Equal(
            "StackTrace message should not be empty",
            "Line number should be greater than or equal to 0",
            "Column number should be greater than or equal to 0");
    }
#endif
}
