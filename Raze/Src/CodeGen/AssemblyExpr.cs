using System.Runtime.CompilerServices;

namespace Raze;

public abstract class AssemblyExpr
{
    public abstract T Accept<T>(IVisitor<T> visitor);

    public interface IVisitor<T>
    {
        public T VisitGlobal(Global instruction);
        public T VisitSection(Section instruction);
        public T VisitRegister(Register instruction);
        public T VisitPointer(Pointer instruction);
        public T VisitLiteral(Literal instruction);
        public T VisitData(Data instruction);
        public T VisitDataRef(DataRef instruction);
        public T VisitProcedure(Procedure instruction);
        public T VisitLocalProcedure(LocalProcedure instruction);
        public T VisitLocalProcedureRef(LocalProcedureRef instruction);
        public T VisitProcedureRef(ProcedureRef instruction);
        public T VisitBinary(Binary instruction);
        public T VisitUnary(Unary instruction);
        public T VisitZero(Zero instruction);
        public T VisitComment(Comment instruction);
    }

    public class Global : AssemblyExpr
    {
        public string name;
        public Global(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitGlobal(this);
        }
    }

    public class Section : AssemblyExpr
    {
        public string name;
        public Section(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitSection(this);
        }
    }

    public abstract class Value : AssemblyExpr
    {
        // 0 = Register, 1 = Pointer, 2 = Literal
        public int valueType;

        public Value(int valueType)
        {
            this.valueType = valueType;
        }

        public bool IsRegister() => valueType == 0;
        public bool IsPointer() => valueType == 1;
        public bool IsLiteral() => valueType == 2;

        internal Register NonPointerNonLiteral(Register.RegisterSize? size, CodeGen assembler) =>
            (Register)this.NonPointer(assembler).NonLiteral(size, assembler);

        public Value NonPointer(CodeGen assembler)
        {
            if (IsPointer())
            {
                Register reg = ((Pointer)this).AsRegister(((Pointer)this).size, assembler);
                assembler.Emit(new Binary("MOV", reg, this));
                return reg;
            }
            return this;
        }
        internal SizedValue NonLiteral(Register.RegisterSize? size, CodeGen assembler)
        {
            if (IsLiteral())
            {
                if (size == null) { Diagnostics.errors.Push(new Error.ImpossibleError("Null size in NonLiteral when operand is literal")); }

                assembler.Emit(new Binary("MOV", assembler.alloc.CurrentRegister((Register.RegisterSize)size), this));
                return assembler.alloc.NextRegister((Register.RegisterSize)size);
            }
            return (SizedValue)this;
        }
    }

    public abstract class SizedValue : Value
    {
        public Register.RegisterSize size;

        internal abstract Register AsRegister(Register.RegisterSize size, CodeGen assembler);

        public SizedValue(int valueType) : base(valueType)
        {

        }
    }

    public class Register : SizedValue
    {
        public enum RegisterSize
        {
            _64Bits = 8,
            _32Bits = 4,
            _16Bits = 2,
            _8BitsUpper = 0,
            _8Bits = 1
        }

        public enum RegisterName
        {
            TMP,
            RAX,
            RCX,
            RDX,
            RBX,
            RSI,
            RDI,
            RSP,
            RBP,
            R8,
            R9,
            R10,
            R11,
            R12,
            R13,
            R14,
            R15
        }

        private StrongBox<RegisterName> _name;
        public RegisterName name
        {
            get => _name.Value;
            set => _name.Value = value;
        }

        public Register(RegisterName register, RegisterSize size) : base(0)
        {
            this._name = new(register);
            this.size = size;
        }

        public Register(RegisterName register, int size) : this(register, InstructionUtils.ToRegisterSize(size))
        {
        }

        public Register(StrongBox<RegisterName> _name, RegisterSize size) : base(0)
        {
            this._name = _name;
            this.size = size;
        }

        internal override Register AsRegister(RegisterSize size, CodeGen assembler)
        {
            return new Register(this.name, size);
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitRegister(this);
        }
    }

    public class Pointer : SizedValue
    {
        internal Register register;
        public int offset;
        public char _operator;

        internal Pointer(Register register, int offset, Register.RegisterSize size, char _operator ='-') : base(1)
        {
            this.register = register;
            this.size = size;
            this.offset = offset;
            this._operator = _operator;
        }
        internal Pointer(Register register, int offset, int size, char _operator ='-') : this(register, offset, InstructionUtils.ToRegisterSize(size), _operator)
        {
        }
        internal Pointer(Register.RegisterName register, int offset, Register.RegisterSize size, char _operator = '-') : this(new Register(register, Register.RegisterSize._64Bits), offset, size, _operator)
        {
        }
        internal Pointer(Register.RegisterName register, int offset, int size, char _operator='-') : this(new Register(register, Register.RegisterSize._64Bits), offset, size, _operator)
        {
        }
        internal Pointer(int offset, Register.RegisterSize size) : this(Register.RegisterName.RBP, offset, size, '-')
        {
        }
        public Pointer(int offset, int size) : this(Register.RegisterName.RBP, offset, size, '-')
        {
        }

