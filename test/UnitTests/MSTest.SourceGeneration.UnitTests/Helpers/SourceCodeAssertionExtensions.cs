// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions.Execution;

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;

internal static class SourceCodeAssertionExtensions
{
    public static SourceCodeAssertions Should(this SourceText sourceText) => new(sourceText.ToString(), AssertionChain.GetOrCreate());
}
