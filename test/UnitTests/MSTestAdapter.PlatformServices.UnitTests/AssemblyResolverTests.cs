// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

using FluentAssertions;

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

        TestableAssemblyResolver assemblyResolver = new([@"c:\dummy"])
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

                return [];
            },
        };

        // Act.
        assemblyResolver.AddSubdirectories(path, searchDirectories);

        // Assert.
        searchDirectories.Count.Should().Be(4);

        resultDirectories.Should().Equal(searchDirectories);
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

            return [];
        };

        assemblyResolver.SearchAssemblySetter =
            (listPath, args, isReflectionOnly) =>
            {
                if (count == 0)
                {
                    // First time SearchAssemblyInTheFollowingLocation should get call with one directory which is in
                    // m_searchDirectories variable
                    listPath.Count.Should().Be(1);
                    listPath[0].Should().Be(dummyDirectories[0], StringComparison.OrdinalIgnoreCase);
                    count++;
                }
                else if (count == 1)
                {
                    // Second time SearchAssemblyInTheFollowingLocation should get call with directory C:\unitTesting
                    // and with all its sub directory, as its isRecursive property is true
                    listPath.Count.Should().Be(3);
                    listPath[0].Should().Be(@"C:\unitTesting", StringComparison.OrdinalIgnoreCase);
                    listPath[1].Should().Be(@"C:\unitTesting\a", StringComparison.OrdinalIgnoreCase);
                    listPath[2].Should().Be(@"C:\unitTesting\b", StringComparison.OrdinalIgnoreCase);
                    count++;
                }
                else if (count == 2)
                {
                    // Third time SearchAssemblyInTheFollowingLocation should get call with directory C:\FunctionalTesting
                    // as its isRecursive property is false
                    listPath.Count.Should().Be(1);
                    listPath[0].Should().Be(@"C:\FunctionalTesting", StringComparison.OrdinalIgnoreCase);
                    count++;
                }
                else if (count == 3)
                {
                    // call will come here when we will call onResolve second time.
                    listPath.Count.Should().Be(5);
                    listPath[0].Should().Be(dummyDirectories[0], StringComparison.OrdinalIgnoreCase);
                    listPath[1].Should().Be(@"C:\unitTesting", StringComparison.OrdinalIgnoreCase);
                    listPath[2].Should().Be(@"C:\unitTesting\a", StringComparison.OrdinalIgnoreCase);
                    listPath[3].Should().Be(@"C:\unitTesting\b", StringComparison.OrdinalIgnoreCase);
                    listPath[4].Should().Be(@"C:\FunctionalTesting", StringComparison.OrdinalIgnoreCase);
                    count++;
                }

                return null!;
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
        var assemblyResolver = new TestableAssemblyResolver([currentAssemblyPath]);

        bool isAssemblyLoaded = false;
        bool isAssemblyReflectionOnlyLoaded = false;

        assemblyResolver.LoadAssemblyFromSetter = path =>
        {
            isAssemblyLoaded = true;
            return typeof(AssemblyResolverTests).Assembly;
        };

        assemblyResolver.ReflectionOnlyLoadAssemblyFromSetter = path =>
        {
            isAssemblyReflectionOnlyLoaded = true;
            return typeof(AssemblyResolverTests).Assembly;
        };

        assemblyResolver.DoesDirectoryExistSetter = (str) => true;
        assemblyResolver.DoesFileExistSetter = (str) => true;

        // Simulate loading the assembly in default context first.
        assemblyResolver.OnResolve(null, new ResolveEventArgs(currentAssembly.FullName));

        isAssemblyLoaded.Should().BeTrue();
        !isAssemblyReflectionOnlyLoaded.Should().BeTrue();

        // Reset.
        isAssemblyLoaded = false;

        // Simulate loading the assembly in Reflection-only context.
        assemblyResolver.ReflectionOnlyOnResolve(null!, new ResolveEventArgs(currentAssembly.FullName));

        // The below assertions ensure that a cached version is not returned out because it actually Reflection only loads the assembly.
        !isAssemblyLoaded.Should().BeTrue();
        isAssemblyReflectionOnlyLoaded.Should().BeTrue();
    }
}

public class TestableAssemblyResolver : AssemblyResolver
{
    public TestableAssemblyResolver(IList<string> directories)
        : base(directories)
    {
    }

    public Func<string, bool> DoesDirectoryExistSetter { get; set; } = null!;

    public Func<string, string[]> GetDirectoriesSetter { get; set; } = null!;

    public Func<string, bool> DoesFileExistSetter { get; set; } = null!;

    public Func<string, Assembly> LoadAssemblyFromSetter { get; set; } = null!;

    public Func<string, Assembly> ReflectionOnlyLoadAssemblyFromSetter { get; set; } = null!;

    public Func<List<string>, string, bool, Assembly> SearchAssemblySetter { get; internal set; } = null!;

    protected override bool DoesDirectoryExist(string path)
        => DoesDirectoryExistSetter?.Invoke(path) ?? base.DoesDirectoryExist(path);

    protected override string[] GetDirectories(string path)
        => GetDirectoriesSetter == null ? base.GetDirectories(path) : GetDirectoriesSetter(path);

    protected override Assembly? SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
        => SearchAssemblySetter == null
            ? base.SearchAssembly(searchDirectorypaths, name, isReflectionOnly)
            : SearchAssemblySetter(searchDirectorypaths, name, isReflectionOnly);

    protected override bool DoesFileExist(string filePath)
        => DoesFileExistSetter?.Invoke(filePath) ?? base.DoesFileExist(filePath);

    protected override Assembly LoadAssemblyFrom(string path)
        => LoadAssemblyFromSetter == null ? base.LoadAssemblyFrom(path) : LoadAssemblyFromSetter(path);

    protected override Assembly ReflectionOnlyLoadAssemblyFrom(string path)
        => ReflectionOnlyLoadAssemblyFromSetter == null
            ? base.ReflectionOnlyLoadAssemblyFrom(path)
            : ReflectionOnlyLoadAssemblyFromSetter(path);
}
#endif
