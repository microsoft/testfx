// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

[DoNotParallelize]
public sealed class PlatformServiceProviderHostCreationPass2MutationTests : TestContainer
{
    private const string NotPortableMessage = "DesktopTestSourceHost.GetResolutionPaths(): Not running in portable mode";
    private const string CreatingResolverMessage = "DesktopTestSourceHost.SetupHost(): Creating assembly resolver with resolution paths {0}.";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly IPlatformServiceProvider _provider;
    private readonly string _previousDirectory = Environment.CurrentDirectory;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "testfx-p14-host-creation-pass2-" + Guid.NewGuid().ToString("N"));

    public PlatformServiceProviderHostCreationPass2MutationTests()
    {
        Directory.CreateDirectory(_root);
        PlatformServiceProvider.Instance = null;
        _provider = PlatformServiceProvider.Instance;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            try
            {
                Environment.CurrentDirectory = _previousDirectory;
                Directory.Delete(_root, recursive: true);
            }
            finally
            {
                PlatformServiceProvider.Instance = _previousProvider;
                base.Dispose(disposing);
            }
        }
    }

    public void CreatingATestSourceHostAlsoSetsItUpBeforeReturningIt()
    {
        List<string> resolutionPaths = [];
        Mock<ITraceLogger> logger = new();
        logger.SetupGet(traceLogger => traceLogger.IsInfoEnabled).Returns(true);
        logger.Setup(traceLogger => traceLogger.Info(CreatingResolverMessage, It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, arguments) => resolutionPaths.Add((string)arguments[0]!));
        _provider.AdapterTraceLogger = logger.Object;

        using ITestSourceHost host = _provider.CreateTestSourceHost(Path.Combine(_root, "tests.dll"), null);

        host.Should().BeOfType<TestSourceHost>();
        logger.Verify(traceLogger => traceLogger.Info(NotPortableMessage), Times.Once);
        resolutionPaths.Should().ContainSingle().Which.Should().StartWith(_root + ",");
    }
}
#endif
