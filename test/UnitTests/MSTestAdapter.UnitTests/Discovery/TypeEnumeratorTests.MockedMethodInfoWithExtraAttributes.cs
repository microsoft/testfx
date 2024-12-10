// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

#if !NET6_0_OR_GREATER
using Polyfills;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public partial class TypeEnumeratorTests
{
    private sealed class MockedMethodInfoWithExtraAttributes : MethodInfo
    {
        private readonly MethodInfo _original;
        private readonly Attribute[] _extraAttributes;

        public MockedMethodInfoWithExtraAttributes(MethodInfo original, params Attribute[] extraAttributes)
        {
            _original = original;
            _extraAttributes = extraAttributes;
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => _original.ReturnTypeCustomAttributes;

        public override RuntimeMethodHandle MethodHandle => _original.MethodHandle;

        public override MethodAttributes Attributes => _original.Attributes;

        public override string Name => _original.Name;

        public override Type DeclaringType => _original.DeclaringType;

        public override Type ReflectedType => _original.ReflectedType;

        public override IEnumerable<CustomAttributeData> CustomAttributes => _original.CustomAttributes;

        public override int MetadataToken => _original.MetadataToken;

        public override Module Module => _original.Module;

        public override MethodImplAttributes MethodImplementationFlags => _original.MethodImplementationFlags;

        public override CallingConventions CallingConvention => _original.CallingConvention;

        public override bool IsGenericMethodDefinition => _original.IsGenericMethodDefinition;

        public override bool ContainsGenericParameters => _original.ContainsGenericParameters;

        public override bool IsGenericMethod => _original.IsGenericMethod;

        public override bool IsSecurityCritical => _original.IsSecurityCritical;

        public override bool IsSecuritySafeCritical => _original.IsSecuritySafeCritical;

        public override bool IsSecurityTransparent => _original.IsSecurityTransparent;

        public override MemberTypes MemberType => _original.MemberType;

        public override Type ReturnType => _original.ReturnType;

        public override ParameterInfo ReturnParameter => _original.ReturnParameter;

        public override Delegate CreateDelegate(Type delegateType) => _original.CreateDelegate(delegateType);

        public override Delegate CreateDelegate(Type delegateType, object target) => _original.CreateDelegate(delegateType, target);

        public override MethodInfo GetBaseDefinition() => _original.GetBaseDefinition();

        public override object[] GetCustomAttributes(bool inherit) => _original.GetCustomAttributes().Concat(_extraAttributes).ToArray();

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => _original.GetCustomAttributes().Concat(_extraAttributes.Where(a => a.GetType().IsAssignableTo(attributeType))).ToArray();

        public override IList<CustomAttributeData> GetCustomAttributesData() => _original.GetCustomAttributesData();

        public override Type[] GetGenericArguments() => _original.GetGenericArguments();

        public override MethodInfo GetGenericMethodDefinition() => _original.GetGenericMethodDefinition();

        public override MethodBody GetMethodBody() => _original.GetMethodBody();

        public override MethodImplAttributes GetMethodImplementationFlags() => _original.GetMethodImplementationFlags();

        public override ParameterInfo[] GetParameters() => _original.GetParameters();

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
            => _original.Invoke(obj, invokeAttr, binder, parameters, culture);

        public override bool IsDefined(Type attributeType, bool inherit)
            => _original.IsDefined(attributeType, inherit) || _extraAttributes.Any(a => a.GetType().IsAssignableTo(attributeType));

        public override MethodInfo MakeGenericMethod(params Type[] typeArguments) => _original.MakeGenericMethod(typeArguments);

        public override string ToString() => _original.ToString();
    }
}
