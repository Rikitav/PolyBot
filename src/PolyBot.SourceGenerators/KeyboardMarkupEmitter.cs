using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace PolyBot.SourceGenerators;

/// <summary>
/// Emits the implementing halves of <c>[CallbackButton]</c>-annotated partial keyboard members (<c>PolyBotKeyboards.g.cs</c>).
/// </summary>
internal static class KeyboardMarkupEmitter
{
    private const string ButtonFqn = "global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton";

    private const string MarkupFqn = "global::Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup";

    public static string? Generate(ImmutableArray<KeyboardModel> keyboards)
    {
        if (keyboards.Length == 0)
        {
            return null;
        }

        List<KeyboardGroup> groups = new();
        foreach (KeyboardModel keyboard in keyboards)
        {
            KeyboardGroup? group = null;
            foreach (KeyboardGroup candidate in groups)
            {
                if (candidate.Namespace == keyboard.Namespace && candidate.TypeDeclarations.Equals(keyboard.ContainingTypeDeclarations))
                {
                    group = candidate;
                    break;
                }
            }

            if (group is null)
            {
                group = new KeyboardGroup(keyboard.Namespace, keyboard.ContainingTypeDeclarations);
                groups.Add(group);
            }

            group.Models.Add(keyboard);
        }

        List<MemberDeclarationSyntax> unitMembers = new();
        foreach (KeyboardGroup group in groups)
        {
            List<MemberDeclarationSyntax> members = new();
            foreach (KeyboardModel model in group.Models)
            {
                members.Add(BuildMember(model));
            }

            MemberDeclarationSyntax containingType = BuildContainingTypeChain(group.TypeDeclarations, members);
            if (group.Namespace is null)
            {
                unitMembers.Add(containingType);
            }
            else
            {
                unitMembers.Add(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(group.Namespace)).AddMembers(containingType));
            }
        }

        CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit().AddMembers(unitMembers.ToArray());
        return GeneratedSource.Render(compilationUnit);
    }

    private sealed class KeyboardGroup
    {
        public KeyboardGroup(string? ns, EquatableArray<string> typeDeclarations)
        {
            Namespace = ns;
            TypeDeclarations = typeDeclarations;
            Models = new List<KeyboardModel>();
        }

        public string? Namespace { get; }

        public EquatableArray<string> TypeDeclarations { get; }

        public List<KeyboardModel> Models { get; }
    }

    private static MemberDeclarationSyntax BuildContainingTypeChain(EquatableArray<string> declarations, List<MemberDeclarationSyntax> members)
    {
        // The innermost declaration holds all generated members; outer declarations wrap the already-built node.
        MemberDeclarationSyntax current = CreateTypeDeclaration(declarations[declarations.Count - 1], members);
        for (int i = declarations.Count - 2; i >= 0; i--)
        {
            List<MemberDeclarationSyntax> single = new() { current };
            current = CreateTypeDeclaration(declarations[i], single);
        }

        return current;
    }

    private static MemberDeclarationSyntax CreateTypeDeclaration(string declaration, IReadOnlyList<MemberDeclarationSyntax> members)
    {
        // The stored form is "{modifiers} {keyword} {name}" (modifiers always contain
        // "partial"; keyword is class/struct, or record class/record struct).
        int lastSpace = declaration.LastIndexOf(' ');
        string name = declaration.Substring(lastSpace + 1);
        int previousSpace = declaration.LastIndexOf(' ', lastSpace - 1);
        string keyword = declaration.Substring(previousSpace + 1, lastSpace - previousSpace - 1);
        string modifiers = declaration.Substring(0, previousSpace);

        if (keyword.StartsWith("record", StringComparison.Ordinal))
        {
            // Record declarations have no SyntaxFactory factory method; reparse the
            // source-shaped declaration and splice the generated members into its body.
            TypeDeclarationSyntax parsed = (TypeDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(declaration + " { }")!;
            return parsed.AddMembers(members.ToArray());
        }

        TypeDeclarationSyntax declarationSyntax = keyword == "struct"
            ? SyntaxFactory.StructDeclaration(name)
            : SyntaxFactory.ClassDeclaration(name);
        return declarationSyntax
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.ParseTokens(modifiers)))
            .AddMembers(members.ToArray());
    }

    private static MemberDeclarationSyntax BuildMember(KeyboardModel model)
    {
        TypeSyntax returnType = SyntaxFactory.ParseTypeName(MarkupFqn);
        SyntaxTokenList modifiers = SyntaxFactory.TokenList(SyntaxFactory.ParseTokens(model.MemberModifiers));

        if (model.IsProperty)
        {
            AccessorDeclarationSyntax getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(BuildMarkupExpression(model))));
            return SyntaxFactory.PropertyDeclaration(type: returnType, identifier: model.MemberName)
                .WithModifiers(modifiers)
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter)));
        }

        return SyntaxFactory.MethodDeclaration(returnType: returnType, identifier: model.MemberName)
            .WithModifiers(modifiers)
            .WithParameterList(SyntaxFactory.ParseParameterList(model.ParametersSource))
            .WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(BuildMarkupExpression(model))));
    }

    private static ExpressionSyntax BuildMarkupExpression(KeyboardModel model)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("new ").Append(MarkupFqn).Append("(new ").Append(ButtonFqn).Append("[][] { ");

        foreach (EquatableArray<KeyboardButtonModel> row in model.Rows.Items)
        {
            builder.Append("new ").Append(ButtonFqn).Append("[] { ");
            foreach (KeyboardButtonModel button in row.Items)
            {
                string dataArgument = button.CallbackData.Contains('{')
                    ? "$" + BotRouterEmitter.EscapeString(button.CallbackData)
                    : BotRouterEmitter.EscapeString(button.CallbackData);
                builder.Append(ButtonFqn)
                    .Append(".WithCallbackData(")
                    .Append(BotRouterEmitter.EscapeString(button.Text))
                    .Append(", ")
                    .Append(dataArgument)
                    .Append("), ");
            }

            builder.Append("}, ");
        }

        builder.Append("})");
        return SyntaxFactory.ParseExpression(builder.ToString());
    }
}
