using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;
using System.Collections.Concurrent;


namespace TestGenerator.TestGenerators
{
    public class Generator
    {
        public async Task<ConcurrentDictionary<string, string>> GenerateTestClasses(string text)
        {
            return await ParseFileForClasses(text);
        }

        private async Task<ConcurrentDictionary<string, string>> ParseFileForClasses(string text) {
            var syntaxTree = CSharpSyntaxTree.ParseText(text);
            var root = syntaxTree.GetRoot();
            var compilation = CSharpCompilation.Create("MyCompilation").AddSyntaxTrees(syntaxTree);
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            ConcurrentDictionary<string, string> dict = new ConcurrentDictionary<string, string>();
            Parallel.ForEach(classes, _class => {
                dict.TryAdd(_class.Identifier.ValueText, Print(Genrate(_class, semanticModel)));
            });
            return dict;
        }

        public CompilationUnitSyntax Genrate(ClassDeclarationSyntax Class, SemanticModel semanticModel)
        {
            CompilationUnitSyntax cu = CompilationUnit()
            .AddUsings(UsingDirective(IdentifierName("System")))
            .AddUsings(UsingDirective(IdentifierName("System.Generic")))
            .AddUsings(UsingDirective(IdentifierName("System.Collections.Generic")))
            .AddUsings(UsingDirective(IdentifierName("System.Linq")))
            .AddUsings(UsingDirective(IdentifierName("System.Text")))
            .AddUsings(UsingDirective(IdentifierName("System.Moq")))
            .AddUsings(UsingDirective(IdentifierName("NUnit.Framework")));

            var constructortors = Class.DescendantNodes().
                OfType<ConstructorDeclarationSyntax>();
            ConstructorDeclarationSyntax? constructor = null;
            List<ParameterSyntax>? parameters = null;
            int interfaceMembersMaxAmount = -1;
            int interfaceMembersAmount = 0;
            if (constructortors.Any())
            {
                foreach (var _constructor in constructortors)
                {
                    List<ParameterSyntax> temp = new List<ParameterSyntax>();
                    interfaceMembersAmount = 0;
                    foreach (var p in _constructor.ParameterList.Parameters)
                    {
                        var ps = semanticModel.GetDeclaredSymbol(p);
                        if ((ps!.Type.TypeKind == TypeKind.Interface) ||
                            (ps.Type.Name.Length > 2 && ps.Type.Name[0] == 'I' && char.IsUpper(ps.Type.Name[1])))
                        {
                            temp.Add(p);
                            interfaceMembersAmount++;
                        }
                    }
                    if (_constructor.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)))
                    {
                        if (interfaceMembersAmount > interfaceMembersMaxAmount)
                        {
                            interfaceMembersMaxAmount = interfaceMembersAmount;
                            constructor = _constructor;
                            parameters = temp;
                        }
                    }
                }
            }
            else
            {
                constructor = ConstructorDeclaration(Identifier(Class.Identifier.ValueText))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithBody(Block());
                parameters = new List<ParameterSyntax>();
            }

