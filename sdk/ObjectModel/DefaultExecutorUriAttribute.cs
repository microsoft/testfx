// ---------------------------------------------------------------------------
// <copyright file="DefaultExecutorUriAttribute.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//      This attribute is applied on the discoverers to inform the framework about their default executor. 
// </summary>
// <owner>aseemb</owner> 
// ---------------------------------------------------------------------------
using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// This attribute is applied on the discoverers to inform the framework about their default executor. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DefaultExecutorUriAttribute : Attribute
    {
        #region Constructor

        /// <summary>
        /// Initializes with the Uri of the executor.
        /// </summary>
        /// <param name="defaultExecutorUri">The Uri of the executor</param>
        public DefaultExecutorUriAttribute(string executorUri)
        {
            if (StringUtilities.IsNullOrWhiteSpace(executorUri))
            {
                throw new ArgumentException(CommonResources.CannotBeNullOrEmpty, "executorUri");
            }

            ExecutorUri = executorUri;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The Uri of the Test Executor.
        /// </summary>
        public string ExecutorUri { get; private set; }

        #endregion

    }
}
