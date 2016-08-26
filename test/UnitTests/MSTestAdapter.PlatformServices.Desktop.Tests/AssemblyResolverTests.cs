// Copyright (c) Microsoft. All rights reserved.

namespace MSTestAdapter.PlatformServices.Desktop.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Microsoft.QualityTools.Testing.Fakes;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Fakes;

    using System;
    using System.Collections.Generic;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    using System.IO.Fakes;
    [TestClass]
    public class AssemblyResolverTests
    {
        [TestMethod]
        public void AddSubDirectoriesShouldReturnSubDirectoriesInDfsOrder()
        {
            string path = @"C:\unitTesting";
            List<string> searchDirectories = new List<string>();

            List<string> resultDirectories = new List<string>();
            resultDirectories.Add(@"C:\unitTesting\a");
            resultDirectories.Add(@"C:\unitTesting\a\c");
            resultDirectories.Add(@"C:\unitTesting\a\c\d");
            resultDirectories.Add(@"C:\unitTesting\b");

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => true;
                ShimDirectory.GetDirectoriesString = (str) =>
                {
                    if (String.Compare(path, str, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new string[] { @"C:\unitTesting\a", @"C:\unitTesting\b" };
                    }
                    else if (String.Compare(@"C:\unitTesting\a", str, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new string[] { @"C:\unitTesting\a\c" };
                    }
                    else if (String.Compare(@"C:\unitTesting\a\c", str, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new string[] { @"C:\unitTesting\a\c\d" };
                    }

                    return new List<string>().ToArray();
                };

                List<string> dummyDirectories = new List<string>();
                dummyDirectories.Add(@"c:\dummy");
                AssemblyResolver sut = new AssemblyResolver(dummyDirectories);

                sut.AddSubdirectories(path, searchDirectories);

                Assert.AreEqual(searchDirectories.Count, 4, "searchDirectories should have only 5 elements");

                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(string.Compare(searchDirectories[i], resultDirectories[i], true), 0);
                }
            }
        }

        public static int count = 0;

        [TestMethod]
        public void OnResolveIsAddingSearchDirectoryListOnTheBasisOfNeed()
        {
            List<RecursiveDirectoryPath> recursiveDirectoryPath = new List<RecursiveDirectoryPath>();
            recursiveDirectoryPath.Add(new RecursiveDirectoryPath(@"C:\unitTesting", true));
            recursiveDirectoryPath.Add(new RecursiveDirectoryPath(@"C:\FunctionalTesting", false));

            List<string> dummyDirectories = new List<string>();
            dummyDirectories.Add(@"c:\dummy");
            AssemblyResolver sut = new AssemblyResolver(dummyDirectories);

            // Adding serach directory with recursive property true/false
            sut.AddSearchDirectoriesFromRunSetting(recursiveDirectoryPath);

            using (ShimsContext.Create())
            {
                ShimDirectory.ExistsString = (str) => true;

                // shimming the Directory.GetDirectories, to get sub directories
                ShimDirectory.GetDirectoriesString = (str) =>
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

                ShimAssemblyResolver.AllInstances.SearchAssemblyListOfStringString =
                    (@this, listPath, args) =>
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
                sut.OnResolve(null, dummyArgs);

                // second call to onResolve to varify that the directory in which we have searched in first attempt
                // is now got added in m_searchDirectories.
                sut.OnResolve(null, dummyArgs);
            }
        }
    }
}
