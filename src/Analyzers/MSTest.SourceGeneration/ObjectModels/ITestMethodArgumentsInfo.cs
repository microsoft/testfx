// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.ObjectModels;

internal interface ITestMethodArgumentsInfo
{
    bool IsTestArgumentsEntryReturnType { get; }

    string? GeneratorMethodFullName { get; }

    void AppendArguments(IndentedStringBuilder nodeBuilder);
}
