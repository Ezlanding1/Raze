using System.Runtime.CompilerServices;
using System.Text;

namespace Raze;

public abstract partial class AssemblyExpr
{
    public abstract T Accept<T>(IVisitor<T> visitor);

    public interface IVisitor<T>
    {
        public T VisitGlobal(Global instruction);
        public T VisitSection(Section instruction);
        public T VisitData(Data instruction);
        public T VisitProcedure(Procedure instruction);
        public T VisitLocalProcedure(LocalProcedure instruction);
        public T VisitBinary(Binary instruction);
        public T VisitUnary(Unary instruction);
        public T VisitZero(Nullary instruction);
        public T VisitComment(Comment instruction);
    }

    public abstract class TopLevelExpr : AssemblyExpr { }
    public abstract class TextExpr : AssemblyExpr { }
    public abstract class DataExpr : AssemblyExpr { }

    public class Global : TopLevelExpr
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

    public class Section : TopLevelExpr
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

    public interface IValue : IOperand
    {
        public Register.RegisterSize Size { get; }

        public bool IsRegister() => IsRegister(out _); 
        public bool IsPointer() => IsPointer(out _); 
        public bool IsLiteral() => IsLiteral(out _);
        public bool IsRegister(out Register register) { register = this as Register; return register != null; }
        public bool IsPointer(out Pointer pointer) { pointer = this as Pointer; return pointer != null; }
        public bool IsLiteral(out ILiteralBase literal) { literal = this as ILiteralBase; return literal != null; }

        internal Register NonPointerNonLiteral(CodeGen assembler, Expr.Type? type) =>
            (Register)this.NonPointer(assembler, type).NonLiteral(assembler, type);

        public IRegisterLiteral NonPointer(CodeGen codeGen, Expr.Type? type)
        {
            if (IsPointer(out var ptr))
            {
                Register reg;
                if (CodeGen.IsFloatingType(type))
                {
                    codeGen.alloc.Free(ptr);
                    reg = codeGen.alloc.NextSseRegister();
                    codeGen.Emit(new Binary(CodeGen.GetMoveInstruction(false, type as Expr.DataType), reg, this));
                }
                else
                {
                    reg = ptr.AsRegister(codeGen);
                    codeGen.Emit(new Binary(Instruction.MOV, reg, this));
                }
                return reg;
            }
            return (IRegisterLiteral)this;
        }
        internal IRegisterPointer NonLiteral(CodeGen codeGen, Expr.Type? type)
        {
            if (IsLiteral())
            {
                Register reg;
                if (CodeGen.IsFloatingType(type))
                {
                    reg = codeGen.alloc.NextSseRegister();
                    codeGen.Emit(new Binary(Instruction.MOVSS, reg, this));
                }
                else
                {
                    reg = codeGen.alloc.NextRegister(Size);
                    codeGen.Emit(new Binary(Instruction.MOV, reg, this));
                }
                return reg;
            }
            return (IRegisterPointer)this;
        }

        internal IValue IfLiteralCreateLiteral(Register.RegisterSize size, bool raw=false)
        {
            if (IsLiteral())
            {
                if (raw)
                {
                    return ((ILiteralBase)this).CreateRawLiteral(size);
                }
                return ((ILiteralBase)this).CreateLiteral(size);
            }
            return this;
        }
    }

    public interface IRegisterPointer : IValue
    {
        new public Register.RegisterSize Size { get; set; }
        public Register? GetRegister();
        // Note: This method should always shallow-clone the returned register
        public Register AsRegister(CodeGen assembler);
        public IRegisterPointer Clone();
    }

    public interface IRegisterLiteral : IValue
    {
    }

    public class Register : IValue, IRegisterLiteral, IRegisterPointer
    {
        public enum RegisterSize
        {
            _128Bits = 16,
            _64Bits = 8,
            _32Bits = 4,
            _16Bits = 2,
            _8BitsUpper = 0,
            _8Bits = 1
        }

        public enum RegisterName
        {
            TMP = -1,

            // General Purpose Registers
            RAX = 0b000,
            RCX = 0b001,
            RDX = 0b010,
            RBX = 0b011,
            RSP = 0b100,
            RBP = 0b101,
            RSI = 0b110,
            RDI = 0b111,
            R8 = 0b000 + 8, // x64
            R9 = 0b001 + 8,
            R10 = 0b010 + 8,
            R11 = 0b011 + 8,
            R12 = 0b100 + 8,
            R13 = 0b101 + 8,
            R14 = 0b110 + 8,
            R15 = 0b111 + 8,

