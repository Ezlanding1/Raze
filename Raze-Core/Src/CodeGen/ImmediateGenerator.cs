using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class AssemblyExpr
{
    internal partial class ImmediateGenerator
    {
        public static byte[] Generate(Literal.LiteralType literalType, string str, Register.RegisterSize size)
        {
            return literalType switch
            {
                Literal.LiteralType.Integer => ParseInteger(str, size),
                Literal.LiteralType.UnsignedInteger => ParseUnsignedInteger(str, size),
                Literal.LiteralType.Floating => ParseFloating(str, size),
                Literal.LiteralType.Boolean => ParseInteger(str, size),
                Literal.LiteralType.String => ParseString(str, size),
                Literal.LiteralType.RefData or
                Literal.LiteralType.RefProcedure or
                Literal.LiteralType.RefLocalProcedure => ParseLabelReference(str, size)
            };
        }

        private static byte[] ParseInteger(string str, Register.RegisterSize size)
        {
            (int _base, str) = GetBaseAndStrFromPrefix(str);

            try
            {
                return size switch
                {
                    Register.RegisterSize._8BitsUpper or
                    Register.RegisterSize._8Bits => [unchecked((byte)Convert.ToSByte(str, _base))],
                    Register.RegisterSize._16Bits => BitConverter.GetBytes(Convert.ToInt16(str, _base)),
                    Register.RegisterSize._32Bits => BitConverter.GetBytes(Convert.ToInt32(str, _base)),
                    Register.RegisterSize._64Bits => BitConverter.GetBytes(Convert.ToInt64(str, _base))
                };
            }
            catch (OverflowException)
            {
                ThrowInvalidSizedLiteral(Parser.LiteralTokenType.Integer, str, size);
            }
            return new byte[(int)size];
        }

        private static byte[] ParseUnsignedInteger(string str, Register.RegisterSize size)
        {
            (int _base, str) = GetBaseAndStrFromPrefix(str[..^1]);

            try
            {
                return size switch
                {
                    Register.RegisterSize._8BitsUpper or
                    Register.RegisterSize._8Bits => [Convert.ToByte(str, _base)],
                    Register.RegisterSize._16Bits => BitConverter.GetBytes(Convert.ToUInt16(str, _base)),
                    Register.RegisterSize._32Bits => BitConverter.GetBytes(Convert.ToUInt32(str, _base)),
                    Register.RegisterSize._64Bits => BitConverter.GetBytes(Convert.ToUInt64(str, _base))
                };
            }
            catch (OverflowException)
            {
                ThrowInvalidSizedLiteral(Parser.LiteralTokenType.UnsignedInteger, str, size);
            }
            return new byte[(int)size];
        }

        public static (int, string) GetBaseAndStrFromPrefix(string str) =>
            (str.Length < 2)? 
                (10, str) : 
                str[..2].ToLower() switch
                {
                    "0x" => (16, str[2..]),
                    "0b" => (2, str[2..]),
                    _ => (10, str),
                };

        private static byte[] ParseFloating(string str, Register.RegisterSize size)
        {
            (bool successfulParse, byte[] result) = size switch
            {
                Register.RegisterSize._8BitsUpper or
                Register.RegisterSize._8Bits => (Half.TryParse(str, out var literal), BitConverter.GetBytes(literal)),
                Register.RegisterSize._16Bits => (Half.TryParse(str, out var literal), BitConverter.GetBytes(literal)),
                Register.RegisterSize._32Bits => (float.TryParse(str, out var literal), BitConverter.GetBytes(literal)),
                Register.RegisterSize._64Bits => (double.TryParse(str, out var literal), BitConverter.GetBytes(literal))
            };

            if (!successfulParse)
            {
                ThrowInvalidSizedLiteral(Parser.LiteralTokenType.Floating, str, size);
            }
            return result;
        }

        private static byte[] ParseString(string str, Register.RegisterSize size)
        {
            var value = Encoding.ASCII.GetBytes(str);
            if (value.Length > (int)size)
            {
                ThrowInvalidSizedLiteral(Parser.LiteralTokenType.String, str, size);
            }
            return value;
        }
        
        public static IEnumerable<byte[]> ParseRefString(string str)
        {
            List<byte[]> value = Encoding.ASCII.GetBytes(str + '\0').Select(x => new byte[] { x }).ToList();
            return value;
        }

        private static byte[] ParseLabelReference(string str, Register.RegisterSize size)
        {
            if (InstructionUtils.SYS_SIZE != size)
            {
                Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InvalidLiteralSize_SystemSize, "RefString", size, InstructionUtils.SYS_SIZE));
            }
            return Encoding.ASCII.GetBytes(str);
        }

        private static void ThrowInvalidSizedLiteral(Parser.LiteralTokenType literalType, string str, Register.RegisterSize dataTypeSize)
        {
            Diagnostics.Report(new Diagnostic.BackendDiagnostic(Diagnostic.DiagnosticName.InvalidLiteralSize, literalType, str, dataTypeSize));
        }
    }
}
