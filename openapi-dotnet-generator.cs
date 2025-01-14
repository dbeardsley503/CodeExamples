using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

public class OpenApiDotNetGenerator
{
    private readonly OpenApiDocument _openApiDocument;
    private readonly string _namespace;
    private readonly string _clientName;

    public OpenApiDotNetGenerator(string specPath, string namespaceName, string clientName)
    {
        using var stream = File.OpenRead(specPath);
        var reader = new OpenApiStreamReader();
        _openApiDocument = reader.Read(stream, out var diagnostic);
        _namespace = namespaceName;
        _clientName = clientName;
    }

    public void GenerateLibrary(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        
        // Generate models
        foreach (var schema in _openApiDocument.Components.Schemas)
        {
            var modelClass = GenerateModelClass(schema.Key, schema.Value);
            var fileName = Path.Combine(outputDirectory, $"{schema.Key}.cs");
            File.WriteAllText(fileName, modelClass.NormalizeWhitespace().ToFullString());
        }

        // Generate API client
        var clientClass = GenerateClientClass();
        var clientFileName = Path.Combine(outputDirectory, $"{_clientName}.cs");
        File.WriteAllText(clientFileName, clientClass.NormalizeWhitespace().ToFullString());
    }

    private CompilationUnitSyntax GenerateModelClass(string className, OpenApiSchema schema)
    {
        var properties = new List<PropertyDeclarationSyntax>();

        foreach (var property in schema.Properties)
        {
            var propertyType = GetCSharpType(property.Value);
            properties.Add(
                PropertyDeclaration(
                    IdentifierName(propertyType),
                    Identifier(property.Key))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(
                    AccessorList(
                        List(new[]
                        {
                            AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                            AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                        })))
            );
        }

        var classDeclaration = ClassDeclaration(className)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithMembers(List<MemberDeclarationSyntax>(properties));

        return CompilationUnit()
            .WithUsings(List(new[]
            {
                UsingDirective(IdentifierName("System")),
                UsingDirective(IdentifierName("System.Text.Json.Serialization"))
            }))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(
                NamespaceDeclaration(IdentifierName(_namespace))
                    .WithMembers(SingletonList<MemberDeclarationSyntax>(classDeclaration))));
    }

    private CompilationUnitSyntax GenerateClientClass()
    {
        var methods = new List<MethodDeclarationSyntax>();

        foreach (var path in _openApiDocument.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var methodName = GetMethodName(path.Key, operation.Key);
                var parameters = GetMethodParameters(operation.Value);
                var returnType = GetReturnType(operation.Value);

                methods.Add(
                    MethodDeclaration(
                        IdentifierName(returnType),
                        Identifier(methodName))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword)))
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithBody(Block(
                        // Add implementation details here
                        ThrowStatement(
                            ObjectCreationExpression(
                                IdentifierName("NotImplementedException"))
                            .WithArgumentList(ArgumentList())))));
            }
        }

        var clientClass = ClassDeclaration(_clientName)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithMembers(List<MemberDeclarationSyntax>(methods));

        return CompilationUnit()
            .WithUsings(List(new[]
            {
                UsingDirective(IdentifierName("System")),
                UsingDirective(IdentifierName("System.Net.Http")),
                UsingDirective(IdentifierName("System.Threading.Tasks"))
            }))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(
                NamespaceDeclaration(IdentifierName(_namespace))
                    .WithMembers(SingletonList<MemberDeclarationSyntax>(clientClass))));
    }

    private string GetCSharpType(OpenApiSchema schema)
    {
        return schema.Type switch
        {
            "string" => "string",
            "integer" => schema.Format == "int64" ? "long" : "int",
            "number" => schema.Format == "float" ? "float" : "double",
            "boolean" => "bool",
            "array" => $"List<{GetCSharpType(schema.Items)}>",
            "object" => "object",
            _ => "object"
        };
    }

    private string GetMethodName(string path, OperationType operationType)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        sb.Append(operationType.ToString());

        foreach (var segment in segments)
        {
            if (segment.StartsWith("{") && segment.EndsWith("}"))
            {
                sb.Append("By");
                sb.Append(char.ToUpper(segment[1]) + segment.Substring(2, segment.Length - 3));
            }
            else
            {
                sb.Append(char.ToUpper(segment[0]) + segment.Substring(1));
            }
        }

        return sb.ToString();
    }

    private List<ParameterSyntax> GetMethodParameters(OpenApiOperation operation)
    {
        var parameters = new List<ParameterSyntax>();

        foreach (var param in operation.Parameters)
        {
            parameters.Add(
                Parameter(Identifier(param.Name))
                    .WithType(IdentifierName(GetCSharpType(param.Schema))));
        }

        if (operation.RequestBody?.Content.ContainsKey("application/json") == true)
        {
            var schema = operation.RequestBody.Content["application/json"].Schema;
            parameters.Add(
                Parameter(Identifier("requestBody"))
                    .WithType(IdentifierName(GetCSharpType(schema))));
        }

        return parameters;
    }

    private string GetReturnType(OpenApiOperation operation)
    {
        if (operation.Responses.TryGetValue("200", out var response))
        {
            if (response.Content.TryGetValue("application/json", out var content))
            {
                return $"Task<{GetCSharpType(content.Schema)}>";
            }
        }
        return "Task";
    }
}

// Example usage:
public class Program
{
    public static void Main(string[] args)
    {
        var generator = new OpenApiDotNetGenerator(
            "openapi.json",
            "MyNamespace.Client",
            "ApiClient"
        );
        
        generator.GenerateLibrary("./generated");
    }
}