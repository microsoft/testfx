// ---------------------------------------------------------------------------
// <copyright file="FriendlyNameAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//      This attribute is applied to Loggers so they can be uniquely identified.
//      It indicates the Friendly Name which uniquely identifies the extension.
//      This attribute is optional.
// </summary>
// <owner>t-aseemg</owner> 
// ---------------------------------------------------------------------------


using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// This attribute is applied to Loggers so they can be uniquely identified.
    /// It indicates the Friendly Name which uniquely identifies the extension.
    /// This attribute is optional.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class FriendlyNameAttribute : Attribute
    {
        #region Constructor

        /// <summary>
        /// Initializes with the Friendly Name of the logger.
        /// </summary>
        /// <param name="friendlyName">The friendly name of the Logger</param>
        public FriendlyNameAttribute(string friendlyName)
        {
            if (StringUtilities.IsNullOrWhiteSpace(friendlyName))
            {
                throw new ArgumentException(CommonResources.CannotBeNullOrEmpty, "friendlyName");
            }

            FriendlyName = friendlyName;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The friendly Name of the Test Logger.
        /// </summary>
        public string FriendlyName { get; private set; }

        #endregion

    }
}
