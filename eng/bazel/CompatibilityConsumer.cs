// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Versioning;

using Microsoft.VisualStudio.TestTools.UnitTesting;

internal static class CompatibilityConsumer
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Expected the framework name of the native MSTest assembly.");
            return 64;
        }

        try
        {
            Assembly framework = typeof(Assert).Assembly;
            Assert.AreEqual(args[0], framework.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
            string publicKeyToken = BitConverter.ToString(framework.GetName().GetPublicKeyToken() ?? []).Replace("-", string.Empty).ToLowerInvariant();
            Assert.AreEqual("b03f5f7f11d50a3a", publicKeyToken);
            Assembly satellite = framework.GetSatelliteAssembly(CultureInfo.GetCultureInfo("ko"));
            Assert.AreEqual("ko", satellite.GetName().CultureName);
            CollectionAssert.AreEqual(framework.GetName().GetPublicKey(), satellite.GetName().GetPublicKey());
#if NETFRAMEWORK
            Assert.AreEqual("mscorlib", typeof(object).Assembly.GetName().Name);
#else
            Assert.AreEqual("System.Private.CoreLib", typeof(object).Assembly.GetName().Name);
#endif
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.AreEqual("expected", "actual"));
            Console.WriteLine("Framework: " + args[0]);
            Console.WriteLine("MSTest assembly: " + framework.Location);
            Console.WriteLine("Public key token: " + publicKeyToken);
            Console.WriteLine("Korean satellite: " + satellite.Location);
            Console.WriteLine("Core library: " + typeof(object).Assembly.Location);
            Console.WriteLine("The mismatched-value assertion threw AssertFailedException.");
            return 0;
        }
        catch (AssertFailedException exception)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }
}
