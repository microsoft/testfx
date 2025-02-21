﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework;

internal interface IExpandableTestNode
{
    TestNode GetExpandedTestNode(object arguments, string argumentFragmentUid, string argumentFragmentDisplayName);
}
