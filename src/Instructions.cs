using Espionage;
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
        public string visitFunction(Function instruction);
        public string visitReference(FunctionRef instruction);
        public string visitClass(Class instruction);
        public string visitBinary(Binary instruction);
        public string visitUnary(Unary instruction);
        public string visitZero(Zero instruction);
        public string visitComment(Comment instruction);
        public string visitAsmInstruction(AsmInstruction instruction);
    }
    internal class AsmInstruction : Instruction
    {
        public string instruction;
        public AsmInstruction(string instruction)
        {
            this.instruction = instruction;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitAsmInstruction(this);
        }
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
        public Pointer(int offset, int size)
            : base(1, "RBP")
        {
            this.offset = offset;
            this.size = size;
        }

        public override string Accept(IVisitor visitor)
        {
            return visitor.visitPointer(this);
        }
    }
    internal class Literal : Register
    {
        public Literal(string name) : base(2, name)
        {
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
            return visitor.visitReference(this);
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
        {
            this.instruction = instruction;
            this.operand1 = new Register(operand1);
            this.operand2 = new Register(operand2);
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
    internal static string ToRegister(int input, bool bits=false, string register="RAX")
    {
        input = bits ? (input / 8) : input;
        return raxRegister[input];
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

    internal readonly static Dictionary<int, string> raxRegister = new()
    {
        { 8, "RAX"}, // 64-Bits
        { 4, "EAX"}, // Lower 32-Bits
        { 2, "AX"}, // Lower 16-Bits
        { 1, "AL"}, // Lower 8-Bits
        { 0, "AH"} // Upper 8-Bits
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