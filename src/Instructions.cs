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
        public string visitData(Data instruction);
        public string visitDataRef(DataRef instruction);
        public string visitFunction(Function instruction);
        public string visitFunctionRef(FunctionRef instruction);
        public string visitClass(Class instruction);
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
    internal class Register : Instruction
    {
        // 0 = Register, 1 = Pointer, 2 = Literal
        public int registerType;
        public string name;
        public Register(string name)
        {
            this.registerType = 0;
            this.name = name;
        }

        public Register(int registerType, string name)
        {
            this.registerType = registerType;
            this.name = name;
        }

        public Register(Register @this)
        {
            this.registerType = @this.registerType;
            this.name = @this.name;
        }

        public bool IsRegister() => registerType == 0;
        public bool IsPointer() => registerType == 1;
        public bool IsLiteral() => registerType == 2;

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitRegister(this);
        }
    }
    internal class Pointer : Register
    {
        public int offset;
        public int size;

        private Pointer(string ptr, int offset, int size, bool checkClassVar) : base(1, ptr)
        {
            this.offset = offset;
            this.size = size;


            if (checkClassVar)
            {
                if (SymbolTableSingleton.SymbolTable.other.globalClassVarOffset != null)
                {
                    this.offset += (int)SymbolTableSingleton.SymbolTable.other.globalClassVarOffset;
                }
            }
        }

        public Pointer(int offset, int size, bool checkClassVar=true)
            : this("RBP", offset, size, checkClassVar)
        {
        }

        public Pointer(bool isClassScoped, int offset, int size, bool checkClassVar=true)
            : this(isClassScoped? InstructionInfo.InstanceRegister : "RBP", offset, size, checkClassVar)
        {
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitPointer(this);
        }
    }
    internal class Literal : Register
    {
        public string type;
        public Literal(string name, string type) : base(2, name)
        {
            this.type = type;
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

    internal class Function : Instruction
    {
        public string name;
        public Function(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitFunction(this);
        }
    }

    internal class FunctionRef : Instruction
    {
        public string name;
        public FunctionRef(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitFunctionRef(this);
        }
    }

    internal class Class : Instruction
    {
        public string name;
        public Class(string name)
        {
            this.name = name;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitClass(this);
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
            : this (instruction, new Register(operand1), new Register(operand2)) 
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

        public Unary(string instruction, string operand)
        {
            this.instruction = instruction;
            this.operand = new Instruction.Register(operand);
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
    public const int MaxLiteral = 4;
    public const string InstanceRegister = "R12";

    internal static bool IsStack(Instruction.Register input, bool addSize=false)
    {
        return (input.name[0] == '[');
    }

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

    internal static string DataSizeOf(int size, ref string value)
    {
        // This check is needed for string literals
        if (value[0] == '"')
        {
            value += ", 0";
            return dataSize[1];
        }

        return dataSize[size];
    }

    internal static string ToRegister(int input, bool bits=false, string register="RAX")
    {
        input = bits ? (input / 8) : input;
        return Registers[(register, input)];
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
        { "return",  "RET" }
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

    internal readonly static Dictionary<(string, int), string> Registers = new()
    {
        { ("RAX", 8), "RAX" }, // 64-Bits 
        { ("RAX", 4), "EAX" }, // Lower 32-Bits
        { ("RAX", 2), "AX" }, // Lower 16-Bits
        { ("RAX", 1), "AL" }, // Lower 8-Bits

        { ("RCX", 8), "RCX" },
        { ("RCX", 4), "ECX" },
        { ("RCX", 2), "CX" },
        { ("RCX", 1), "CL" },

        { ("RDX", 8), "RDX" },
        { ("RDX", 4), "EDX" },
        { ("RDX", 2), "DX" },
        { ("RDX", 1), "DL" },

        { ("RBX", 8), "RBX" },
        { ("RBX", 4), "EBX" },
        { ("RBX", 2), "BX" },
        { ("RBX", 1), "BL" },

        { ("RSI", 8), "RSI" },
        { ("RSI", 4), "ESI" },
        { ("RSI", 2), "SI" },
        { ("RSI", 1), "SIL" },

        { ("RDI", 8), "RDI" },
        { ("RDI", 4), "EDI" },
        { ("RDI", 2), "DI" },
        { ("RDI", 1), "DIL" },

        { ("RSP", 8), "RSP" },
        { ("RSP", 4), "ESP" },
        { ("RSP", 2), "SP" },
        { ("RSP", 1), "SPL" },

        { ("RBP", 8), "RBP" },
        { ("RBP", 4), "EBP" },
        { ("RBP", 2), "BP" },
        { ("RBP", 1), "BPL" },

        { ("R8", 8), "R8" },
        { ("R8", 4), "R8D" },
        { ("R8", 2), "R8W" },
        { ("R8", 1), "R8B" },

        { ("R9", 8), "R9" },
        { ("R9", 4), "R9D" },
        { ("R9", 2), "R9W" },
        { ("R9", 1), "R9B" },

        { ("R10", 8), "R10" },
        { ("R10", 4), "R10D" },
        { ("R10", 2), "R10W" },
        { ("R10", 1), "R10B" },

        { ("R11", 8), "R11" },
        { ("R11", 4), "R11D" },
        { ("R11", 2), "R11W" },
        { ("R11", 1), "R11B" },

        { ("R12", 8), "R12" },
        { ("R12", 4), "R12D" },
        { ("R12", 2), "R12W" },
        { ("R12", 1), "R12B" },

        { ("R13", 8), "R13" },
        { ("R13", 4), "R13D" },
        { ("R13", 2), "R13W" },
        { ("R13", 1), "R13B" },

        { ("R14", 8), "R14" },
        { ("R14", 4), "R14D" },
        { ("R14", 2), "R14W" },
        { ("R14", 1), "R14B" },

        { ("R15", 8), "R15" },
        { ("R15", 4), "R15D" },
        { ("R15", 2), "R15W" },
        { ("R15", 1), "R15B" },
    };

    internal readonly static Dictionary<int, string> wordSize = new()
    {
        { 8, "QWORD"}, // 64-Bits
        { 4, "DWORD"}, // 32-Bits
        { 2, "WORD"}, // 16-Bits
        { 1, "BYTE"}, // 8-Bits
    };

    internal readonly static Dictionary<int, string> dataSize = new()
    {
        { 8, "dq"}, // 64-Bits
        { 4, "dd"}, // 32-Bits
        { 2, "dw"}, // 16-Bits
        { 1, "db"}, // 8-Bits
    };
}