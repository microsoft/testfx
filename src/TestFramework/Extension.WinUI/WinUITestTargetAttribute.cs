// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer
{
    using System;

    /// <summary>
    /// Specifies <see cref="Microsoft.UI.Xaml.Application" /> derived class to run UI tests on.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public class WinUITestTargetAttribute : Attribute
    {
        private readonly Type applicationType;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinUITestTargetAttribute"/> class.
        /// </summary>
        /// <param name="application">
        /// Specifies <see cref="Microsoft.UI.Xaml.Application" /> derived class to run UI tests on.
        /// </param>
        public WinUITestTargetAttribute(Type application)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            if (!typeof(Microsoft.UI.Xaml.Application).IsAssignableFrom(application))
            {
                throw new ArgumentException(string.Format(FrameworkMessages.ArgumentXMustDeriveFromClassY, nameof(application), "Microsoft.UI.Xaml.Application"), nameof(application));
            }

            this.applicationType = application;
        }

        /// <summary>
        /// Gets the <see cref="Microsoft.UI.Xaml.Application" /> class.
        /// </summary>
        public Type ApplicationType => this.applicationType;
    }
}