            // SEE Registers
            XMM0 = 0b000 + 16,
            XMM1 = 0b001 + 16,
            XMM2 = 0b010 + 16,
            XMM3 = 0b011 + 16,
            XMM4 = 0b100 + 16,
            XMM5 = 0b101 + 16,
            XMM6 = 0b110 + 16,
            XMM7 = 0b111 + 16,
            XMM8 = 0b000 + 24, // x64
            XMM9 = 0b001 + 24,
            XMM10 = 0b010 + 24,
            XMM11 = 0b011 + 24,
            XMM12 = 0b100 + 24,
            XMM13 = 0b101 + 24,
            XMM14 = 0b110 + 24,
            XMM15 = 0b111 + 24,
        }

        public StrongBox<RegisterName> nameBox;
        public RegisterName Name
        {
            get => nameBox.Value;
            set => nameBox.Value = value;
        }

        private RegisterSize _size;
        public RegisterSize Size { get => _size; set => _size = value; }

        public Register(RegisterName register, RegisterSize size)
        {
            this.nameBox = new(register);
            this.Size = size;
        }

        public Register(RegisterName register, int size) : this(register, InstructionUtils.ToRegisterSize(size))
        {
        }

        public Register(StrongBox<RegisterName> _name, RegisterSize size)
        {
            this.nameBox = _name;
            this.Size = size;
        }

        internal static bool IsSseRegister(RegisterName name) => name >= RegisterName.XMM0 && name <= RegisterName.XMM15;

        public Register GetRegister() => this;
        public Register AsRegister(CodeGen assembler) => (Register)Clone();
        public IRegisterPointer Clone() => new Register(nameBox, Size);

        public T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitRegister(this);
        }

