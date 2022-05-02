// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;

    /// <summary>
    /// The helper.
    /// </summary>
    internal static class Helper
    {
        /// <summary>
        /// The check parameter not null.
        /// </summary>
        /// <param name="param">
        /// The parameter.
        /// </param>
        /// <param name="parameterName">
        /// The parameter name.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <exception cref="ArgumentNullException"> Throws argument null exception when parameter is null. </exception>
        internal static void CheckParameterNotNull(object param, string parameterName, string message)
        {
            if (param == null)
            {
                throw new ArgumentNullException(parameterName, message);
            }
        }

        /// <summary>
        /// The check parameter not null or empty.
        /// </summary>
        /// <param name="param">
        /// The parameter.
        /// </param>
        /// <param name="parameterName">
        /// The parameter name.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <exception cref="ArgumentException"> Throws ArgumentException when parameter is null. </exception>
        internal static void CheckParameterNotNullOrEmpty(string param, string parameterName, string message)
        {
            if (string.IsNullOrEmpty(param))
            {
                throw new ArgumentException(message, parameterName);
            }
        }
    }
}
