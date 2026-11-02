// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class DataSourceAttributeTests : TestContainer
{
    public void DataSourceMetadata_SeparatesExplicitDefaultAndNamedConfigurationRoutes()
    {
        var explicitSource = new DataSourceAttribute("custom.provider", "task-owned connection", "records", DataAccessMethod.Sequential);
        explicitSource.ProviderInvariantName.Should().Be("custom.provider");
        explicitSource.ConnectionString.Should().Be("task-owned connection");
        explicitSource.TableName.Should().Be("records");
        explicitSource.DataAccessMethod.Should().Be(DataAccessMethod.Sequential);
        explicitSource.DataSourceSettingName.Should().BeNull();

        var defaultSource = new DataSourceAttribute("default connection", "rows");
        defaultSource.ProviderInvariantName.Should().Be("System.Data.OleDb");
        defaultSource.ConnectionString.Should().Be("default connection");
        defaultSource.TableName.Should().Be("rows");
        defaultSource.DataAccessMethod.Should().Be(DataAccessMethod.Random);
        defaultSource.DataSourceSettingName.Should().BeNull();

        var configured = new DataSourceAttribute("configured-source");
        configured.ProviderInvariantName.Should().Be("System.Data.OleDb");
        configured.ConnectionString.Should().BeNull();
        configured.TableName.Should().BeNull();
        configured.DataAccessMethod.Should().Be(DataAccessMethod.Sequential);
        configured.DataSourceSettingName.Should().Be("configured-source");

        var nullMetadata = new DataSourceAttribute(null!, null!, null!, DataAccessMethod.Random);
        nullMetadata.ProviderInvariantName.Should().BeNull();
        nullMetadata.ConnectionString.Should().BeNull();
        nullMetadata.TableName.Should().BeNull();
        nullMetadata.DataAccessMethod.Should().Be(DataAccessMethod.Random);
    }
}
