using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public abstract partial class Diagnostic
{
    private static Dictionary<DiagnosticName, (string details, Severity severity)> DiagnosticInfo = new()
    {
        { DiagnosticName.Impossible, ("{0}", Severity.Exception) },

        // DriverError
        { DiagnosticName.UnsupportedSystem_CPU_Architecture, ("Unsuppored CPU_Architecture '{0}'", Severity.Exception) },
        { DiagnosticName.UnsupportedSystem_OsAbi, ("Unsuppored OsABI '{0}'", Severity.Exception) },
        { DiagnosticName.UnsupportedSystem_BitFormat, ("Unsuppored BitFormat '{0}'", Severity.Exception) },
        { DiagnosticName.TargetSystemModified, ("Cannot run the binary on your system. Try compiling for your system or using the 'compile' command instead", Severity.Exception) },
        { DiagnosticName.LibraryDirectoryPathNotFound, ("Library directory path '{0}' not found", Severity.Exception) },

        // LexerError
        { DiagnosticName.EmptyStringLiteral, ("A String literal may not be empty", Severity.Error) },
        { DiagnosticName.NonTerminatedString, ("String '{0}' was not terminated", Severity.Error) },
        { DiagnosticName.InvalidFormattedNumber, ("'{0}' is incorectly formatted", Severity.Error) },
        { DiagnosticName.IllegalCharError, ("Character '{0}' is Illegal", Severity.Error) },
        { DiagnosticName.UnrecognizedEscapeSequence, ("The string escape sequence '{0}' is not recognized", Severity.Error) },
        
        // ParserError
        { DiagnosticName.UnexpectedEndInFunctionParameters, ("Function '{0}' reached an unexpected end in its parameters", Severity.Error) },
        { DiagnosticName.InvalidClassName, ("The name of a class may not be a literal ({0})", Severity.Error) },
        { DiagnosticName.InvalidClassDefinition, ("A class may only contain declarations and definitions. Got '{0}'", Severity.Error) },
        { DiagnosticName.InvalidPrimitiveName, ("The name of a class may not be a literal ({0})", Severity.Error) },
        { DiagnosticName.InvalidPrimitiveSize, ("The size of primitive classes must be the integers '8', '4', '2', or '1'. Got: '{0}'", Severity.Error) },
        { DiagnosticName.InvalidPrimitiveSuperclass, ("The superclass of a primitive must be a valid literal (" + string.Join(", ", Enum.GetValues<Parser.LiteralTokenType>()) + ") Got: '{0}'", Severity.Error) },
        { DiagnosticName.NoMatchingIf, ("'{0}' conditional has no matching 'if'", Severity.Error) },
        { DiagnosticName.InvalidAssignStatement, ("Cannot assign to {0}", Severity.Error) },
        { DiagnosticName.InvalidDeclareStatement, ("Cannot declare to a non-type value", Severity.Error) },
        { DiagnosticName.InvalidThisKeyword, ("The 'this' keyword may only be used in a member to reference the enclosing class", Severity.Error) },
        { DiagnosticName.ExpressionReachedUnexpectedEnd, ("Expression '{0}' reached an unexpected end", Severity.Error) },
        { DiagnosticName.UnexpectedTokenInFunctionArguments, ("Unexpected {0}", Severity.Error) },
        { DiagnosticName.InvalidAssemblyBlockReturn, ("Only one return may appear in an assembly block", Severity.Error) },
        { DiagnosticName.InvalidAssemblyBlockReturnArity, ("Return on a zero instruction is not allowed", Severity.Error) },
        { DiagnosticName.InvalidTruncationSize, ("You may only truncate an assembly value to 64, 32, 16, or 8 bits. Got '{0}'", Severity.Error) },
        { DiagnosticName.InvalidAssemblyRegister, ("Invalid assembly register '{0}'", Severity.Error) },
        { DiagnosticName.TokenExpected, ("Expected token {0}. Expected {1}. Got: '{2}'", Severity.Error) },
        { DiagnosticName.ImportNotFound, ("Import with file path '{0}' not found", Severity.Error) },
        { DiagnosticName.InlineAssemblyInvalidRegisterOption, ("A register allocation may only have the register options: 'r', 'x', and the size options: '8', '16', '32', '64'. Got: '{0}'", Severity.Error) },
        { DiagnosticName.InlineAssemblyInvalidPtrOffset, ("The offset of an inline assembly pointer must be a valid signed 32-bit integer. Got: '{0}'", Severity.Error) },
        { DiagnosticName.InlineAssemblyInvalidFreeOperand, ("The operand of the inline assembly 'free' expression must be a valid allocated resource. Got: '{0}'", Severity.Error) },
        { DiagnosticName.MultipleCallingConventionsSpecified, ("Function '{0}' may not have multiple calling conventions specified", Severity.Error) },
        { DiagnosticName.CallingConventionWithoutExtern, ("Calling conventions may only be specified for externs", Severity.Error) },
        
        // AnalyzerError
        { DiagnosticName.TypeMismatch, ("You cannot assign type '{0}' to type '{1}'", Severity.Error) },
        { DiagnosticName.TypeMismatch_Return, ("You cannot return type '{0}' from type '{1}'", Severity.Error) },
        { DiagnosticName.TypeMismatch_Statement, ("'{0}' expects condition to return '{1}'. Got '{2}'", Severity.Error) },
        { DiagnosticName.NoReturn, ("A Function must have a 'return' expression", Severity.Error) },
        { DiagnosticName.NoReturn_FromAllPaths, ("A Function must have a 'return' expression from all paths", Severity.Error) },
        { DiagnosticName.ConstructorCalledAsMethod, ("A Constructor may not be called as a method of its class", Severity.Error) },
        { DiagnosticName.MethodCalledAsConstructor, ("A Method may not be called as a constructor of its class", Severity.Error) },
        { DiagnosticName.StaticMethodCalledFromInstanceContext, ("You cannot call a static method from an instance context", Severity.Error) },
        { DiagnosticName.InstanceMethodCalledFromStaticContext, ("You cannot call an instance method from a static context", Severity.Error) },
        { DiagnosticName.DoubleDeclaration, ("A {0} '{1}' is already declared in this scope", Severity.Error) },
        { DiagnosticName.MainDoubleDeclaration, ("A program may have only one 'Main' method", Severity.Error) },
        { DiagnosticName.UndefinedReference, ("The {0} '{1}' does not exist in the current context", Severity.Error) },
        { DiagnosticName.UndefinedReference_Suggestion, ("The {0} '{1}' does not exist in the current context. Did you mean '{2}'?", Severity.Error) },
        { DiagnosticName.InvalidCall, ("'{0}' is not invokable", Severity.Error) },
        { DiagnosticName.AmbiguousCall, ("Call is ambiguous between {0} and {1}", Severity.Error) },
        { DiagnosticName.InvalidOperatorCall_Arity1, ("Type {0} doesn't have a definition for '{1}'", Severity.Error) },
        { DiagnosticName.InvalidOperatorCall_Arity2, ("Types '{0}' and '{1}' don't have a definition for '{2}'", Severity.Error) },
        { DiagnosticName.InvalidOperatorArity, ("The '{0}' operator must have an arity of {1}", Severity.Error) },
        { DiagnosticName.UnrecognizedOperator, ("'{0}' is not a recognized operator", Severity.Error) },
        { DiagnosticName.InvalidStatementLocation, ("{0} must be placed in {1}", Severity.Error) },
        { DiagnosticName.ExpressionWithNonNullReturn, ("Expression returned with type '{0}'", Severity.Error) },
        { DiagnosticName.UnsafeCodeInSafeFunction, ("Mark a function with 'unsafe' to include unsafe code", Severity.Error) },
        { DiagnosticName.TopLevelCode, ("Only functions and classes may be declared at the top level", Severity.Error) },
        { DiagnosticName.ConstructorWithNonVoidReturnType, ("The return type of a constructor must be 'void'", Severity.Error) },
        { DiagnosticName.InvalidConstructorModifier, ("A constructor cannot have the {0} modifier", Severity.Error) },
        { DiagnosticName.EntrypointNotFound, ("Program does not contain a Main method", Severity.Error) },
        { DiagnosticName.InvalidFunctionModifierPair, ("A function may not have the modifiers '{0}' and '{1}'", Severity.Error) },
        { DiagnosticName.InvalidMainFunctionModifier_Include, ("The Main function must have the '{0}' modifier", Severity.Error) },
        { DiagnosticName.InvalidMainFunctionModifier_NoInclude, ("The Main function may not have the '{0}' modifier", Severity.Error) },
        { DiagnosticName.InvalidParameterModifier_Ref, ("Cannot assign when non-variable is passed to 'ref' parameter", Severity.Error) },
        { DiagnosticName.InvalidMainFunctionReturnType, ("Main can only return types 'void', 'Integer', and 'Unsigned Integer'. Got '{0}'", Severity.Error) },
        { DiagnosticName.PrimitiveWithConstructor, ("A primitive may not have a constructor", Severity.Error) },
        { DiagnosticName.CircularInheritance, ("Cicular inheritance between types '{0}' and '{1}'", Severity.Error) },
        { DiagnosticName.VariableUsedBeforeInitialization, ("Variable '{0}' used before initialization", Severity.Error) },
        { DiagnosticName.AbstractFunctionNotInTrait, ("Abstract functions may only be defined in traits", Severity.Error) },
        { DiagnosticName.InstanceOfTraitCreated, ("An instance of a trait may not be created", Severity.Error) },
        { DiagnosticName.InvalidOverrideModifier, ("Function '{0}' has no suitable method to override", Severity.Error) },
        { DiagnosticName.ClassDoesNotOverrideAbstractFunction, ("Class '{0}' does not contain a method to override the abstract function '{2}' in '{1}'", Severity.Error) },
        { DiagnosticName.TypeMismatch_OverridenMethod, ("The return-type of overriden method '{0}' must match its virtual method's return-type '{1}'", Severity.Error) },
        { DiagnosticName.DanglingPointerCreated_Assigned, ("Local variable '{0}' cannot be assigned as 'ref' to a variable in local scope", Severity.Error) },
        { DiagnosticName.DanglingPointerCreated_Returned, ("Local variable '{0}' cannot be returned as 'ref' to a variable in local scope", Severity.Error) },
        { DiagnosticName.InvalidFunctionModifier_Ref, ("Cannot assign when non-variable is returned as 'ref'", Severity.Error) },
        { DiagnosticName.InvalidRefModifier, ("Non-variable values ({0}s) may mot be marked 'ref'", Severity.Error) },
        { DiagnosticName.InvalidRefModifier_Location, ("Variables marked 'ref' may not be used in a {0}", Severity.Error) },
        { DiagnosticName.RequiredRuntimeTypeNotFound, ("Required runtime type '{0}' from '{1}' not found", Severity.Error) },
        { DiagnosticName.ReadonlyFieldModified, ("Variables marked 'readonly' may not be modified (except in constructors)", Severity.Error) },
        { DiagnosticName.ReadonlyFieldModified_Ref, ("Variables marked 'readonly' may not be assigned as a 'ref' non-'readonly' variable (except in constructors)", Severity.Error) },
        { DiagnosticName.InvalidReadonlyModifier, ("The 'readonly' modifier is not valid in this context", Severity.Error) },
        { DiagnosticName.ExternWithoutExternFileName, ("Function '{0}' marked extern must specify an extern file name", Severity.Error) },
        { DiagnosticName.ExternWithBlock, ("Function '{0}' marked extern may not have a function body", Severity.Error) },
        
        // BackendError
        { DiagnosticName.InvalidLiteralSize, ("{0} literal '{1}' exceeds size of assigned data type '{2}'", Severity.Error) },
        { DiagnosticName.InvalidLiteralSize_SystemSize, ("{0} literal with size '{1}' must have the same size as the system size '{2}'", Severity.Error) },
        { DiagnosticName.UnsupportedInstruction, ("Instruction '{0}' is not supported", Severity.Error) },
        { DiagnosticName.InvalidInstructionOperandType_Arity1, ("'{0}' Instruction's operand must be a {2}. Got {3}\"", Severity.Error) },
        { DiagnosticName.InvalidInstructionOperandType_Arity2, ("'{0}' Instruction's {1} operand must be a {2}. Got {3}\"", Severity.Error) },
        { DiagnosticName.InstructionOperandsSizeMismatch, ("Instruction operand sizes don't match", Severity.Error) },
        { DiagnosticName.InlineAssemblySizeMismatchReturn_NonPrimitive, ("Only primitive types may be the return type of a value returned from an inline assembly expression", Severity.Error) },
        { DiagnosticName.InlineAssemblySizeMismatchReturn_Primitive, ("You cannot return an inline assembly value with size '{0}' from type '{1}'", Severity.Error) },
        { DiagnosticName.ExternFileExtensionNotSupported, ("Externs for the file extension '{0}' are not supported", Severity.Error) },
        { DiagnosticName.UnsupportedOsAbiForImportType, ("Import type '{0}' is not supported for the platform '{1}'", Severity.Error) },
    };
}
