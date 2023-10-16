using System.Runtime.CompilerServices;

namespace Raze;

internal abstract class Instruction
{
    public abstract string Accept(IVisitor visitor);

    public interface IVisitor
    {
        public string VisitGlobal(Global instruction);
        public string VisitSection(Section instruction);
        public string VisitRegister(Register instruction);
        public string VisitPointer(Pointer instruction);
        public string VisitLiteral(Literal instruction);
        public string VisitData(Data instruction);
        public string VisitDataRef(DataRef instruction);
        public string VisitProcedure(Procedure instruction);
        public string VisitLocalProcedure(LocalProcedure instruction);
        public string VisitLocalProcedureRef(LocalProcedureRef instruction);
        public string VisitProcedureRef(ProcedureRef instruction);
        public string VisitBinary(Binary instruction);
        public string VisitUnary(Unary instruction);
        public string VisitZero(Zero instruction);
        public string VisitComment(Comment instruction);
    }

    internal class Global : Instruction
    {
        public string name;
        public Global(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitGlobal(this);
        }
    }

    internal class Section : Instruction
    {
        public string name;
        public Section(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitSection(this);
        }
    }

    internal abstract class Value : Instruction
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
    }

    internal abstract class SizedValue : Value
    {
        public Register.RegisterSize size;

        public abstract Register AsRegister(Register.RegisterSize size, Assembler assembler);

        public SizedValue(int valueType) : base(valueType)
        {

        }
    }

    internal class Register : SizedValue
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

        public override Register AsRegister(RegisterSize size, Assembler assembler)
        {
            return new Register(this.name, size);
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitRegister(this);
        }
    }

    internal class Pointer : SizedValue
    {
        public Register register;
        public int offset;
        public char _operator;

        public Pointer(Register register, int offset, Register.RegisterSize size, char _operator ='-') : base(1)
        {
            this.register = register;
            this.size = size;
            this.offset = offset;
            this._operator = _operator;
        }
        public Pointer(Register register, int offset, int size, char _operator ='-') : this(register, offset, InstructionUtils.ToRegisterSize(size), _operator)
        {
        }
        public Pointer(Register.RegisterName register, int offset, Register.RegisterSize size, char _operator = '-') : this(new Register(register, Register.RegisterSize._64Bits), offset, size, _operator)
        {
        }
        public Pointer(Register.RegisterName register, int offset, int size, char _operator='-') : this(new Register(register, Register.RegisterSize._64Bits), offset, size, _operator)
        {
        }
        public Pointer(int offset, Register.RegisterSize size) : this(Register.RegisterName.RBP, offset, size, '-')
        {
        }
        public Pointer(int offset, int size) : this(Register.RegisterName.RBP, offset, size, '-')
        {
        }

        public bool IsOnStack() => register.name == Register.RegisterName.RBP;

        public override Register AsRegister(Register.RegisterSize size, Assembler assembler)
        {
            if (!IsOnStack())
            {
                return new Register(register.name, size);
            }
            return assembler.alloc.NextRegister(size);
        }


        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitPointer(this);
        }
    }

    internal class Literal : Value
    {
        public Token.TokenType type;
        public string value;

        public Literal(Token.TokenType type, string value) : base(2)
        {
            this.type = type;
            this.value = value;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitLiteral(this);
        }
    }

    internal class Data : Instruction
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

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitData(this);
        }
    }

    internal class DataRef : Instruction
    {
        public string dataName;

        public DataRef(string dataName)
        {
            this.dataName = dataName;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitDataRef(this);
        }
    }

    internal class Procedure : Instruction
    {
        public string name;
        public Procedure(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitProcedure(this);
        }
    }

    internal class LocalProcedure : Instruction
    {
        public string name;
        public LocalProcedure(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitLocalProcedure(this);
        }
    }

    internal class ProcedureRef : Instruction
    { 
        public string name;

        public ProcedureRef(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitProcedureRef(this);
        }
    }

    internal class LocalProcedureRef : Instruction
    {
        public string name;

        public LocalProcedureRef(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitLocalProcedureRef(this);
        }
    }

    internal class Binary : Instruction
    {
        public string instruction;
        public Instruction operand1, operand2;

        public Binary(string instruction, Instruction operand1, Instruction operand2)
        {
            this.instruction = instruction;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }

        public Binary(string instruction, Register.RegisterName operand1, Instruction.Register.RegisterName operand2)
            : this(instruction, new Register(operand1, Register.RegisterSize._64Bits), new Register(operand2, Register.RegisterSize._64Bits))
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitBinary(this);
        }
    }

    internal class Unary : Instruction
    {
        public Instruction operand;
        public string instruction;

        public Unary(string instruction, Instruction operand)
        {
            this.instruction = instruction;
            this.operand = operand;
        }

        public Unary(string instruction, Instruction.Register.RegisterName operand) : this(instruction, new Instruction.Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    internal class Zero : Instruction 
    {
        public string instruction;
        public Zero(string instruction)
        {
            this.instruction = instruction;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitZero(this);
        }
    }

    internal class Comment : Instruction
    {
        public string comment;
        public Comment(string comment)
        {
            this.comment = comment;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.VisitComment(this);
        }
    }

    internal abstract class CustomInstruction : Instruction
    {
        public override string Accept(IVisitor visitor)
        {
            return GetInstruction(visitor);
        }

        public abstract string GetInstruction(IVisitor visitor);
    }
}

internal class InstructionUtils
{
    public const Instruction.Register.RegisterSize SYS_SIZE = Instruction.Register.RegisterSize._64Bits;

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

    internal readonly static Instruction.Register.RegisterName[] paramRegister = new Instruction.Register.RegisterName[] 
    {
        Instruction.Register.RegisterName.RDI,
        Instruction.Register.RegisterName.RSI,
        Instruction.Register.RegisterName.RDX,
        Instruction.Register.RegisterName.RCX,
        Instruction.Register.RegisterName.R8,
        Instruction.Register.RegisterName.R9
    };

    internal readonly static string[] ReturnOverload = new string[]
    {
        "r15",
        "r14",
        "r13",
        "r12",
        "rbx"
    };

    internal readonly static Instruction.Register.RegisterName[] storageRegisters = new Instruction.Register.RegisterName[]
    {
        Instruction.Register.RegisterName.RAX,
        Instruction.Register.RegisterName.RBX,
        Instruction.Register.RegisterName.R12,
        Instruction.Register.RegisterName.R13,
        Instruction.Register.RegisterName.R14,
        Instruction.Register.RegisterName.R15
    };

    internal readonly static Dictionary<string, (Instruction.Register.RegisterName, Instruction.Register.RegisterSize)> Registers = new()
    {
        { "RAX", (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._64Bits) }, // 64-Bits 
        { "EAX", (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._32Bits) }, // Lower 32-Bits
        { "AX", (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._16Bits) }, // Lower 16-Bits
        { "AH", (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._8BitsUpper) }, // Upper 16-Bits
        { "AL", (Instruction.Register.RegisterName.RAX, Instruction.Register.RegisterSize._8Bits) }, // Lower 8-Bits

        { "RCX", (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._64Bits) },
        { "ECX", (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._32Bits) },
        { "CX", (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._16Bits) },
        { "CH", (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._8BitsUpper) },
        { "CL", (Instruction.Register.RegisterName.RCX, Instruction.Register.RegisterSize._8Bits) },

        { "RDX", (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._64Bits) },
        { "EDX", (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._32Bits) },
        { "DX", (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._16Bits) },
        { "DH", (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._8BitsUpper) },
        { "DL", (Instruction.Register.RegisterName.RDX, Instruction.Register.RegisterSize._8Bits) },

        { "RBX", (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._64Bits) },
        { "EBX", (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._32Bits) },
        { "BX", (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._16Bits) },
        { "BH", (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._8BitsUpper) },
        { "BL", (Instruction.Register.RegisterName.RBX, Instruction.Register.RegisterSize._8Bits) },

        { "RSI", (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._64Bits) },
        { "ESI", (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._32Bits) },
        { "SI", (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._16Bits) },
        { "SIL", (Instruction.Register.RegisterName.RSI, Instruction.Register.RegisterSize._8Bits) },

        { "RDI", (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._64Bits) },
        { "EDI", (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._32Bits) },
        { "DI", (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._16Bits) },
        { "DIL", (Instruction.Register.RegisterName.RDI, Instruction.Register.RegisterSize._8Bits) },

        { "RSP", (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._64Bits) },
        { "ESP", (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._32Bits) },
        { "SP", (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._16Bits) },
        { "SPL", (Instruction.Register.RegisterName.RSP, Instruction.Register.RegisterSize._8Bits) },

        { "RBP", (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._64Bits) },
        { "EBP", (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._32Bits) },
        { "BP", (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._16Bits) },
        { "BPL", (Instruction.Register.RegisterName.RBP, Instruction.Register.RegisterSize._8Bits) },

        { "R8", (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._64Bits) },
        { "R8D", (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._32Bits) },
        { "R8W", (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._16Bits) },
        { "R8B", (Instruction.Register.RegisterName.R8, Instruction.Register.RegisterSize._8Bits) },

        { "R9", (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._64Bits) },
        { "R9D", (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._32Bits) },
        { "R9W", (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._16Bits) },
        { "R9B", (Instruction.Register.RegisterName.R9, Instruction.Register.RegisterSize._8Bits) },

        { "R10", (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._64Bits) },
        { "R10D", (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._32Bits) },
        { "R10W", (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._16Bits) },
        { "R10B", (Instruction.Register.RegisterName.R10, Instruction.Register.RegisterSize._8Bits) },

        { "R11", (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._64Bits) },
        { "R11D", (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._32Bits) },
        { "R11W", (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._16Bits) },
        { "R11B", (Instruction.Register.RegisterName.R11, Instruction.Register.RegisterSize._8Bits) },

        { "R12", (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._64Bits) },
        { "R12D", (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._32Bits) },
        { "R12W", (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._16Bits) },
        { "R12B", (Instruction.Register.RegisterName.R12, Instruction.Register.RegisterSize._8Bits) },

        { "R13", (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._64Bits) },
        { "R13D", (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._32Bits) },
        { "R13W", (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._16Bits) },
        { "R13B", (Instruction.Register.RegisterName.R13, Instruction.Register.RegisterSize._8Bits) },

        { "R14", (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._64Bits) },
        { "R14D", (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._32Bits) },
        { "R14W", (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._16Bits) },
        { "R14B", (Instruction.Register.RegisterName.R14, Instruction.Register.RegisterSize._8Bits) },

        { "R15", (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._64Bits) },
        { "R15D", (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._32Bits) },
        { "R15W", (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._16Bits) },
        { "R15B", (Instruction.Register.RegisterName.R15, Instruction.Register.RegisterSize._8Bits) },
    };

    public static Instruction.Register.RegisterSize ToRegisterSize(int size)
    {
        if (Enum.IsDefined(typeof(Instruction.Register.RegisterSize), size))
        {
            return (Instruction.Register.RegisterSize)size;
        }

        Diagnostics.errors.Push(new Error.ImpossibleError($"Invalid Register Size ({size})"));
        return 0;
    }
    

    internal readonly static Dictionary<Instruction.Register.RegisterSize?, string> wordSize = new()
    {
        { Instruction.Register.RegisterSize._64Bits, "QWORD"}, // 64-Bits
        { Instruction.Register.RegisterSize._32Bits, "DWORD"}, // 32-Bits
        { Instruction.Register.RegisterSize._16Bits, "WORD"}, // 16-Bits
        { Instruction.Register.RegisterSize._8BitsUpper, "BYTE"}, // 8-Bits
        { Instruction.Register.RegisterSize._8Bits, "BYTE"}, // 8-Bits
    };

    internal readonly static Dictionary<int, string> dataSize = new()
    {
        { 8, "dq"}, // 64-Bits
        { 4, "dd"}, // 32-Bits
        { 2, "dw"}, // 16-Bits
        { 1, "db"}, // 8-Bits
    };
}