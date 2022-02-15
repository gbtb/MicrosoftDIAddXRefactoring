using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MicrosoftDI.AddXRefactoring.Provider;

public class TriggerLocationSyntaxWalker: CSharpSyntaxWalker
{
    private bool _isComplete;
    public BaseTypeSyntax? BaseType { get; private set; }
    
    public TypeDeclarationSyntax? TypeDeclarationSyntax { get; private set; }

    public override void DefaultVisit(SyntaxNode node)
    {
        if (node is MemberDeclarationSyntax)
            return;
        
        if (!_isComplete && node.Parent != null)
            Visit(node.Parent);
    }

    public override void VisitSimpleBaseType(SimpleBaseTypeSyntax node)
    {
        BaseType = node;
        base.VisitSimpleBaseType(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        TypeDeclarationSyntax = node;
        _isComplete = true;
    }
    
    public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
    {
        TypeDeclarationSyntax = node;
        _isComplete = true;
    }
    
    public override void VisitStructDeclaration(StructDeclarationSyntax node)
    {
        TypeDeclarationSyntax = node;
        _isComplete = true;
    }
}