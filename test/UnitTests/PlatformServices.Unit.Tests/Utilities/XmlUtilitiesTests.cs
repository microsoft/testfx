// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET462
namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using TestFramework.ForTestingMSTest;

using static AppDomainUtilitiesTests;

public class XmlUtilitiesTests : TestContainer
{
    private readonly TestableXmlUtilities _testableXmlUtilities;

    public XmlUtilitiesTests()
    {
        _testableXmlUtilities = new TestableXmlUtilities();
    }

    public void AddAssemblyRedirectionShouldAddRedirectionToAnEmptyXml()
    {
        _testableXmlUtilities.ConfigXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>";
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        var configBytes = _testableXmlUtilities.AddAssemblyRedirection(
            "foo.xml",
            assemblyName,
            "99.99.99.99",
            assemblyName.Version.ToString());

        // Assert.
        var expectedXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"MSTestAdapter.PlatformServices.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly></assemblyBinding></runtime></configuration>";
        var doc = new XmlDocument();
        doc.LoadXml(expectedXml);
        byte[] expectedConfigBytes = null;

        using (var ms = new MemoryStream())
        {
            doc.Save(ms);
            expectedConfigBytes = ms.ToArray();
        }

        Verify(expectedConfigBytes.SequenceEqual(configBytes));
    }

    public void AddAssemblyRedirectionShouldAddRedirectionToAnEmptyConfig()
    {
        _testableXmlUtilities.ConfigXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
</configuration>";
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        var configBytes = _testableXmlUtilities.AddAssemblyRedirection(
            "foo.xml",
            assemblyName,
            "99.99.99.99",
            assemblyName.Version.ToString());

        // Assert.
        var expectedXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"MSTestAdapter.PlatformServices.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly></assemblyBinding></runtime></configuration>";
        var doc = new XmlDocument();
        doc.LoadXml(expectedXml);
        byte[] expectedConfigBytes = null;

        using (var ms = new MemoryStream())
        {
            doc.Save(ms);
            expectedConfigBytes = ms.ToArray();
        }

        Verify(expectedConfigBytes.SequenceEqual(configBytes));
    }

    public void AddAssemblyRedirectionShouldAddRedirectionToAConfigWithARuntimeSectionOnly()
    {
        _testableXmlUtilities.ConfigXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
<runtime>
</runtime>
</configuration>";
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        var configBytes = _testableXmlUtilities.AddAssemblyRedirection(
            "foo.xml",
            assemblyName,
            "99.99.99.99",
            assemblyName.Version.ToString());

        // Assert.
        var expectedXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"MSTestAdapter.PlatformServices.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly></assemblyBinding></runtime></configuration>";
        var doc = new XmlDocument();
        doc.LoadXml(expectedXml);
        byte[] expectedConfigBytes = null;

        using (var ms = new MemoryStream())
        {
            doc.Save(ms);
            expectedConfigBytes = ms.ToArray();
        }

        Verify(expectedConfigBytes.SequenceEqual(configBytes));
    }

    public void AddAssemblyRedirectionShouldAddRedirectionToAConfigWithRedirections()
    {
        _testableXmlUtilities.ConfigXml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"Random.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly></assemblyBinding></runtime></configuration>";
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        var configBytes = _testableXmlUtilities.AddAssemblyRedirection(
            "foo.xml",
            assemblyName,
            "99.99.99.99",
            assemblyName.Version.ToString());

        // Assert.
        var expectedXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><runtime><assemblyBinding xmlns=\"urn:schemas-microsoft-com:asm.v1\"><dependentAssembly><assemblyIdentity name=\"Random.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly><dependentAssembly><assemblyIdentity name=\"MSTestAdapter.PlatformServices.UnitTests\" publicKeyToken=\"b03f5f7f11d50a3a\" culture=\"neutral\" /><bindingRedirect oldVersion=\"99.99.99.99\" newVersion=\"14.0.0.0\" /></dependentAssembly></assemblyBinding></runtime></configuration>";
        var doc = new XmlDocument();
        doc.LoadXml(expectedXml);
        byte[] expectedConfigBytes = null;

        using (var ms = new MemoryStream())
        {
            doc.Save(ms);
            expectedConfigBytes = ms.ToArray();
        }

        Verify(expectedConfigBytes.SequenceEqual(configBytes));
    }
}
#endif
