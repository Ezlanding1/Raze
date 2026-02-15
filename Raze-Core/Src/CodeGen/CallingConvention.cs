using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static Raze.AssemblyExpr.Register.RegisterName;

namespace Raze;

public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal class CallingConvention(int shadowSpace, AssemblyExpr.Register.RegisterName[] nonVolatileRegisters, CallingConvention.TypeRegisterVariant paramRegisters, CallingConvention.TypeRegisterVariant returnRegisters)
    {
        public readonly int shadowSpace = shadowSpace;
        public readonly AssemblyExpr.Register.RegisterName[] nonVolatileRegisters = nonVolatileRegisters;
        public readonly TypeRegisterVariant paramRegisters = paramRegisters;
        public readonly TypeRegisterVariant returnRegisters = returnRegisters;

        internal class TypeRegisterVariant(AssemblyExpr.Register.RegisterName[] integer, AssemblyExpr.Register.RegisterName[] floating)
        {
            public readonly AssemblyExpr.Register.RegisterName[] integer = integer;
            public readonly AssemblyExpr.Register.RegisterName[] floating = floating;

            public AssemblyExpr.Register.RegisterName[] GetRegisters(bool _ref, Expr.Type? type)
                => _ref ? this.integer : IsFloatingType(type) ? this.floating : this.integer;

            public void Deconstruct(out AssemblyExpr.Register.RegisterName[] integer, out AssemblyExpr.Register.RegisterName[] floating)
                => (integer, floating) = (this.integer, this.floating);

            public ParameterRegisterIterator ToIter() => new(this);
        }

        internal class ParameterRegisterIterator(TypeRegisterVariant typeRegisterVariant)
        {
            public readonly IEnumerator<AssemblyExpr.Register.RegisterName> integer = 
                typeRegisterVariant.integer.ToList().GetEnumerator();
            public readonly IEnumerator<AssemblyExpr.Register.RegisterName> floating =
                typeRegisterVariant.floating.ToList().GetEnumerator();

            public IEnumerator<AssemblyExpr.Register.RegisterName> GetIter(Expr.Type? type, bool _ref) =>
                (!_ref && IsFloatingType(type)) ? floating : integer;
        }

        internal static readonly Dictionary<Expr.Function.CallingConvention, CallingConvention> callingConventions = new()
        {
            {
                Expr.Function.CallingConvention.RazeCall,  new(
                    shadowSpace: 0,
                    nonVolatileRegisters: [RBX, R12, R13, R14, R15],
                    paramRegisters: new([RDI, RSI, RDX, RCX, R8, R9], [XMM0, XMM1, XMM2, XMM3, XMM4, XMM5, XMM6, XMM7]),
                    returnRegisters: new([RAX, RDX], [XMM0, XMM1])
                )
            },
            {
                Expr.Function.CallingConvention.FastCall,  new(
                    shadowSpace: 32,
                    nonVolatileRegisters: [RBX, RBP, RDI, RSI, RSP, R12, R13, R14, R15, XMM6, XMM7, XMM8, XMM9, XMM10, XMM11, XMM12, XMM13, XMM14, XMM15],
                    paramRegisters: new([RCX, RDX, R8, R9], [XMM0, XMM1, XMM2, XMM3]),
                    returnRegisters: new([RAX], [XMM0])
                )
            }
        };
    }
}
