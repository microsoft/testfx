// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    #region Access method
    /// <summary>
    /// Enumeration for how how we access data rows in data driven testing.
    /// </summary>
    public enum DataAccessMethod
    {
        /// <summary>
        /// Rows are returned in sequential order.
        /// </summary>
        Sequential,

        /// <summary>
        /// Rows are returned in random order.
        /// </summary>
        Random,
    }
    #endregion
}