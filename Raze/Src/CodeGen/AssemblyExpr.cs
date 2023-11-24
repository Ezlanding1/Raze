using System.Runtime.CompilerServices;

namespace Raze;

public abstract partial class AssemblyExpr
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
                assembler.Emit(new Binary(Instruction.MOV, reg, this));
                return reg;
            }
            return this;
        }
        internal SizedValue NonLiteral(Register.RegisterSize? size, CodeGen assembler)
        {
            if (IsLiteral())
            {
                if (size == null) { Diagnostics.errors.Push(new Error.ImpossibleError("Null size in NonLiteral when operand is literal")); }

                assembler.Emit(new Binary(Instruction.MOV, assembler.alloc.CurrentRegister((Register.RegisterSize)size), this));
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

        // Register Encoding => if RRegister: -RegCode-1 else: RegCode. TMP = -9
        public enum RegisterName : sbyte
        {
            TMP = -9,
            RAX = 0b000,
            RCX = 0b001,
            RDX = 0b010,
            RBX = 0b011,
            RSI = 0b110,
            RDI = 0b111,
            RSP = 0b100,
            RBP = 0b101,
            R8 = -0b000 - 1,
            R9 = -0b001 - 1,
            R10 = -0b010 - 1,
            R11 = -0b011 - 1,
            R12 = -0b100 - 1,
            R13 = -0b101 - 1,
            R14 = -0b110 - 1,
            R15 = -0b111 - 1
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

        internal Pointer(Register register, int offset, Register.RegisterSize size) : base(1)
        {
            this.register = register;
            this.size = size;
            this.offset = -offset;
        }
        internal Pointer(Register register, int offset, int size) : this(register, offset, InstructionUtils.ToRegisterSize(size))
        {
        }
        internal Pointer(Register.RegisterName register, int offset, Register.RegisterSize size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(Register.RegisterName register, int offset, int size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(int offset, Register.RegisterSize size) : this(Register.RegisterName.RBP, offset, size)
        {
        }
        public Pointer(int offset, int size) : this(Register.RegisterName.RBP, offset, size)
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
        public Instruction instruction;
        public AssemblyExpr operand1, operand2;

        public Binary(Instruction instruction, AssemblyExpr operand1, AssemblyExpr operand2)
        {
            this.instruction = instruction;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }

        internal Binary(Instruction instruction, Register.RegisterName operand1, AssemblyExpr.Register.RegisterName operand2)
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
        public Instruction instruction;

        public Unary(Instruction instruction, AssemblyExpr operand)
        {
            this.instruction = instruction;
            this.operand = operand;
        }

        internal Unary(Instruction instruction, AssemblyExpr.Register.RegisterName operand) : this(instruction, new AssemblyExpr.Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    public class Zero : AssemblyExpr
    {
        public Instruction instruction;
        public Zero(Instruction instruction)
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

    internal static AssemblyExpr.Instruction ToType(Token.TokenType input, bool unary = false)
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

    private readonly static Dictionary<Token.TokenType, AssemblyExpr.Instruction> StringToOperatorTypeBinary = new()
    {
        // Binary
        { Token.TokenType.SHIFTRIGHT , AssemblyExpr.Instruction.SHR },
        { Token.TokenType.SHIFTLEFT , AssemblyExpr.Instruction.SHL },
        { Token.TokenType.DIVIDE , AssemblyExpr.Instruction.DIV },
        { Token.TokenType.MULTIPLY , AssemblyExpr.Instruction.IMUL },
        { Token.TokenType.B_NOT , AssemblyExpr.Instruction.NOT },
        { Token.TokenType.B_OR , AssemblyExpr.Instruction.OR },
        { Token.TokenType.B_AND , AssemblyExpr.Instruction.AND },
        { Token.TokenType.B_XOR , AssemblyExpr.Instruction.XOR },
        { Token.TokenType.PLUS , AssemblyExpr.Instruction.ADD },
        { Token.TokenType.MINUS , AssemblyExpr.Instruction.SUB },
        { Token.TokenType.EQUALTO , AssemblyExpr.Instruction.CMP },
        { Token.TokenType.NOTEQUALTO , AssemblyExpr.Instruction.CMP },
        { Token.TokenType.GREATER , AssemblyExpr.Instruction.CMP },
        { Token.TokenType.LESS , AssemblyExpr.Instruction.CMP },
        { Token.TokenType.GREATEREQUAL , AssemblyExpr.Instruction.CMP },
        { Token.TokenType.LESSEQUAL , AssemblyExpr.Instruction.CMP },
    };
    private readonly static Dictionary<Token.TokenType, AssemblyExpr.Instruction> StringToOperatorTypeUnary = new()
    {
        // Unary
        { Token.TokenType.PLUSPLUS,  AssemblyExpr.Instruction.INC },
        { Token.TokenType.MINUSMINUS,  AssemblyExpr.Instruction.DEC },
        { Token.TokenType.MINUS,  AssemblyExpr.Instruction.NEG },
    };

    internal readonly static Dictionary<AssemblyExpr.Instruction, AssemblyExpr.Instruction> ConditionalJump = new()
    {
        { AssemblyExpr.Instruction.SETE , AssemblyExpr.Instruction.JE },
        { AssemblyExpr.Instruction.SETNE , AssemblyExpr.Instruction.JNE },
        { AssemblyExpr.Instruction.SETG , AssemblyExpr.Instruction.JG },
        { AssemblyExpr.Instruction.SETL , AssemblyExpr.Instruction.JL },
        { AssemblyExpr.Instruction.SETGE , AssemblyExpr.Instruction.JGE },
        { AssemblyExpr.Instruction.SETLE , AssemblyExpr.Instruction.JLE },
    };
    internal readonly static Dictionary<AssemblyExpr.Instruction, AssemblyExpr.Instruction> ConditionalJumpReversed = new()
    {
        { AssemblyExpr.Instruction.SETE , AssemblyExpr.Instruction.JNE },
        { AssemblyExpr.Instruction.SETNE , AssemblyExpr.Instruction.JE },
        { AssemblyExpr.Instruction.SETG , AssemblyExpr.Instruction.JLE },
        { AssemblyExpr.Instruction.SETL , AssemblyExpr.Instruction.JGE },
        { AssemblyExpr.Instruction.SETGE , AssemblyExpr.Instruction.JE },
        { AssemblyExpr.Instruction.SETLE , AssemblyExpr.Instruction.JG },
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
