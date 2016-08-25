// ---------------------------------------------------------------------------
// <copyright file="TestPropertyAttributes.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
// Defines the available attributes of the test property. 
// </summary>
// ---------------------------------------------------------------------------

using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    [Flags]
    public enum TestPropertyAttributes
    {
        None = 0x00, // Default
        Hidden = 0x01,
        Immutable = 0x02,
        [Obsolete("Use TestObject.Traits collection to create traits")]
        Trait = 0x04,
    }
}
