﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MSTest.Analyzers {
    using System;
    
    
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
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("MSTest.Analyzers.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to Methods marked with [AssemblyInitialize] should follow the following layout to be valid:
        ///- it should be &apos;public&apos; 
        ///- it should be &apos;static&apos;
        ///- it should not be generic
        ///- it should take one parameter of type TestContext
        ///- return type should be &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;
        ///- it should not be &apos;async void&apos;
        ///- it should not be a special method (finalizer, operator...)..
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidDescription {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should return &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_NotAsyncVoid {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_NotAsyncVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should not be generic.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_NotGeneric {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_NotGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should be an &apos;ordinary&apos; method.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_Ordinary {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_Ordinary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should return &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_ReturnType {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_ReturnType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should take a single parameter of type TestContext.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_SingleContextParameter {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_SingleContextParameter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method &apos;{0}&apos; should be &apos;static&apos;.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidMessageFormat_Static {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidMessageFormat_Static", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to AssemblyInitialize Method should have valid layout.
        /// </summary>
        internal static string AssemblyInitializeShouldBeValidTitle {
            get {
                return ResourceManager.GetString("AssemblyInitializeShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Prefer &apos;Assert.ThrowsException&apos; or &apos;Assert.ThrowsExceptionAsync&apos; over &apos;[ExpectedException]&apos; as it ensures that only the expected call throws the expected exception. The assert APIs also provide more flexibility and allow you to assert extra properties of the exeption..
        /// </summary>
        internal static string AvoidExpectedExceptionAttributeDescription {
            get {
                return ResourceManager.GetString("AvoidExpectedExceptionAttributeDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Prefer &apos;Assert.ThrowsException/ThrowsExceptionAsync&apos; over &apos;[ExpectedException]&apos;.
        /// </summary>
        internal static string AvoidExpectedExceptionAttributeMessageFormat {
            get {
                return ResourceManager.GetString("AvoidExpectedExceptionAttributeMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Avoid &apos;[ExpectedException]&apos;.
        /// </summary>
        internal static string AvoidExpectedExceptionAttributeTitle {
            get {
                return ResourceManager.GetString("AvoidExpectedExceptionAttributeTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to It&apos;s considered a good practice to have only test classes marked public in a test project..
        /// </summary>
        internal static string PublicTypeShouldBeTestClassDescription {
            get {
                return ResourceManager.GetString("PublicTypeShouldBeTestClassDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Public type &apos;{0}&apos; should be marked with &apos;[TestClass]&apos; or changed to &apos;internal&apos;.
        /// </summary>
        internal static string PublicTypeShouldBeTestClassMessageFormat {
            get {
                return ResourceManager.GetString("PublicTypeShouldBeTestClassMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Public types should be test classes.
        /// </summary>
        internal static string PublicTypeShouldBeTestClassTitle {
            get {
                return ResourceManager.GetString("PublicTypeShouldBeTestClassTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test classes, classes marked with the &apos;[TestClass]&apos; attribute, should respect the following layout to be considered valid by MSTest:
        ///- it should be &apos;public&apos; (or &apos;internal&apos; if &apos;[assembly: DiscoverInternals]&apos; attribute is set)
        ///- it should not be &apos;static&apos; (except if it contains only &apos;AssemblyInitialize&apos; and/or &apos;AssemblyCleanup&apos; methods)
        ///- it should not be generic..
        /// </summary>
        internal static string TestClassShouldBeValidDescription {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test class &apos;{0}&apos; should not be generic.
        /// </summary>
        internal static string TestClassShouldBeValidMessageFormat_NotGeneric {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidMessageFormat_NotGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test class &apos;{0}&apos; should not be &apos;static&apos;.
        /// </summary>
        internal static string TestClassShouldBeValidMessageFormat_NotStatic {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidMessageFormat_NotStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test class &apos;{0}&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string TestClassShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test class &apos;{0}&apos; should be &apos;public&apos; or &apos;internal&apos;.
        /// </summary>
        internal static string TestClassShouldBeValidMessageFormat_PublicOrInternal {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidMessageFormat_PublicOrInternal", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test classes should have valid layout.
        /// </summary>
        internal static string TestClassShouldBeValidTitle {
            get {
                return ResourceManager.GetString("TestClassShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup attribute should follow the following layout to be valid:
        ///- it should be &apos;public&apos; 
        ///- it should not be &apos;static&apos;
        ///- it should not be generic
        ///- it should not be &apos;abstract&apos;
        ///- it should not take any parameter
        ///- return type should be &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;
        ///- it should not be &apos;async void&apos;
        ///- it should not be a special method (finalizer, operator...)..
        /// </summary>
        internal static string TestCleanupShouldBeValidDescription {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should not take any parameter.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_NoParameters {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_NoParameters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should not be &apos;abstract&apos;.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_NotAbstract {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_NotAbstract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should not be &apos;async void&apos;.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_NotAsyncVoid {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_NotAsyncVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should not be generic.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_NotGeneric {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_NotGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should not be &apos;static&apos;.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_NotStatic {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_NotStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should be an &apos;ordinary&apos; method.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_Ordinary {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_Ordinary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup &apos;{0}&apos; should return &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;.
        /// </summary>
        internal static string TestCleanupShouldBeValidMessageFormat_ReturnType {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidMessageFormat_ReturnType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestCleanup method should have valid layout.
        /// </summary>
        internal static string TestCleanupShouldBeValidTitle {
            get {
                return ResourceManager.GetString("TestCleanupShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestContext property should follow the following layout to be valid:
        ///- it should be a property
        ///- it should be &apos;public&apos; (or &apos;internal&apos; if &apos;[assembly: DiscoverInternals]&apos; attribute is set)
        ///- it should not be &apos;static&apos;
        ///- it should not be readonly..
        /// </summary>
        internal static string TestContextShouldBeValidDescription {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Member &apos;TestContext&apos; should be a property and not a field.
        /// </summary>
        internal static string TestContextShouldBeValidMessageFormat_NotField {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidMessageFormat_NotField", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;TestContext&apos; should be settable.
        /// </summary>
        internal static string TestContextShouldBeValidMessageFormat_NotReadonly {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidMessageFormat_NotReadonly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;TestContext&apos; should not be &apos;static&apos;.
        /// </summary>
        internal static string TestContextShouldBeValidMessageFormat_NotStatic {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidMessageFormat_NotStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;TestContext&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string TestContextShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;TestContext&apos; should be &apos;public&apos; or &apos;internal&apos;.
        /// </summary>
        internal static string TestContextShouldBeValidMessageFormat_PublicOrInternal {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidMessageFormat_PublicOrInternal", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test context property should have valid layout.
        /// </summary>
        internal static string TestContextShouldBeValidTitle {
            get {
                return ResourceManager.GetString("TestContextShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TestInitialize attribute should follow the following layout to be valid:
        ///- it should be &apos;public&apos; 
        ///- it should not be &apos;static&apos;
        ///- it should not be generic
        ///- it should not be &apos;abstract&apos;
        ///- it should not take any parameter
        ///- return type should be &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;
        ///- it should not be &apos;async void&apos;
        ///- it should not be a special method (finalizer, operator...)..
        /// </summary>
        internal static string TestInitializeShouldBeValidDescription {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should not take any parameter.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_NoParameters {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_NoParameters", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should not be &apos;abstract&apos;.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_NotAbstract {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_NotAbstract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should not be &apos;async void&apos;.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_NotAsyncVoid {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_NotAsyncVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should not be generic.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_NotGeneric {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_NotGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should not be &apos;static&apos;.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_NotStatic {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_NotStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should be an &apos;ordinary&apos; method.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_Ordinary {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_Ordinary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test Initialize &apos;{0}&apos; should return &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;.
        /// </summary>
        internal static string TestInitializeShouldBeValidMessageFormat_ReturnType {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidMessageFormat_ReturnType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test methods should have valid layout.
        /// </summary>
        internal static string TestInitializeShouldBeValidTitle {
            get {
                return ResourceManager.GetString("TestInitializeShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test methods, methods marked with the &apos;[TestMethod]&apos; attribute, should respect the following layout to be considered valid by MSTest:
        ///- it should be &apos;public&apos; (or &apos;internal&apos; if &apos;[assembly: DiscoverInternals]&apos; attribute is set)
        ///- it should not be &apos;static&apos;
        ///- it should not be generic
        ///- it should not be &apos;abstract&apos;
        ///- return type should be &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;
        ///- it should not be &apos;async void&apos;
        ///- it should not be a special method (finalizer, operator...)..
        /// </summary>
        internal static string TestMethodShouldBeValidDescription {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should not be &apos;abstract&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_NotAbstract {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_NotAbstract", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should not be &apos;async void&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_NotAsyncVoid {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_NotAsyncVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should not be generic.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_NotGeneric {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_NotGeneric", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should not be &apos;static&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_NotStatic {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_NotStatic", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should be an &apos;ordinary&apos; method.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_Ordinary {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_Ordinary", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should be &apos;public&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_Public {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_Public", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should be &apos;public&apos; or &apos;internal&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_PublicOrInternal {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_PublicOrInternal", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test method &apos;{0}&apos; should return &apos;void&apos;, &apos;Task&apos; or &apos;ValueTask&apos;.
        /// </summary>
        internal static string TestMethodShouldBeValidMessageFormat_ReturnType {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidMessageFormat_ReturnType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Test methods should have valid layout.
        /// </summary>
        internal static string TestMethodShouldBeValidTitle {
            get {
                return ResourceManager.GetString("TestMethodShouldBeValidTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [{0}] can only be set on methods marked with [TestMethod].
        /// </summary>
        internal static string UseAttributeOnTestMethodAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("UseAttributeOnTestMethodAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [{0}] can only be set on methods marked with [TestMethod].
        /// </summary>
        internal static string UseAttributeOnTestMethodAnalyzerTitle {
            get {
                return ResourceManager.GetString("UseAttributeOnTestMethodAnalyzerTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to By default, MSTest runs tests within the same assembly sequentially, which can lead to severe performance limitations. It is recommended to enable assembly attribute &apos;[Parallelize]&apos; to run tests in parallel, or if the assembly is known to not be parallelizable, to use explicitly the assembly level attribute &apos;[DoNotParallelize]&apos;..
        /// </summary>
        internal static string UseParallelizeAttributeAnalyzerDescription {
            get {
                return ResourceManager.GetString("UseParallelizeAttributeAnalyzerDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Explicitly enable or disable tests parallelization.
        /// </summary>
        internal static string UseParallelizeAttributeAnalyzerMessageFormat {
            get {
                return ResourceManager.GetString("UseParallelizeAttributeAnalyzerMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Explicitly enable or disable tests parallelization.
        /// </summary>
        internal static string UseParallelizeAttributeAnalyzerTitle {
            get {
                return ResourceManager.GetString("UseParallelizeAttributeAnalyzerTitle", resourceCulture);
            }
        }
    }
}
