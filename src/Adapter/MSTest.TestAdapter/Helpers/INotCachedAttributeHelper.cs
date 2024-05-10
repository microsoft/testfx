// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal interface INotCachedAttributeHelper
{
    object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit);
}
