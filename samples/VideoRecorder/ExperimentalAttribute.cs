// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP

namespace System.Diagnostics.CodeAnalysis;

// Minimal polyfill so the [Experimental] compiler feature is available on net462, where the BCL
// type does not exist. The C# compiler recognizes this type by its full name.
[AttributeUsage(
    AttributeTargets.Assembly | AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct
    | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property
    | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate,
    Inherited = false)]
internal sealed class ExperimentalAttribute : Attribute
{
    public ExperimentalAttribute(string diagnosticId) => DiagnosticId = diagnosticId;

    public string DiagnosticId { get; }

    public string? UrlFormat { get; set; }
}

#endif
