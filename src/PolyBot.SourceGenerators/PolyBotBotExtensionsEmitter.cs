using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PolyBot.SourceGenerators;

internal static class PolyBotExtensionsEmitter
{
    public static string Generate()
    {
        ClassDeclarationSyntax extensionsClass = SyntaxFactory.ClassDeclaration("PolyBotExtensions")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddMembers(BuildAddPolyBotBotMethod())
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                "DI registration helpers for the generated <c>PolyBot.BotRouter</c>; do not",
                "hand-write a type with this name in the <c>PolyBot</c> namespace.",
                "</summary>"));

        return EmitterSyntax.RenderPolyBotFile("PolyBot", extensionsClass);
    }

    private static MethodDeclarationSyntax BuildAddPolyBotBotMethod()
    {
        return SyntaxFactory.MethodDeclaration(
                returnType: SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection"),
                identifier: "AddPolyBotRouter")
            .AddModifiers(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword)
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("services"))
                    .AddModifiers(SyntaxKind.ThisKeyword)
                    .WithType(SyntaxFactory.ParseTypeName("global::Microsoft.Extensions.DependencyInjection.IServiceCollection")))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                    "global::PolyBot.PolyBotServiceCollectionExtensions.AddPolyBotDefaults(services)")),
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                    "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::PolyBot.BotRouter>(services, static (global::System.IServiceProvider sp) => new global::PolyBot.BotRouter(sp))")),
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                    "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::Telegram.Bot.Polling.IUpdateHandler>(services, static (global::System.IServiceProvider sp) => " + EmitterSyntax.RequiredService("global::PolyBot.BotRouter", "sp") + ")")),
                SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression(
                    "global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<global::PolyBot.BotFather.IBotFatherSync>(services, static (global::System.IServiceProvider sp) => new global::PolyBot.PolyBotBotFatherSync(" + EmitterSyntax.RequiredService("global::Telegram.Bot.ITelegramBotClient", "sp") + "))")),
                SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("services"))))
            .WithLeadingTrivia(EmitterSyntax.DocComment(
                "<summary>",
                "Registers the generated <c>PolyBot.BotRouter</c> as itself and as",
                "<c>Telegram.Bot.Polling.IUpdateHandler</c>, plus <c>PolyBot.PolyBotBotFatherSync</c> as <c>PolyBot.IBotFatherSync</c>.",
                "</summary>"));
    }

}
