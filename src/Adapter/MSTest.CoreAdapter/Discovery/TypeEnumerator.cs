// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Initializes a new instance of the <see cref="TypeEnumerator"/> class.
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
        /// Walk through all methods in the type, and find out the test methods
        /// </summary>
        /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
        /// <returns> list of test cases.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "0#", Justification = "This is only for internal use.")]
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
            bool foundDuplicateTests = false;
            var foundTests = new HashSet<string>();
            var tests = new Collection<UnitTestElement>();

            // Test class is already valid. Verify methods.
            foreach (var method in this.type.GetRuntimeMethods())
            {
                var isMethodDeclaredInTestTypeAssembly = this.reflectHelper.IsMethodDeclaredInSameAssemblyAsType(method, this.type);
                var enableMethodsFromOtherAssemblies = MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies;

                if (!isMethodDeclaredInTestTypeAssembly && !enableMethodsFromOtherAssemblies)
                {
                    continue;
                }

                if (this.testMethodValidator.IsValidTestMethod(method, this.type, warnings))
                {
                    foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.Name);
                    tests.Add(this.GetTestFromMethod(method, isMethodDeclaredInTestTypeAssembly, warnings));
                }
            }

            if (!foundDuplicateTests)
            {
                return tests;
            }

            // Remove duplicate test methods by taking the first one of each name
            // that is declared closest to the test class in the hierarchy.
            var inheritanceDepths = new Dictionary<string, int>();
            var currentType = this.type;
            int currentDepth = 0;

            while (currentType != null)
            {
                inheritanceDepths[currentType.FullName] = currentDepth;
                ++currentDepth;
                currentType = currentType.GetTypeInfo().BaseType;
            }

            return new Collection<UnitTestElement>(
                tests.GroupBy(
                    t => t.TestMethod.Name,
                    (_, elements) =>
                        elements.OrderBy(t => inheritanceDepths[t.TestMethod.DeclaringClassFullName ?? t.TestMethod.FullClassName]).First())
                    .ToList());
        }

        /// <summary>
        /// Gets a UnitTestElement from a MethodInfo object filling it up with appropriate values.
        /// </summary>
        /// <param name="method">The reflected method.</param>
        /// <param name="isDeclaredInTestTypeAssembly">True if the reflected method is declared in the same assembly as the current type.</param>
        /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
        /// <returns> Returns a UnitTestElement.</returns>
        internal UnitTestElement GetTestFromMethod(MethodInfo method, bool isDeclaredInTestTypeAssembly, ICollection<string> warnings)
        {
            Debug.Assert(this.type.AssemblyQualifiedName != null, "AssemblyQualifiedName for method is null.");

            // This allows void returning async test method to be valid test method. Though they will be executed similar to non-async test method.
            var isAsync = ReflectHelper.MatchReturnType(method, typeof(Task));

            var testMethod = new TestMethod(method.Name, this.type.FullName, this.assemblyName, isAsync);

            if (!method.DeclaringType.FullName.Equals(this.type.FullName))
            {
                testMethod.DeclaringClassFullName = method.DeclaringType.FullName;
            }

            if (!isDeclaredInTestTypeAssembly)
            {
                testMethod.DeclaringAssemblyName =
                    PlatformServiceProvider.Instance.FileOperations.GetAssemblyPath(
                        method.DeclaringType.GetTypeInfo().Assembly);
            }

            var testElement = new UnitTestElement(testMethod);

            // Get compiler generated type name for async test method (either void returning or task returning).
            var asyncTypeName = method.GetAsyncTypeName();
            testElement.AsyncTypeName = asyncTypeName;

            testElement.TestCategory = this.reflectHelper.GetCategories(method, this.type);

            testElement.DoNotParallelize = this.reflectHelper.IsDoNotParallelizeSet(method, this.type);

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

            var cssIteration = this.reflectHelper.GetCustomAttribute(method, typeof(CssIterationAttribute)) as CssIterationAttribute;
            if (cssIteration != null)
            {
                testElement.CssIteration = cssIteration.CssIteration;
            }

            var cssProjectStructure = this.reflectHelper.GetCustomAttribute(method, typeof(CssProjectStructureAttribute)) as CssProjectStructureAttribute;
            if (cssProjectStructure != null)
            {
                testElement.CssProjectStructure = cssProjectStructure.CssProjectStructure;
            }

            var descriptionAttribute = this.reflectHelper.GetCustomAttribute(method, typeof(DescriptionAttribute)) as DescriptionAttribute;
            if (descriptionAttribute != null)
            {
                testElement.Description = descriptionAttribute.Description;
            }

            var workItemAttributeArray = this.reflectHelper.GetCustomAttributes(method, typeof(WorkItemAttribute)) as WorkItemAttribute[];
            if (workItemAttributeArray != null)
            {
                testElement.WorkItemIds = workItemAttributeArray.Select(x => x.Id.ToString()).ToArray();
            }

            // Get Deployment items if any.
            testElement.DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(
                method,
                this.type,
                warnings);

            // get DisplayName from TestMethodAttribute
            var testMethodAttribute = this.reflectHelper.GetCustomAttribute(method, typeof(TestMethodAttribute)) as TestMethodAttribute;
            testElement.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;

            return testElement;
        }
    }
}
