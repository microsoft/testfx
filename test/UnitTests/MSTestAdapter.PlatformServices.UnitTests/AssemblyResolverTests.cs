// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public class AssemblyResolverTests : TestContainer
{
    public void AddSubDirectoriesShouldReturnSubDirectoriesInDfsOrder()
    {
        // Arrange.
        string path = @"C:\unitTesting";
        List<string> searchDirectories = [];

        List<string> resultDirectories =
        [
            @"C:\unitTesting\a",
            @"C:\unitTesting\a\c",
            @"C:\unitTesting\a\c\d",
            @"C:\unitTesting\b",
        ];

        TestableAssemblyResolver assemblyResolver = new(new List<string> { @"c:\dummy" })
        {
            DoesDirectoryExistSetter = (str) => true,
            GetDirectoriesSetter = (str) =>
            {
                if (string.Equals(path, str, StringComparison.OrdinalIgnoreCase))
                {
                    return [@"C:\unitTesting\a", @"C:\unitTesting\b"];
                }
                else if (string.Equals(@"C:\unitTesting\a", str, StringComparison.OrdinalIgnoreCase))
                {
                    return [@"C:\unitTesting\a\c"];
                }
                else if (string.Equals(@"C:\unitTesting\a\c", str, StringComparison.OrdinalIgnoreCase))
                {
                    return [@"C:\unitTesting\a\c\d"];
                }

                return new List<string>().ToArray();
            },
        };

        // Act.
        assemblyResolver.AddSubdirectories(path, searchDirectories);

        // Assert.
        Verify(searchDirectories.Count == 4);

        Verify(resultDirectories.SequenceEqual(searchDirectories, StringComparer.OrdinalIgnoreCase));
    }

    public void OnResolveShouldAddSearchDirectoryListOnANeedToBasis()
    {
        int count = 0;

        List<RecursiveDirectoryPath> recursiveDirectoryPath =
        [
            new RecursiveDirectoryPath(@"C:\unitTesting", true),
            new RecursiveDirectoryPath(@"C:\FunctionalTesting", false),
        ];

        List<string> dummyDirectories =
        [
            @"c:\dummy",
        ];
        TestableAssemblyResolver assemblyResolver = new(dummyDirectories);

        // Adding search directory with recursive property true/false
        assemblyResolver.AddSearchDirectoriesFromRunSetting(recursiveDirectoryPath);

        assemblyResolver.DoesDirectoryExistSetter = (str) => true;

        // mocking the Directory.GetDirectories, to get sub directories
        assemblyResolver.GetDirectoriesSetter = (str) =>
        {
            if (string.Equals(@"C:\unitTesting", str, StringComparison.OrdinalIgnoreCase))
            {
                return [@"C:\unitTesting\a", @"C:\unitTesting\b"];
            }
            else if (string.Equals(@"C:\FunctionalTesting", str, StringComparison.OrdinalIgnoreCase))
            {
                return [@"C:\FunctionalTesting\c"];
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
                    Verify(listPath.Count == 1);
                    Verify(string.Equals(listPath[0], dummyDirectories[0], StringComparison.OrdinalIgnoreCase));
                    count++;
                }
                else if (count == 1)
                {
                    // Second time SearchAssemblyInTheFollowingLocation should get call with directory C:\unitTesting
                    // and with all its sub directory, as its isRecursive property is true
                    Verify(listPath.Count == 3);
                    Verify(string.Equals(listPath[0], @"C:\unitTesting", StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[1], @"C:\unitTesting\a", StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[2], @"C:\unitTesting\b", StringComparison.OrdinalIgnoreCase));
                    count++;
                }
                else if (count == 2)
                {
                    // Third time SearchAssemblyInTheFollowingLocation should get call with directory C:\FunctionalTesting
                    // as its isRecursive property is false
                    Verify(listPath.Count == 1);
                    Verify(string.Equals(listPath[0], @"C:\FunctionalTesting", StringComparison.OrdinalIgnoreCase));
                    count++;
                }
                else if (count == 3)
                {
                    // call will come here when we will call onResolve second time.
                    Verify(listPath.Count == 5);
                    Verify(string.Equals(listPath[0], dummyDirectories[0], StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[1], @"C:\unitTesting", StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[2], @"C:\unitTesting\a", StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[3], @"C:\unitTesting\b", StringComparison.OrdinalIgnoreCase));
                    Verify(string.Equals(listPath[4], @"C:\FunctionalTesting", StringComparison.OrdinalIgnoreCase));
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
        Assembly currentAssembly = typeof(AssemblyResolverTests).Assembly;
        string currentAssemblyPath = Path.GetDirectoryName(currentAssembly.Location);
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

    protected override bool DoesDirectoryExist(string path) => DoesDirectoryExistSetter == null ? base.DoesDirectoryExist(path) : DoesDirectoryExistSetter(path);

    protected override string[] GetDirectories(string path) => GetDirectoriesSetter == null ? base.GetDirectories(path) : GetDirectoriesSetter(path);

    protected override Assembly SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly) => SearchAssemblySetter == null
            ? base.SearchAssembly(searchDirectorypaths, name, isReflectionOnly)
            : SearchAssemblySetter(searchDirectorypaths, name, isReflectionOnly);

    protected override bool DoesFileExist(string filePath) => DoesFileExistSetter == null ? base.DoesFileExist(filePath) : DoesFileExistSetter(filePath);

    protected override Assembly LoadAssemblyFrom(string path) => LoadAssemblyFromSetter == null ? base.LoadAssemblyFrom(path) : LoadAssemblyFromSetter(path);

    protected override Assembly ReflectionOnlyLoadAssemblyFrom(string path) => ReflectionOnlyLoadAssemblyFromSetter == null
            ? base.ReflectionOnlyLoadAssemblyFrom(path)
            : ReflectionOnlyLoadAssemblyFromSetter(path);
}
#endif
