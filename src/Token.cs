namespace Raze
{
    internal class Token
    {
        internal string type;
        internal string lexeme;

        public Token(string type)
        {
            this.type = type;
            this.lexeme = "";
        }
        public Token(string type, string lexeme)
        {
            this.type = type;
            this.lexeme = lexeme;
        }

        public override string ToString()
        {
            return $"{{ {type} }}:{{ {lexeme} }}";
        }
    }
}