        public bool IsOnStack() => register.name == Register.RegisterName.RBP;

        internal override Register AsRegister(Register.RegisterSize size, CodeGen assembler)
        {
            if (!IsOnStack())
            {
                return new Register(register.name, size);
            }
            return assembler.alloc.NextRegister(size);
        }


        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitPointer(this);
        }
    }

    public class Literal : Value
    {
        internal Parser.LiteralTokenType type;
        public string value;

        internal Literal(Parser.LiteralTokenType type, string value) : base(2)
        {
            this.type = type;
            this.value = value;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitLiteral(this);
        }
    }

    public class Data : AssemblyExpr
    {
        public string name;
        public string size;
        public string value;
        public Data(string name, string size, string value)
        {
            this.name = name;
            this.size = size;
            this.value = value;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitData(this);
        }
    }

    public class DataRef : AssemblyExpr
    {
        public string dataName;

        public DataRef(string dataName)
        {
            this.dataName = dataName;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitDataRef(this);
        }
    }

    public class Procedure : AssemblyExpr
    {
        public string name;
        public Procedure(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitProcedure(this);
        }
    }

    public class LocalProcedure : AssemblyExpr
    {
        public string name;
        public LocalProcedure(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitLocalProcedure(this);
        }
    }

    public class ProcedureRef : AssemblyExpr
    {
        public string name;

        public ProcedureRef(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitProcedureRef(this);
        }
    }

    public class LocalProcedureRef : AssemblyExpr
    {
        public string name;

        public LocalProcedureRef(string name)
        {
            this.name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitLocalProcedureRef(this);
        }
    }

    public class Binary : AssemblyExpr
    {
        public string instruction;
        public AssemblyExpr operand1, operand2;

        public Binary(string instruction, AssemblyExpr operand1, AssemblyExpr operand2)
        {
            this.instruction = instruction;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }

        internal Binary(string instruction, Register.RegisterName operand1, AssemblyExpr.Register.RegisterName operand2)
            : this(instruction, new Register(operand1, Register.RegisterSize._64Bits), new Register(operand2, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitBinary(this);
        }
    }

    public class Unary : AssemblyExpr
    {
        public AssemblyExpr operand;
        public string instruction;

        public Unary(string instruction, AssemblyExpr operand)
        {
            this.instruction = instruction;
            this.operand = operand;
        }

        internal Unary(string instruction, AssemblyExpr.Register.RegisterName operand) : this(instruction, new AssemblyExpr.Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    public class Zero : AssemblyExpr
    {
        public string instruction;
        public Zero(string instruction)
        {
            this.instruction = instruction;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitZero(this);
        }
    }

    public class Comment : AssemblyExpr
    {
        public string comment;
        public Comment(string comment)
        {
            this.comment = comment;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitComment(this);
        }
    }
}

public class InstructionUtils
{
    internal const AssemblyExpr.Register.RegisterSize SYS_SIZE = AssemblyExpr.Register.RegisterSize._64Bits;

    internal static string ToType(Token.TokenType input, bool unary=false)
    {
        if (!unary)
        {
            return StringToOperatorTypeBinary[input];
        }
        else
        {
            return StringToOperatorTypeUnary[input];
        }
    }

    private readonly static Dictionary<Token.TokenType, string> StringToOperatorTypeBinary = new()
    {
        // Binary
        { Token.TokenType.SHIFTRIGHT , "SHR" },
        { Token.TokenType.SHIFTLEFT , "SHL" },
        { Token.TokenType.DIVIDE , "DIV" },
        { Token.TokenType.MULTIPLY , "IMUL" },
        { Token.TokenType.B_NOT , "NOT" },
        { Token.TokenType.B_OR , "OR" },
        { Token.TokenType.B_AND , "AND" },
        { Token.TokenType.B_XOR , "XOR" },
        { Token.TokenType.PLUS , "ADD" },
        { Token.TokenType.MINUS , "SUB" },
        { Token.TokenType.EQUALTO , "CMP" },
        { Token.TokenType.NOTEQUALTO , "CMP" },
        { Token.TokenType.GREATER , "CMP" },
        { Token.TokenType.LESS , "CMP" },
        { Token.TokenType.GREATEREQUAL , "CMP" },
        { Token.TokenType.LESSEQUAL , "CMP" },
    };
    private readonly static Dictionary<Token.TokenType, string> StringToOperatorTypeUnary = new()
    {
        // Unary
        { Token.TokenType.PLUSPLUS,  "INC" },
        { Token.TokenType.MINUSMINUS,  "DEC" },
        { Token.TokenType.MINUS,  "NEG" },
    };

    internal readonly static Dictionary<string, string> ConditionalJump = new()
    {
        { "SETE" , "JE" },
        { "SETNE" , "JNE" },
        { "SETG" , "JG" },
        { "SETL" , "JL" },
        { "SETGE" , "JGE" },
        { "SETLE" , "JLE" },
    };
    internal readonly static Dictionary<string, string> ConditionalJumpReversed = new()
    {
        { "SETE" , "JNE" },
        { "SETNE" , "JE" },
        { "SETG" , "JLE" },
        { "SETL" , "JGE" },
        { "SETGE" , "JE" },
        { "SETLE" , "JG" },
    };

    internal readonly static AssemblyExpr.Register.RegisterName[] paramRegister = new AssemblyExpr.Register.RegisterName[]
    {
        AssemblyExpr.Register.RegisterName.RDI,
        AssemblyExpr.Register.RegisterName.RSI,
        AssemblyExpr.Register.RegisterName.RDX,
        AssemblyExpr.Register.RegisterName.RCX,
        AssemblyExpr.Register.RegisterName.R8,
        AssemblyExpr.Register.RegisterName.R9
    };

    internal readonly static string[] ReturnOverload = new string[]
    {
        "r15",
        "r14",
        "r13",
        "r12",
        "rbx"
    };

    internal readonly static AssemblyExpr.Register.RegisterName[] storageRegisters = new AssemblyExpr.Register.RegisterName[]
    {
        AssemblyExpr.Register.RegisterName.RAX,
        AssemblyExpr.Register.RegisterName.RBX,
        AssemblyExpr.Register.RegisterName.R12,
        AssemblyExpr.Register.RegisterName.R13,
        AssemblyExpr.Register.RegisterName.R14,
        AssemblyExpr.Register.RegisterName.R15
    };

    internal readonly static Dictionary<string, (AssemblyExpr.Register.RegisterName, AssemblyExpr.Register.RegisterSize)> Registers = new()
    {
        { "RAX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._64Bits) }, // 64-Bits 
        { "EAX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._32Bits) }, // Lower 32-Bits
        { "AX", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._16Bits) }, // Lower 16-Bits
        { "AH", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8BitsUpper) }, // Upper 16-Bits
        { "AL", (AssemblyExpr.Register.RegisterName.RAX, AssemblyExpr.Register.RegisterSize._8Bits) }, // Lower 8-Bits

        { "RCX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ECX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "CX", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "CH", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "CL", (AssemblyExpr.Register.RegisterName.RCX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RDX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EDX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "DX", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "DH", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "DL", (AssemblyExpr.Register.RegisterName.RDX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RBX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EBX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "BX", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "BH", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8BitsUpper) },
        { "BL", (AssemblyExpr.Register.RegisterName.RBX, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RSI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ESI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "SI", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "SIL", (AssemblyExpr.Register.RegisterName.RSI, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RDI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EDI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "DI", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "DIL", (AssemblyExpr.Register.RegisterName.RDI, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RSP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "ESP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "SP", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "SPL", (AssemblyExpr.Register.RegisterName.RSP, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "RBP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "EBP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "BP", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "BPL", (AssemblyExpr.Register.RegisterName.RBP, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R8", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R8D", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R8W", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R8B", (AssemblyExpr.Register.RegisterName.R8, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R9", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R9D", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R9W", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R9B", (AssemblyExpr.Register.RegisterName.R9, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R10", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R10D", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R10W", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R10B", (AssemblyExpr.Register.RegisterName.R10, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R11", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R11D", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R11W", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R11B", (AssemblyExpr.Register.RegisterName.R11, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R12", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R12D", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R12W", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R12B", (AssemblyExpr.Register.RegisterName.R12, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R13", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R13D", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R13W", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R13B", (AssemblyExpr.Register.RegisterName.R13, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R14", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R14D", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R14W", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R14B", (AssemblyExpr.Register.RegisterName.R14, AssemblyExpr.Register.RegisterSize._8Bits) },

        { "R15", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._64Bits) },
        { "R15D", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._32Bits) },
        { "R15W", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._16Bits) },
        { "R15B", (AssemblyExpr.Register.RegisterName.R15, AssemblyExpr.Register.RegisterSize._8Bits) },
    };

    internal static AssemblyExpr.Register.RegisterSize ToRegisterSize(int size)
    {
        if (Enum.IsDefined(typeof(AssemblyExpr.Register.RegisterSize), size))
        {
            return (AssemblyExpr.Register.RegisterSize)size;
        }

        Diagnostics.errors.Push(new Error.ImpossibleError($"Invalid Register Size ({size})"));
        return 0;
    }


    internal readonly static Dictionary<AssemblyExpr.Register.RegisterSize?, string> wordSize = new()
    {
        { AssemblyExpr.Register.RegisterSize._64Bits, "QWORD"}, // 64-Bits
        { AssemblyExpr.Register.RegisterSize._32Bits, "DWORD"}, // 32-Bits
        { AssemblyExpr.Register.RegisterSize._16Bits, "WORD"}, // 16-Bits
        { AssemblyExpr.Register.RegisterSize._8BitsUpper, "BYTE"}, // 8-Bits
        { AssemblyExpr.Register.RegisterSize._8Bits, "BYTE"}, // 8-Bits
    };

    internal readonly static Dictionary<int, string> dataSize = new()
    {
        { 8, "dq"}, // 64-Bits
        { 4, "dd"}, // 32-Bits
        { 2, "dw"}, // 16-Bits
        { 1, "db"}, // 8-Bits
    };
}
