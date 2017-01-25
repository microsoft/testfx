// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Pex.Framework.Packages;
using MSTestv2IntelliTestExtension;

[assembly: PexPackageType(typeof(MSTestv2TestFrameworkPackage))]
namespace MSTestv2IntelliTestExtension
{
    using Microsoft.ExtendedReflection.ComponentModel;
    using Microsoft.Pex.Engine.ComponentModel;
    using Microsoft.Pex.Engine.TestFrameworks;

    /// <summary>
    /// Extensions package for MSTestv2.
    /// </summary>
    public class MSTestv2TestFrameworkPackageAttribute : PexPackageAttributeBase
    {
        protected override void Initialize(IEngine engine)
        {
            base.Initialize(engine);

            var testFrameworkService = engine.GetService<IPexTestFrameworkManager>();
            var host = testFrameworkService as IPexComponent;

            testFrameworkService.AddTestFramework(new MSTestv2TestFramework(host));
        }

        public override string Name => "MSTestv2TestFrameworkPackage";
    }

    [MSTestv2TestFrameworkPackage]
    static class MSTestv2TestFrameworkPackage
    {
    }
}
