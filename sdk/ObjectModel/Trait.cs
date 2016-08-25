// ---------------------------------------------------------------------------
// <copyright file="Trait.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Class that holds Trait. 
    /// A traits is Name, Value pair.
    /// </summary>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class Trait
    {
        public string Name { get; private set; }
        public string Value { get; private set; }

        internal Trait(KeyValuePair<string, string> data)
            : this(data.Key, data.Value)
        {
        }

        public Trait(string name, string value)
        {
            ValidateArg.NotNull(name, "name");

            this.Name = name;
            this.Value = value;
        }
    }
}