        public Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return Assembler.Encoder.Operand.RegisterOperandType(this);
        }

        public T Accept<T>(IBinaryOperandVisitor<T> visitor, IOperand operand)
        {
            return operand.VisitOperandRegister(visitor, this);
        }
        public T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg)
        {
            return visitor.VisitRegisterRegister(this, reg);
        }
        public T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr)
        {
            return visitor.VisitRegisterMemory(this, ptr);
        }
        public T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm)
        {
            return visitor.VisitRegisterImmediate(this, imm);
        }
    }

    public class Pointer : IValue, IRegisterPointer
    {
        internal Register? value;
        internal Literal offset;

        private Register.RegisterSize _size;
        public Register.RegisterSize Size { get => _size; set => _size = value; }

        // Only LiteralTypes that resolve to signed integers are supported (Integer, RefData, RefProcedure, RefLocalProcedure)
        internal Pointer(Register? value, Literal offset, Register.RegisterSize _size)
        {
            this.value = value;
            this._size = _size;
            this.offset = offset;
        }
        internal Pointer(Register value, int offset, Register.RegisterSize _size) 
            : this(value, new Literal(Literal.LiteralType.Integer, ImmediateGenerator.MinimizeImmediate(Literal.LiteralType.Integer, BitConverter.GetBytes(offset))), _size)
        {
        }
        internal Pointer(Register value, Literal offset, int size) : this(value, offset, (Register.RegisterSize)size)
        {
        }
        internal Pointer(StrongBox<Register.RegisterName> register, int offset, Register.RegisterSize size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(Register.RegisterName register, Literal offset, Register.RegisterSize size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }
        internal Pointer(Register.RegisterName register, int offset, Register.RegisterSize size) : this(new Register(register, Register.RegisterSize._64Bits), offset, size)
        {
        }

        public Register? GetRegister() => value;

        public bool IsOnStack() => value.Name == Register.RegisterName.RBP;

        public Register AsRegister(CodeGen assembler)
        {
            return (value == null || IsOnStack()) ? assembler.alloc.NextRegister(Size) : new Register(value!.nameBox, Size);
        }
        public IRegisterPointer Clone() => new Pointer(value, offset, Size);

        public T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitMemory(this);
        }

        public Assembler.Encoder.Operand ToAssemblerOperand()
        {
            if (value != null)
            {
                Assembler.Encoder.Operand.ThrowTMP(value);
            }
            return new(Assembler.Encoder.Operand.OperandType.M, (int)Size);
        }

        public T Accept<T>(IBinaryOperandVisitor<T> visitor, IOperand operand)
        {
            return operand.VisitOperandMemory(visitor, this);
        }
        public T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm) => visitor.VisitMemoryImmediate(this, imm);
        public T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr) => visitor.VisitMemoryMemory(this, ptr);
        public T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg) => visitor.VisitMemoryRegister(this, reg);
    }

    public interface ILiteralBase : IValue, IRegisterLiteral
    {
        public IValue CreateLiteral(Register.RegisterSize size);
        public Literal CreateRawLiteral(Register.RegisterSize size);
    }

    public class UnresolvedLiteral : ILiteralBase
    {
        internal Literal.LiteralType type;

        public string value;

        public Register.RegisterSize Size => throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Attempted access of UnresolvedRegister size"));

        internal UnresolvedLiteral(Literal.LiteralType type, string value)
        {
            this.type = type;
            this.value = value;
        }

        public virtual IValue CreateLiteral(Register.RegisterSize size) => CreateRawLiteral(size);
        public virtual Literal CreateRawLiteral(Register.RegisterSize size)
        {
            return type >= Literal.LiteralType.RefData ? new LabelLiteral(type, value, size) : new Literal(type, value, size);
        }

        public T Accept<T>(IBinaryOperandVisitor<T> visitor, IOperand operand) =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));

        public T Accept<T>(IUnaryOperandVisitor<T> visitor) =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));

        public Assembler.Encoder.Operand ToAssemblerOperand() =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));

        public T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm) =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));

        public T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr) =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));

        public T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg) =>
            throw Diagnostics.Panic(new Diagnostic.ImpossibleDiagnostic("Literal left unresolved"));
    }

    public class UnresolvedDataLiteral : UnresolvedLiteral
    {
        CodeGen codeGen;

        internal UnresolvedDataLiteral(Literal.LiteralType type, string value, CodeGen codeGen) : base(type, value)
        {
            this.codeGen = codeGen;
        }

        public override IValue CreateLiteral(Register.RegisterSize size)
        {
            string name = codeGen.DataLabel;
            codeGen.EmitData(
                new Data(
                    name,
                    type,
                    [ImmediateGenerator.Generate(type, value, size)]
                )
            );
            codeGen.dataCount++;
            return new Pointer(null, new DataRef(name), size);
        }

        public override Literal CreateRawLiteral(Register.RegisterSize size)
        {
            return new Literal(type, ImmediateGenerator.Generate(type, value, size));
        }
    }

    public class Literal : ILiteralBase
    {
        internal enum LiteralType
        {
            Integer = Parser.LiteralTokenType.Integer,
            UnsignedInteger = Parser.LiteralTokenType.UnsignedInteger,
            Floating = Parser.LiteralTokenType.Floating,
            String = Parser.LiteralTokenType.String,
            Binary = Parser.LiteralTokenType.Binary,
            Hex = Parser.LiteralTokenType.Hex,
            Boolean = Parser.LiteralTokenType.Boolean,
            RefData,
            RefProcedure,
            RefLocalProcedure
        }
        internal LiteralType type;
        public byte[] value;
        public Register.RegisterSize Size => (Register.RegisterSize)value.Length;

        private protected Literal(LiteralType type)
        {
            this.type = type;
        }
        internal Literal(LiteralType type, byte[] value)
        {
            this.type = type;
            this.value = value;
        }
        internal Literal(LiteralType type, string value, Register.RegisterSize size)
        {
            this.type = type;
            this.value = ImmediateGenerator.Generate(type, value, size);
        }

        public IValue CreateLiteral(Register.RegisterSize size) => CreateRawLiteral(size);
        public Literal CreateRawLiteral(Register.RegisterSize size) => this;

        public T Accept<T>(IUnaryOperandVisitor<T> visitor)
        {
            return visitor.VisitImmediate(this);
        }

        public virtual Assembler.Encoder.Operand ToAssemblerOperand()
        {
            value = ImmediateGenerator.MinimizeImmediate(type, value);
            return new(
                ImmediateGenerator.IsOne(this)? Assembler.Encoder.Operand.OperandType.One : Assembler.Encoder.Operand.OperandType.IMM,
                (Assembler.Encoder.Operand.OperandSize)this.Size
            );
        }

        public T Accept<T>(IBinaryOperandVisitor<T> visitor, IOperand operand)
        {
            return operand.VisitOperandImmediate(visitor, this);
        }

        public T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm) =>
            throw Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Immediate");
        public T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr) =>
            throw Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Memory");
        public T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg) =>
            throw Assembler.Encoder.EncodingUtils.ThrowIvalidEncodingType("Immediate", "Register");
    }

    public class LabelLiteral : Literal
    {
        internal bool scoped;
        internal string Name
        {
            get => Encoding.ASCII.GetString(this.value);
            set => this.value = Encoding.ASCII.GetBytes(value);
        }

        private protected LabelLiteral(LiteralType type, string name) : base(type)
        {
            this.Name = name;
        }
        internal LabelLiteral(LiteralType type, byte[] value) : base(type)
        {
            this.value = value;
        }
        internal LabelLiteral(LiteralType type, string name, Register.RegisterSize dataTypeSize) : base(type)
        {
            this.value = ImmediateGenerator.Generate(type, name, dataTypeSize);
        }

        public override Assembler.Encoder.Operand ToAssemblerOperand()
        {
            return new(
                ImmediateGenerator.IsOne(this) ? Assembler.Encoder.Operand.OperandType.One : Assembler.Encoder.Operand.OperandType.IMM,
                (Assembler.Encoder.Operand.OperandSize)this.Size
            );
        }
    }

    public class Data : DataExpr
    {
        public string? name;
        internal (Literal.LiteralType type, IEnumerable<byte[]> value) literal;
        public Register.RegisterSize Size => (Register.RegisterSize)literal.value.ElementAt(0).Length;

        internal Data(string? name, Literal literal)
        {
            this.name = name;
            this.literal.type = literal.type;
            this.literal.value = [literal.value];
        }
        internal Data(string? name, Literal.LiteralType type, IEnumerable<byte[]> value)
        {
            this.name = name;
            this.literal.type = type;
            this.literal.value = value;
        }


        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitData(this);
        }
    }

    public class DataRef : LabelLiteral
    {
        public DataRef(string dataName) : base(LiteralType.RefData, dataName)
        {
        }
    }

    public class Procedure : TextExpr
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

    public class LocalProcedure : TextExpr
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

    public class ProcedureRef : LabelLiteral
    {
        public ProcedureRef(string name) : base(LiteralType.RefProcedure, name)
        {
        }
    }

    public class LocalProcedureRef : LabelLiteral
    {
        public LocalProcedureRef(string name) : base(LiteralType.RefLocalProcedure, name)
        {
        }
    }

    public interface IUnaryOperandVisitor<T>
    {
        public T VisitRegister(Register reg);
        public T VisitMemory(Pointer ptr);
        public T VisitImmediate(Literal imm);
    }
    public interface IBinaryOperandVisitor<T>
    {
        public T VisitRegisterRegister(Register reg1, Register reg2);
        public T VisitRegisterMemory(Register reg1, Pointer ptr2);
        public T VisitRegisterImmediate(Register reg1, Literal imm2);

        public T VisitMemoryRegister(Pointer ptr1, Register reg2);
        public T VisitMemoryMemory(Pointer ptr1, Pointer ptr2);
        public T VisitMemoryImmediate(Pointer ptr1, Literal imm2);
    }
    public interface IOperand
    {
        public T Accept<T>(IBinaryOperandVisitor<T> visitor, IOperand operand);
        public T Accept<T>(IUnaryOperandVisitor<T> visitor);

        public T VisitOperandRegister<T>(IBinaryOperandVisitor<T> visitor, Register reg);
        public T VisitOperandMemory<T>(IBinaryOperandVisitor<T> visitor, Pointer ptr);
        public T VisitOperandImmediate<T>(IBinaryOperandVisitor<T> visitor, Literal imm);

        public Assembler.Encoder.Operand ToAssemblerOperand();
    }

    public abstract class OperandInstruction : TextExpr
    {
        public Instruction instruction;
        public abstract IValue[] Operands { get; }
    }

    public class Binary : OperandInstruction
    {
        public IValue operand1, operand2;

        public override IValue[] Operands => new IValue[] { operand1, operand2 };

        public Binary(Instruction instruction, IValue operand1, IValue operand2)
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

    public class Unary : OperandInstruction
    {
        public IValue operand;

        public override IValue[] Operands => new IValue[] { operand };

        public Unary(Instruction instruction, IValue operand)
        {
            this.instruction = instruction;
            this.operand = operand;
        }
        internal Unary(Instruction instruction, Register.RegisterName operand) : this(instruction, new Register(operand, Register.RegisterSize._64Bits))
        {
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitUnary(this);
        }
    }

    public class Nullary : OperandInstruction
    {
        public override IValue[] Operands => new IValue[0];

        public Nullary(Instruction instruction)
        {
            this.instruction = instruction;
        }

        public override T Accept<T>(IVisitor<T> visitor)
        {
            return visitor.VisitZero(this);
        }
    }

    public class Comment : TopLevelExpr
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
