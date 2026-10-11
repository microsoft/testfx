using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnskipClosedTests.Tool;

internal static partial class InventoryEngine
{
    private static OwnerIdentity CreateMethodOwner(MethodDeclarationSyntax method, ToolConfig config)
    {
        TypeDeclarationSyntax? type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (type is null)
        {
            throw new ContractException("An Ignore attribute on a method has no actual containing type.");
        }

        string typeFqn = TypeFqn(type);
        string signature = MethodSignature(method);
        bool isTest = method.AttributeLists.SelectMany(static list => list.Attributes)
            .Any(attribute => AttributeMatches(attribute, config.TestAttributeNames));
        return new OwnerIdentity
        {
            Kind = "method",
            Namespace = NamespaceName(type),
            ContainingTypes = ContainingTypeNames(type),
            TypeFqn = typeFqn,
            DeclarationId = MethodDeclarationId(method),
            MethodName = method.Identifier.ValueText,
            MethodSignature = signature,
            TestFqns = isTest ? [$"{typeFqn}.{method.Identifier.ValueText}"] : [],
        };
    }

    private static (OwnerIdentity Owner, List<string> Deferrals) CreateClassOwner(
        ClassDeclarationSyntax type,
        ToolConfig config,
        IReadOnlyDictionary<string, int> declarationCounts)
    {
        string typeFqn = TypeFqn(type);
        List<string> deferrals = [];
        List<string> containingTypes = ContainingTypeNames(type);
        if (containingTypes.Count > 1)
        {
            deferrals.Add("class_is_nested");
        }

        if (type.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            deferrals.Add("class_is_partial");
        }

        if (type.BaseList is not null && type.BaseList.Types.Count > 0)
        {
            deferrals.Add("class_has_base_types");
        }

        if (declarationCounts.GetValueOrDefault(typeFqn) != 1)
        {
            deferrals.Add("duplicate_type_declarations");
        }

        List<string> tests = type.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(method => method.AttributeLists.SelectMany(static list => list.Attributes)
                .Any(attribute => AttributeMatches(attribute, config.TestAttributeNames)))
            .Select(method => $"{typeFqn}.{method.Identifier.ValueText}")
            .Order(StringComparer.Ordinal)
            .ToList();
        if (tests.Count == 0)
        {
            deferrals.Add("no_enumerated_tests");
        }

        if (tests.Distinct(StringComparer.Ordinal).Count() != tests.Count)
        {
            deferrals.Add("ambiguous_test_fqns");
        }

        OwnerIdentity owner = new()
        {
            Kind = "class",
            Namespace = NamespaceName(type),
            ContainingTypes = containingTypes,
            TypeFqn = typeFqn,
            DeclarationId = $"T:{typeFqn}",
            TestFqns = tests.Distinct(StringComparer.Ordinal).ToList(),
        };
        return (owner, deferrals.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }

    private static string StableOwnerId(
        string repository,
        string path,
        OwnerIdentity owner,
        MemberDeclarationSyntax declaration)
    {
        int declarationOrdinal = declaration switch
        {
            MethodDeclarationSyntax method => method.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(candidate => string.Equals(
                    MethodDeclarationId(candidate),
                    owner.DeclarationId,
                    StringComparison.Ordinal))
                .Count(candidate => candidate.SpanStart < method.SpanStart) + 1,
            TypeDeclarationSyntax type => type.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(candidate => string.Equals(
                    $"T:{TypeFqn(candidate)}",
                    owner.DeclarationId,
                    StringComparison.Ordinal))
                .Count(candidate => candidate.SpanStart < type.SpanStart) + 1,
            _ => throw new ContractException("Unsupported owner declaration kind."),
        };
        return JsonSupport.Sha256(
            $"owner-v1\0{repository}\0{path}\0{owner.DeclarationId}\0{declarationOrdinal}");
    }

    private static bool HasGeneratedMarker(MemberDeclarationSyntax declaration) =>
        HasDirectGeneratedMarker(declaration) ||
        declaration.Ancestors().OfType<TypeDeclarationSyntax>().Any(HasDirectGeneratedMarker);

    private static bool HasDirectGeneratedMarker(MemberDeclarationSyntax declaration) =>
        declaration.AttributeLists
            .SelectMany(static list => list.Attributes)
            .Any(static attribute =>
            {
                string name = attribute.Name.WithoutTrivia().ToFullString()
                    .Replace("global::", "", StringComparison.Ordinal)
                    .Split('.')
                    .Last();
                if (name.EndsWith("Attribute", StringComparison.Ordinal))
                {
                    name = name[..^"Attribute".Length];
                }

                return name is "GeneratedCode" or "CompilerGenerated";
            });

    private static string MethodSignature(MethodDeclarationSyntax method)
    {
        string explicitInterface = method.ExplicitInterfaceSpecifier is null
            ? ""
            : $"{method.ExplicitInterfaceSpecifier.Name.WithoutTrivia().ToFullString()}.";
        string arity = method.TypeParameterList is null ? "" : $"`{method.TypeParameterList.Parameters.Count}";
        string parameters = string.Join(",",
            method.ParameterList.Parameters.Select(static parameter =>
                $"{parameter.Modifiers.ToFullString().Trim()}:{parameter.Type?.WithoutTrivia().ToFullString() ?? "?"}"));
        return $"{explicitInterface}{method.Identifier.ValueText}{arity}({parameters})";
    }

    private static string MethodDeclarationId(MethodDeclarationSyntax method)
    {
        TypeDeclarationSyntax? type = method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (type is null)
        {
            throw new ContractException("A method owner has no actual containing type.");
        }

        return $"M:{TypeFqn(type)}.{MethodSignature(method)}";
    }

    private static string TypeFqn(TypeDeclarationSyntax type)
    {
        List<string> parts = [];
        string namespaceName = NamespaceName(type);
        if (namespaceName.Length > 0)
        {
            parts.Add(namespaceName);
        }

        parts.AddRange(ContainingTypeNames(type));
        return string.Join('.', parts);
    }

    private static string NamespaceName(SyntaxNode node) =>
        string.Join('.',
            node.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Reverse()
                .Select(static declaration => declaration.Name.WithoutTrivia().ToFullString()));

    private static List<string> ContainingTypeNames(TypeDeclarationSyntax type) =>
        type.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .Reverse()
            .Select(static declaration =>
                declaration.TypeParameterList is null
                    ? declaration.Identifier.ValueText
                    : $"{declaration.Identifier.ValueText}`{declaration.TypeParameterList.Parameters.Count}")
            .ToList();
}
