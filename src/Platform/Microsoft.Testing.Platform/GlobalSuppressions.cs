// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

// We usually do not want to suppress issues through GlobalSuppressions file but in this case we have to do it because
// we are not able to suppress the issue differently.
#pragma warning disable IDE0076
[assembly: SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Source-generated file that cannot match analyzers requirements", Scope = "member", Target = "~M:System.Reflection.NullabilityInfoContext.CheckParameterMetadataType(System.Reflection.ParameterInfo,System.Reflection.NullabilityInfo)")]
#pragma warning restore IDE0076
