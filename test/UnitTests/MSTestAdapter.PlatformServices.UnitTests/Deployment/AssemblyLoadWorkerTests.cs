﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;
public class AssemblyLoadWorkerTests : TestContainer
{
    public void GetFullPathToDependentAssembliesShouldReturnV1FrameworkAssembly()
    {
        // Arrange.
        var v1AssemblyName = new AssemblyName("Microsoft.VisualStudio.QualityTools.UnitTestFramework");
        var testableAssembly = new TestableAssembly
        {
            GetReferencedAssembliesSetter = () => new AssemblyName[] { v1AssemblyName },
        };

        var mockAssemblyUtility = new Mock<IAssemblyUtility>();
        mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoadFrom(It.IsAny<string>())).Returns(testableAssembly);
        mockAssemblyUtility.Setup(au => au.ReflectionOnlyLoad(It.IsAny<string>()))
            .Returns(new TestableAssembly(v1AssemblyName.Name));

        var worker = new AssemblyLoadWorker(mockAssemblyUtility.Object);

        // Act.
        var dependentAssemblies = worker.GetFullPathToDependentAssemblies("C:\\temp\\test3424.dll", out var warnings);

        // Assert.
        var utfAssembly = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
        Verify(dependentAssemblies.Contains(utfAssembly));
    }

    public void GetFullPathToDependentAssembliesShouldReturnV1FrameworkReferencedInADependency()
    {
        // Arrange.
        var v1AssemblyName = new AssemblyName("Microsoft.VisualStudio.QualityTools.UnitTestFramework");

        var dependentAssemblyName = new AssemblyName("Common.TestFramework");
        var dependentAssembly = new TestableAssembly(dependentAssemblyName.Name)
        {
            GetReferencedAssembliesSetter = () => new AssemblyName[] { v1AssemblyName },
        };

        var testableAssembly = new TestableAssembly
        {
            GetReferencedAssembliesSetter = () => new AssemblyName[] { dependentAssemblyName },
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

        // Act.
        var dependentAssemblies = worker.GetFullPathToDependentAssemblies("C:\\temp\\test3424.dll", out var warnings);

        // Assert.
        var utfAssembly = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll");
        Verify(dependentAssemblies.Contains(utfAssembly));
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
                Name = string.Concat(assemblyName, ".dll");
            }

            FullNameSetter = () => { return assemblyName; };
        }

        public Func<AssemblyName[]> GetReferencedAssembliesSetter { get; set; }

        public Func<string> FullNameSetter { get; set; }

        public override AssemblyName[] GetReferencedAssemblies()
        {
            if (GetReferencedAssembliesSetter != null)
            {
                return GetReferencedAssembliesSetter.Invoke();
            }

            return Array.Empty<AssemblyName>();
        }

        public string Name
        {
            get; set;
        }

        public override string FullName
        {
            get
            {
                if (FullNameSetter != null)
                {
                    return FullNameSetter.Invoke();
                }

                return GetExecutingAssembly().FullName;
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
                return Path.Combine(Path.GetDirectoryName(GetExecutingAssembly().Location), Name);
            }
        }

        public override Module[] GetModules(bool getResourceModules)
        {
            return Array.Empty<Module>();
        }
    }

    #endregion
}
#endif
