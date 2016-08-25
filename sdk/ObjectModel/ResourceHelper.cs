//****************************************************************************
// ResourceHelper.cs
// Owner: apatters
//
// Copyright (c) Microsoft Corporation.  All rights reserved.
//
// Helper class for things involving resources.
//****************************************************************************

#region Using

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;

#endregion

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Helper class for things involving resources.
    /// </summary>
    public static class ResourceHelper
    {
        #region Constants

        private const string RESOURCE_EXTENSION = ".resources";

        #endregion

        #region Public Static Methods

        /// <summary>
        /// Looks up the resource with the specified name in the assembly provided.
        /// </summary>
        /// <param name="name">Name of the resource to look up.</param>
        /// <param name="assembly">Assembly to look for the resource in.</param>
        /// <param name="cultureInfo">Culture to use when looking up the resource.</param>
        /// <returns>The localized resource or null if one could not be found.</returns>
        public static string GetString(string name, Assembly assembly, CultureInfo cultureInfo)
        {
            ValidateArg.NotNullOrEmpty(name, "name");
            ValidateArg.NotNull(assembly, "assembly");
            ValidateArg.NotNull(cultureInfo, "cultureInfo");

            string result = null;

            // Get all of the base resources in the assembly.
            string[] manifestResourceNames = assembly.GetManifestResourceNames();
            
            // Check each of the base resources for the resource name.
            foreach (string manifestResourceName in manifestResourceNames) {
                if (manifestResourceName.EndsWith(RESOURCE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    String resourceRoot = manifestResourceName.Substring(0, manifestResourceName.Length - RESOURCE_EXTENSION.Length);
                    ResourceManager manager = new ResourceManager(resourceRoot, assembly);

                    result = manager.GetString(name, cultureInfo);

                    if (result != null)
                    {
                        break;
                    }
                }
            }

            return result;
        }
        #endregion
    }
}
