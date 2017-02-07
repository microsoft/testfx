// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Enumerates through the type looking for Valid Test Methods to execute.
    /// </summary>
    internal class TypeEnumerator
    {
        private readonly Type type;
        private readonly string assemblyName;
        private readonly TypeValidator typeValidator;
        private readonly TestMethodValidator testMethodValidator;
        private readonly ReflectHelper reflectHelper;
        
        /// <summary>
        /// Whether test class has [Ignore] attribute on it. Nullable so that we can cache the value.
        /// </summary>
        private bool? isIgnoreAttributeOnClass;

        /// <summary>
        /// TypeEnumerator constructor.
        /// </summary>
        /// <param name="type"> The reflected type. </param>
        /// <param name="assemblyName"> The name of the assembly being reflected. </param>
        /// <param name="reflectHelper"> An instance to reflection helper for type information. </param>
        /// <param name="typeValidator"> The validator for test classes. </param>
        /// <param name="testMethodValidator"> The validator for test methods. </param>
        internal TypeEnumerator(Type type, string assemblyName, ReflectHelper reflectHelper, TypeValidator typeValidator, TestMethodValidator testMethodValidator)
        {
            this.type = type;
            this.assemblyName = assemblyName;
            this.reflectHelper = reflectHelper;
            this.typeValidator = typeValidator;
            this.testMethodValidator = testMethodValidator;
        }

        /// <summary>
        /// Gets a value indicating whether the test class has an ignored attribute or not.
        /// </summary>
        internal bool IsIgnoreAttributeOnTestClass
        {
            get
            {
                if (this.isIgnoreAttributeOnClass == null)
                {
                    this.isIgnoreAttributeOnClass = this.reflectHelper.IsAttributeDefined(this.type, typeof(IgnoreAttribute), false);
                }

                Debug.Assert(this.isIgnoreAttributeOnClass.HasValue, "isIgnoreAttributeOnClass.HasValue");
                return this.isIgnoreAttributeOnClass.Value;
            }
        }

        /// <summary>
        /// Walk through all methods in the type, and find out the test methods
        /// </summary>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> list of test cases.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "0#")]
        internal virtual ICollection<UnitTestElement> Enumerate(out ICollection<string> warnings)
        {
            warnings = new Collection<string>();

            if (!this.typeValidator.IsValidTestClass(this.type, warnings))
            {
                return null;
            }

            // If test class is valid, then get the tests
            return this.GetTests(warnings);
        }

        /// <summary>
        /// Gets a list of valid tests in a type.
        /// </summary>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> List of Valid Tests. </returns>
        internal Collection<UnitTestElement> GetTests(ICollection<string> warnings)
        {
            var tests = new Collection<UnitTestElement>();

            // Test class is already valid. Verify methods.
            foreach (var method in this.type.GetRuntimeMethods())
            {
                // Todo: Provide settings to allow users to pick up tests from other assemblies as well.
                if (!method.DeclaringType.GetTypeInfo().Assembly.Equals(this.type.GetTypeInfo().Assembly))
                {
                    continue;
                }

                if (this.testMethodValidator.IsValidTestMethod(method, this.type, warnings))
                {
                    tests.Add(this.GetTestFromMethod(method, warnings));
                }
            }

            return tests;
        }

        /// <summary>
        /// Gets a UnitTestElement from a MethodInfo object filling it up with appropriate values.
        /// </summary>
        /// <param name="method">  The reflected method. </param>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> Returns a UnitTestElement. </returns>
        internal UnitTestElement GetTestFromMethod(MethodInfo method, ICollection<string> warnings)
        {
            Debug.Assert(null != this.type.AssemblyQualifiedName, "AssemblyQualifiedName for method is null.");

            // This allows void returning async test method to be valid test method. Though they will be executed similar to non-async test method.
            var isAsync = ReflectHelper.MatchReturnType(method, typeof(Task));

            var testMethod = new TestMethod(method.Name, this.type.FullName, this.assemblyName, isAsync);
            if (!method.DeclaringType.FullName.Equals(this.type.FullName))
            {
                testMethod.DeclaringClassFullName = method.DeclaringType.FullName;
            }

            var testElement = new UnitTestElement(testMethod);

            // Get compiler generated type name for async test method (either void returning or task returning).
            var asyncTypeName = method.GetAsyncTypeName();
            testElement.AsyncTypeName = asyncTypeName;

            testElement.Ignored = this.IsIgnoreAttributeOnTestClass || this.reflectHelper.IsAttributeDefined(method, typeof(IgnoreAttribute), false);

            testElement.TestCategory = this.reflectHelper.GetCategories(method);

            var traits = this.reflectHelper.GetTestPropertiesAsTraits(method);

            var ownerTrait = this.reflectHelper.GetTestOwnerAsTraits(method);
            if (ownerTrait != null)
            {
                traits = traits.Concat(new[] { ownerTrait });
            }

            testElement.Priority = this.reflectHelper.GetPriority(method);

            var priorityTrait = this.reflectHelper.GetTestPriorityAsTraits(testElement.Priority);
            if (priorityTrait != null)
            {
                traits = traits.Concat(new[] { priorityTrait });
            }

            testElement.Traits = traits.ToArray();

            // Get Deployment items if any.
            testElement.DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(
                method,
                this.type,
                warnings);

            return testElement;
        }
    }
}
