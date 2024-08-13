// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || WIN_UI
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;

#if WIN_UI
using System.Runtime.Versioning;
#endif

[assembly: ComVisible(false)]

#if WIN_UI
[assembly: TypeForwardedTo(typeof(SerializableAttribute))]
[assembly: TypeForwardedTo(typeof(MarshalByRefObject))]

// Extra .0 at the end is a workaround for https://github.com/dotnet/roslyn-analyzers/issues/5728
// Can be removed after the issue is closed.
[assembly: SupportedOSPlatform("windows10.0.18362.0")]
#endif

#if NETFRAMEWORK
[assembly: TypeForwardedTo(typeof(SerializableAttribute))]
[assembly: TypeForwardedTo(typeof(MarshalByRefObject))]
#endif
