namespace Raze;

public partial class Token
{
    internal TokenType type;
    internal string lexeme;

    internal Token(TokenType type)
    {
        this.type = type;
        this.lexeme = "";
    }
    internal Token(TokenType type, string lexeme)
    {
        this.type = type;
        this.lexeme = lexeme;
    }

    public override string ToString()
    {
        return $"{{ {type} }}:{{ {lexeme} }}";
    }
}
