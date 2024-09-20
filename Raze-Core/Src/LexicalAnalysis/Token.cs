namespace Raze;

public partial class Token
{
    internal TokenType type;
    internal string lexeme;
    readonly internal Location location;

    internal Token(TokenType type, Location location)
    {
        this.type = type;
        this.lexeme = "";
        this.location = location;

    }
    internal Token(TokenType type, string lexeme, Location location)
    {
        this.type = type;
        this.lexeme = lexeme;
        this.location = location;
    }

    public override string ToString()
    {
        return $"{{ {type} }}:{{ {lexeme} }}";
    }
}

public class LiteralToken : Token
{
    internal new Parser.LiteralTokenType type 
    { 
        get => (Parser.LiteralTokenType)base.type; 
        set => base.type = (TokenType)value; 
    }

    internal LiteralToken(Parser.LiteralTokenType type, string lexeme, Location location) : base((TokenType)type, lexeme, location)
    {
    }
}

// If needed to reduce the size of this struct, an index into currentFile can be used to find location data
public readonly record struct Location(int Ln, int Col)
{
    public readonly static Location NoLocation = new(-1, -1);
    public readonly int Idx => Col - 1;

    public override string ToString()
    {
        return $"Line: {Ln}, Col: {Col}";
    }
}
