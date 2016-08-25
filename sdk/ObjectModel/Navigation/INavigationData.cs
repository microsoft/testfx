// <copyright file="INavigationData.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores the navigation data associated with the .exe/.dll file
// </summary>
// <owner>aajohn</owner> 
// ---------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Navigation
{
    /// <summary>
    /// Stores the navigation data associated with the .exe/.dll file
    /// </summary>
    public interface INavigationData
    {
        /// <summary>
        /// Gets or sets the file name of the file containing the method being navigated.
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        /// Gets or sets the min line number of the method being navigated in the file.
        /// </summary>
        int MinLineNumber { get; set; }

        /// <summary>
        /// Gets or sets the max line number of the method being navigated in the file.
        /// </summary>
        int MaxLineNumber { get; set; }
    }
}
