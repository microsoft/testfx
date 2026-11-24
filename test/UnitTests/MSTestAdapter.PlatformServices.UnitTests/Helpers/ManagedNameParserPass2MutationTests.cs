// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ManagedNameParserPass2MutationTests : TestContainer
{
    public void ParseManagedMethodName_ParsesArrayParameterTypesWhoseNameEndsWithAQuotedPart()
    {
        foreach ((string managedMethodName, string[] expected) in new (string, string[])[]
        {
            ("Method(Ns.'Quoted name'[],System.Int32)", ["Ns.'Quoted name'[]", "System.Int32"]),
            ("Method('1ns'.'Type Name'[,],System.String)", ["'1ns'.'Type Name'[,]", "System.String"]),
        })
        {
            ManagedNameParser.ParseManagedMethodName(managedMethodName, out string methodName, out int arity, out string[]? parameterTypes);

            methodName.Should().Be("Method", because: $"'{managedMethodName}' names the method before its parameter list");
            arity.Should().Be(0, because: $"'{managedMethodName}' has no arity");
            parameterTypes.Should().Equal(expected, because: $"the array brackets of '{managedMethodName}' directly follow a quoted part, and the parameter list goes on after them");
        }
    }
}
