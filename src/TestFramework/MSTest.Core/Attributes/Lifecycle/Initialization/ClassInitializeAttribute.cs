// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// The class initialize attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClassInitializeAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClassInitializeAttribute"/> class.
        /// ClassInitializeAttribute
        /// </summary>
        public ClassInitializeAttribute()
        {
            this.InheritanceBehavior = InheritanceBehavior.None;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassInitializeAttribute"/> class.
        /// ClassInitializeAttribute
        /// </summary>
        /// <param name="inheritanceBehavior">
        /// Specifies the ClassInitialize Inheritance Behavior
        /// </param>
        public ClassInitializeAttribute(InheritanceBehavior inheritanceBehavior)
        {
            this.InheritanceBehavior = inheritanceBehavior;
        }

        /// <summary>
        /// Gets the Inheritance Behavior
        /// </summary>
        public InheritanceBehavior InheritanceBehavior { get; private set; }
    }
}
