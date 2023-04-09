using Raze;
using System.Reflection;

internal abstract class Instruction
{
    public abstract string Accept(IVisitor visitor);

    public interface IVisitor
    {
        public string visitGlobal(Global instruction);
        public string visitSection(Section instruction);
        public string visitRegister(Register instruction);
        public string visitPointer(Pointer instruction);
        public string visitLiteral(Literal instruction);
        public string visitData(Data instruction);
        public string visitDataRef(DataRef instruction);
        public string visitProcedure(Procedure instruction);
        public string visitProcedureRef(ProcedureRef instruction);
        public string visitBinary(Binary instruction);
        public string visitUnary(Unary instruction);
        public string visitZero(Zero instruction);
        public string visitComment(Comment instruction);
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
            return visitor.visitGlobal(this);
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
            return visitor.visitSection(this);
        }
    }

    internal abstract class Value : Instruction
    {
        public string name;

        // 0 = Register, 1 = Pointer, 2 = Literal
        public int valueType;

        public Value(int valueType, string name)
        {
            this.valueType = valueType;
            this.name = name;
        }

        public bool IsRegister() => valueType == 0;
        public bool IsPointer() => valueType == 1;
        public bool IsLiteral() => valueType == 2;
    }

    internal class Register : Value
    {
        public enum RegisterSize
        {
            _64Bits = 8,
            _32Bits = 4, 
            _16Bits = 2, 
            _8BitsUpper = 0, 
            _8Bits = 1
        }

        public RegisterSize? size;

        private protected Register(int registerType, string register, RegisterSize? size) : base(registerType, register)
        {
            this.size = size;
        }
        
        private protected Register(int registerType, string register, int size) : base(registerType, register)
        {
            this.size = Enum.IsDefined(typeof(RegisterSize), size) ? ((RegisterSize)size) : throw new Errors.ImpossibleError($"Invalid Register Size ({size})"); ;
        }
        
        public Register(string register, RegisterSize? size) : base(0, register)
        {
            this.size = size;
        }

        public Register(string register, int size) : base(0, register)
        {
            this.size = Enum.IsDefined(typeof(RegisterSize), size) ? ((RegisterSize)size) : throw new Errors.ImpossibleError($"Invalid Register Size ({size})");
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitRegister(this);
        }
    }

    internal class Pointer : Register
    {
        public int offset;
        public char _operator;

        public Pointer(string register, int offset, int size, char _operator) : base(1, register, size)
        {
            this.offset = offset;
            this._operator = _operator;
        }

        public Pointer(int offset, int size) : this("RBP", offset, size, '-')
        {
        }

        public Pointer(string register, int offset, int size) : this(register, offset, size, '-')
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitPointer(this);
        }
    }

    internal class Literal : Value
    {
        public string type;

        public Literal(string name, string type) : base(2, name)
        {
            this.type = type;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitLiteral(this);
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
            return visitor.visitData(this);
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
            return visitor.visitDataRef(this);
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
            return visitor.visitProcedure(this);
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
            return visitor.visitProcedureRef(this);
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

        public Binary(string instruction, string operand1, string operand2)
            : this(instruction, new Register(operand1, Register.RegisterSize._64Bits), new Register(operand2, Register.RegisterSize._64Bits))
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitBinary(this);
        }
    }

    internal class StackAlloc : Binary
    {
        public StackAlloc(string instruction, Instruction operand1, Instruction operand2)
            : base (instruction, operand1, operand2)
        {
        }

        public StackAlloc(string instruction, string operand1, string operand2)
            : base(instruction, operand1, operand2)
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitBinary(this);
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

        public Unary(string instruction, string operand) : this(instruction, new Instruction.Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitUnary(this);
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
            return visitor.visitZero(this);
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
            return visitor.visitComment(this);
        }
    }
}

internal class InstructionInfo
{
    internal static string ToType(string input, bool unary=false)
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

