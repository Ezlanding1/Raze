namespace Raze;

internal partial class Token
{
    internal TokenType type;
    internal string lexeme;

    public Token(TokenType type)
    {
        this.type = type;
        this.lexeme = "";
    }
    public Token(TokenType type, string lexeme)
    {
        this.type = type;
        this.lexeme = lexeme;
    }

    public override string ToString()
    {
        return $"{{ {type} }}:{{ {lexeme} }}";
    }
}
