// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class UnitTestRunnerTests : TestContainer
{
    private readonly Dictionary<string, object> _testRunParameters;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly Mock<IMessageLogger> _mockMessageLogger;

    private UnitTestRunner _unitTestRunner;

    public UnitTestRunnerTests()
    {
        _testRunParameters = [];
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _mockMessageLogger = new Mock<IMessageLogger>();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _unitTestRunner = new UnitTestRunner(GetSettingsWithDebugTrace(false), Array.Empty<UnitTestElement>(), null);
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
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        Verify(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Verify(MSTestSettings.CurrentSettings.TestSettingsFile == "DummyPath\\TestSettings1.testsettings");
    }

    #endregion

    #region RunSingleTest tests

    public void RunSingleTestShouldThrowIfTestMethodIsNull()
    {
        void A() => _unitTestRunner.RunSingleTest(null, null);
        Exception ex = VerifyThrows(A);
        Verify(ex.GetType() == typeof(ArgumentNullException));
    }

    public void RunSingleTestShouldThrowIfTestRunParametersIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void A() => _unitTestRunner.RunSingleTest(testMethod, null);
        Exception ex = VerifyThrows(A);
        Verify(ex.GetType() == typeof(ArgumentNullException));
    }

    public void RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.NotFound);
        Verify(results[0].ErrorMessage == "Test method M was not found.");
    }

    public void RunSingleTestShouldReturnTestResultIndicatingNotRunnableTestIfTestMethodCannotBeRun()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.NotRunnable);
        Verify(expectedMessage == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == "IgnoreTestClassMessage");
    }

    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClass);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == string.Empty);
    }

    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == "IgnoreTestMessage");
    }

    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTest);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == string.Empty);
    }

    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == "IgnoreTestClassMessage");
    }

    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Ignored);
        Verify(results[0].ErrorMessage == "IgnoreTestMessage");
    }

    public void RunSingleTestShouldReturnTestResultIndicatingFailureIfThereIsAnyTypeInspectionExceptionWhenInspectingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var testMethod = new TestMethod("ImaginaryTestMethod", type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Failed);
        Verify(expectedMessage == results[0].ErrorMessage);
    }

    public void RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
    {
        Type type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Passed);
        Verify(results[0].ErrorMessage is null);
    }

    public void RunSingleTestShouldSetTestsAsInProgressInTestContext()
    {
        Type type = typeof(DummyTestClass);
        MethodInfo methodInfo = type.GetMethod("TestMethodToTestInProgress");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        // Asserting in the test method execution flow itself.
        UnitTestResult[] results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(results.Length == 1);
        Verify(results[0].Outcome == UnitTestOutcome.Passed);
    }

    public void RunSingleTestShouldCallAssemblyInitializeAndClassInitializeMethodsInOrder()
    {
        var mockReflectHelper = new Mock<ReflectHelper>();
        _unitTestRunner = new UnitTestRunner(new MSTestSettings(), Array.Empty<UnitTestElement>(), null, mockReflectHelper.Object);

        Type type = typeof(DummyTestClassWithInitializeMethods);
        MethodInfo methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        mockReflectHelper.Setup(
            rh => rh.IsNonDerivedAttributeDefined<UTF.AssemblyInitializeAttribute>(type.GetMethod("AssemblyInitialize"), It.IsAny<bool>()))
            .Returns(true);

        int validator = 1;
        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => validator <<= 2;
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => validator >>= 2;

        _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(validator == 1);
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
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        return MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsName, _mockMessageLogger.Object);
    }

    #endregion

    #region Dummy implementations

    [DummyTestClass]
    private class DummyTestClass
    {
        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.TestMethod]
        public void TestMethodToTestInProgress() => Assert.AreEqual(UTF.UnitTestOutcome.InProgress, TestContext.CurrentTestOutcome);
    }

    [DummyTestClass]
    private class DummyTestClassWithInitializeMethods
    {
        public static Action AssemblyInitializeMethodBody { get; set; }

        public static Action ClassInitializeMethodBody { get; set; }

        // The reflectHelper instance would set the AssemblyInitialize attribute here before running any tests.
        // Setting an attribute causes conflicts with other tests.
        public static void AssemblyInitialize(UTFExtension.TestContext tc) => AssemblyInitializeMethodBody.Invoke();

        [UTF.ClassInitialize]
        public static void ClassInitialize(UTFExtension.TestContext tc) => ClassInitializeMethodBody.Invoke();

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public static Action AssemblyCleanupMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public static Action<UTFExtension.TestContext> TestMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.AssemblyCleanup]
        public static void AssemblyCleanup() => AssemblyCleanupMethodBody?.Invoke();

        [UTF.ClassCleanup]
        public static void ClassCleanup() => ClassCleanupMethodBody?.Invoke();

        [UTF.TestMethod]
        public void TestMethod() => TestMethodBody?.Invoke(TestContext);
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute;

    #endregion
}
