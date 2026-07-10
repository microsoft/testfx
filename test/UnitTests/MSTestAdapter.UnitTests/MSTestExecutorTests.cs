// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestExecutorTests : TestContainer
{
    private readonly Mock<IRunContext> _mockRunContext;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<IFrameworkHandle> _mockFrameworkHandle;
    private readonly MSTestExecutor _mstestExecutor;

    public MSTestExecutorTests()
    {
        _mockRunContext = new Mock<IRunContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _mockFrameworkHandle = new Mock<IFrameworkHandle>();
        _mstestExecutor = new MSTestExecutor(CancellationToken.None);
    }

    public async Task RunTestsWithSourcesShouldReportErrorAndBailOutOnSettingsException()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Scope>Pond</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """;
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        // Act.
        await _mstestExecutor.RunTestsAsync(sources, _mockRunContext.Object, _mockFrameworkHandle.Object, null, isMTP: false);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }
}
