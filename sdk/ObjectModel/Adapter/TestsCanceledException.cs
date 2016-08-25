// ---------------------------------------------------------------------------
// <copyright file="TestCanceledException.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Exception thrown by the framework when an executor attempts to send 
//     test result to the framework when the test is canceled.  
// </summary>
// <owner>aseemb</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// Exception thrown by the framework when an executor attempts to send 
    /// test result to the framework when the test is canceled.  
    /// </summary>
#if !SILVERLIGHT
    [Serializable]
#endif
    public class TestCanceledException : Exception
    {
        #region Constructors

        /// <summary>
        /// Creates a new TestCanceledException
        /// </summary>
        public TestCanceledException()
        {
        }

        /// <summary>
        /// Initializes with the message.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public TestCanceledException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes with message and inner exception.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        /// <param name="innerException">The inner exception.</param>
        public TestCanceledException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if !SILVERLIGHT
        /// <summary>
        /// Seralization constructor.
        /// </summary>
        protected TestCanceledException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

#endif
        #endregion
    }
}
