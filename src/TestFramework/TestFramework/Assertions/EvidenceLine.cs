// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents a single labeled line in the evidence block of a structured assertion message.
/// </summary>
[StackTraceHidden]
internal readonly record struct EvidenceLine(string Label, string Value);
