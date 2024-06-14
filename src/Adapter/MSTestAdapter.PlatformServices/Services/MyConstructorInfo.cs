// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

#pragma warning disable RS0016 // Add public types and members to the declared API
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
public class MyConstructorInfo
{
    // TODO, parameters can be `params` or optional, add special type to represent that, or structure to describe it
    public Type[] Parameters { get; internal set; }

    public Func<object?[], object> Invoker { get; internal set; }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore RS0016 // Add public types and members to the declared API
