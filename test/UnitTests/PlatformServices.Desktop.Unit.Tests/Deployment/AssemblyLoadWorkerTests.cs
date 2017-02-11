// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests.Deployment
{
    extern alias FrameworkV1;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

    using Moq;

    using Assert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class AssemblyLoadWorkerTests
    {
        [TestMethod]
        public void GetFullPathToDependentAssembliesShouldReturnV1FrameworkAssembly()
        {
            // Arrange.
            var v1AssemblyName = new AssemblyName("Microsoft.VisualStudio.QualityTools.UnitTestFramework");
            var testableAssembly = new TestableAssembly();
            testableAssembly.GetReferencedAssembliesSetter = () =>
                {
                    return new AssemblyName[] { v1AssemblyName };
                };
     
            var mockAssemblyUtility = new Mock<IAssemblyUtility>();
            mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoadFrom(It.IsAny<string>())).Returns(testableAssembly);
            mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoad(It.IsAny<string>()))
                .Returns(new TestableAssembly(v1AssemblyName.Name));
            
            var worker = new AssemblyLoadWorker(mockAssemblyUtility.Object);
            IList<string> warnings;
            
            // Act.
            var dependentAssemblies = worker.GetFullPathToDependentAssemblies("C:\\temp\\test3424.dll", out warnings);
            
            // Assert.
            var utfassembly = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
            CollectionAssert.Contains(dependentAssemblies, utfassembly);
        }

        [TestMethod]
        public void GetFullPathToDependentAssembliesShouldReturnV1FrameworkReferencedInADependency()
        {
            // Arrange.
            var v1AssemblyName = new AssemblyName("Microsoft.VisualStudio.QualityTools.UnitTestFramework");
            
            var dependentAssemblyName = new AssemblyName("Common.TestFramework");
            var dependentAssembly = new TestableAssembly(dependentAssemblyName.Name);
            dependentAssembly.GetReferencedAssembliesSetter = () =>
            {
                return new AssemblyName[] { v1AssemblyName };
            };

            var testableAssembly = new TestableAssembly();
            testableAssembly.GetReferencedAssembliesSetter = () =>
            {
                return new AssemblyName[] { dependentAssemblyName };
            };

            var mockAssemblyUtility = new Mock<IAssemblyUtility>();
            mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoadFrom(It.IsAny<string>()))
                .Returns(
                    (string assemblyPath) =>
                        {
                            if (assemblyPath.Contains(dependentAssembly.Name))
                            {
                                return dependentAssembly;
                            }
                            else if (assemblyPath.Contains("test3424"))
                            {
                                return testableAssembly;
                            }

                            return null;
                        });

            mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoad(It.IsAny<string>()))
                .Returns(
                (string assemblyPath) =>
                {
                    if (assemblyPath.Contains(dependentAssembly.FullName))
                    {
                        return dependentAssembly;
                    }
                    else if (assemblyPath.Contains(v1AssemblyName.FullName))
                    {
                        return new TestableAssembly(v1AssemblyName.FullName);
                    }

                    return null;
                }); 

            var worker = new AssemblyLoadWorker(mockAssemblyUtility.Object);
            IList<string> warnings;

            // Act.
            var dependentAssemblies = worker.GetFullPathToDependentAssemblies("C:\\temp\\test3424.dll", out warnings);

            // Assert.
            var utfassembly = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
            CollectionAssert.Contains(dependentAssemblies, utfassembly);
        }

        #region Testable Implementations

        private class TestableAssembly : Assembly
        {
            public TestableAssembly()
            {
            }

            public TestableAssembly(string assemblyName)
            {
                // YAGNI *.exe.
                if (!assemblyName.EndsWith(".dll"))
                {
                    this.Name = string.Concat(assemblyName, ".dll");
                }

                this.FullNameSetter = () => { return assemblyName; };
            }

            public Func<AssemblyName[]> GetReferencedAssembliesSetter { get; set; }

            public Func<string> FullNameSetter { get; set; }

            public override AssemblyName[] GetReferencedAssemblies()
            {
                if (this.GetReferencedAssembliesSetter != null)
                {
                    return this.GetReferencedAssembliesSetter.Invoke();
                }

                return new AssemblyName[] { };
            }

            public string Name
            {
                get; set;
            }

            public override string FullName
            {
                get
                {
                    if (this.FullNameSetter != null)
                    {
                        return this.FullNameSetter.Invoke();
                    }

                    return Assembly.GetExecutingAssembly().FullName;
                }
            }

            public override bool GlobalAssemblyCache
            {
                get
                {
                    return false;
                }
            }

            public override string Location
            {
                get
                {
                    return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), this.Name);
                }
            }

            public override Module[] GetModules(bool getResourceModules)
            {
                return new Module[] { };
            }
        }

        #endregion
    }
}
