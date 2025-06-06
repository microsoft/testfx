﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class UnitTestRunnerTests : TestContainer
{
    private readonly Dictionary<string, object?> _testRunParameters;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    private UnitTestRunner _unitTestRunner;

    public UnitTestRunnerTests()
    {
        _testRunParameters = [];
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _mockMessageLogger = new Mock<IMessageLogger>();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _unitTestRunner = new UnitTestRunner(GetSettingsWithDebugTrace(false)!, [], null);
    }

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
                <ForcedLegacyMode>True</ForcedLegacyMode>
                <SettingsFile>DummyPath\TestSettings1.testsettings</SettingsFile>
              </MSTest>
            </RunSettings>
            """;

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                actualReader.Read();
                actualReader.ReadInnerXml();
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object)!;
        var assemblyEnumerator = new UnitTestRunner(adapterSettings, [], null);

        Verify(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Verify(MSTestSettings.CurrentSettings.TestSettingsFile == "DummyPath\\TestSettings1.testsettings");
    }

    #endregion

    #region RunSingleTest tests

    public async Task RunSingleTestShouldThrowIfTestMethodIsNull() =>
        await VerifyThrowsAsync<ArgumentNullException>(async () => await _unitTestRunner.RunSingleTestAsync(null!, null!, null!));

    public async Task RunSingleTestShouldThrowIfTestRunParametersIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        await VerifyThrowsAsync<ArgumentNullException>(async () => await _unitTestRunner.RunSingleTestAsync(testMethod, null!, null!));
    }

    public async Task RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.NotFound);
        Verify(results[0].IgnoreReason == "Test method M was not found.");
    }

    public async Task RunSingleTestShouldReturnTestResultIndicatingNotRunnableTestIfTestMethodCannotBeRun()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType!.FullName,
            methodInfo.Name);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.NotRunnable);
        Verify(expectedMessage == results[0].IgnoreReason);
    }

    public async Task ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == "IgnoreTestClassMessage");
    }

    public async Task ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClass);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == string.Empty);
    }

    public async Task ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == "IgnoreTestMessage");
    }

    public async Task ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTest);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == string.Empty);
    }

    public async Task ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == "IgnoreTestClassMessage");
    }

    public async Task ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Ignored);
        Verify(results[0].IgnoreReason == "IgnoreTestMessage");
    }

    public async Task RunSingleTestShouldReturnTestResultIndicatingFailureIfThereIsAnyTypeInspectionExceptionWhenInspectingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var testMethod = new TestMethod("ImaginaryTestMethod", type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Failed);
        Verify(expectedMessage == results[0].IgnoreReason);
    }

    public async Task RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
        Verify(results[0].IgnoreReason is null);
    }

    public async Task RunSingleTestShouldSetTestsAsInProgressInTestContext()
    {
        Type type = typeof(DummyTestClass);
        MethodInfo methodInfo = type.GetMethod("TestMethodToTestInProgress")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        // Asserting in the test method execution flow itself.
        TestResult[] results = await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UTF.UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestShouldCallAssemblyInitializeAndClassInitializeMethodsInOrder()
    {
        var mockReflectHelper = new Mock<ReflectHelper>();
        _unitTestRunner = new UnitTestRunner(new MSTestSettings(), [], null, mockReflectHelper.Object);

        Type type = typeof(DummyTestClassWithInitializeMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod")!;
        var testMethod = new TestMethod(methodInfo.Name, type.FullName!, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        mockReflectHelper.Setup(
            rh => rh.IsAttributeDefined<AssemblyInitializeAttribute>(type.GetMethod("AssemblyInitialize")!, It.IsAny<bool>()))
            .Returns(true);

        int validator = 1;
        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => validator <<= 2;
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => validator >>= 2;

        await _unitTestRunner.RunSingleTestAsync(testMethod, _testRunParameters, null!);

        Verify(validator == 1);
    }

    #endregion

    #region private helpers

    private MSTestSettings? GetSettingsWithDebugTrace(bool captureDebugTraceValue)
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

        return MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
    }

    #endregion

    #region Dummy implementations

    [DummyTestClass]
    private class DummyTestClass
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void TestMethodToTestInProgress() => Assert.AreEqual(UTF.UnitTestOutcome.InProgress, TestContext.CurrentTestOutcome);
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
    }

    private class DummyTestClassAttribute : TestClassAttribute;

    #endregion
}
