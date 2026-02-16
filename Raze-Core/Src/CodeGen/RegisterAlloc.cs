using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Raze;
public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal partial class RegisterAlloc
    {
        RegisterGraph registerGraph = new RegisterGraph();
        public Dictionary<AssemblyExpr.IValue, AssemblyExpr.Pointer?> ColorGraph() => 
            registerGraph.ColorGraph();

        // Returns and allocates next register
        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size, Expr.Type? type) => 
            IsFloatingType(type) ? NextSseRegister() : NextRegister(size);

        public AssemblyExpr.Register NextRegister(AssemblyExpr.Register.RegisterSize size)
        {
            var register = registerGraph.AllocateNode().register;
            register.Size = size;
            return register;
        }

        public AssemblyExpr.Register NextSseRegister()
        {
            var node = registerGraph.AllocateNode();
            node.SetState(StateUtils.sseRegisters);
            var register = node.register;
            register.Size = AssemblyExpr.Register.RegisterSize._128Bits;
            return register;
        }

        // Returns next register without allocation
        public AssemblyExpr.Register CurrentRegister(AssemblyExpr.Register.RegisterSize size, Expr.Type? type) =>
            IsFloatingType(type) ? CurrentSseRegister() : CurrentRegister(size);

        public AssemblyExpr.Register CurrentRegister(AssemblyExpr.Register.RegisterSize size)
        {
            var register = NextRegister(size);
            Free(register);
            return register;
        }
        public AssemblyExpr.Register CurrentSseRegister()
        {
            var register = NextSseRegister();
            Free(register);
            return register;
        }

        public void SaveScratchRegistersBeforeCall(List<AssemblyExpr.Register> l, Expr.Function function)
        {
            var cconv = InstructionUtils.GetCallingConvention(function.callingConvention);

            //var usedParameters = Enumerable.Range(0, function.Arity).Select(i => cconv.paramRegisters.GetRegisters(IsFloatingType(function.parameters[i].stack.type))[i]);

            foreach (var v in registerGraph.GetAliveNodes())
            {
                if (!l.Contains(v.register))
                {
                    v.SetState(StateUtils.NonVolatileRegisters(cconv));
                }
            }
        }

        public void ReserveRegisterAndFree(AssemblyExpr.Register.RegisterName name)
        {
            var node = registerGraph.AllocateNode();
            node.SetState([name], Node.NodePriority.High, false);
            node.Free();
        }

        public AssemblyExpr.Register ReserveRegister(AssemblyExpr.Register.RegisterName name, AssemblyExpr.Register.RegisterSize size)
        {
            var node = registerGraph.AllocateNode();
            node.SetState([name], Node.NodePriority.High, false);
            node.register.Size = size;
            return node.register;
        }

        // Allocate and return the next parameter register for a function call
        // 'value' should be the value of the ith argument. It will be freed in this function
        // The resulting allocated parameter register will be added to the end of 'usedRegs'
        // If a parameter register is found, this function will return 'true'
        // Otherwise, it will add the value to 'stackArgs' and return 'false'
        public bool AllocParam(CallingConvention.ParameterRegisterIterator paramRegIter, 
            AssemblyExpr.IValue value,
            Expr.Type? type,
            bool _ref,
            List<AssemblyExpr.Register> usedRegs,
            List<(bool, AssemblyExpr.IValue)> stackArgs)
        {
            var iter = paramRegIter.GetIter(type, _ref);
            
            if (iter.MoveNext())
            {
                var regName = iter.Current;
                var regSize = GetRegisterSize(value.Size, type, _ref);

                Free(value);
                SetSuggestedRegister(value, regName);
                usedRegs.Add(ReserveRegister(regName, regSize));

                return true;
            }
            stackArgs.Add((_ref, value));
            return false;
        }

        public AssemblyExpr.Register CallAllocReturnRegister(CodeGen codeGen, Expr.Function.CallingConvention cconv, bool _ref, Expr.DataType _returnType)
        {
            var size = InstructionUtils.ToRegisterSize(_returnType.allocSize);

            var regName = InstructionUtils
                .GetCallingConvention(cconv)
                .returnRegisters
                .GetRegisters(_ref, _returnType)[0];

            var regSize = GetRegisterSize(size, _returnType, _ref);

            var _returnReg = ReserveRegister(regName, regSize);
            codeGen.alloc.Free(_returnReg);

            var reg = codeGen.alloc.NextRegister(size, _ref ? null : _returnType);
            SetSuggestedRegister(reg, regName);

            codeGen.Emit(new AssemblyExpr.Binary(GetMoveInstruction(false, _returnType), reg, _returnReg));

            return reg;
        }

        public void SetSuggestedRegister(AssemblyExpr.IValue value, AssemblyExpr.Register.RegisterName suggested)
        {
            var register = value?.GetRegisterOrDefualt();
            if (register == null) return;

            Node? node = registerGraph.GetNodeForRegister(register);
            node?.SetSuggestedRegister(suggested);
        }

        public void ListAccept<T, T2>(List<T> list, Expr.IVisitor<T2> visitor)
            where T : Expr
            where T2 : AssemblyExpr.IValue
        {
            foreach (var expr in list)
            {
                Free(expr.Accept(visitor));
            }
        }

        // Frees a register by allowing it to be alloc-ed elsewhere, and making the other uses pull from a new instance
        public void Free(AssemblyExpr.IValue? value) => Free(value?.GetRegisterOrDefualt());
        public void Free(AssemblyExpr.Pointer? ptr) => Free(ptr?.value);
        public void Free(AssemblyExpr.Register? register)
        {
            if (register == null) return;

            Node? node = registerGraph.GetNodeForRegister(register);
            node?.Free();
        }

        public void UnlockAndFree(AssemblyExpr.IValue? value)
        {
            Unlock(value);
            Free(value);
        }

        public void Lock(AssemblyExpr.IValue? value) => Lock(value?.GetRegisterOrDefualt());
        public void Lock(AssemblyExpr.Pointer? ptr) => Lock(ptr?.value);
        public void Lock(AssemblyExpr.Register? register)
        {
            if (register == null) return;

            Node? node = registerGraph.GetNodeForRegister(register);
            node?.Lock();
        }

        public bool IsLocked(AssemblyExpr.IValue value)
        {
            AssemblyExpr.Register? register = value.GetRegisterOrDefualt();

            if (register == null) 
                return false;

            Node? node = registerGraph.GetNodeForRegister(register);
            return node?.Locked != 0;
        }

        public void Unlock(AssemblyExpr.IValue? value) => Unlock(value?.GetRegisterOrDefualt());
        public void Unlock(AssemblyExpr.Pointer? ptr) => Unlock(ptr?.value);
        public void Unlock(AssemblyExpr.Register? register)
        {
            if (register == null) return;

            Node? node = registerGraph.GetNodeForRegister(register);
            node?.Unlock();
        }
    }
}
