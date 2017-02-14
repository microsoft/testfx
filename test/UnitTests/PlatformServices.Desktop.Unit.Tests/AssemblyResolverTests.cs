// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class AssemblyResolverTests
    {
        [TestMethod]
        public void AddSubDirectoriesShouldReturnSubDirectoriesInDfsOrder()
        {
            // Arrange.
            string path = @"C:\unitTesting";
            List<string> searchDirectories = new List<string>();

            List<string> resultDirectories = new List<string>();
            resultDirectories.Add(@"C:\unitTesting\a");
            resultDirectories.Add(@"C:\unitTesting\a\c");
            resultDirectories.Add(@"C:\unitTesting\a\c\d");
            resultDirectories.Add(@"C:\unitTesting\b");

            TestableAssemblyResolver assemblyResolver = new TestableAssemblyResolver(new List<string> { @"c:\dummy" });

            assemblyResolver.DoesDirectoryExistSetter = (str) => true;
            assemblyResolver.GetDirectoriesSetter = (str) =>
            {
                if (string.Compare(path, str, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { @"C:\unitTesting\a", @"C:\unitTesting\b" };
                }
                else if (string.Compare(@"C:\unitTesting\a", str, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { @"C:\unitTesting\a\c" };
                }
                else if (string.Compare(@"C:\unitTesting\a\c", str, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { @"C:\unitTesting\a\c\d" };
                }

                return new List<string>().ToArray();
            };

            // Act.
            assemblyResolver.AddSubdirectories(path, searchDirectories);

            // Assert.
            Assert.AreEqual(searchDirectories.Count, 4, "searchDirectories should have only 5 elements");

            CollectionAssert.AreEqual(resultDirectories, searchDirectories, StringComparer.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void OnResolveShouldAddSearchDirectoryListOnANeedToBasis()
        {
            int count = 0;

            List<RecursiveDirectoryPath> recursiveDirectoryPath = new List<RecursiveDirectoryPath>();
            recursiveDirectoryPath.Add(new RecursiveDirectoryPath(@"C:\unitTesting", true));
            recursiveDirectoryPath.Add(new RecursiveDirectoryPath(@"C:\FunctionalTesting", false));

            List<string> dummyDirectories = new List<string>();
            dummyDirectories.Add(@"c:\dummy");
            TestableAssemblyResolver assemblyResolver = new TestableAssemblyResolver(dummyDirectories);

            // Adding search directory with recursive property true/false
            assemblyResolver.AddSearchDirectoriesFromRunSetting(recursiveDirectoryPath);

            assemblyResolver.DoesDirectoryExistSetter = (str) => true;

            // shimming the Directory.GetDirectories, to get sub directories
            assemblyResolver.GetDirectoriesSetter = (str) =>
            {
                if (string.Compare(@"C:\unitTesting", str, true) == 0)
                {
                    return new string[] { @"C:\unitTesting\a", @"C:\unitTesting\b" };
                }
                else if (string.Compare(@"C:\FunctionalTesting", str, true) == 0)
                {
                    return new string[] { @"C:\FunctionalTesting\c" };
                }

                return new List<string>().ToArray();
            };

            assemblyResolver.SearchAssemblySetter =
                (listPath, args, isReflectionOnly) =>
                {
                    if (count == 0)
                    {
                        // First time SearchAssemblyInTheFollowingLocation should get call with one directory which is in
                        // m_searchDirectories variable
                        Assert.AreEqual(listPath.Count, 1);
                        Assert.AreEqual(string.Compare(listPath[0], dummyDirectories[0], true), 0);
                        count++;
                    }
                    else if (count == 1)
                    {
                        // Second time SearchAssemblyInTheFollowingLocation should get call with directory C:\unitTesting
                        // and with all its sub directory, as its isRecursive property is true
                        Assert.AreEqual(listPath.Count, 3);
                        Assert.AreEqual(string.Compare(listPath[0], @"C:\unitTesting", true), 0);
                        Assert.AreEqual(string.Compare(listPath[1], @"C:\unitTesting\a", true), 0);
                        Assert.AreEqual(string.Compare(listPath[2], @"C:\unitTesting\b", true), 0);
                        count++;
                    }
                    else if (count == 2)
                    {
                        // Third time SearchAssemblyInTheFollowingLocation should get call with directory C:\FunctionalTesting
                        // as its isRecursive property is false
                        Assert.AreEqual(listPath.Count, 1);
                        Assert.AreEqual(string.Compare(listPath[0], @"C:\FunctionalTesting", true), 0);
                        count++;
                    }
                    else if (count == 3)
                    {
                        // call will come here when we will call onResolve second time.
                        Assert.AreEqual(listPath.Count, 5);
                        Assert.AreEqual(string.Compare(listPath[0], dummyDirectories[0], true), 0);
                        Assert.AreEqual(string.Compare(listPath[1], @"C:\unitTesting", true), 0);
                        Assert.AreEqual(string.Compare(listPath[2], @"C:\unitTesting\a", true), 0);
                        Assert.AreEqual(string.Compare(listPath[3], @"C:\unitTesting\b", true), 0);
                        Assert.AreEqual(string.Compare(listPath[4], @"C:\FunctionalTesting", true), 0);
                        count++;
                    }

                    return null;
                };

            ResolveEventArgs dummyArgs = new ResolveEventArgs("DummyTestDllForTest");
            assemblyResolver.OnResolve(null, dummyArgs);

            // second call to onResolve to verify that the directory in which we have searched in first attempt
            // is now got added in m_searchDirectories.
            assemblyResolver.OnResolve(null, dummyArgs);
        }
    }

    public class TestableAssemblyResolver : AssemblyResolver
    {
        public TestableAssemblyResolver(IList<string> directories)
            : base(directories)
        {
        }

        public Func<string, bool> DoesDirectoryExistSetter { get; set; }

        public Func<string, string[]> GetDirectoriesSetter { get; set; }

        public Func<List<string>, string, bool, Assembly> SearchAssemblySetter { get; internal set; }

        protected override bool DoesDirectoryExist(string path)
        {
            if (this.DoesDirectoryExistSetter == null)
            {
                return base.DoesDirectoryExist(path);
            }

            return this.DoesDirectoryExistSetter(path);
        }

        protected override string[] GetDirectories(string path)
        {
            if (this.GetDirectoriesSetter == null)
            {
                return base.GetDirectories(path);
            }

            return this.GetDirectoriesSetter(path);
        }

        protected override Assembly SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
        {
            if (this.SearchAssemblySetter == null)
            {
                return base.SearchAssembly(searchDirectorypaths, name, isReflectionOnly);
            }

            return this.SearchAssemblySetter(searchDirectorypaths, name, isReflectionOnly);
        }
    }
}
