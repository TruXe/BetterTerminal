namespace BetterTerminal.Shell.Services
{
    /// <summary>One coloured run inside a line. Everything not covered by one is plain text.</summary>
    public struct SyntaxToken
    {
        public SyntaxToken(int start, int length, TokenKind kind)
        {
            Start = start;
            Length = length;
            Kind = kind;
        }

        public int Start;

        public int Length;

        public TokenKind Kind;
    }
}