    private readonly static Dictionary<string, string> StringToOperatorTypeBinary = new()
    {
        // Binary
        { "SHIFTRIGHT" , "SHR" },
        { "SHIFTLEFT" , "SHL" },
        { "DIVIDE" , "DIV" },
        { "MULTIPLY" , "IMUL" },
        { "B_NOT" , "NOT" },
        { "B_OR" , "OR" },
        { "B_AND" , "AND" },
        { "B_XOR" , "XOR" },
        { "PLUS" , "ADD" },
        { "MINUS" , "SUB" },
        { "EQUALTO" , "CMP" },
        { "NOTEQUALTO" , "CMP" },
        { "GREATER" , "CMP" },
        { "LESS" , "CMP" },
        { "GREATEREQUAL" , "CMP" },
        { "LESSEQUAL" , "CMP" },
    };
    private readonly static Dictionary<string, string> StringToOperatorTypeUnary = new()
    {
        // Unary
        { "PLUSPLUS",  "INC" },
        { "MINUSMINUS",  "DEC" },
        { "MINUS",  "NEG" },
    };

    internal readonly static Dictionary<string, string> ConditionalJump = new()
    {
        { "EQUALTO" , "JNE" },
        { "NOTEQUALTO" , "JE" },
        { "GREATER" , "JLE" },
        { "LESS" , "JGE" },
        { "GREATEREQUAL" , "JE" },
        { "LESSEQUAL" , "JG" },
    };

    internal readonly static string[] paramRegister = new string[] 
    {
        "RDI",
        "RSI",
        "RDX",
        "RCX",
        "R8",
        "R9"
    };

    internal readonly static string[] ReturnOverload = new string[]
    {
        "r15",
        "r14",
        "r13",
        "r12",
        "rbx"
    };

    internal readonly static string[] storageRegisters = new string[]
    {
        // REGISTER_NAME : true = scratch, false = preserved
        "RAX",
        "RBX",
        "R12",
        "R13",
        "R14",
        "R15"
    };

    public static string NextRegister(ref int idx)
    {
        return storageRegisters[idx++];
    }

    public static string CurrentRegister(int idx)
    {
        return storageRegisters[idx];
    }

    public static void FreeRegister(ref int idx)
    {
        idx--;
    }

