﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// The class cleanup attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClassCleanupAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
        /// </summary>
        public ClassCleanupAttribute()
            : this(InheritanceBehavior.None)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassCleanupAttribute"/> class.
        /// </summary>
        /// <param name="inheritanceBehavior">
        /// Specifies the ClassCleanup Inheritance Behavior
        /// </param>
        public ClassCleanupAttribute(InheritanceBehavior inheritanceBehavior)
        {
            this.InheritanceBehavior = inheritanceBehavior;
        }

        /// <summary>
        /// Gets the Inheritance Behavior
        /// </summary>
        public InheritanceBehavior InheritanceBehavior { get; }
    }
}
