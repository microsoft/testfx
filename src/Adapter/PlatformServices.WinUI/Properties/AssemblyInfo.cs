// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[assembly: AssemblyDescription("")]
[assembly: AssemblyCopyright("© Microsoft Corporation. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: TypeForwardedTo(typeof(SerializableAttribute))]
[assembly: TypeForwardedTo(typeof(MarshalByRefObject))]

// Extra .0 at the end is a workaround for https://github.com/dotnet/roslyn-analyzers/issues/5728
// Can be removed after the issue is closed.
[assembly: SupportedOSPlatform("windows10.0.17763.0")]