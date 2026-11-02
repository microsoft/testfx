// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Reflection.Emit;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
#if WIN_UI
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
#endif

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestSourceHandlerContinuationTests : TestContainer
{
    public void OrdinarySourcesRetainTheirOriginalOrderAndIdentity()
    {
        TestSourceHandler handler = new();
        string[] sources = ["second.EXE", "first.dll", "unchanged.appx.backup"];

        handler.GetTestSources(sources).Should().BeSameAs(sources);
        handler.GetTestSources(sources).Should().Equal("second.EXE", "first.dll", "unchanged.appx.backup");
#if !NETFRAMEWORK
        handler.IsAssemblyReferenced(new AssemblyName("AbsentAd03Reference"), "missing.dll").Should().BeTrue();
#endif
    }

#if NETFRAMEWORK
    public void ReferenceMatchingIgnoresVersionButRejectsTokenLengthAndEveryByte()
    {
        TestSourceHandler handler = new();
        AssemblyName framework = typeof(TestMethodAttribute).Assembly.GetName();
        string source = typeof(TestSourceHandlerContinuationTests).Assembly.Location;
        AssemblyName differentVersion = new(framework.FullName) { Version = new Version(99, 0, 0, 0), Name = framework.Name.ToLowerInvariant() };
        handler.IsAssemblyReferenced(differentVersion, source).Should().BeTrue();
        byte[] token = framework.GetPublicKeyToken();
        token.Should().HaveCount(8);
        AssemblyName shortToken = new(framework.FullName);
        shortToken.SetPublicKeyToken([token[0]]);
        handler.IsAssemblyReferenced(shortToken, source).Should().BeFalse();
        byte[] changed = (byte[])token.Clone();
        changed[changed.Length - 1] ^= 0xff;
        AssemblyName lastByteDiffers = new(framework.FullName);
        lastByteDiffers.SetPublicKeyToken(changed);
        handler.IsAssemblyReferenced(lastByteDiffers, source).Should().BeFalse();
    }

    public void UnsignedReferencesMatchOnlyUnsignedRequests()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-references-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var domain = AppDomain.CreateDomain("Unsigned reference fixture " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
        {
            ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
            ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
        });
        try
        {
            var worker = (UnsignedReferenceWorker)domain.CreateInstanceFromAndUnwrap(typeof(UnsignedReferenceWorker).Assembly.Location, typeof(UnsignedReferenceWorker).FullName);
            worker.Check(root).Should().Equal(true, true, false, false);
        }
        finally
        {
            AppDomain.Unload(domain);
            Directory.Delete(root, recursive: true);
        }
    }

    public void UndeterminableImagesConservativelyAllowDiscovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-reference-errors-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string invalid = Path.Combine(root, "invalid.dll");
            File.WriteAllText(invalid, "not a managed image");
            TestSourceHandler handler = new();
            AssemblyName framework = typeof(TestMethodAttribute).Assembly.GetName();

            handler.IsAssemblyReferenced(framework, invalid).Should().BeTrue();
            handler.IsAssemblyReferenced(framework, Path.Combine(root, "missing.dll")).Should().BeTrue();
            handler.IsAssemblyReferenced(framework, string.Empty).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public sealed class UnsignedReferenceWorker : MarshalByRefObject
    {
        public bool[] Check(string root)
        {
            string name = "Ad03Unsigned" + Guid.NewGuid().ToString("N");
            Type dependency = Save(root, name, typeof(object));
            string consumer = "Ad03Consumer" + Guid.NewGuid().ToString("N");
            Save(root, consumer, dependency);
            string source = Path.Combine(root, consumer + ".dll");
            TestSourceHandler handler = new();
            AssemblyName emptyToken = new(name);
            emptyToken.SetPublicKeyToken([]);
            AssemblyName signed = new(name);
            signed.SetPublicKeyToken([1, 2, 3, 4, 5, 6, 7, 8]);
            return
            [
                handler.IsAssemblyReferenced(new AssemblyName(name.ToUpperInvariant()) { Version = new Version(99, 0) }, source),
                handler.IsAssemblyReferenced(emptyToken, source),
                handler.IsAssemblyReferenced(signed, source),
                handler.IsAssemblyReferenced(new AssemblyName("NotTheDependency"), source),
            ];
        }

        private static Type Save(string directory, string name, Type reference)
        {
            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Save, directory);
            ModuleBuilder module = assembly.DefineDynamicModule(name, name + ".dll");
            TypeBuilder type = module.DefineType("Anchor", TypeAttributes.Public);
            type.DefineField("Reference", reference, FieldAttributes.Public);
            Type created = type.CreateType();
            assembly.Save(name + ".dll");
            return created;
        }
    }
#endif

#if WIN_UI
    public void UnpackagedAppxSourcesFailWithoutInventingPackageFiles()
    {
        AppModel.GetCurrentPackagePath().Should().BeNull("this SDK test executable is not an MSIX package");
        TestSourceHandler handler = new();
        Action enumerate = () => handler.GetTestSources(["ordinary.dll", "local.AppX"]);

        enumerate.Should().ThrowExactly<ArgumentNullException>().WithParameterName("path");
    }
#endif
}
