// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

using MessageLevel = Microsoft.VisualStudio.TestTools.UnitTesting.MessageLevel;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

/// <summary>
/// Pins the diagnostics and the edge-application rules of the <c>testconfig.json</c> dependency declarations that
/// the sibling <see cref="TestDependencyDeclarationTests"/> leave unobserved.
/// </summary>
public sealed class TestDependencyDeclarationMutationTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";

    private static UnitTestElement CreateElement(string className, string methodName)
        => new(new TestMethod(methodName, className, "DummyAssembly", displayName: null));

    private static string InvalidReference(string reference)
        => string.Format(CultureInfo.CurrentCulture, Resource.DependencyConfigurationInvalidReference, reference);

    private static string DependentNotFound(string reference)
        => string.Format(CultureInfo.CurrentCulture, Resource.DependencyConfigurationDependentNotFound, reference);

    public void ReportUnmatchedDeclarations_WithoutALogger_DoesNotDereferenceIt()
    {
        // Both declarations would need a diagnostic, so falling through the null-logger guard dereferences null.
        Action act = () => TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [
                new TestDependencyDeclaration("PlaceOrder", $"{ClassA}.Setup", proceedOnFailure: false),
                new TestDependencyDeclaration("Ns.Nowhere.Test", $"{ClassA}.Setup", proceedOnFailure: false),
            ],
            [CreateElement(ClassA, "Setup")],
            logger: null);

        act.Should().NotThrow();
    }

    public void ReportUnmatchedDeclarations_WhenOnlyThePrerequisiteIsMalformed_ReportsThePrerequisite()
    {
        // The dependent is well formed and matches a test, so the malformed prerequisite is the only thing to
        // say - and it has to be said, because the declaration is otherwise silently ignored.
        var logger = new RecordingLogger();

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", "Setup", proceedOnFailure: false)],
            [CreateElement(ClassA, "PlaceOrder")],
            logger);

        logger.Warnings.Should().Equal(InvalidReference("Setup"));
    }

    public void ReportUnmatchedDeclarations_WhenThePrerequisiteIsMalformed_DoesNotAlsoJudgeTheDependent()
    {
        // A declaration that is already rejected as malformed must produce one diagnostic, not a second
        // "matches no test" one for the half that happens to be well formed.
        var logger = new RecordingLogger();

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration("Ns.Nowhere.Test", "Setup", proceedOnFailure: false)],
            [CreateElement(ClassA, "Setup")],
            logger);

        logger.Warnings.Should().Equal(InvalidReference("Setup"));
    }

    public void ReportUnmatchedDeclarations_RejectsAReferenceWithoutAClassName()
    {
        // ".Setup" has a dot, but nothing in front of it: it names no class, so it is not a usable reference.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        var dependentLogger = new RecordingLogger();
        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration(".PlaceOrder", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            dependentLogger);
        dependentLogger.Warnings.Should().Equal(InvalidReference(".PlaceOrder"));

        var prerequisiteLogger = new RecordingLogger();
        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", ".Setup", proceedOnFailure: false)],
            tests,
            prerequisiteLogger);
        prerequisiteLogger.Warnings.Should().Equal(InvalidReference(".Setup"));
    }

    public void ReportUnmatchedDeclarations_RejectsAReferenceWithoutAMethodName()
    {
        // "Ns.ClassA." ends on the separator: it names a class and an empty method, which is neither a method
        // reference nor the "Ns.ClassA.*" class wildcard.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        var dependentLogger = new RecordingLogger();
        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration($"{ClassA}.", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            dependentLogger);
        dependentLogger.Warnings.Should().Equal(InvalidReference($"{ClassA}."));

        var prerequisiteLogger = new RecordingLogger();
        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", $"{ClassA}.", proceedOnFailure: false)],
            tests,
            prerequisiteLogger);
        prerequisiteLogger.Warnings.Should().Equal(InvalidReference($"{ClassA}."));
    }

    public void ReportUnmatchedDeclarations_AcceptsAWellFormedReferenceThatMatchesATest()
    {
        // The counterpart of the rejection cases above: the shortest valid shapes - a one-character class and a
        // one-character method, and the wildcard - are accepted and, since the dependent exists, silent.
        var logger = new RecordingLogger();

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [
                new TestDependencyDeclaration("C.M", "C.*", proceedOnFailure: false),
                new TestDependencyDeclaration("C.*", "C.M", proceedOnFailure: false),
            ],
            [CreateElement("C", "M")],
            logger);

        logger.Warnings.Should().BeEmpty();
    }

    public void ReportUnmatchedDeclarations_WhenADependentMatchesNothing_NamesTheDependent()
    {
        var logger = new RecordingLogger();

        TestDependencyDeclaration.ReportUnmatchedDeclarations(
            [new TestDependencyDeclaration("Ns.Nowhere.Test", $"{ClassA}.Setup", proceedOnFailure: false)],
            [CreateElement(ClassA, "Setup")],
            logger);

        logger.Warnings.Should().Equal(DependentNotFound("Ns.Nowhere.Test"));
    }

    public void ApplyAll_WhenTheDependentIsMalformed_IgnoresTheDeclaration()
    {
        // The prerequisite is fine, so the only thing standing between the declaration and the tests is the
        // rejection of the malformed dependent.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration("PlaceOrder", $"{ClassA}.Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeFalse();
        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().BeNull();
    }

    public void ApplyAll_WhenThePrerequisiteIsMalformed_IgnoresTheDeclaration()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.PlaceOrder", "Setup", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeFalse();
        tests[0].Dependencies.Should().BeNull();
        tests[1].Dependencies.Should().BeNull();
    }

    public void ApplyAll_WhenBothSidesCoverAWholeClass_KeepsTheClassWideEdgeOnEveryTest()
    {
        // The self-edge suppression is only for a *specific* prerequisite. A whole-class prerequisite is handed to
        // the graph as is, which removes each test's own self-edge itself; dropping it here would silently discard
        // the whole declaration.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.*", $"{ClassA}.*", proceedOnFailure: false)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeTrue();
        foreach (UnitTestElement test in tests)
        {
            test.Dependencies.Should().ContainSingle();
            test.Dependencies![0].TargetClassFullName.Should().Be(ClassA);
            test.Dependencies[0].TargetMethodName.Should().BeNull();
        }
    }

    public void ApplyAll_WhenASpecificTestNamesItself_KeepsTheExplicitSelfEdge()
    {
        // The user wrote the self reference by name, so it must reach the graph and surface there as a cycle,
        // rather than being swallowed as if it were a wildcard artifact.
        UnitTestElement[] tests = [CreateElement(ClassA, "Setup"), CreateElement(ClassA, "PlaceOrder")];

        bool applied = TestDependencyDeclaration.ApplyAll(
            [new TestDependencyDeclaration($"{ClassA}.Setup", $"{ClassA}.Setup", proceedOnFailure: true)],
            tests,
            adapterMessageLogger: null);

        applied.Should().BeTrue();
        tests[0].Dependencies.Should().ContainSingle();
        TestDependencyInfo selfEdge = tests[0].Dependencies![0];
        selfEdge.TargetClassFullName.Should().Be(ClassA);
        selfEdge.TargetMethodName.Should().Be("Setup");
        selfEdge.ProceedOnFailure.Should().BeTrue();
        tests[1].Dependencies.Should().BeNull();
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
}
