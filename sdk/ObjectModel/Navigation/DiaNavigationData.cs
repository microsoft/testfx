// <copyright file="DiaNavigationData.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores the navigation data associated with the .exe/.dll file
// </summary>
// <owner>satins</owner> 
// ---------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Navigation;

    /// <summary>
    /// A struct that stores the infomation needed by the navigation: file name, line number, column number.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Dia is a specific name.")]
    public class DiaNavigationData : INavigationData
    {
        private string m_fileName;
        private int m_minLineNumber;
        private int m_maxLineNumber;

        public string FileName
        {
            get
            {
                return m_fileName;
            }
            set
            {
                m_fileName = value;
            }
        }

        public int MinLineNumber
        {
            get
            {
                return m_minLineNumber;
            }
            set
            {
                m_minLineNumber = value;
            }
        }

        public int MaxLineNumber
        {
            get
            {
                return m_maxLineNumber;
            }
            set
            {
                m_maxLineNumber = value;
            }
        }

        public DiaNavigationData(string fileName, int minLineNumber, int maxLineNumber)
        {
            this.m_fileName = fileName;
            this.m_minLineNumber = minLineNumber;
            this.m_maxLineNumber = maxLineNumber;
        }
    }
}