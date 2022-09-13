// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using global::MSTestAdapter.TestUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = Microsoft.VisualStudio.TestTools.UnitTesting;

public class UnitTestRunnerTests : TestContainer
{
    private UnitTestRunner _unitTestRunner;

    private Dictionary<string, object> _testRunParameters;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public UnitTestRunnerTests()
    {
        _testRunParameters = new Dictionary<string, object>();
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _unitTestRunner = new UnitTestRunner(GetSettingsWithDebugTrace(false));
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
        string runSettingxml =
             @"<RunSettings>
                     <MSTest>
                        <ForcedLegacyMode>True</ForcedLegacyMode>
                        <SettingsFile>DummyPath\TestSettings1.testsettings</SettingsFile>
                     </MSTest>
                   </RunSettings>";

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
        var assemblyEnumerator = new UnitTestRunner(adapterSettings);

        Verify(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Verify("DummyPath\\TestSettings1.testsettings" == MSTestSettings.CurrentSettings.TestSettingsFile);
    }

    #endregion

    #region RunSingleTest tests

    public void RunSingleTestShouldThrowIfTestMethodIsNull()
    {
        void a() => _unitTestRunner.RunSingleTest(null, null);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    public void RunSingleTestShouldThrowIfTestRunParamtersIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void a() => _unitTestRunner.RunSingleTest(testMethod, null);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    public void RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.NotFound == results[0].Outcome);
        Verify("Test method M was not found." == results[0].ErrorMessage);
    }

    public void RunSingleTestShouldReturnTestResultIndicatingNotRunnableTestIfTestMethodCannotBeRun()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        var expectedMessage = string.Format(
            "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
            methodInfo.DeclaringType.FullName,
            methodInfo.Name);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.NotRunnable == results[0].Outcome);
        Verify(expectedMessage == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify("IgnoreTestClassMessage" == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClass);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify(string.Empty == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify("IgnoreTestMessage" == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTest);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify(string.Empty == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify("IgnoreTestClassMessage" == results[0].ErrorMessage);
    }

    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Ignored == results[0].Outcome);
        Verify("IgnoreTestMessage" == results[0].ErrorMessage);
    }

    public void RunSingleTestShouldReturnTestResultIndicatingFailureIfThereIsAnyTypeInspectionExceptionWhenInspectingTestMethod()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var testMethod = new TestMethod("ImaginaryTestMethod", type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        var expectedMessage = string.Format(
            "Method {0}.{1} does not exist.",
            testMethod.FullClassName,
            testMethod.Name);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Failed == results[0].Outcome);
        Verify(expectedMessage == results[0].ErrorMessage);
    }

    public void RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Passed == results[0].Outcome);
        Verify(results[0].ErrorMessage is null);
    }

    public void RunSingleTestShouldSetTestsAsInProgressInTestContext()
    {
        var type = typeof(DummyTestClass);
        var methodInfo = type.GetMethod("TestMethodToTestInProgress");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        // Asserting in the test method execution flow itself.
        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(results is not null);
        Verify(1 == results.Length);
        Verify(UnitTestOutcome.Passed == results[0].Outcome);
    }

    public void RunSingleTestShouldCallAssemblyInitializeAndClassInitializeMethodsInOrder()
    {
        var mockReflectHelper = new Mock<ReflectHelper>();
        _unitTestRunner = new UnitTestRunner(new MSTestSettings(), mockReflectHelper.Object);

        var type = typeof(DummyTestClassWithInitializeMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        mockReflectHelper.Setup(
            rh =>
            rh.IsAttributeDefined(
                type.GetMethod("AssemblyInitialize"),
                typeof(UTF.AssemblyInitializeAttribute),
                It.IsAny<bool>())).Returns(true);

        var validator = 1;
        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => { validator <<= 2; };
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => { validator >>= 2; };

        _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Verify(1 == validator);
    }

    #endregion

    #region RunCleanup Tests

    public void RunCleanupShouldReturnNullOnNoCleanUpMethods()
    {
        Verify(_unitTestRunner.RunCleanup() is null);
    }

    public void RunCleanupShouldReturnCleanupResultsForAssemblyAndClassCleanupMethods()
    {
        var type = typeof(DummyTestClassWithCleanupMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        var assemblyCleanupCount = 0;
        var classCleanupCount = 0;

        DummyTestClassWithCleanupMethods.AssemblyCleanupMethodBody = () =>
            {
                assemblyCleanupCount++;
                throw new NotImplementedException();
            };

        DummyTestClassWithCleanupMethods.ClassCleanupMethodBody = () =>
        {
            classCleanupCount++;
            throw new NotImplementedException();
        };

        var cleanupresult = _unitTestRunner.RunCleanup();

        Verify(1 == assemblyCleanupCount);
        Verify(1 == classCleanupCount);
        Verify(2 == cleanupresult.Warnings.Count);
        Verify(cleanupresult.Warnings.All(w => w.Contains("NotImplemented")));
    }

    #endregion

    #region private helpers

    private MSTestSettings GetSettingsWithDebugTrace(bool captureDebugTraceValue)
    {
        string runSettingxml =
             @"<RunSettings>
                     <MSTest>
                        <CaptureTraceOutput>" + captureDebugTraceValue + @"</CaptureTraceOutput>
                     </MSTest>
                   </RunSettings>";

        _testablePlatformServiceProvider.MockSettingsProvider.Setup(sp => sp.Load(It.IsAny<XmlReader>()))
            .Callback((XmlReader actualReader) =>
            {
                if (actualReader != null)
                {
                    actualReader.Read();
                    actualReader.ReadInnerXml();
                }
            });

        return MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsName);
    }

    #endregion

    #region Dummy implementations

    [DummyTestClass]
    private class DummyTestClass
    {
        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.TestMethod]
        public void TestMethodToTestInProgress()
        {
            Assert.AreEqual(UTF.UnitTestOutcome.InProgress, TestContext.CurrentTestOutcome);
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithInitializeMethods
    {
        public static Action AssemblyInitializeMethodBody { get; set; }

        public static Action ClassInitializeMethodBody { get; set; }

        // The reflectHelper instance would set the AssemblyInitialize attribute here before running any tests.
        // Setting an attribute causes conflicts with other tests.
        public static void AssemblyInitialize(UTFExtension.TestContext tc)
        {
            AssemblyInitializeMethodBody.Invoke();
        }

        [UTF.ClassInitialize]
        public static void ClassInitialize(UTFExtension.TestContext tc)
        {
            ClassInitializeMethodBody.Invoke();
        }

        [UTF.TestMethod]
        public void TestMethod()
        {
        }
    }

    [DummyTestClass]
    private class DummyTestClassWithCleanupMethods
    {
        public DummyTestClassWithCleanupMethods()
        {
        }

        public static Action AssemblyCleanupMethodBody { get; set; }

        public static Action ClassCleanupMethodBody { get; set; }

        public static Action<UTFExtension.TestContext> TestMethodBody { get; set; }

        public UTFExtension.TestContext TestContext { get; set; }

        [UTF.AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            AssemblyCleanupMethodBody?.Invoke();
        }

        [UTF.ClassCleanup]
        public static void ClassCleanup()
        {
            ClassCleanupMethodBody?.Invoke();
        }

        [UTF.TestMethod]
        public void TestMethod()
        {
            TestMethodBody?.Invoke(TestContext);
        }
    }

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }

    #endregion
}
