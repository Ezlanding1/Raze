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
        LibraryDirectoryPathNotFound,

        // LexerError
        EmptyStringLiteral,
        NonTerminatedString,
        InvalidFormattedNumber,
        IllegalCharError,
        UnrecognizedEscapeSequence,

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
        InvalidThisKeyword,
        ExpressionReachedUnexpectedEnd,
        UnexpectedTokenInFunctionArguments,
        InvalidAssemblyBlockReturn,
        InvalidAssemblyBlockReturnArity,
        InvalidTruncationSize,
        InvalidAssemblyRegister,
        TokenExpected,
        ImportNotFound,
        InlineAssemblyInvalidRegisterOption,
        InlineAssemblyInvalidPtrOffset,
        InlineAssemblyInvalidFreeOperand,
        MultipleCallingConventionsSpecified,
        CallingConventionWithoutExtern,

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
        UndefinedReference_Suggestion,
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
        InvalidFunctionModifierPair,
        InvalidMainFunctionModifier_Include,
        InvalidMainFunctionModifier_NoInclude,
        InvalidParameterModifier_Ref,
        InvalidMainFunctionReturnType,
        PrimitiveWithConstructor,
        CircularInheritance,
        VariableUsedBeforeInitialization,
        AbstractFunctionNotInTrait,
        InstanceOfTraitCreated,
        InvalidOverrideModifier,
        ClassDoesNotOverrideAbstractFunction,
        TypeMismatch_OverridenMethod,
        DanglingPointerCreated_Assigned,
        DanglingPointerCreated_Returned,
        InvalidFunctionModifier_Ref,
        InvalidRefModifier,
        InvalidRefModifier_Location,
        RequiredRuntimeTypeNotFound,
        ReadonlyFieldModified,
        ReadonlyFieldModified_Ref,
        InvalidReadonlyModifier,
        ExternWithoutExternFileName,
        ExternWithBlock,
        NoConversionFound,

        // BackendError
        InvalidLiteralSize,
        InvalidLiteralSize_SystemSize,
        UnsupportedInstruction,
        InvalidInstructionOperandType_Arity1,
        InvalidInstructionOperandType_Arity2,
        InstructionOperandsSizeMismatch,
        InlineAssemblySizeMismatchReturn_NonPrimitive,
        InlineAssemblySizeMismatchReturn_Primitive,
        ExternFileExtensionNotSupported,
        UnsupportedOsAbiForImportType
    };
}
