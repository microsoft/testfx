// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace Microsoft.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.All)]
    internal sealed partial class EmbeddedAttribute : Attribute
    {
    }
}

namespace System.Runtime.InteropServices
{
    [Embedded]
    internal static class RuntimeInformation
    {
        public static bool IsOSPlatform(OSPlatform osPlatform)
            => osPlatform == OSPlatform.Windows;
    }
}
#endif
