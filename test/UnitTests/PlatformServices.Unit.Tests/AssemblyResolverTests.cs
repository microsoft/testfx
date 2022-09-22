// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

public class AssemblyResolverTests : TestContainer
{
    public void AddSubDirectoriesShouldReturnSubDirectoriesInDfsOrder()
    {
        // Arrange.
        string path = @"C:\unitTesting";
        List<string> searchDirectories = new();

        List<string> resultDirectories = new()
        {
            @"C:\unitTesting\a",
            @"C:\unitTesting\a\c",
            @"C:\unitTesting\a\c\d",
            @"C:\unitTesting\b"
        };

        TestableAssemblyResolver assemblyResolver = new(new List<string> { @"c:\dummy" })
        {
            DoesDirectoryExistSetter = (str) => true,
            GetDirectoriesSetter = (str) =>
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
            }
        };

        // Act.
        assemblyResolver.AddSubdirectories(path, searchDirectories);

        // Assert.
        Verify(4 == searchDirectories.Count);

        Verify(resultDirectories.SequenceEqual(searchDirectories, StringComparer.OrdinalIgnoreCase));
    }

    public void OnResolveShouldAddSearchDirectoryListOnANeedToBasis()
    {
        int count = 0;

        List<RecursiveDirectoryPath> recursiveDirectoryPath = new()
        {
            new RecursiveDirectoryPath(@"C:\unitTesting", true),
            new RecursiveDirectoryPath(@"C:\FunctionalTesting", false)
        };

        List<string> dummyDirectories = new()
        {
            @"c:\dummy"
        };
        TestableAssemblyResolver assemblyResolver = new(dummyDirectories);

        // Adding search directory with recursive property true/false
        assemblyResolver.AddSearchDirectoriesFromRunSetting(recursiveDirectoryPath);

        assemblyResolver.DoesDirectoryExistSetter = (str) => true;

        // mocking the Directory.GetDirectories, to get sub directories
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
                    Verify(1 == listPath.Count);
                    Verify(0 == string.Compare(listPath[0], dummyDirectories[0], true));
                    count++;
                }
                else if (count == 1)
                {
                    // Second time SearchAssemblyInTheFollowingLocation should get call with directory C:\unitTesting
                    // and with all its sub directory, as its isRecursive property is true
                    Verify(3 == listPath.Count);
                    Verify(0 == string.Compare(listPath[0], @"C:\unitTesting", true));
                    Verify(0 == string.Compare(listPath[1], @"C:\unitTesting\a", true));
                    Verify(0 == string.Compare(listPath[2], @"C:\unitTesting\b", true));
                    count++;
                }
                else if (count == 2)
                {
                    // Third time SearchAssemblyInTheFollowingLocation should get call with directory C:\FunctionalTesting
                    // as its isRecursive property is false
                    Verify(1 == listPath.Count);
                    Verify(0 == string.Compare(listPath[0], @"C:\FunctionalTesting", true));
                    count++;
                }
                else if (count == 3)
                {
                    // call will come here when we will call onResolve second time.
                    Verify(5 == listPath.Count);
                    Verify(0 == string.Compare(listPath[0], dummyDirectories[0], true));
                    Verify(0 == string.Compare(listPath[1], @"C:\unitTesting", true));
                    Verify(0 == string.Compare(listPath[2], @"C:\unitTesting\a", true));
                    Verify(0 == string.Compare(listPath[3], @"C:\unitTesting\b", true));
                    Verify(0 == string.Compare(listPath[4], @"C:\FunctionalTesting", true));
                    count++;
                }

                return null;
            };

        ResolveEventArgs dummyArgs = new("DummyTestDllForTest");
        assemblyResolver.OnResolve(null, dummyArgs);

        // second call to onResolve to verify that the directory in which we have searched in first attempt
        // is now got added in m_searchDirectories.
        assemblyResolver.OnResolve(null, dummyArgs);
    }

    public void ReflectionOnlyOnResolveShouldNotReturnACachedDefaultLoadedAssembly()
    {
        var currentAssembly = typeof(AssemblyResolverTests).Assembly;
        var currentAssemblyPath = Path.GetDirectoryName(currentAssembly.Location);
        var assemblyResolver = new TestableAssemblyResolver(new List<string> { currentAssemblyPath });

        bool isAssemblyLoaded = false;
        bool isAssemblyReflectionOnlyLoaded = false;

        assemblyResolver.LoadAssemblyFromSetter = (string path) =>
        {
            isAssemblyLoaded = true;
            return typeof(AssemblyResolverTests).Assembly;
        };

        assemblyResolver.ReflectionOnlyLoadAssemblyFromSetter = (string path) =>
        {
            isAssemblyReflectionOnlyLoaded = true;
            return typeof(AssemblyResolverTests).Assembly;
        };

        assemblyResolver.DoesDirectoryExistSetter = (str) => true;
        assemblyResolver.DoesFileExistSetter = (str) => true;

        // Simulate loading the assembly in default context first.
        assemblyResolver.OnResolve(null, new ResolveEventArgs(currentAssembly.FullName));

        Verify(isAssemblyLoaded);
        Verify(!isAssemblyReflectionOnlyLoaded);

        // Reset.
        isAssemblyLoaded = false;

        // Simulate loading the assembly in Reflection-only context.
        assemblyResolver.ReflectionOnlyOnResolve(null, new ResolveEventArgs(currentAssembly.FullName));

        // The below assertions ensure that a cached version is not returned out because it actually Reflection only loads the assembly.
        Verify(!isAssemblyLoaded);
        Verify(isAssemblyReflectionOnlyLoaded);
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

    public Func<string, bool> DoesFileExistSetter { get; set; }

    public Func<string, Assembly> LoadAssemblyFromSetter { get; set; }

    public Func<string, Assembly> ReflectionOnlyLoadAssemblyFromSetter { get; set; }

    public Func<List<string>, string, bool, Assembly> SearchAssemblySetter { get; internal set; }

    protected override bool DoesDirectoryExist(string path)
    {
        if (DoesDirectoryExistSetter == null)
        {
            return base.DoesDirectoryExist(path);
        }

        return DoesDirectoryExistSetter(path);
    }

    protected override string[] GetDirectories(string path)
    {
        if (GetDirectoriesSetter == null)
        {
            return base.GetDirectories(path);
        }

        return GetDirectoriesSetter(path);
    }

    protected override Assembly SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
    {
        if (SearchAssemblySetter == null)
        {
            return base.SearchAssembly(searchDirectorypaths, name, isReflectionOnly);
        }

        return SearchAssemblySetter(searchDirectorypaths, name, isReflectionOnly);
    }

    protected override bool DoesFileExist(string filePath)
    {
        if (DoesFileExistSetter == null)
        {
            return base.DoesFileExist(filePath);
        }

        return DoesFileExistSetter(filePath);
    }

    protected override Assembly LoadAssemblyFrom(string path)
    {
        if (LoadAssemblyFromSetter == null)
        {
            return base.LoadAssemblyFrom(path);
        }

        return LoadAssemblyFromSetter(path);
    }

    protected override Assembly ReflectionOnlyLoadAssemblyFrom(string path)
    {
        if (ReflectionOnlyLoadAssemblyFromSetter == null)
        {
            return base.ReflectionOnlyLoadAssemblyFrom(path);
        }

        return ReflectionOnlyLoadAssemblyFromSetter(path);
    }
}
#endif
