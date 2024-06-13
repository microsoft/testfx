// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable RS0016 // Add public types and members to the declared API

public class MyConstructorInfo
{
    // TODO, parameters can be `params` or optional, add special type to represent that, or structure to describe it
    public Type[] Parameters { get; internal set; }

    public Func<object?[], object> Invoker { get; internal set; }
}

#pragma warning restore RS0016 // Add public types and members to the declared API
