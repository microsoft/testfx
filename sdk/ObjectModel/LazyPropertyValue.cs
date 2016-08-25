// ---------------------------------------------------------------------------
// <copyright file="LazyPropertyValue.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Represents a lazy value calculation for a TestObject
    /// </summary>
    internal interface ILazyPropertyValue
    {
        /// <summary>
        /// Forces calculation of the value
        /// </summary>
        object Value { get; }
    }

    /// <summary>
    /// Represents a lazy value calculation for a TestObject
    /// </summary>
    /// <typeparam name="T">The type of the value to be calculated</typeparam>
    public sealed class LazyPropertyValue<T> : ILazyPropertyValue
    {
        private T value;
        private Func<T> getValue;
        private bool isValueCreated;

        public LazyPropertyValue(Func<T> getValue)
        {
            this.isValueCreated = false;
            this.value = default(T);
            this.getValue = getValue;
        }

        /// <summary>
        /// Forces calculation of the value
        /// </summary>
        public T Value
        {
            get
            {
                if (!isValueCreated)
                {
                    this.value = this.getValue();
                    isValueCreated = true;
                }

                return this.value;
            }
        }

        object ILazyPropertyValue.Value
        {
            get
            {
                return this.Value;
            }
        }
    }
}
