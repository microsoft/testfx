// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerTests : TestContainer
{
    private readonly Dictionary<string, object?> _testRunParameters;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    public UnitTestRunnerTests()
    {
        _testRunParameters = [];
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _mockMessageLogger = new Mock<IMessageLogger>();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    private TestMethod CreateTestMethod(string methodName, string typeFullName, string assemblyName, string? displayName)
        => new(methodName, null, methodName, typeFullName, assemblyName, displayName, null);

    private UnitTestRunner CreateUnitTestRunner(UnitTestElement[] testsToRun)
        => new(GetSettingsWithDebugTrace(false), testsToRun);

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    #region Constructor tests

    public void ConstructorShouldPopulateSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <TestTimeout>12</TestTimeout>
              </MSTest>
            </RunSettings>
            """;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                actualReader.Read();
                actualReader.ReadInnerXml();
            });

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        adapterSettings.Should().NotBeNull();
        var assemblyEnumerator = new UnitTestRunner(adapterSettings, []);

        MSTestSettings.CurrentSettings.TestTimeout.Should().Be(12);
    }

    #endregion

    #region RunSingleTest tests

    public async Task RunSingleTestShouldThrowIfTestMethodIsNull()
    {
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([]);
        Func<Task> func = () => unitTestRunner.RunSingleTestAsync(null!, null!, null!);
        await func.Should().ThrowAsync<ArgumentNullException>();
    }

    public async Task RunSingleTestShouldThrowIfTestRunParametersIsNull()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);
        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        Func<Task> func = () => unitTestRunner.RunSingleTestAsync(unitTestElement, null!, null!);
        await func.Should().ThrowAsync<ArgumentNullException>();
    }

    public async Task RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
    {
        TestMethod testMethod = CreateTestMethod("M", "C", "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.NotFound);
        results[0].IgnoreReason.Should().Be("Test method M was not found.");
    }

    public async Task ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be("IgnoreTestClassMessage");
    }

    public async Task ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClass);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be(string.Empty);
    }

    public async Task ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be("IgnoreTestMessage");
    }

    public async Task ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTest);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be(string.Empty);
    }

    public async Task ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be("IgnoreTestClassMessage");
    }

    public async Task ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Ignored);
        results[0].IgnoreReason.Should().Be("IgnoreTestMessage");
    }

    public async Task RunSingleTestShouldReturnTestResultIndicatingFailureIfThereIsAnyTypeInspectionExceptionWhenInspectingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        TestMethod testMethod = CreateTestMethod("ImaginaryTestMethod", type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Error);
        results[0].IgnoreReason.Should().Be(expectedMessage);
    }

    public async Task RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        results[0].IgnoreReason.Should().BeNull();
    }

    public async Task RunSingleTestShouldSetTestsAsInProgressInTestContext()
    {
        Type type = typeof(DummyTestClass);
        MethodInfo methodInfo = type.GetMethod("TestMethodToTestInProgress")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        // Asserting in the test method execution flow itself.
        var unitTestElement = new UnitTestElement(testMethod);
        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement]);
        TestResult[] results = await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        results.Should().NotBeNull();
        results.Length.Should().Be(1);
        results[0].Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestShouldCallAssemblyInitializeAndClassInitializeMethodsInOrder()
    {
        var mockReflectHelper = new Mock<ReflectHelper>();

        Type type = typeof(DummyTestClassWithInitializeMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        TestMethod testMethod = CreateTestMethod(methodInfo.Name, type.FullName!, "A", displayName: null);
        var unitTestElement = new UnitTestElement(testMethod);
        var unitTestRunner = new UnitTestRunner(new MSTestSettings(), [unitTestElement], mockReflectHelper.Object);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInitialize")!))
            .Returns(true);

        int validator = 1;
        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => validator <<= 2;
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => validator >>= 2;

        await unitTestRunner.RunSingleTestAsync(unitTestElement, _testRunParameters, null!);

        validator.Should().Be(1);
    }

    public async Task RunSingleTestWhenAssemblyAndClassInitializeAlreadyPassedShouldTakeFastPathAndSkipContextAllocation()
    {
        var mockReflectHelper = new Mock<ReflectHelper>();

        Type type = typeof(DummyTestClassWithInitializeMethods);
        TestMethod testMethod1 = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);
        TestMethod testMethod2 = CreateTestMethod("TestMethod2", type.FullName!, "A", displayName: null);
        // A third (never executed) test keeps the class "incomplete" so that running the second
        // test does not trigger class/assembly cleanup, isolating the init fast-path allocations.
        TestMethod testMethod3 = CreateTestMethod("TestMethod3", type.FullName!, "A", displayName: null);
        var unitTestElement1 = new UnitTestElement(testMethod1);
        var unitTestElement2 = new UnitTestElement(testMethod2);
        var unitTestElement3 = new UnitTestElement(testMethod3);
        var unitTestRunner = new UnitTestRunner(new MSTestSettings(), [unitTestElement1, unitTestElement2, unitTestElement3], mockReflectHelper.Object);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInitialize")!))
            .Returns(true);

        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => { };
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => { };

        // First test in the assembly/class goes through the slow path, which allocates dedicated
        // contexts for assembly initialize and class initialize.
        TestResult[] firstResults = await unitTestRunner.RunSingleTestAsync(unitTestElement1, _testRunParameters, null!);
        firstResults[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        int contextsAllocatedForFirstRun = _testablePlatformServiceProvider.GetTestContextCallCount;

        // Second test takes both the assembly-init and class-init fast paths, so neither an
        // assembly-init nor a class-init context is allocated, and the test still passes.
        TestResult[] secondResults = await unitTestRunner.RunSingleTestAsync(unitTestElement2, _testRunParameters, null!);
        secondResults[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        int contextsAllocatedForSecondRun = _testablePlatformServiceProvider.GetTestContextCallCount - contextsAllocatedForFirstRun;

        // The fast-path run allocates strictly fewer TestContext instances than the slow-path run
        // because it skips both the assembly-init and class-init contexts.
        contextsAllocatedForSecondRun.Should().BeLessThan(contextsAllocatedForFirstRun);
    }

    public async Task RunSingleTestWhenClassInitializeAlreadyFailedShouldTakeFastPathAndReturnCachedFailure()
    {
        Type type = typeof(DummyTestClassWithFailingClassInitialize);
        TestMethod testMethod1 = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);
        TestMethod testMethod2 = CreateTestMethod("TestMethod2", type.FullName!, "A", displayName: null);
        // A third (never executed) test keeps the class "incomplete" so that running the second
        // test does not trigger class/assembly cleanup, isolating the init fast-path allocations.
        TestMethod testMethod3 = CreateTestMethod("TestMethod3", type.FullName!, "A", displayName: null);
        var unitTestElement1 = new UnitTestElement(testMethod1);
        var unitTestElement2 = new UnitTestElement(testMethod2);
        var unitTestElement3 = new UnitTestElement(testMethod3);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestRunner unitTestRunner = CreateUnitTestRunner([unitTestElement1, unitTestElement2, unitTestElement3]);

        // First test in the class runs class initialize through the slow path (allocating a
        // dedicated class-init context) and it fails.
        TestResult[] firstResults = await unitTestRunner.RunSingleTestAsync(unitTestElement1, _testRunParameters, null!);
        firstResults[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        int contextsAllocatedForFirstRun = _testablePlatformServiceProvider.GetTestContextCallCount;

        // Second test takes the class-init fast path: it returns the cached failure clone without
        // allocating a class-init context, while still reporting the cached failure outcome.
        TestResult[] secondResults = await unitTestRunner.RunSingleTestAsync(unitTestElement2, _testRunParameters, null!);
        secondResults[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        int contextsAllocatedForSecondRun = _testablePlatformServiceProvider.GetTestContextCallCount - contextsAllocatedForFirstRun;

        // The fast-path run allocates strictly fewer TestContext instances than the slow-path run
        // because it skips the class-init context.
        contextsAllocatedForSecondRun.Should().BeLessThan(contextsAllocatedForFirstRun);
    }

    public async Task RunSingleTestShouldDeferClassCleanupContextAllocationToLastTestInClass()
    {
        Type type = typeof(DummyTestClassWithCleanupMethods);
        TestMethod testMethod1 = CreateTestMethod("TestMethod", type.FullName!, "A", displayName: null);
        TestMethod testMethod2 = CreateTestMethod("TestMethod2", type.FullName!, "A", displayName: null);
        TestMethod testMethod3 = CreateTestMethod("TestMethod3", type.FullName!, "A", displayName: null);
        var unitTestElement1 = new UnitTestElement(testMethod1);
        var unitTestElement2 = new UnitTestElement(testMethod2);
        var unitTestElement3 = new UnitTestElement(testMethod3);
        var unitTestRunner = new UnitTestRunner(new MSTestSettings(), [unitTestElement1, unitTestElement2, unitTestElement3]);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A"))
            .Returns(Assembly.GetExecutingAssembly());

        DummyTestClassWithCleanupMethods.ClassCleanupMethodBody = () => { };
        DummyTestClassWithCleanupMethods.AssemblyCleanupMethodBody = () => { };

        // Run the first test to warm up assembly/class initialize (slow path, sets cached results).
        TestResult[] firstResults = await unitTestRunner.RunSingleTestAsync(unitTestElement1, _testRunParameters, null!);
        firstResults[0].Outcome.Should().Be(UnitTestOutcome.Passed);

        // Second test (non-last): both assembly-init and class-init take the fast path (no contexts),
        // and the class-cleanup context is NOT allocated (deferred to the last test).
        int countBeforeSecond = _testablePlatformServiceProvider.GetTestContextCallCount;
        TestResult[] secondResults = await unitTestRunner.RunSingleTestAsync(unitTestElement2, _testRunParameters, null!);
        secondResults[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        int allocationsForSecond = _testablePlatformServiceProvider.GetTestContextCallCount - countBeforeSecond;

        // Third test (last in class): takes fast paths for init, but allocates a class-cleanup context
        // and an assembly-cleanup context. Allocation count must exceed the middle test's count.
        int countBeforeThird = _testablePlatformServiceProvider.GetTestContextCallCount;
        TestResult[] thirdResults = await unitTestRunner.RunSingleTestAsync(unitTestElement3, _testRunParameters, null!);
        thirdResults[0].Outcome.Should().Be(UnitTestOutcome.Passed);
        int allocationsForThird = _testablePlatformServiceProvider.GetTestContextCallCount - countBeforeThird;

        // The last-test run allocates exactly two more contexts than a non-last test: the class-cleanup
        // context and the assembly-cleanup context, both of which are deferred from all non-last tests.
        // Asserting the exact delta (rather than a loose "greater than") catches the regression this test
        // guards against, where a non-last test mistakenly allocates the deferred class-cleanup context.
        allocationsForThird.Should().Be(allocationsForSecond + 2);
    }

    #endregion

    #region private helpers

    private MSTestSettings GetSettingsWithDebugTrace(bool captureDebugTraceValue)
    {
        string runSettingsXml =
            $"""
             <RunSettings>
               <MSTest>
                 <CaptureTraceOutput>{captureDebugTraceValue}</CaptureTraceOutput>
               </MSTest>
             </RunSettings>
             """;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                actualReader.Read();
                actualReader.ReadInnerXml();
            });

        var settings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
        settings.Should().NotBeNull();
        return settings;
    }

    #endregion

    #region Dummy implementations

    [DummyTestClass]
    private class DummyTestClass
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
#pragma warning disable RS0030 // Do not use banned APIs
        public void TestMethodToTestInProgress() => Assert.AreEqual(UnitTestOutcome.InProgress, TestContext.CurrentTestOutcome);
#pragma warning restore RS0030 // Do not use banned APIs
    }

    [DummyTestClass]
    private class DummyTestClassWithInitializeMethods
    {
        public static Action AssemblyInitializeMethodBody { get; set; } = null!;

        public static Action ClassInitializeMethodBody { get; set; } = null!;

        // The reflectHelper instance would set the AssemblyInitialize attribute here before running any tests.
        // Setting an attribute causes conflicts with other tests.
        public static void AssemblyInitialize(TestContext tc) => AssemblyInitializeMethodBody.Invoke();

        [ClassInitialize]
        public static void ClassInitialize(TestContext tc) => ClassInitializeMethodBody.Invoke();

        [TestMethod]
        public void TestMethod()
        {
        }

        [TestMethod]
        public void TestMethod2()
        {
        }

        [TestMethod]
        public void TestMethod3()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithFailingClassInitialize
    {
        [ClassInitialize]
        public static void ClassInitialize(TestContext tc) => throw new InvalidOperationException("Class initialize failure.");

        [TestMethod]
        public void TestMethod()
        {
        }

        [TestMethod]
        public void TestMethod2()
        {
        }

        [TestMethod]
        public void TestMethod3()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public static Action AssemblyCleanupMethodBody { get; set; } = null!;

        public static Action ClassCleanupMethodBody { get; set; } = null!;

        public static Action<TestContext> TestMethodBody { get; set; } = null!;

        public TestContext TestContext { get; set; } = null!;

        [AssemblyCleanup]
        public static void AssemblyCleanup() => AssemblyCleanupMethodBody?.Invoke();

        [ClassCleanup]
        public static void ClassCleanup() => ClassCleanupMethodBody?.Invoke();

        [TestMethod]
        public void TestMethod() => TestMethodBody?.Invoke(TestContext);

        [TestMethod]
        public void TestMethod2() => TestMethodBody?.Invoke(TestContext);

        [TestMethod]
        public void TestMethod3() => TestMethodBody?.Invoke(TestContext);
    }

    private class DummyTestClassAttribute : TestClassAttribute;

    #endregion
}
