﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resource {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resource() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources.Resource", typeof(Resource).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly cleanup method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string AssemblyCleanupTimedOut {
            get {
                return ResourceManager.GetString("AssemblyCleanupTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly cleanup method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string AssemblyCleanupWasCancelled {
            get {
                return ResourceManager.GetString("AssemblyCleanupWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly initialize method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string AssemblyInitializeTimedOut {
            get {
                return ResourceManager.GetString("AssemblyInitializeTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly initialize method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string AssemblyInitializeWasCancelled {
            get {
                return ResourceManager.GetString("AssemblyInitializeWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSTestAdapterV2.
        /// </summary>
        internal static string AttachmentSetDisplayName {
            get {
                return ResourceManager.GetString("AttachmentSetDisplayName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exception occurred while enumerating IDataSource attribute on &quot;{0}.{1}&quot;: {2}.
        /// </summary>
        internal static string CannotEnumerateIDataSourceAttribute {
            get {
                return ResourceManager.GetString("CannotEnumerateIDataSourceAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exception occurred while expanding IDataSource rows from attribute on &quot;{0}.{1}&quot;: {2}.
        /// </summary>
        internal static string CannotExpandIDataSourceAttribute {
            get {
                return ResourceManager.GetString("CannotExpandIDataSourceAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Data on index {0} for &quot;{1}&quot; cannot be serialized. All data provided through &quot;IDataSource&quot; should be serializable. If you need to test non-serializable data sources, please make sure you add &quot;TestDataSourceDiscovery&quot; attribute on your test assembly and set the discovery option to &quot;DuringExecution&quot;..
        /// </summary>
        internal static string CannotExpandIDataSourceAttribute_CannotSerialize {
            get {
                return ResourceManager.GetString("CannotExpandIDataSourceAttribute_CannotSerialize", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Display name &quot;{2}&quot; on indexes {0} and {1} are duplicate. Display names should be unique..
        /// </summary>
        internal static string CannotExpandIDataSourceAttribute_DuplicateDisplayName {
            get {
                return ResourceManager.GetString("CannotExpandIDataSourceAttribute_DuplicateDisplayName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot run test method &apos;{0}.{1}&apos;: Test data doesn&apos;t match method parameters. Either the count or types are different.
        ///Test expected {2} parameter(s), with types &apos;{3}&apos;,
        ///but received {4} argument(s), with types &apos;{5}&apos;..
        /// </summary>
        internal static string CannotRunTestArgumentsMismatchError {
            get {
                return ResourceManager.GetString("CannotRunTestArgumentsMismatchError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Cannot run test method &apos;{0}.{1}&apos;: Method has parameters, but does not define any test source. Use &apos;[DataRow]&apos;, &apos;[DynamicData]&apos;, or a custom &apos;ITestDataSource&apos; data source to provide test data..
        /// </summary>
        internal static string CannotRunTestMethodNoDataError {
            get {
                return ResourceManager.GetString("CannotRunTestMethodNoDataError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class cleanup method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string ClassCleanupTimedOut {
            get {
                return ResourceManager.GetString("ClassCleanupTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class cleanup method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string ClassCleanupWasCancelled {
            get {
                return ResourceManager.GetString("ClassCleanupWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class initialize method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string ClassInitializeTimedOut {
            get {
                return ResourceManager.GetString("ClassInitializeTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class initialize method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string ClassInitializeWasCancelled {
            get {
                return ResourceManager.GetString("ClassInitializeWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSTestAdapter failed to discover tests in class &apos;{0}&apos; of assembly &apos;{1}&apos; because {2}..
        /// </summary>
        internal static string CouldNotInspectTypeDuringDiscovery {
            get {
                return ResourceManager.GetString("CouldNotInspectTypeDuringDiscovery", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} (Data Row {1}).
        /// </summary>
        internal static string DataDrivenResultDisplayName {
            get {
                return ResourceManager.GetString("DataDrivenResultDisplayName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Debug Trace:.
        /// </summary>
        internal static string DebugTraceBanner {
            get {
                return ResourceManager.GetString("DebugTraceBanner", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [MSTest][Discovery][{0}] {1}.
        /// </summary>
        internal static string DiscoveryWarning {
            get {
                return ResourceManager.GetString("DiscoveryWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0}: {1}.
        /// </summary>
        internal static string EnumeratorLoadTypeErrorFormat {
            get {
                return ResourceManager.GetString("EnumeratorLoadTypeErrorFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &quot;{0}&quot;: (Failed to get exception description due to an exception of type &quot;{1}&quot;..
        /// </summary>
        internal static string ExceptionOccuredWhileGettingTheExceptionDescription {
            get {
                return ResourceManager.GetString("ExceptionOccuredWhileGettingTheExceptionDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exceptions thrown:.
        /// </summary>
        internal static string ExceptionsThrown {
            get {
                return ResourceManager.GetString("ExceptionsThrown", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test &apos;{0}&apos; execution has been aborted..
        /// </summary>
        internal static string Execution_Test_Cancelled {
            get {
                return ResourceManager.GetString("Execution_Test_Cancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test &apos;{0}&apos; exceeded execution timeout period..
        /// </summary>
        internal static string Execution_Test_Timeout {
            get {
                return ResourceManager.GetString("Execution_Test_Timeout", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to get attribute cache. Ignoring attribute inheritance and falling into &apos;type defines Attribute model&apos;, so that we have some data..
        /// </summary>
        internal static string FailedFetchAttributeCache {
            get {
                return ResourceManager.GetString("FailedFetchAttributeCache", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting custom attributes for type {0} threw exception (will ignore and use the reflection way): {1}.
        /// </summary>
        internal static string FailedToGetCustomAttribute {
            get {
                return ResourceManager.GetString("FailedToGetCustomAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid value &apos;{0}&apos; specified for &apos;ClassCleanupLifecycle&apos;. Supported scopes are {1}..
        /// </summary>
        internal static string InvalidClassCleanupLifecycleValue {
            get {
                return ResourceManager.GetString("InvalidClassCleanupLifecycleValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid value &apos;{0}&apos; specified for &apos;Scope&apos;. Supported scopes are {1}..
        /// </summary>
        internal static string InvalidParallelScopeValue {
            get {
                return ResourceManager.GetString("InvalidParallelScopeValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid value &apos;{0}&apos; specified for &apos;Workers&apos;. The value should be a non-negative integer..
        /// </summary>
        internal static string InvalidParallelWorkersValue {
            get {
                return ResourceManager.GetString("InvalidParallelWorkersValue", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid settings &apos;{0}&apos;. Unexpected XmlAttribute: &apos;{1}&apos;..
        /// </summary>
        internal static string InvalidSettingsXmlAttribute {
            get {
                return ResourceManager.GetString("InvalidSettingsXmlAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid settings &apos;{0}&apos;. Unexpected XmlElement: &apos;{1}&apos;..
        /// </summary>
        internal static string InvalidSettingsXmlElement {
            get {
                return ResourceManager.GetString("InvalidSettingsXmlElement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Warning : A testsettings file or a vsmdi file is not supported with the MSTest V2 Adapter..
        /// </summary>
        internal static string LegacyScenariosNotSupportedWarning {
            get {
                return ResourceManager.GetString("LegacyScenariosNotSupportedWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An older version of MSTestV2 package is loaded in assembly, test discovery might fail to discover all data tests if they depend on `.runsettings` file..
        /// </summary>
        internal static string OlderTFMVersionFound {
            get {
                return ResourceManager.GetString("OlderTFMVersionFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An older version of MSTestV2 package is loaded in assembly, test cleanup methods might not run as expected. Please make sure all your test projects references MSTest packages newer then version 2.2.8..
        /// </summary>
        internal static string OlderTFMVersionFoundClassCleanup {
            get {
                return ResourceManager.GetString("OlderTFMVersionFoundClassCleanup", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Running tests in any of the provided sources is not supported for the selected platform.
        /// </summary>
        internal static string SourcesNotSupported {
            get {
                return ResourceManager.GetString("SourcesNotSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Runsettings entry &apos;&lt;ExecutionApartmentState&gt;STA&lt;/ExecutionApartmentState&gt;&apos; is not supported on non-Windows OSes..
        /// </summary>
        internal static string STAIsOnlySupportedOnWindowsWarning {
            get {
                return ResourceManager.GetString("STAIsOnlySupportedOnWindowsWarning", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to discover tests from assembly {0}. Reason:{1}.
        /// </summary>
        internal static string TestAssembly_AssemblyDiscoveryFailure {
            get {
                return ResourceManager.GetString("TestAssembly_AssemblyDiscoveryFailure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to File does not exist: {0}.
        /// </summary>
        internal static string TestAssembly_FileDoesNotExist {
            get {
                return ResourceManager.GetString("TestAssembly_FileDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test cleanup method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string TestCleanupTimedOut {
            get {
                return ResourceManager.GetString("TestCleanupTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test cleanup method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string TestCleanupWasCancelled {
            get {
                return ResourceManager.GetString("TestCleanupWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestContext cannot be Null..
        /// </summary>
        internal static string TestContextIsNull {
            get {
                return ResourceManager.GetString("TestContextIsNull", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestContext Messages:.
        /// </summary>
        internal static string TestContextMessageBanner {
            get {
                return ResourceManager.GetString("TestContextMessageBanner", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test initialize method &apos;{0}.{1}&apos; timed out after {2}ms.
        /// </summary>
        internal static string TestInitializeTimedOut {
            get {
                return ResourceManager.GetString("TestInitializeTimedOut", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test initialize method &apos;{0}.{1}&apos; was cancelled.
        /// </summary>
        internal static string TestInitializeWasCancelled {
            get {
                return ResourceManager.GetString("TestInitializeWasCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method {0} was not found..
        /// </summary>
        internal static string TestNotFound {
            get {
                return ResourceManager.GetString("TestNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MSTest Executor: Test Parallelization enabled for {0} (Workers: {1}, Scope: {2})..
        /// </summary>
        internal static string TestParallelizationBanner {
            get {
                return ResourceManager.GetString("TestParallelizationBanner", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to load types from the test source &apos;{0}&apos;. Some or all of the tests in this source may not be discovered.
        ///Error: {1}.
        /// </summary>
        internal static string TypeLoadFailed {
            get {
                return ResourceManager.GetString("TypeLoadFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly Cleanup method {0}.{1} failed. Error Message: {2}. StackTrace: {3}.
        /// </summary>
        internal static string UTA_AssemblyCleanupMethodWasUnsuccesful {
            get {
                return ResourceManager.GetString("UTA_AssemblyCleanupMethodWasUnsuccesful", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Assembly Initialization method {0}.{1} threw exception. {2}: {3}. Aborting test execution..
        /// </summary>
        internal static string UTA_AssemblyInitMethodThrows {
            get {
                return ResourceManager.GetString("UTA_AssemblyInitMethodThrows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class Cleanup method {0}.{1} failed. Error Message: {2}. Stack Trace: {3}.
        /// </summary>
        internal static string UTA_ClassCleanupMethodWasUnsuccesful {
            get {
                return ResourceManager.GetString("UTA_ClassCleanupMethodWasUnsuccesful", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Class Initialization method {0}.{1} threw exception. {2}: {3}..
        /// </summary>
        internal static string UTA_ClassInitMethodThrows {
            get {
                return ResourceManager.GetString("UTA_ClassInitMethodThrows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be &apos;Task&apos; or &apos;ValueTask&apos;..
        /// </summary>
        internal static string UTA_ClassOrAssemblyCleanupMethodHasWrongSignature {
            get {
                return ResourceManager.GetString("UTA_ClassOrAssemblyCleanupMethodHasWrongSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be &apos;Task&apos; or &apos;ValueTask&apos;..
        /// </summary>
        internal static string UTA_ClassOrAssemblyInitializeMethodHasWrongSignature {
            get {
                return ResourceManager.GetString("UTA_ClassOrAssemblyInitializeMethodHasWrongSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup method {0}.{1} threw exception. {2}..
        /// </summary>
        internal static string UTA_CleanupMethodThrows {
            get {
                return ResourceManager.GetString("UTA_CleanupMethodThrows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error calling Test Cleanup method for test class {0}: {1}.
        /// </summary>
        internal static string UTA_CleanupMethodThrowsGeneralError {
            get {
                return ResourceManager.GetString("UTA_CleanupMethodThrowsGeneralError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup Stack Trace.
        /// </summary>
        internal static string UTA_CleanupStackTrace {
            get {
                return ResourceManager.GetString("UTA_CleanupStackTrace", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to --- End of inner exception stack trace ---.
        /// </summary>
        internal static string UTA_EndOfInnerExceptionTrace {
            get {
                return ResourceManager.GetString("UTA_EndOfInnerExceptionTrace", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA015: A generic method cannot be a test method. {0}.{1} has invalid signature.
        /// </summary>
        internal static string UTA_ErrorGenericTestMethod {
            get {
                return ResourceManager.GetString("UTA_ErrorGenericTestMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA007: Method {1} defined in class {0} does not have correct signature. Test method marked with the [TestMethod] attribute must be non-static, public, return-type as void  and should not take any parameter. Example: public void Test.Class1.Test(). Additionally, if you are using async-await in test method then return-type must be &apos;Task&apos; or &apos;ValueTask&apos;. Example: public async Task Test.Class1.Test2().
        /// </summary>
        internal static string UTA_ErrorIncorrectTestMethodSignature {
            get {
                return ResourceManager.GetString("UTA_ErrorIncorrectTestMethodSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA031: class {0} does not have valid TestContext property. TestContext must be of type TestContext, must be non-static, public and must not be read-only. For example: public TestContext TestContext..
        /// </summary>
        internal static string UTA_ErrorInValidTestContextSignature {
            get {
                return ResourceManager.GetString("UTA_ErrorInValidTestContextSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be a valid integer value and cannot be less than 0..
        /// </summary>
        internal static string UTA_ErrorInvalidTimeout {
            get {
                return ResourceManager.GetString("UTA_ErrorInvalidTimeout", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA014: {0}: Cannot define more than one method with the AssemblyCleanup attribute inside an assembly..
        /// </summary>
        internal static string UTA_ErrorMultiAssemblyClean {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiAssemblyClean", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA013: {0}: Cannot define more than one method with the AssemblyInitialize attribute inside an assembly..
        /// </summary>
        internal static string UTA_ErrorMultiAssemblyInit {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiAssemblyInit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA026: {0}: Cannot define more than one method with the ClassCleanup attribute inside a class..
        /// </summary>
        internal static string UTA_ErrorMultiClassClean {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiClassClean", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA025: {0}: Cannot define more than one method with the ClassInitialize attribute inside a class..
        /// </summary>
        internal static string UTA_ErrorMultiClassInit {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiClassInit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA024: {0}: Cannot define more than one method with the TestCleanup attribute..
        /// </summary>
        internal static string UTA_ErrorMultiClean {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiClean", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA018: {0}: Cannot define more than one method with the TestInitialize attribute..
        /// </summary>
        internal static string UTA_ErrorMultiInit {
            get {
                return ResourceManager.GetString("UTA_ErrorMultiInit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA001: TestClass attribute defined on non-public class {0}.
        /// </summary>
        internal static string UTA_ErrorNonPublicTestClass {
            get {
                return ResourceManager.GetString("UTA_ErrorNonPublicTestClass", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA023: {0}: Cannot define predefined property {2} on method {1}..
        /// </summary>
        internal static string UTA_ErrorPredefinedTestProperty {
            get {
                return ResourceManager.GetString("UTA_ErrorPredefinedTestProperty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestClass attribute defined on generic non-abstract class {0}.
        /// </summary>
        internal static string UTA_ErrorTestClassIsGenericNonAbstract {
            get {
                return ResourceManager.GetString("UTA_ErrorTestClassIsGenericNonAbstract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name..
        /// </summary>
        internal static string UTA_ErrorTestPropertyNullOrEmpty {
            get {
                return ResourceManager.GetString("UTA_ErrorTestPropertyNullOrEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exception thrown while executing test. If using extension of TestMethodAttribute then please contact vendor. Error message: {0}, Stack trace: {1}.
        /// </summary>
        internal static string UTA_ExecuteThrewException {
            get {
                return ResourceManager.GetString("UTA_ExecuteThrewException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The ExpectedException attribute defined on test method {0}.{1} threw an exception during construction.
        ///{2}.
        /// </summary>
        internal static string UTA_ExpectedExceptionAttributeConstructionException {
            get {
                return ResourceManager.GetString("UTA_ExpectedExceptionAttributeConstructionException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to obtain the exception thrown by test method {0}.{1}..
        /// </summary>
        internal static string UTA_FailedToGetTestMethodException {
            get {
                return ResourceManager.GetString("UTA_FailedToGetTestMethodException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Initialization method {0}.{1} threw exception. {2}..
        /// </summary>
        internal static string UTA_InitMethodThrows {
            get {
                return ResourceManager.GetString("UTA_InitMethodThrows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to create instance of class {0}. Error: {1}..
        /// </summary>
        internal static string UTA_InstanceCreationError {
            get {
                return ResourceManager.GetString("UTA_InstanceCreationError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method {0}.{1} does not exist..
        /// </summary>
        internal static string UTA_MethodDoesNotExists {
            get {
                return ResourceManager.GetString("UTA_MethodDoesNotExists", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The test method {0}.{1} has multiple attributes derived from ExpectedExceptionBaseAttribute defined on it. Only one such attribute is allowed..
        /// </summary>
        internal static string UTA_MultipleExpectedExceptionsOnTestMethod {
            get {
                return ResourceManager.GetString("UTA_MultipleExpectedExceptionsOnTestMethod", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to get default constructor for class {0}..
        /// </summary>
        internal static string UTA_NoDefaultConstructor {
            get {
                return ResourceManager.GetString("UTA_NoDefaultConstructor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error in executing test. No result returned by extension. If using extension of TestMethodAttribute then please contact vendor..
        /// </summary>
        internal static string UTA_NoTestResult {
            get {
                return ResourceManager.GetString("UTA_NoTestResult", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to find property {0}.TestContext. Error:{1}..
        /// </summary>
        internal static string UTA_TestContextLoadError {
            get {
                return ResourceManager.GetString("UTA_TestContextLoadError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to set TestContext property for the class {0}. Error: {1}..
        /// </summary>
        internal static string UTA_TestContextSetError {
            get {
                return ResourceManager.GetString("UTA_TestContextSetError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The {0}.TestContext has incorrect type..
        /// </summary>
        internal static string UTA_TestContextTypeMismatchLoadError {
            get {
                return ResourceManager.GetString("UTA_TestContextTypeMismatchLoadError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be &apos;Task&apos; or &apos;ValueTask&apos;..
        /// </summary>
        internal static string UTA_TestInitializeAndCleanupMethodHasWrongSignature {
            get {
                return ResourceManager.GetString("UTA_TestInitializeAndCleanupMethodHasWrongSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method {0}.{1} threw exception: 
        ///{2}.
        /// </summary>
        internal static string UTA_TestMethodThrows {
            get {
                return ResourceManager.GetString("UTA_TestMethodThrows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to get type {0}. Error: {1}..
        /// </summary>
        internal static string UTA_TypeLoadError {
            get {
                return ResourceManager.GetString("UTA_TypeLoadError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The called code threw an exception that was caught, but the exception value was null.
        /// </summary>
        internal static string UTA_UserCodeThrewNullValueException {
            get {
                return ResourceManager.GetString("UTA_UserCodeThrewNullValueException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} For UWP projects, if you are using UI objects in test consider using [UITestMethod] attribute instead of [TestMethod] to execute test in UI thread..
        /// </summary>
        internal static string UTA_WrongThread {
            get {
                return ResourceManager.GetString("UTA_WrongThread", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to (Failed to get the message for an exception of type {0} due to an exception.).
        /// </summary>
        internal static string UTF_FailedToGetExceptionMessage {
            get {
                return ResourceManager.GetString("UTF_FailedToGetExceptionMessage", resourceCulture);
            }
        }
    }
}
