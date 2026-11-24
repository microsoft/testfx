// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class EnvironmentWrapperTests : TestContainer
{
    public void EnvironmentWrapper_ForwardsTaskOwnedProcessStateAndRemovesVariables()
    {
        const string Name = "TESTFX_CFW_ENV_FDF4CABC";
        string? original = Environment.GetEnvironmentVariable(Name);
        IEnvironment environment = EnvironmentWrapper.Instance;
        try
        {
            environment.SetEnvironmentVariable(Name, "first value");
            environment.GetEnvironmentVariable(Name).Should().Be("first value");
            Environment.GetEnvironmentVariable(Name).Should().Be("first value");
            environment.SetEnvironmentVariable(Name, "second value");
            environment.GetEnvironmentVariable(Name).Should().Be("second value");
            environment.SetEnvironmentVariable(Name, null);
            environment.GetEnvironmentVariable(Name).Should().BeNull();
            environment.MachineName.Should().Be(Environment.MachineName);

            Action invalid = () => environment.GetEnvironmentVariable(null!);
            invalid.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("variable");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, original);
        }
    }
}
