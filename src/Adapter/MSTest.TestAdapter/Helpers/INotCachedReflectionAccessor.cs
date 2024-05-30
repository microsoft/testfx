// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

/// <summary>
/// This interface is for unit tests tests so they can easily replace the implementation of accessing the attributes.
/// </summary>
internal interface INotCachedReflectionAccessor
{
    object[]? GetCustomAttributesNotCached(ICustomAttributeProvider attributeProvider, bool inherit);
}
