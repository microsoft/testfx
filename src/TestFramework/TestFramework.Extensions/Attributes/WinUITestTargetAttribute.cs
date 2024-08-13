// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

/// <summary>
/// Specifies <see cref="UI.Xaml.Application" /> derived class to run UI tests on.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class WinUITestTargetAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WinUITestTargetAttribute"/> class.
    /// </summary>
    /// <param name="applicationType">
    /// Specifies <see cref="UI.Xaml.Application" /> derived class to run UI tests on.
    /// </param>
    public WinUITestTargetAttribute(Type applicationType)
    {
        Guard.NotNull(applicationType);

        if (!typeof(UI.Xaml.Application).IsAssignableFrom(applicationType))
        {
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, FrameworkMessages.ArgumentXMustDeriveFromClassY, nameof(applicationType), "Microsoft.UI.Xaml.Application"), nameof(applicationType));
        }

        ApplicationType = applicationType;
    }

    /// <summary>
    /// Gets the <see cref="UI.Xaml.Application" /> class.
    /// </summary>
    public Type ApplicationType { get; }
}
#endif
