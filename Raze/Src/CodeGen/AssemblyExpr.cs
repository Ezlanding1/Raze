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

    public abstract class Value : Operand
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

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return new(Assembler.Encoder.Operand.RegisterOperandType(this), (int)size);
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

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            Assembler.Encoder.Operand.ThrowTMP(register);
            return new(Assembler.Encoder.Operand.OperandType.M, (int)size);
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

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return new(Assembler.Encoder.Operand.OperandType.IMM, CodeGen.SizeOfLiteral(this));
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

    public interface IOperand
    {
        Assembler.Encoder.Operand ToAssemblerOperand();
    }
    public abstract class Operand : AssemblyExpr, IOperand
    {
        public abstract Assembler.Encoder.Operand ToAssemblerOperand();
    }


    public class Binary : AssemblyExpr
    {
        public Instruction instruction;
        public Operand operand1, operand2;

        public Binary(Instruction instruction, Operand operand1, Operand operand2)
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
