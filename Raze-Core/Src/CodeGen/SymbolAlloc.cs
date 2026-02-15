using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;
public partial class CodeGen : Expr.IVisitor<AssemblyExpr.IValue?>
{
    internal partial class RegisterAlloc
    {
        public Expr.Definition Current { get => currentInlined ?? current; set => current = value; }
        private Expr.Definition current;
        public Expr.Function? currentInlined;

        private Stack<int> frameSize = new();

        public void UpContext()
        {
            Diagnostics.Assert(
                current != null,
                "Up Context Called On 'GLOBAL' context (no enclosing) in assembler"
            );

            current = (Expr.Definition)Current.enclosing;
        }

        public static void AllocateHeapVariable(Expr.Definition current, Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName = AssemblyExpr.Register.RegisterName.RBP)
        {
            var variableSize = variable._ref ? 8 : variable.size;
            variable.value = new AssemblyExpr.Pointer(registerName, current.size, (AssemblyExpr.Register.RegisterSize)variableSize);
            current.size += variableSize;
        }
        public void AllocateVariable(Expr.StackData variable, AssemblyExpr.Register.RegisterName registerName=AssemblyExpr.Register.RegisterName.RBP)
        {
            var variableSize = variable._ref ? 8 : variable.size;
            current.size += variableSize;
            variable.value = new AssemblyExpr.Pointer(registerName, -current.size, (AssemblyExpr.Register.RegisterSize)variableSize);
        }

        public void AllocateParameters(Expr.Function function, CodeGen codeGen)
        {
            bool instance = !function.modifiers["static"];
            var paramRegIter = InstructionUtils.GetParamRegisters().ToIter();
            var stackRegName = AssemblyExpr.Register.RegisterName.RBP;

            if (instance)
            {
                var enclosing = SymbolTableSingleton.SymbolTable.NearestEnclosingClass(function);
                Expr.Type? _thisType = enclosing;
                var _thisSize = (AssemblyExpr.Register.RegisterSize)enclosing!.allocSize;

                var iter = paramRegIter.GetIter(enclosing, false);
                iter.MoveNext();

                var regName = iter.Current;
                var regSize = GetRegisterSize(_thisSize, _thisType, false);

                codeGen.Emit(new AssemblyExpr.Binary(
                    GetMoveInstruction(false, _thisType as Expr.DataType), 
                    new AssemblyExpr.Pointer(stackRegName, -(int)_thisSize, _thisSize), 
                    new AssemblyExpr.Register(regName, regSize))
                );
                function.size += (int)_thisSize;
            }

            int currentStackParamOffset = 8;

            foreach (var parameter in function.parameters)
            {
                var type = parameter.stack.type;
                var _ref = parameter.modifiers["ref"];
                var paramSize = (AssemblyExpr.Register.RegisterSize)parameter.stack.size;
                var stackPtrSize = GetPointerSize(paramSize, _ref);

                var iter = paramRegIter.GetIter(type, _ref);

                if (iter.MoveNext())
                {
                    AllocateVariable(parameter.stack);

                    codeGen.Emit(new AssemblyExpr.Binary(
                        GetMoveInstruction(false, _ref ? null : parameter.stack.type),
                        new AssemblyExpr.Pointer(stackRegName, parameter.stack.ValueAsPointer.offset, stackPtrSize),
                        new AssemblyExpr.Register(iter.Current, GetRegisterSize(paramSize, type, _ref))
                    ));   
                }
                else
                {
                    currentStackParamOffset += 8;
                    parameter.stack.value = new AssemblyExpr.Pointer(stackRegName, currentStackParamOffset, stackPtrSize);
                }
            }
        }

        public void InitializeFunction(Expr.Function function)
        {
            Current = function;
            CreateBlock();
            FunctionPushPreserved.totalSizes.Add(0);
        }

        public void CreateBlock() => frameSize.Push(Current.size);
        public void RemoveBlock()
        {
            if (Current is Expr.Function)
            {
                FunctionPushPreserved.totalSizes[^1] = Math.Max(FunctionPushPreserved.totalSizes[^1], Current.size);
            }
            Current.size = frameSize.Pop();
        }
    }
}