    internal readonly static Dictionary<(string, Instruction.Register.RegisterSize?), string> Registers = new()
    {
        { ("RAX", Instruction.Register.RegisterSize._64Bits), "RAX" }, // 64-Bits 
        { ("RAX", Instruction.Register.RegisterSize._32Bits), "EAX" }, // Lower 32-Bits
        { ("RAX", Instruction.Register.RegisterSize._16Bits), "AX" }, // Lower 16-Bits
        { ("RAX", Instruction.Register.RegisterSize._8BitsUpper), "AH" }, // Upper 16-Bits
        { ("RAX", Instruction.Register.RegisterSize._8Bits), "AL" }, // Lower 8-Bits

        { ("RCX", Instruction.Register.RegisterSize._64Bits), "RCX" },
        { ("RCX", Instruction.Register.RegisterSize._32Bits), "ECX" },
        { ("RCX", Instruction.Register.RegisterSize._16Bits), "CX" },
        { ("RCX", Instruction.Register.RegisterSize._8BitsUpper), "CH" },
        { ("RCX", Instruction.Register.RegisterSize._8Bits), "CL" },

        { ("RDX", Instruction.Register.RegisterSize._64Bits), "RDX" },
        { ("RDX", Instruction.Register.RegisterSize._32Bits), "EDX" },
        { ("RDX", Instruction.Register.RegisterSize._16Bits), "DX" },
        { ("RDX", Instruction.Register.RegisterSize._8BitsUpper), "DH" },
        { ("RDX", Instruction.Register.RegisterSize._8Bits), "DL" },

        { ("RBX", Instruction.Register.RegisterSize._64Bits), "RBX" },
        { ("RBX", Instruction.Register.RegisterSize._32Bits), "EBX" },
        { ("RBX", Instruction.Register.RegisterSize._16Bits), "BX" },
        { ("RBX", Instruction.Register.RegisterSize._8BitsUpper), "BH" },
        { ("RBX", Instruction.Register.RegisterSize._8Bits), "BL" },

        { ("RSI", Instruction.Register.RegisterSize._64Bits), "RSI" },
        { ("RSI", Instruction.Register.RegisterSize._32Bits), "ESI" },
        { ("RSI", Instruction.Register.RegisterSize._16Bits), "SI" },
        { ("RSI", Instruction.Register.RegisterSize._8Bits), "SIL" },

        { ("RDI", Instruction.Register.RegisterSize._64Bits), "RDI" },
        { ("RDI", Instruction.Register.RegisterSize._32Bits), "EDI" },
        { ("RDI", Instruction.Register.RegisterSize._16Bits), "DI" },
        { ("RDI", Instruction.Register.RegisterSize._8Bits), "DIL" },

        { ("RSP", Instruction.Register.RegisterSize._64Bits), "RSP" },
        { ("RSP", Instruction.Register.RegisterSize._32Bits), "ESP" },
        { ("RSP", Instruction.Register.RegisterSize._16Bits), "SP" },
        { ("RSP", Instruction.Register.RegisterSize._8Bits), "SPL" },

        { ("RBP", Instruction.Register.RegisterSize._64Bits), "RBP" },
        { ("RBP", Instruction.Register.RegisterSize._32Bits), "EBP" },
        { ("RBP", Instruction.Register.RegisterSize._16Bits), "BP" },
        { ("RBP", Instruction.Register.RegisterSize._8Bits), "BPL" },

        { ("R8", Instruction.Register.RegisterSize._64Bits), "R8" },
        { ("R8", Instruction.Register.RegisterSize._32Bits), "R8D" },
        { ("R8", Instruction.Register.RegisterSize._16Bits), "R8W" },
        { ("R8", Instruction.Register.RegisterSize._8Bits), "R8B" },

        { ("R9", Instruction.Register.RegisterSize._64Bits), "R9" },
        { ("R9", Instruction.Register.RegisterSize._32Bits), "R9D" },
        { ("R9", Instruction.Register.RegisterSize._16Bits), "R9W" },
        { ("R9", Instruction.Register.RegisterSize._8Bits), "R9B" },

        { ("R10", Instruction.Register.RegisterSize._64Bits), "R10" },
        { ("R10", Instruction.Register.RegisterSize._32Bits), "R10D" },
        { ("R10", Instruction.Register.RegisterSize._16Bits), "R10W" },
        { ("R10", Instruction.Register.RegisterSize._8Bits), "R10B" },

        { ("R11", Instruction.Register.RegisterSize._64Bits), "R11" },
        { ("R11", Instruction.Register.RegisterSize._32Bits), "R11D" },
        { ("R11", Instruction.Register.RegisterSize._16Bits), "R11W" },
        { ("R11", Instruction.Register.RegisterSize._8Bits), "R11B" },

        { ("R12", Instruction.Register.RegisterSize._64Bits), "R12" },
        { ("R12", Instruction.Register.RegisterSize._32Bits), "R12D" },
        { ("R12", Instruction.Register.RegisterSize._16Bits), "R12W" },
        { ("R12", Instruction.Register.RegisterSize._8Bits), "R12B" },

        { ("R13", Instruction.Register.RegisterSize._64Bits), "R13" },
        { ("R13", Instruction.Register.RegisterSize._32Bits), "R13D" },
        { ("R13", Instruction.Register.RegisterSize._16Bits), "R13W" },
        { ("R13", Instruction.Register.RegisterSize._8Bits), "R13B" },

        { ("R14", Instruction.Register.RegisterSize._64Bits), "R14" },
        { ("R14", Instruction.Register.RegisterSize._32Bits), "R14D" },
        { ("R14", Instruction.Register.RegisterSize._16Bits), "R14W" },
        { ("R14", Instruction.Register.RegisterSize._8Bits), "R14B" },

        { ("R15", Instruction.Register.RegisterSize._64Bits), "R15" },
        { ("R15", Instruction.Register.RegisterSize._32Bits), "R15D" },
        { ("R15", Instruction.Register.RegisterSize._16Bits), "R15W" },
        { ("R15", Instruction.Register.RegisterSize._8Bits), "R15B" },
    };

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