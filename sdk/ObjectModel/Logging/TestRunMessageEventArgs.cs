// ---------------------------------------------------------------------------
// <copyright file="TestRunMessageEventArgs.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Event arguments used for raising Test Run Message events.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Runtime.Serialization;

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
{
    /// <summary>
    /// Event arguments used for raising Test Run Message events.
    /// </summary>
    [DataContract]
    public class TestRunMessageEventArgs : EventArgs
    {
        #region Constructor

        /// <summary>
        /// Initializes with the level and the message for the event.
        /// </summary>
        /// <param name="level">Level of the message.</param>
        /// <param name="message">The message.</param>
        public TestRunMessageEventArgs(TestMessageLevel level, string message)
        {
            if (StringUtilities.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(CommonResources.CannotBeNullOrEmpty, "message");
            }
            if (level < TestMessageLevel.Informational || level > TestMessageLevel.Error)
            {
                throw new ArgumentOutOfRangeException("level");
            }

            Level = level;
            Message = message;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The message.
        /// </summary>
        [DataMember]
        public string Message { get; set; }
        
        /// <summary>
        /// Level of the message.
        /// </summary>
        [DataMember]
        public TestMessageLevel Level { get; set; }

        #endregion
    }
}
