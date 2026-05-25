// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETCOREAPP

#pragma warning disable SA1403 // File may only contain a single namespace

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill so the generator project (which targets netstandard2.0 because Roslyn
/// source generators must) can use C# 9 records / init-only setters.
/// </summary>
internal static class IsExternalInit;

#endif
