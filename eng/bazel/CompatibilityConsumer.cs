// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
#if NETFRAMEWORK
            Assert.AreEqual("mscorlib", typeof(object).Assembly.GetName().Name);
#else
            Assert.AreEqual("System.Private.CoreLib", typeof(object).Assembly.GetName().Name);
#endif
            Assert.ThrowsExactly<AssertFailedException>(() => Assert.AreEqual("expected", "actual"));
            Console.WriteLine("Framework: " + args[0]);
            Console.WriteLine("MSTest assembly: " + framework.Location);
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
