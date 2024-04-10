using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Diagnostic
{
    public enum DiagnosticName
    {
        Impossible = -1,

        // DriverError
        UnsupportedSystem_CPU_Architecture,
        UnsupportedSystem_OsAbi,
        UnsupportedSystem_BitFormat,
        TargetSystemModified,

        // LexerError
        EmptyStringLiteral,
        NonTerminatedString,
        InvalidFormattedNumber,
        IllegalCharError,
        
        // ParserError
        UnexpectedEndInFunctionParameters,
        InvalidClassName,
        InvalidClassDefinition,
        InvalidPrimitiveName,
        InvalidPrimitiveSize,
        InvalidPrimitiveSuperclass,
        NoMatchingIf,
        InvalidAssignStatement,
        InvalidDeclareStatement,
        InvalidFunctionModifier_Ref,
        InvalidThisKeyword,
        ExpressionReachedUnexpectedEnd,
        UnexpectedTokenInFunctionArguments,
        InvalidAssemblyBlockReturn,
        InvalidAssemblyBlockReturnArity,
        InvalidTruncationSize,
        InvalidAssemblyRegister,
        TokenExpected,
        ImportNotFound,
        
        // AnalyzerError
        TypeMismatch,
        TypeMismatch_Return,
        TypeMismatch_Statement,
        NoReturn,
        NoReturn_FromAllPaths,
        ConstructorCalledAsMethod,
        MethodCalledAsConstructor,
        StaticMethodCalledFromInstanceContext,
        InstanceMethodCalledFromStaticContext,
        DoubleDeclaration,
        MainDoubleDeclaration,
        UndefinedReference,
        InvalidCall,
        AmbiguousCall,
        InvalidOperatorCall_Arity1,
        InvalidOperatorCall_Arity2,
        InvalidOperatorArity,
        UnrecognizedOperator,
        InvalidStatementLocation,
        InvalidOperatorArgument,
        ExpressionWithNonNullReturn,
        InvalidFunctionArgument,
        UnsafeCodeInSafeFunction,
        TopLevelCode,
        ConstructorWithNonVoidReturnType,
        InvalidConstructorModifier,
        EntrypointNotFound,
        InvalidMainFunctionModifier_Include,
        InvalidMainFunctionModifier_NoInclude,
        InvalidMainFunctionReturnType,
        PrimitiveWithConstructor,
        CircularInheritance,

        // BackendError
        InvalidLiteralSize,
        InvalidLiteralSize_SystemSize,
        UnsupportedInstruction,
        InvalidInstructionOperandType_Arity1,
        InvalidInstructionOperandType_Arity2,
        InstructionOperandsSizeMismatch
    };
}
