﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Specifies <see cref="Microsoft.UI.Xaml.Application" /> derived class to run UI tests on.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class WinUITestTargetAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WinUITestTargetAttribute"/> class.
        /// </summary>
        /// <param name="applicationType">
        /// Specifies <see cref="Microsoft.UI.Xaml.Application" /> derived class to run UI tests on.
        /// </param>
        public WinUITestTargetAttribute(Type applicationType)
        {
            if (applicationType == null)
            {
                throw new ArgumentNullException(nameof(applicationType));
            }

            if (!typeof(UI.Xaml.Application).IsAssignableFrom(applicationType))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.ArgumentXMustDeriveFromClassY, nameof(applicationType), "Microsoft.UI.Xaml.Application"), nameof(applicationType));
            }

            this.ApplicationType = applicationType;
        }

        /// <summary>
        /// Gets the <see cref="Microsoft.UI.Xaml.Application" /> class.
        /// </summary>
        public Type ApplicationType { get; }
    }
}
