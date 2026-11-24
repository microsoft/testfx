// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CtrfReportExtensionsTests
{
    [TestMethod]
    public void RegistrationApi_IsNotExperimental()
    {
        Type extensions = typeof(CtrfReportExtensions);
        MethodInfo? registration = extensions.GetMethod(nameof(CtrfReportExtensions.AddCtrfReportProvider));

        Assert.IsNotNull(registration);
        Assert.DoesNotContain(
            attribute => attribute.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.ExperimentalAttribute",
            extensions.GetCustomAttributesData().Concat(registration.GetCustomAttributesData()));
    }
}
