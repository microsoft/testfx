// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

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

using Moq;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;
using UTFExtension = FrameworkV2CoreExtension::Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTestRunnerTests
{
    private UnitTestRunner _unitTestRunner;

    private Dictionary<string, object> _testRunParameters;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    [TestInitialize]
    public void TestInit()
    {
        _testRunParameters = new Dictionary<string, object>();
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _unitTestRunner = new UnitTestRunner(GetSettingsWithDebugTrace(false));
    }

    [TestCleanup]
    public void Cleanup() => PlatformServiceProvider.Instance = null;

    #region Constructor tests

    [TestMethodV1]
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

        Assert.IsTrue(MSTestSettings.CurrentSettings.ForcedLegacyMode);
        Assert.AreEqual("DummyPath\\TestSettings1.testsettings", MSTestSettings.CurrentSettings.TestSettingsFile);
    }

    #endregion

    #region RunSingleTest tests

    [TestMethodV1]
    public void RunSingleTestShouldThrowIfTestMethodIsNull()
    {
        void a() => _unitTestRunner.RunSingleTest(null, null);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethodV1]
    public void RunSingleTestShouldThrowIfTestRunParamtersIsNull()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);
        void a() => _unitTestRunner.RunSingleTest(testMethod, null);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethodV1]
    public void RunSingleTestShouldReturnTestResultIndicateATestNotFoundIfTestMethodCannotBeFound()
    {
        var testMethod = new TestMethod("M", "C", "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.NotFound, results[0].Outcome);
        Assert.AreEqual("Test method M was not found.", results[0].ErrorMessage);
    }

    [TestMethodV1]
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

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.NotRunnable, results[0].Outcome);
        Assert.AreEqual(expectedMessage, results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassAndHasMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual("IgnoreTestClassMessage", results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestClassButHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClass);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual(string.Empty, results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodAndHasMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual("IgnoreTestMessage", results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndSkipFillingIgnoreMessageIfIgnoreAttributeIsPresentOnTestMethodButHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreTest);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual(string.Empty, results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndFillInClassIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethod()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassAndIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual("IgnoreTestClassMessage", results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void ExecuteShouldSkipTestAndFillInMethodIgnoreMessageIfIgnoreAttributeIsPresentOnBothClassAndMethodButClassHasNoMessage()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithIgnoreClassWithNoMessageAndIgnoreTestWithMessage);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Ignored, results[0].Outcome);
        Assert.AreEqual("IgnoreTestMessage", results[0].ErrorMessage);
    }

    [TestMethodV1]
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

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Failed, results[0].Outcome);
        Assert.AreEqual(expectedMessage, results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void RunSingleTestShouldReturnTestResultsForAPassingTestMethod()
    {
        var type = typeof(TypeCacheTests.DummyTestClassWithTestMethods);
        var methodInfo = type.GetMethod("TestMethod");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Passed, results[0].Outcome);
        Assert.IsNull(results[0].ErrorMessage);
    }

    [TestMethodV1]
    public void RunSingleTestShouldSetTestsAsInProgressInTestContext()
    {
        var type = typeof(DummyTestClass);
        var methodInfo = type.GetMethod("TestMethodToTestInProgress");
        var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly("A", It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());

        // Asserting in the test method execution flow itself.
        var results = _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.IsNotNull(results);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(UnitTestOutcome.Passed, results[0].Outcome);
    }

    [TestMethodV1]
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
        DummyTestClassWithInitializeMethods.AssemblyInitializeMethodBody = () => validator <<= 2;
        DummyTestClassWithInitializeMethods.ClassInitializeMethodBody = () => validator >>= 2;

        _unitTestRunner.RunSingleTest(testMethod, _testRunParameters);

        Assert.AreEqual(1, validator);
    }

    #endregion

    #region RunCleanup Tests

    [TestMethodV1]
    public void RunCleanupShouldReturnNullOnNoCleanUpMethods() => Assert.IsNull(_unitTestRunner.RunCleanup());

    [TestMethodV1]
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

        Assert.AreEqual(1, assemblyCleanupCount);
        Assert.AreEqual(1, classCleanupCount);
        Assert.AreEqual(2, cleanupresult.Warnings.Count);
        Assert.IsTrue(cleanupresult.Warnings.All(w => w.Contains("NotImplemented")));
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
        public DummyTestClassWithCleanupMethods()
        {
        }

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

    private class DummyTestClassAttribute : UTF.TestClassAttribute
    {
    }

    #endregion
}
