// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Defines type cache which reflects upon a type and cache its test artifacts
    /// </summary>
    internal class TypeCache : MarshalByRefObject
    {
        /// <summary>
        /// Test context property name
        /// </summary>
        private const string TestContextPropertyName = "TestContext";

        /// <summary>
        /// Predefined test Attribute names.
        /// </summary>
        private static readonly string[] PredefinedNames = new string[] { "Priority", "TestCategory", "Owner" };

        /// <summary>
        /// Helper for reflection API's.
        /// </summary>
        private ReflectHelper reflectionHelper;

        /// <summary>
        /// Assembly info cache
        /// </summary>
        private Dictionary<Assembly, TestAssemblyInfo> testAssemblyInfoCache;

        /// <summary>
        /// ClassInfo cache
        /// </summary>
        private Dictionary<string, TestClassInfo> classInfoCache;

        private object assemblyInfoSyncObject;
        private object classInfoSyncObject;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeCache"/> class.
        /// </summary>
        internal TypeCache()
            : this(new ReflectHelper())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypeCache"/> class.
        /// </summary>
        /// <param name="reflectionHelper"> An instance to the <see cref="ReflectHelper"/> object. </param>
        internal TypeCache(ReflectHelper reflectionHelper)
        {
            this.reflectionHelper = reflectionHelper;
            this.testAssemblyInfoCache = new Dictionary<Assembly, TestAssemblyInfo>();
            this.classInfoCache = new Dictionary<string, TestClassInfo>(StringComparer.Ordinal);
            this.assemblyInfoSyncObject = new object();
            this.classInfoSyncObject = new object();
        }

        /// <summary>
        /// Gets Class Info cache which has cleanup methods to execute
        /// </summary>
        public IEnumerable<TestClassInfo> ClassInfoListWithExecutableCleanupMethods
        {
            get
            {
                return this.classInfoCache.Values.Where(classInfo => classInfo.HasExecutableCleanupMethod).ToList();
            }
        }

        /// <summary>
        /// Gets Assembly Info cache which has cleanup methods to execute
        /// </summary>
        public IEnumerable<TestAssemblyInfo> AssemblyInfoListWithExecutableCleanupMethods
        {
            get
            {
                return this.testAssemblyInfoCache.Values.Where(assemblyInfo => assemblyInfo.HasExecutableCleanupMethod).ToList();
            }
        }

        /// <summary>
        /// Gets the set of cached assembly info values.
        /// </summary>
        public IEnumerable<TestAssemblyInfo> AssemblyInfoCache
        {
            get
            {
                return this.testAssemblyInfoCache.Values.ToList();
            }
        }

        /// <summary>
        /// Gets the set of cached class info values.
        /// </summary>
        public IEnumerable<TestClassInfo> ClassInfoCache
        {
            get
            {
                return this.classInfoCache.Values.ToList();
            }
        }

        /// <summary>
        /// Get the test method info corresponding to the parameter test Element
        /// </summary>
        /// <param name="testMethod"> The test Method. </param>
        /// <param name="testContext"> The test Context. </param>
        /// <param name="captureDebugTraces"> Indicates whether the test method should capture debug traces.</param>
        /// <returns> The <see cref="TestMethodInfo"/>. </returns>
        public TestMethodInfo GetTestMethodInfo(TestMethod testMethod, ITestContext testContext, bool captureDebugTraces)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException("testMethod");
            }

            if (testContext == null)
            {
                throw new ArgumentNullException("testContext");
            }

            // Get the classInfo (This may throw as GetType calls assembly.GetType(..,true);)
            var testClassInfo = this.GetClassInfo(testMethod);

            if (testClassInfo == null)
            {
                // This means the class containing the test method could not be found.
                // Return null so we return a not found result.
                return null;
            }

            // Get the testMethod
            return this.ResolveTestMethod(testMethod, testClassInfo, testContext, captureDebugTraces);
        }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        #region ClassInfo creation and cache logic.

        /// <summary>
        /// Gets the classInfo corresponding to the unit test.
        /// </summary>
        /// <param name="testMethod"> The test Method.  </param>
        /// <returns> The <see cref="TestClassInfo"/>. </returns>
        private TestClassInfo GetClassInfo(TestMethod testMethod)
        {
            Debug.Assert(testMethod != null, "test method is null");

            var typeName = testMethod.FullClassName;

            TestClassInfo classInfo;
            lock (this.classInfoSyncObject)
            {
                if (!this.classInfoCache.TryGetValue(typeName, out classInfo))
                {
                    // Load the class type
                    Type type = this.LoadType(typeName, testMethod.AssemblyName);

                    if (type == null)
                    {
                        // This means the class containing the test method could not be found.
                        // Return null so we return a not found result.
                        return null;
                    }

                    // Get the classInfo
                    classInfo = this.CreateClassInfo(type, testMethod);

                    // Use the full type name for the cache.
                    this.classInfoCache.Add(typeName, classInfo);
                }
            }

            return classInfo;
        }

        /// <summary>
        /// Loads the parameter type from the parameter assembly
        /// </summary>
        /// <param name="typeName"> The type Name. </param>
        /// <param name="assemblyName"> The assembly Name. </param>
        /// <returns> The <see cref="Type"/>. </returns>
        /// <exception cref="TypeInspectionException"> Thrown when there is a type load exception from the assembly. </exception>
        private Type LoadType(string typeName, string assemblyName)
        {
            try
            {
                // Attempt to load the assembly using the full type name (includes assembly)
                // This call will load the assembly from the first location it is
                // found in (i.e. GAC, current directory, path)
                // If this fails, we will try to load the type from the assembly
                // location in the Out directory.
                var t = Type.GetType(typeName);

                if (t == null)
                {
                    var assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyName, isReflectionOnly: false);

                    // Attempt to load the type from the test assembly.
                    // Allow this call to throw if the type can't be loaded.
                    t = assembly.GetType(typeName);
                }

                return t;
            }
            catch (TypeLoadException)
            {
                // This means the class containing the test method could not be found.
                // Return null so we return a not found result.
                return null;
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TypeLoadError, typeName, ex);
                throw new TypeInspectionException(message);
            }
        }

        /// <summary>
        /// Create the class Info
        /// </summary>
        /// <param name="classType"> The class Type. </param>
        /// <param name="testMethod"> The test Method. </param>
        /// <returns> The <see cref="TestClassInfo"/>. </returns>
        private TestClassInfo CreateClassInfo(Type classType, TestMethod testMethod)
        {
            var constructors = classType.GetTypeInfo().DeclaredConstructors;
            var constructor = constructors.FirstOrDefault(ctor => ctor.GetParameters().Length == 0 && ctor.IsPublic);

            if (constructor == null)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_NoDefaultConstructor, testMethod.FullClassName);
                throw new TypeInspectionException(message);
            }

            var testContextProperty = this.ResolveTestContext(classType);

            var assemblyInfo = this.GetAssemblyInfo(classType);

            var classInfo = new TestClassInfo(classType, constructor, testContextProperty, this.reflectionHelper.GetDerivedAttribute<TestClassAttribute>(classType, false), assemblyInfo);

            var testInitializeAttributeType = typeof(TestInitializeAttribute);
            var testCleanupAttributeType = typeof(TestCleanupAttribute);
            var classInitializeAttributeType = typeof(ClassInitializeAttribute);
            var classCleanupAttributeType = typeof(ClassCleanupAttribute);

            // List of instance methods present in the type as well its base type
            // which is used to decide whether TestInitialize/TestCleanup methods
            // present in the base type should be used or not. They are not used if
            // the method is overridden in the derived type.
            var instanceMethods = new Dictionary<string, string>();

            foreach (var methodInfo in classType.GetTypeInfo().DeclaredMethods)
            {
                // Update test initialize/cleanup method
                this.UpdateInfoIfTestInitializeOrCleanupMethod(classInfo, methodInfo, isBase: false, instanceMethods: instanceMethods,  testInitializeAttributeType: testInitializeAttributeType, testCleanupAttributeType: testCleanupAttributeType);

                if (this.IsAssemblyOrClassInitializeMethod(methodInfo, classInitializeAttributeType))
                {
                    // update class initialize method
                    classInfo.ClassInitializeMethod = methodInfo;
                }
                else if (this.IsAssemblyOrClassCleanupMethod(methodInfo, classCleanupAttributeType))
                {
                    // update class cleanup method
                    classInfo.ClassCleanupMethod = methodInfo;
                }
            }

            var baseType = classType.GetTypeInfo().BaseType;
            while (baseType != null)
            {
                foreach (var methodInfo in baseType.GetTypeInfo().DeclaredMethods)
                {
                    if (methodInfo.IsPublic && !methodInfo.IsStatic)
                    {
                        // Update test initialize/cleanup method from base type.
                        this.UpdateInfoIfTestInitializeOrCleanupMethod(classInfo, methodInfo, true, instanceMethods, testInitializeAttributeType, testCleanupAttributeType);
                    }
                }

                baseType = baseType.GetTypeInfo().BaseType;
            }

            return classInfo;
        }

        /// <summary>
        /// Resolves the test context property
        /// </summary>
        /// <param name="classType"> The class Type. </param>
        /// <returns> The <see cref="PropertyInfo"/> for TestContext property. Null if not defined. </returns>
        private PropertyInfo ResolveTestContext(Type classType)
        {
            try
            {
                var testContextProperty = classType.GetRuntimeProperty(TestContextPropertyName);
                if (testContextProperty == null)
                {
                    // that's okay may be the property was not defined
                    return null;
                }

                // check if testContextProperty is of correct type
                if (!testContextProperty.PropertyType.FullName.Equals(typeof(TestContext).FullName, StringComparison.Ordinal))
                {
                    var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextTypeMismatchLoadError, classType.FullName);
                    throw new TypeInspectionException(errorMessage);
                }

                return testContextProperty;
            }
            catch (AmbiguousMatchException ex)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestContextLoadError, classType.FullName, ex.Message);
                throw new TypeInspectionException(errorMessage);
            }
        }

        #endregion

        #region AssemblyInfo creation and cache logic.

        /// <summary>
        /// Get the assembly info for the parameter type
        /// </summary>
        /// <param name="type"> The type. </param>
        /// <returns> The <see cref="TestAssemblyInfo"/> instance. </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Discoverer should continue with remaining sources.")]
        private TestAssemblyInfo GetAssemblyInfo(Type type)
        {
            var assembly = type.GetTypeInfo().Assembly;
            TestAssemblyInfo assemblyInfo;

            lock (this.assemblyInfoSyncObject)
            {
                if (this.testAssemblyInfoCache.TryGetValue(assembly, out assemblyInfo))
                {
                    return assemblyInfo;
                }

                var assemblyInitializeType = typeof(AssemblyInitializeAttribute);
                var assemblyCleanupType = typeof(AssemblyCleanupAttribute);

                assemblyInfo = new TestAssemblyInfo();

                var types = new AssemblyEnumerator().GetTypes(assembly, assembly.FullName, null);

                foreach (var t in types)
                {
                    if (t == null)
                    {
                        continue;
                    }

                    try
                    {
                        // Only examine test classes for test attributes
                        if (!this.reflectionHelper.IsAttributeDefined(t, typeof(TestClassAttribute), inherit: true))
                        {
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we fail to discover type from an assembly, then do not abort. Pick the next type.
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(
                            "TypeCache: Exception occured while checking whether type {0} is a test class or not. {1}",
                            t.FullName,
                            ex);

                        continue;
                    }

                    // Enumerate through all methods and identify the Assembly Init and cleanup methods.
                    foreach (var methodInfo in t.GetTypeInfo().DeclaredMethods)
                    {
                        if (this.IsAssemblyOrClassInitializeMethod(methodInfo, assemblyInitializeType))
                        {
                            assemblyInfo.AssemblyInitializeMethod = methodInfo;
                        }
                        else if (this.IsAssemblyOrClassCleanupMethod(methodInfo, assemblyCleanupType))
                        {
                            assemblyInfo.AssemblyCleanupMethod = methodInfo;
                        }
                    }
                }

                this.testAssemblyInfoCache.Add(assembly, assemblyInfo);
            }

            return assemblyInfo;
        }

        /// <summary>
        /// Verify if a given method is an Assembly or Class Initialize method.
        /// </summary>
        /// <param name="methodInfo"> The method info. </param>
        /// <param name="initializeAttributeType"> The initialization attribute type. </param>
        /// <returns> True if its an initialization method. </returns>
        private bool IsAssemblyOrClassInitializeMethod(MethodInfo methodInfo, Type initializeAttributeType)
        {
            if (!this.reflectionHelper.IsAttributeDefined(methodInfo, initializeAttributeType, false))
            {
                return false;
            }

            if (!methodInfo.HasCorrectClassOrAssemblyInitializeSignature())
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ClassOrAssemblyInitializeMethodHasWrongSignature, methodInfo.DeclaringType.FullName, methodInfo.Name);
                throw new TypeInspectionException(message);
            }

            return true;
        }

        /// <summary>
        /// Verify if a given method is an Assembly or Class cleanup method.
        /// </summary>
        /// <param name="methodInfo"> The method info. </param>
        /// <param name="cleanupAttributeType"> The cleanup attribute type. </param>
        /// <returns> True if its a cleanup method. </returns>
        private bool IsAssemblyOrClassCleanupMethod(MethodInfo methodInfo, Type cleanupAttributeType)
        {
            if (!this.reflectionHelper.IsAttributeDefined(methodInfo, cleanupAttributeType, false))
            {
                return false;
            }

            if (!methodInfo.HasCorrectClassOrAssemblyCleanupSignature())
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ClassOrAssemblyCleanupMethodHasWrongSignature, methodInfo.DeclaringType.FullName, methodInfo.Name);
                throw new TypeInspectionException(message);
            }

            return true;
        }

        #endregion

        /// <summary>
        /// Update the classInfo if the parameter method is a testInitialize/cleanup method
        /// </summary>
        /// <param name="classInfo"> The class Info. </param>
        /// <param name="methodInfo"> The method Info. </param>
        /// <param name="isBase"> If this needs to validate in base class or not. </param>
        /// <param name="instanceMethods"> The instance Methods. </param>
        /// <param name="testInitializeAttributeType"> The test Initialize Attribute Type. </param>
        /// <param name="testCleanupAttributeType"> The test Cleanup Attribute Type. </param>
        private void UpdateInfoIfTestInitializeOrCleanupMethod(
            TestClassInfo classInfo,
            MethodInfo methodInfo,
            bool isBase,
            Dictionary<string, string> instanceMethods,
            Type testInitializeAttributeType,
            Type testCleanupAttributeType)
        {
            var hasTestInitialize = this.reflectionHelper.IsAttributeDefined(methodInfo, testInitializeAttributeType, inherit: false);
            var hasTestCleanup = this.reflectionHelper.IsAttributeDefined(methodInfo, testCleanupAttributeType, inherit: false);

            if (!hasTestCleanup && !hasTestInitialize)
            {
                if (methodInfo.HasCorrectTestInitializeOrCleanupSignature())
                {
                    instanceMethods[methodInfo.Name] = null;
                }

                return;
            }

            if (!methodInfo.HasCorrectTestInitializeOrCleanupSignature())
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestInitializeAndCleanupMethodHasWrongSignature, methodInfo.DeclaringType.FullName, methodInfo.Name);
                throw new TypeInspectionException(message);
            }

            if (hasTestInitialize)
            {
                if (!isBase)
                {
                    classInfo.TestInitializeMethod = methodInfo;
                }
                else
                {
                    if (!instanceMethods.ContainsKey(methodInfo.Name))
                    {
                        classInfo.BaseTestInitializeMethodsQueue.Enqueue(methodInfo);
                    }
                }
            }

            if (hasTestCleanup)
            {
                if (!isBase)
                {
                    classInfo.TestCleanupMethod = methodInfo;
                }
                else
                {
                    if (!instanceMethods.ContainsKey(methodInfo.Name))
                    {
                        classInfo.BaseTestCleanupMethodsQueue.Enqueue(methodInfo);
                    }
                }
            }

            instanceMethods[methodInfo.Name] = null;
        }

        /// <summary>
        /// Resolve the test method. The function will try to
        /// find a function that has the method name with 0 parameters. If the function
        /// cannot be found, or a function is found that returns non-void, the result is
        /// set to error.
        /// </summary>
        /// <param name="testMethod"> The test Method. </param>
        /// <param name="testClassInfo"> The test Class Info. </param>
        /// <param name="testContext"> The test Context. </param>
        /// <param name="captureDebugTraces"> Indicates whether the test method should capture debug traces.</param>
        /// <returns>
        /// The TestMethodInfo for the given test method. Null if the test method could not be found.
        /// </returns>
        private TestMethodInfo ResolveTestMethod(TestMethod testMethod, TestClassInfo testClassInfo, ITestContext testContext, bool captureDebugTraces)
        {
            Debug.Assert(testMethod != null, "testMethod is Null");
            Debug.Assert(testClassInfo != null, "testClassInfo is Null");

            var methodInfo = this.GetMethodInfoForTestMethod(testMethod, testClassInfo);
            if (methodInfo == null)
            {
                // Means the specified test method could not be found.
                return null;
            }

            var expectedExceptionAttribute = this.reflectionHelper.ResolveExpectedExceptionHelper(methodInfo, testMethod);
            var timeout = this.GetTestTimeout(methodInfo, testMethod);

            var testMethodOptions = new TestMethodOptions() { Timeout = timeout, Executor = this.GetTestMethodAttribute(methodInfo, testClassInfo), ExpectedException = expectedExceptionAttribute, TestContext = testContext, CaptureDebugTraces = captureDebugTraces };
            var testMethodInfo = new TestMethodInfo(methodInfo, testClassInfo, testMethodOptions);

            this.SetCustomProperties(testMethodInfo, testContext);

            return testMethodInfo;
        }

        /// <summary>
        /// Provides the Test Method Extension Attribute of the TestClass.
        /// </summary>
        /// <param name="methodInfo"> The method info. </param>
        /// <param name="testClassInfo"> The test class info. </param>
        /// <returns>Test Method Attribute</returns>
        private TestMethodAttribute GetTestMethodAttribute(MethodInfo methodInfo, TestClassInfo testClassInfo)
        {
            // Get the derived TestMethod attribute from reflection
            var testMethodAttribute = this.reflectionHelper.GetDerivedAttribute<TestMethodAttribute>(methodInfo, false);

            // Get the derived TestMethod attribute from Extended TestClass Attribute
            // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
            testMethodAttribute = testClassInfo.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;

            return testMethodAttribute;
        }

        /// <summary>
        /// Resolves a method by using the method name.
        /// </summary>
        /// <param name="testMethod"> The test Method. </param>
        /// <param name="testClassInfo"> The test Class Info. </param>
        /// <returns> The <see cref="MethodInfo"/>. </returns>
        private MethodInfo GetMethodInfoForTestMethod(TestMethod testMethod, TestClassInfo testClassInfo)
        {
            var methodsInClass = testClassInfo.ClassType.GetRuntimeMethods().ToArray();

            var testMethodInfo =
                methodsInClass.Where(method => method.Name.Equals(testMethod.Name))
                    .FirstOrDefault(method => method.HasCorrectTestMethodSignature(true));

            // if correct method is not found, throw appropriate
            // exception about what is wrong.
            if (testMethodInfo == null)
            {
                var errorMessage = string.Format(CultureInfo.CurrentCulture, Resource.UTA_MethodDoesNotExists, testMethod.FullClassName, testMethod.Name);
                throw new TypeInspectionException(errorMessage);
            }

            return testMethodInfo;
        }

        /// <summary>
        /// Gets the test timeout for the parameter test method
        /// </summary>
        /// <param name="methodInfo"> The method Info. </param>
        /// <param name="testMethod"> The test Method. </param>
        /// <returns> The timeout value if defined. 0 if not defined. </returns>
        private int GetTestTimeout(MethodInfo methodInfo, TestMethod testMethod)
        {
            Debug.Assert(methodInfo != null, "TestMethod should be non-null");
            var timeoutAttribute = this.reflectionHelper.GetAttribute<TimeoutAttribute>(methodInfo);

            if (timeoutAttribute != null)
            {
                if (!methodInfo.HasCorrectTimeout())
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorInvalidTimeout, testMethod.FullClassName, testMethod.Name);
                    throw new TypeInspectionException(message);
                }

                return timeoutAttribute.Timeout;
            }

            return TestMethodInfo.TimeoutWhenNotSet;
        }

        /// <summary>
        /// Set custom properties
        /// </summary>
        /// <param name="testMethodInfo"> The test Method Info. </param>
        /// <param name="testContext"> The test Context. </param>
        private void SetCustomProperties(TestMethodInfo testMethodInfo, ITestContext testContext)
        {
            Debug.Assert(testMethodInfo != null, "testMethodInfo is Null");
            Debug.Assert(testMethodInfo.TestMethod != null, "testMethodInfo.TestMethod is Null");

            var attributes = testMethodInfo.TestMethod.GetCustomAttributes(typeof(TestPropertyAttribute), false);
            Debug.Assert(attributes != null, "attributes is null");

            foreach (TestPropertyAttribute attribute in attributes)
            {
                if (!this.ValidateAndAssignTestProperty(testMethodInfo, testContext, attribute.Name, attribute.Value))
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Validates If a Custom test property is valid and then adds it to the TestContext property list.
        /// </summary>
        /// <param name="testMethodInfo"> The test method info. </param>
        /// <param name="testContext"> The test context. </param>
        /// <param name="propertyName"> The property name. </param>
        /// <param name="propertyValue"> The property value. </param>
        /// <returns> True if its a valid Test Property. </returns>
        private bool ValidateAndAssignTestProperty(
            TestMethodInfo testMethodInfo,
            ITestContext testContext,
            string propertyName,
            string propertyValue)
        {
            if (PredefinedNames.Any(predefinedProp => predefinedProp == propertyName))
            {
                testMethodInfo.NotRunnableReason = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_ErrorPredefinedTestProperty,
                    testMethodInfo.TestMethod.DeclaringType.FullName,
                    testMethodInfo.TestMethod.Name,
                    propertyName);

                return false;
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                testMethodInfo.NotRunnableReason = string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_ErrorTestPropertyNullOrEmpty,
                    testMethodInfo.TestMethod.DeclaringType.FullName,
                    testMethodInfo.TestMethod.Name);

                return false;
            }

            object existingValue;
            if (testContext.TryGetPropertyValue(propertyName, out existingValue))
            {
                // Do not add to the test context because it would conflict with an already existing value.
                // We were at one point reporting a warning here. However with extensibility centered around TestProperty where
                // users can have multiple WorkItemAttributes(say) we cannot throw a warning here. Users would have multiple of these attributes
                // so that it shows up in reporting rather than seeing them in TestContext properties.
            }
            else
            {
                testContext.AddProperty(propertyName, propertyValue);
            }

            return true;
        }
    }
}
