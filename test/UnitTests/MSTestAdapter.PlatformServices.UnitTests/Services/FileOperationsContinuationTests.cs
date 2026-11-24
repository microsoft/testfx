// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class FileOperationsContinuationTests : TestContainer
{
    public void AssemblyLoadsAreCachedByThePlatformIdentity()
    {
        FileOperations operations = new();
        Assembly expected = typeof(FileOperationsContinuationTests).Assembly;

        Assembly first = operations.LoadAssembly(expected.Location);
        Assembly second = operations.LoadAssembly(expected.Location);

        first.FullName.Should().Be(expected.FullName);
        second.Should().BeSameAs(first);
#if NETCOREAPP && !WIN_UI
        operations.LoadAssembly(Path.Combine("not-an-existing-directory", Path.GetFileName(expected.Location)))
            .Should().BeSameAs(first, "the Core cache uses the simple filename rather than the supplied directory");
#endif
    }

#if NETFRAMEWORK
    public void FrameworkFileValidationAndInvalidPathFallbackPreserveDiagnostics()
    {
        string path = Path.Combine(Path.GetTempPath(), "testfx-ad03-operations-" + Guid.NewGuid().ToString("N") + ".txt");
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        TestablePlatformServiceProvider provider = new();
        provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        PlatformServiceProvider.Instance = provider;
        try
        {
            File.WriteAllText(path, "existing file");
            FileOperations operations = new();
            operations.DoesFileExist(path).Should().BeTrue();
            operations.DoesFileExist(path + ".absent").Should().BeFalse();
            operations.DoesFileExist(null!).Should().BeFalse();
            operations.GetFullFilePath(path).Should().Be(path);
            Action invalidBclPath = () => Path.GetFullPath("\0");
            ArgumentException expected = invalidBclPath.Should().Throw<ArgumentException>().Which;

            operations.GetFullFilePath("\0").Should().Be("\0");

            provider.MockTraceLogger.Verify(logger => logger.Error("{0}", expected.Message), Times.Once);
        }
        finally
        {
            PlatformServiceProvider.Instance = previous;
            File.Delete(path);
        }
    }
#elif !WIN_UI
    public void NonFrameworkValidationDefersExistenceToTheLoader()
    {
        FileOperations operations = new();
        string simpleName = "TestFxAd03Missing" + Guid.NewGuid().ToString("N");
        string absent = Path.Combine(Path.GetTempPath(), simpleName + ".dll");

        operations.DoesFileExist(absent).Should().BeTrue();
        operations.GetFullFilePath(absent).Should().Be(absent);
        Action load = () => operations.LoadAssembly(absent);
        load.Should().ThrowExactly<FileNotFoundException>().Which.FileName.Should().StartWith(simpleName);
    }
#endif
}
