// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// Specification to disable parallelization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class DoNotParallelizeAttribute : Attribute
    {
    }
}