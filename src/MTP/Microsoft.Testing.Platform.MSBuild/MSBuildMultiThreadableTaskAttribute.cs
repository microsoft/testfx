// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework;

// MSBuild recognizes this marker by name, allowing the task assembly to load on older hosts.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
[Microsoft.CodeAnalysis.Embedded]
internal sealed class MSBuildMultiThreadableTaskAttribute : Attribute;
