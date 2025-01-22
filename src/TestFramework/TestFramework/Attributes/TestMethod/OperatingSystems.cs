// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[Flags]
public enum OperatingSystems
{
    Linux = 1,
    // TODO: This is copied from aspnetcore repo. Should we name it MacOS instead?
    MacOSX = 2,
    Windows = 4,
}
