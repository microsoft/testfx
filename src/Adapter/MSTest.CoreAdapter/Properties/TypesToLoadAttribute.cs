// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

[assembly: TypesToLoad(typeof(MSTestDiscoverer), typeof(MSTestExecutor))]

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{
    /// <summary>
    /// Custom Attribute to specify the exact types which should be loaded from assembly
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    internal sealed class TypesToLoadAttribute : Attribute
    {
        public TypesToLoadAttribute(params Type[] types)
        {
            this.Types = types;
        }

        public Type[] Types { get; }
    }
}