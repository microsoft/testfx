// ---------------------------------------------------------------------------
// <copyright file="TestMessageLevel.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Levels for test messages.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
{
    /// <summary>
    /// Levels for test messages.
    /// </summary>
    public enum TestMessageLevel
    {
        /// <summary>
        /// Informational message.
        /// </summary>
        Informational = 0,
        
        /// <summary>
        /// Warning message.
        /// </summary>
        Warning = 1,
        
        /// <summary>
        /// Error message.
        /// </summary>
        Error = 2
    }
}
