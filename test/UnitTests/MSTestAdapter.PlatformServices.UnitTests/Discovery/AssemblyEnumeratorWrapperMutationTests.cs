// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestElement = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class AssemblyEnumeratorWrapperMutationTests : TestContainer
{
    // A PE header must be followed by at least 256 bytes: PE signature and header, standard and NT fields, and the data directories.
    private const int PeHeaderMinimumTrailingSize = 256;
    private const byte PeHeaderPointer = 0x40;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider = new();
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler = new();

    public AssemblyEnumeratorWrapperMutationTests()
        => PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void GetTestsRethrowsBadImageFormatExceptionWhenTheManagedPeHeaderEndsExactlyAtTheLastPossiblePosition()
    {
        byte[] managedAssembly = CreatePortableExecutable(PeHeaderPointer + PeHeaderMinimumTrailingSize);

        Action action = () => GetTestsOfFile(managedAssembly);

        action.Should().Throw<BadImageFormatException>();
    }

    public void GetTestsIgnoresBadImageFormatExceptionWhenThePeHeaderLeavesNoRoomForTheDataDirectories()
    {
        byte[] truncatedAssembly = CreatePortableExecutable(PeHeaderPointer + PeHeaderMinimumTrailingSize - 1);

        GetTestsOfFile(truncatedAssembly).Should().BeNull();
    }

    private static byte[] CreatePortableExecutable(int length)
    {
        byte[] content = new byte[length];

        // Little-endian PE header pointer at 0x3C, 'PE\0\0' signature, PE32 magic number 0x10B after the 20 bytes PE header,
        // and a non-zero CLI header RVA in the 15th data directory (offset 232 for PE32), which makes the file a managed assembly.
        content[0x3C] = PeHeaderPointer;
        content[PeHeaderPointer] = (byte)'P';
        content[PeHeaderPointer + 1] = (byte)'E';
        content[PeHeaderPointer + 24] = 0x0B;
        content[PeHeaderPointer + 25] = 0x01;
        content[PeHeaderPointer + 232] = 0x01;
        return content;
    }

    private ICollection<UnitTestElement>? GetTestsOfFile(byte[] content)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllBytes(path, content);
        try
        {
            _testablePlatformServiceProvider.MockFileOperations.Setup(fileOperations => fileOperations.GetFullFilePath(path)).Returns(path);
            _testablePlatformServiceProvider.MockFileOperations.Setup(fileOperations => fileOperations.DoesFileExist(path)).Returns(true);
            _mockTestSourceHandler.Setup(handler => handler.IsAssemblyReferenced(It.IsAny<AssemblyName>(), path)).Returns(true);
            _testablePlatformServiceProvider.MockTestSourceHost
                .Setup(host => host.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()))
                .Throws<BadImageFormatException>();

            return AssemblyEnumeratorWrapper.GetTests(path, null, _mockTestSourceHandler.Object, false, out _);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
