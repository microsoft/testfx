// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using TestFramework.ForTestingMSTest;

using MessageLevel = Microsoft.VisualStudio.TestTools.UnitTesting.MessageLevel;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

/// <summary>
/// Tests for the <c>testconfig.json</c> side of test dependencies - the edges declared outside the test
/// source, which have to end up indistinguishable from attribute-declared ones.
/// </summary>
public sealed class TestDependencyDeclarationTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";

    private static UnitTestElement CreateElement(string className, string methodName)
        => new(new TestMethod(methodName, className, "DummyAssembly", displayName: null));

    public void ApplyAll_AttachesTheDeclaredEdgeToTheNamedTest()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeTrue();
        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().ContainSingle();
        tests[1].Dependencies![0].TargetMethodName.Should().Be("Setup");
    }

    public void ApplyAll_WhenAWildcardDependentCoversThePrerequisite_DoesNotMakeItDependOnItself()
    {
        // "Ns.ClassA.*" expands onto every test of the class, including Setup - the prerequisite itself. The
        // declaration means "every test of this class waits for Setup", so that generated self-edge has to be
        // dropped, exactly as discovery drops it for a class-level [DependsOn]. Keeping it would report Setup
        // as a cycle and skip the whole class.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.*", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().ContainSingle();
        tests[1].Dependencies![0].TargetMethodName.Should().Be("Setup");
    }

    public void ApplyAll_WhenAWildcardDependentTargetsAnotherClass_KeepsEveryEdge()
    {
        // The self-edge suppression must not fire across classes: a same-named method elsewhere is a genuine
        // prerequisite.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.*", "Ns.Other.Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        tests[0].Dependencies.Should().ContainSingle();
        tests[0].Dependencies![0].TargetClassFullName.Should().Be("Ns.Other");
        tests[0].Dependencies![0].TargetMethodName.Should().Be("Setup");
        tests[1].Dependencies.Should().ContainSingle();
        tests[1].Dependencies![0].TargetClassFullName.Should().Be("Ns.Other");
        tests[1].Dependencies![0].TargetMethodName.Should().Be("Setup");
    }

    public void ApplyAll_WhenTheDependentMatchesNothing_ReportsNothingApplied()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration("Ns.Missing.Test", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeFalse();
        tests[0].Dependencies.Should().BeNull();
    }

    public void ReportUnmatchedDeclarations_JudgesTheDependentAgainstTheWholeRun()
    {
        // ApplyAll runs once per source, so it cannot tell "names a test in another assembly" from "names
        // nothing at all". Diagnosing per source would report every valid declaration as unmatched once for
        // each *other* assembly in the run; this pass sees them all at once.
        var logger = new RecordingLogger();
        UnitTestElement[] assemblyA = [CreateElement(ClassA, "Setup")];
        UnitTestElement[] assemblyB = [CreateElement("Ns.ClassB", "Other")];

        TestDependencyDeclaration[] declarations =
        [
            new TestDependencyDeclaration($"{ClassA}.Setup", "Ns.ClassB.Other", proceedOnFailure: false),
        ];

        TestDependencyDeclaration.ReportUnmatchedDeclarations(declarations, [.. assemblyA, .. assemblyB], logger);

        logger.Warnings.Should().BeEmpty("the dependent exists in the run, just not in every source");
    }

    public void ReportUnmatchedDeclarations_StillWarnsWhenNothingInTheRunMatches()
    {
        var logger = new RecordingLogger();
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup")];

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration("Ns.Nowhere.Test", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            logger);

        logger.Warnings.Should().ContainSingle();
        logger.Warnings[0].Should().Contain("Ns.Nowhere.Test");
    }

    public void ReportUnmatchedDeclarations_WarnsOnAMalformedReference()
    {
        var logger = new RecordingLogger();

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration("PlaceOrder", "Setup", proceedOnFailure: false)],
            [CreateElement(ClassA, "PlaceOrder")],
            logger);

        // Naming the offending reference is the whole value of the diagnostic - a bare count would still
        // pass if the message pointed at the wrong one. Both parts of this declaration are malformed; the
        // dependent is checked first, so it is the one that must be reported.
        logger.Warnings.Should().ContainSingle();
        logger.Warnings[0].Should().Contain("PlaceOrder");
    }

    private sealed class RecordingLogger : IAdapterMessageLogger
    {
        public List<string> Warnings { get; } = [];

        public void SendMessage(MessageLevel level, string message)
        {
            if (level == MessageLevel.Warning)
            {
                Warnings.Add(message);
            }
        }
    }

    public void ApplyAll_WhenAReferenceIsMalformed_SkipsThatDeclaration()
    {
        // A bare identifier could be a class or a method; guessing would point the edge at the wrong thing.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration("PlaceOrder", "Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeFalse();
        tests[1].Dependencies.Should().BeNull();
    }

    public void ApplyAll_PreservesProceedOnFailure()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "Audit")];

        TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.Audit", $"{ClassA}.Setup", proceedOnFailure: true)],
            tests,
            adapterMessageLogger: null);

        tests[1].Dependencies![0].ProceedOnFailure.Should().BeTrue();
    }
}
