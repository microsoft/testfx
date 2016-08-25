// ---------------------------------------------------------------------------
// <copyright file="ITestLogger.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Interface implemented to log messages and results from tests.  A class that
//     implements this interface will be available for use if it exports its type via
//     MEF, and if its containing assembly is placed in the Extensions folder.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// Interface implemented to log messages and results from tests.  A class that
    /// implements this interface will be available for use if it exports its type via
    /// MEF, and if its containing assembly is placed in the Extensions folder.
    /// </summary>
    public interface ITestLogger
    {
        /// <summary>
        /// Initializes the Test Logger.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="testRunDirectory">Test Run Directory</param>
        void Initialize(TestLoggerEvents events, string testRunDirectory);
    }
}
