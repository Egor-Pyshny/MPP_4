using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using System.Xml.Linq;


namespace MPP_4.TestGenerators
{
    public class Generator
    {
        public void Genrate(Type Class) {
            CompilationUnitSyntax cu = CompilationUnit()
            .AddUsings(UsingDirective(IdentifierName("System")))
            .AddUsings(UsingDirective(IdentifierName("System.Generic")))
            .AddUsings(UsingDirective(IdentifierName("System.Collections.Generic")))
            .AddUsings(UsingDirective(IdentifierName("System.Linq")))
            .AddUsings(UsingDirective(IdentifierName("System.Text")))
            .AddUsings(UsingDirective(IdentifierName("System.Moq")))
            .AddUsings(UsingDirective(IdentifierName("NUnit.Framework")));

            ConstructorInfo? constructor = null;
            int interfaceMembersMaxAmount = -1;
            foreach (var constr in Class.GetConstructors())
            {
                int interfaceMembersAmount = 0;
                foreach (var param in constr.GetParameters())
                {
                    if (param.ParameterType.IsInterface) interfaceMembersAmount++;
                }
                if (constr.IsPublic && interfaceMembersAmount > interfaceMembersMaxAmount)
                {
                    interfaceMembersMaxAmount = interfaceMembersAmount;
                    constructor = constr;
                }
            }
            if (constructor == null) throw new NotImplementedException();

            NamespaceDeclarationSyntax ns = NamespaceDeclaration(IdentifierName("Tests"));

            ClassDeclarationSyntax c = GenerateTestClass(Class);
            int num = 1;
            foreach (var p in constructor.GetParameters()) {
                if (p.ParameterType.IsInterface) { 
                    c = c.AddMembers(GenerateField($"Mock<{p.ParameterType.Name}>", $"_dependency{num}"));
                    num++;
                }
            }
            c = c.AddMembers(GenerateField(Class.Name, $"_myClassUnderTest"));

            var testMethods = Class.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in testMethods)
            {
                c = c.AddMembers(GenerateTestMethod(method));
            }
            c = c.AddMembers(GenerateSetUpMethod(constructor));
            ns = ns.AddMembers(c);
            cu = cu.AddMembers(ns);

            Print(cu);
        }

        private FieldDeclarationSyntax GenerateField(string type, string name) {
            VariableDeclarationSyntax fieldDeclaration = VariableDeclaration(
            IdentifierName(type))
            .AddVariables(VariableDeclarator(Identifier(name)));
            FieldDeclarationSyntax field = FieldDeclaration(fieldDeclaration)
                .AddModifiers(Token(SyntaxKind.PrivateKeyword));
            return field;
        }

        private MethodDeclarationSyntax GenerateTestMethod(MethodInfo method) {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            List<ArgumentSyntax> arguments = new List<ArgumentSyntax>();

            var _params = method.GetParameters();

            foreach (var p in _params)
            {
                if (p.ParameterType.IsInterface)
                {
                    var s = GenerateMoqType(p.ParameterType, p.Name);
                    var a = Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(p.Name),
                                    IdentifierName("Object")));
                    arguments.Add(a);
                    statements.Add(s);
                }
                else
                {
                    var variableDeclaration = GeneratePrimitiveType(p.ParameterType, p.Name);
                    var a = Argument(IdentifierName(p.Name));
                    arguments.Add(a);
                    statements.Add(LocalDeclarationStatement(variableDeclaration));
                }
            }
            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));
            if (method.ReturnType.FullName == "System.Void")
            {
                var v = ExpressionStatement(InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("_myClassUnderTest"),
                            IdentifierName(method.Name)))
                        .WithArgumentList(argumentList));
                statements.Add(v);
            }
            else 
            {
                var parameterType = method.ReturnType;
                var typeSyntax = ParseTypeName(parameterType.Name);
                if (parameterType.IsPrimitive)
                {
                    var compilation = CSharpCompilation.Create("temp").AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                    var typeSymbol = compilation.GetTypeByMetadataName(parameterType.FullName);
                    typeSyntax = SyntaxFactory.ParseTypeName(typeSymbol.ToDisplayString());
                }
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
                                        IdentifierName(method.Name)))
                                .WithArgumentList(argumentList)))));
                statements.Add(LocalDeclarationStatement(v));
                var v1 = GeneratePrimitiveType(parameterType, "expected");
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
            }
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
            MethodDeclarationSyntax m = MethodDeclaration(
            PredefinedType(Token(SyntaxKind.VoidKeyword)),
            Identifier(method.Name+"Test"))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(
            AttributeList(SingletonSeparatedList(
            Attribute(IdentifierName("Test")))))
            .WithBody(Block(statements));
            return m;
        }

        private ClassDeclarationSyntax GenerateTestClass(Type t) {
            ClassDeclarationSyntax c = ClassDeclaration(t.Name + "Tests")
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

        private MethodDeclarationSyntax GenerateSetUpMethod(ConstructorInfo constructor) {
            List<StatementSyntax> statements = new List<StatementSyntax>();
            List<ArgumentSyntax> arguments = new List<ArgumentSyntax>();
            int numInterfaces = 1;
            int numParams = 1;
            foreach (var p in constructor.GetParameters()) {
                if (p.ParameterType.IsInterface) 
                {
                    var s = GenerateMoqType(p.ParameterType, $"_dependency{numInterfaces}");
                    var a = Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName($"_dependency{numInterfaces}"),
                                    IdentifierName("Object")));
                    arguments.Add(a);
                    statements.Add(s);
                    numInterfaces++;
                } 
                else
                {
                    var variableDeclaration = GeneratePrimitiveType(p.ParameterType, $"param{numParams}");
                    var a = Argument(IdentifierName($"param{numParams}"));
                    arguments.Add(a);
                    numParams++;
                    statements.Add(LocalDeclarationStatement(variableDeclaration));
                }
            }
            var argumentList = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments));

            var c = ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("_myClassUnderTest"),
                    ObjectCreationExpression(
                        IdentifierName(constructor.DeclaringType.Name))
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

        private VariableDeclarationSyntax GeneratePrimitiveType(Type parameterType, string name) {
            var typeSyntax = ParseTypeName(parameterType.Name);
            if (parameterType.IsPrimitive)
            {
                var compilation = CSharpCompilation.Create("temp").AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                var typeSymbol = compilation.GetTypeByMetadataName(parameterType.FullName);
                typeSyntax = SyntaxFactory.ParseTypeName(typeSymbol.ToDisplayString());
            }
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

        private ExpressionStatementSyntax GenerateMoqType(Type parameterType, string name) { 
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
                                                IdentifierName(parameterType.Name)
                                            )
                                        )
                                    )
                                )
                            )
                        );
        }

        private void Print(CompilationUnitSyntax cu) {
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
        }
    }
}