            NamespaceDeclarationSyntax ns = NamespaceDeclaration(IdentifierName("Tests"));
            ClassDeclarationSyntax c = GenerateTestClass(Class.Identifier.ValueText);
            int num = 1;
            foreach (var p in parameters!)
            {
                c = c.AddMembers(GenerateField($"Mock<{p.Type}>", $"_dependency{num}"));
                num++;
            }
            c = c.AddMembers(GenerateField(Class.Identifier.ValueText, $"_myClassUnderTest"));
            c = c.AddMembers(GenerateSetUpMethod(constructor!, semanticModel, Class));
            var publicMethods = Class.Members.OfType<MethodDeclarationSyntax>().Where(method => method.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)));
            foreach (var method in publicMethods)
            {
                c = c.AddMembers(GenerateTestMethod(method, semanticModel));
            }
            ns = ns.AddMembers(c);
            cu = cu.AddMembers(ns);
            return cu;
        }

        private FieldDeclarationSyntax GenerateField(string type, string name) {
            VariableDeclarationSyntax fieldDeclaration = VariableDeclaration(
            IdentifierName(type))
            .AddVariables(VariableDeclarator(Identifier(name)));
            FieldDeclarationSyntax field = FieldDeclaration(fieldDeclaration)
                .AddModifiers(Token(SyntaxKind.PrivateKeyword));
            return field;
        }

        private MethodDeclarationSyntax GenerateTestMethod(MethodDeclarationSyntax method,
            SemanticModel semanticModel) {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            List<ArgumentSyntax> arguments = new List<ArgumentSyntax>();

            var _params = method.ParameterList.Parameters;

            foreach (var p in _params)
            {
                var ps = semanticModel.GetDeclaredSymbol(p);
                if ((ps!.Type.TypeKind == TypeKind.Interface) ||
                            (ps.Type.Name.Length > 2 && ps.Type.Name[0] == 'I' && char.IsUpper(ps.Type.Name[1])))
                {
                    var s = GenerateMoqType(p.Type!, p.Identifier.ValueText);
                    var a = Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("n"),
                                    IdentifierName("Object")));
                    arguments.Add(a);
                    statements.Add(s);
                }
                else
                {
                    var variableDeclaration = GeneratePrimitiveType(p.Type!, p.Identifier.ValueText);
                    var a = Argument(IdentifierName(p.Identifier.ValueText));
                    arguments.Add(a);
                    statements.Add(LocalDeclarationStatement(variableDeclaration));
                }
            }
            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));
            if (method.ReturnType.ToString() == "void")
            {
                var res = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Assert"),
                        IdentifierName("DoesNotThrow")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                ParenthesizedLambdaExpression()
                                .WithBlock(
                                    Block(
                                        SingletonList<StatementSyntax>(
                                            ExpressionStatement(
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        IdentifierName("_myClassUnderTest"),
                                                        IdentifierName(method.Identifier.ValueText)))
                                                .WithArgumentList(argumentList))))))))));
                statements.Add(res);
            }
            else 
            {
                var typeSyntax = method.ReturnType;
                var v = VariableDeclaration(
                    typeSyntax,
                    SingletonSeparatedList(
                    VariableDeclarator(
                            Identifier("actual")
                        ).WithInitializer(
                            EqualsValueClause(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("_myClassUnderTest"),
                                        IdentifierName(method.Identifier.ValueText)))
                                .WithArgumentList(argumentList)))));
                statements.Add(LocalDeclarationStatement(v));
                var v1 = GeneratePrimitiveType(typeSyntax, "expected");
                statements.Add(LocalDeclarationStatement(v1));
                var v2 = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("Assert"),
                            IdentifierName("That")))
                    .WithArgumentList(
                        ArgumentList(
                            SeparatedList<ArgumentSyntax>(
                                new SyntaxNodeOrToken[]{
                                    Argument(
                                        IdentifierName("actual")),
                                    Token(SyntaxKind.CommaToken),
                                    Argument(
                                        InvocationExpression(
                                            MemberAccessExpression(
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                IdentifierName("Is"),
                                                IdentifierName("EqualTo")))
                                        .WithArgumentList(
                                            ArgumentList(
                                                SingletonSeparatedList<ArgumentSyntax>(
                                                    Argument(
                                                        IdentifierName("expected"))))))}))));
                statements.Add(v2);
                var res = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Assert"),
                        IdentifierName("Fail")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList<ArgumentSyntax>(
                            Argument(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal("autogenerated")))))));
                statements.Add(res);
            }
            
            MethodDeclarationSyntax m = MethodDeclaration(
            PredefinedType(Token(SyntaxKind.VoidKeyword)),
            Identifier(method.Identifier.ValueText +"Test"))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(
            AttributeList(SingletonSeparatedList(
            Attribute(IdentifierName("Test")))))
            .WithBody(Block(statements));
            return m;
        }

        private ClassDeclarationSyntax GenerateTestClass(string name) {
            ClassDeclarationSyntax c = ClassDeclaration(name + "Tests")
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .WithAttributeLists(
                SingletonList(
                    AttributeList(
                        SingletonSeparatedList(
                            Attribute(
                                IdentifierName("TestFixture")
                            )
                        )
                    )
                )
            );
            return c;
        }

        private MethodDeclarationSyntax GenerateSetUpMethod(ConstructorDeclarationSyntax constructor, 
            SemanticModel semanticModel, ClassDeclarationSyntax Class) 
        {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            List<ArgumentSyntax> arguments = new List<ArgumentSyntax>();
            int numInterfaces = 1;
            int numParams = 1;
            var parameters = constructor.ParameterList.Parameters;
            foreach (var p in parameters) {
                var ps = semanticModel.GetDeclaredSymbol(p);
                if ((ps!.Type.TypeKind == TypeKind.Interface) ||
                    (ps.Type.Name.Length > 2 && ps.Type.Name[0] == 'I' && char.IsUpper(ps.Type.Name[1])))
                {
                    var s = GenerateMoqType(p.Type!, $"_dependency{numInterfaces}");
                    var a = Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName($"_dependency{numInterfaces}"),
                                    IdentifierName("Object")));
                    arguments.Add(a);
                    statements.Add(s);
                    numInterfaces++;
                } 
                else
                {
                    var variableDeclaration = GeneratePrimitiveType(p.Type!, $"param{numParams}");
                    var a = Argument(IdentifierName($"param{numParams}"));
                    arguments.Add(a);
                    numParams++;
                    statements.Add(LocalDeclarationStatement(variableDeclaration));
                }
            }
            var argumentList = ArgumentList(SeparatedList(arguments));

            var c = ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("_myClassUnderTest"),
                    ObjectCreationExpression(
                        IdentifierName(Class.Identifier.ValueText))
                    .WithArgumentList(argumentList)));
            statements.Add(c);
            MethodDeclarationSyntax m = MethodDeclaration(
            PredefinedType(Token(SyntaxKind.VoidKeyword)),
            Identifier("SetUp"))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(
            AttributeList(SingletonSeparatedList(
            Attribute(IdentifierName("SetUp")))))
            .WithBody(Block(statements));
            return m;
        }

        private VariableDeclarationSyntax GeneratePrimitiveType(TypeSyntax typeSyntax, string name) {
            return VariableDeclaration(
                typeSyntax,
                SingletonSeparatedList(
                    VariableDeclarator(
                        Identifier(name)
                    ).WithInitializer(
                        EqualsValueClause(
                            DefaultExpression(typeSyntax)
                        )
                    )
                )
            );
        }

        private ExpressionStatementSyntax GenerateMoqType(TypeSyntax parameterType, string name) { 
            return ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(name),
                        ObjectCreationExpression(
                            GenericName(
                                Identifier("Mock")
                                    ).WithTypeArgumentList(
                                        TypeArgumentList(
                                            SingletonSeparatedList<TypeSyntax>(
                                                parameterType
                                            )
                                        )
                                    )
                                )
                            )
                        );
        }

        private string Print(CompilationUnitSyntax cu) {
            AdhocWorkspace cw = new AdhocWorkspace();
            OptionSet options = cw.Options;
            options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, false);
            options = options.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, false);
            SyntaxNode formattedNode = Formatter.Format(cu, cw, options);
            StringBuilder sb = new StringBuilder();
            using (StringWriter writer = new StringWriter(sb))
            {
                formattedNode.WriteTo(writer);
            }

            Console.WriteLine(sb.ToString());
            return sb.ToString();
        }
    }
}
