// ---------------------------------------------------------------------------
// <copyright file="ITestLoggerWithParams.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Interface extends ITestLogger and adds capability to pass
//     parameters to loggers such as TfsPublisher.
//     This is meant for internal consumption (For ex. TfsPublisher implements
//     this interface to get parameters from vstest.console command).
//     Its containing assembly is placed in the Extensions folder.
// </summary>
// <owner>shyvel</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    /// <summary>
    /// This Interface extends ITestLogger and adds capability to pass
    /// parameters to loggers such as TfsPublisher.
    /// Currently it is marked for internal consumption (ex: TfsPublisher)
    /// </summary>
    public interface ITestLoggerWithParameters : ITestLogger
    {
        /// <summary>
        /// Initializes the Test Logger with given parameters.
        /// </summary>
        /// <param name="events">Events that can be registered for.</param>
        /// <param name="parameters">Collection of parameters</param>
        void Initialize(TestLoggerEvents events, Dictionary<string, string> parameters);
    }
}