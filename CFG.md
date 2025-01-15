# Raze Language Context-Free Grammar (EBNF)
> The definition of the terminals: IDENTIFIER, INTEGER, FLOATING, STRING, and CHAR, can be found as regex in [Patterns.cs](https://github.com/Ezlanding1/Raze/blob/main/Raze-Core/Src/LexicalAnalysis/Patterns.cs)
```python
# 1. Program Structure
Program ::= { TopLevel }
TopLevel ::= Import
           | Definition

# 2. Imports
Import ::= "import" ImportPath ";"
         | "import" ImportSelector "from" ImportPath ";"
ImportSelector ::= "*" 
                 | TypeReference [ ".*" ]
ImportPath ::= { IDENTIFIER | "." | "/" } [ ".rz" ]

# 3. Definitions
Definition ::= FunctionDefinition
             | ClassDefinition
             | TraitDefinition
             | PrimitiveDefinition

# 3.1 Functions
FunctionDefinition ::=
    "function"
    { FunctionModifier }
    [ TypeReference ]
    IDENTIFIER
    "(" [ ParameterList ] ")"
    [ "from" STRING ]
    ( ";" | Block )
FunctionModifier ::= [ "static"
                     | "unsafe"
                     | "operator"
                     | "inline"
                     | "virtual"
                     | "override"
                     | "extern"
                     | "dll"
                     | "fastcall" ]
ParameterList ::= Parameter { "," Parameter }
Parameter ::= { ("ref" | "readonly") } TypeReference IDENTIFIER

# 3.2 Classes and Traits
ClassDefinition ::=
    "class" IDENTIFIER [ "extends" TypeReference ] "{"
        { ClassMember }
    "}"
TraitDefinition ::=
    "trait" IDENTIFIER [ "extends" TypeReference ] "{"
        { ClassMember }
    "}"
ClassMember ::= Declaration ";"
              | Definition

# 3.3 Primitive Types
PrimitiveDefinition ::=
    "primitive" "class" IDENTIFIER "sizeof" INTEGER [ "extends" "INTEGER" | "FLOATING" | "STRING" | "CHAR" ] "{"
        { Definition }
    "}"

# 4. Statements and Blocks
Block ::= "{" { Statement } "}"
Statement ::= Block
            | Conditional
            | Loop
            | ReturnStmt ";"
            | Declaration ";"
            | Expression ";"

# 4.1 Conditionals
Conditional ::= "if" "(" Expression ")" Block
              { "elif" "(" Expression ")" Block }
              [ "else" Block ]

# 4.2 Loops
Loop ::= WhileLoop
       | ForLoop
WhileLoop ::= "while" "(" Expression ")" Block
ForLoop ::= "for" "(" Expression ";" Expression ";" Expression ")" Block

# 4.3 Return Statements
ReturnStmt ::= "return" [ Expression ]

# 4.4 Declarations
Declaration ::= [ "ref" ] TypeReference IDENTIFIER [ "=" Expression ]

# 4.5 Assignment
Assignment ::= GetReference "=" Expression
             | GetReference BinaryOp "=" Expression

# 4.6 Inline Assembly Block
InlineAssembly ::= "asm" "{" { InlineAssemblyExpr } "}"

# 5. Expressions
Expression ::= Binary
             | Unary
             | GetReference
             | Call
             | IsExpr
             | AsExpr
             | HeapAlloc
             | Literal
             | Keyword

# 5.1 Arithmetic Expressions
Binary ::= Expression BinaryOp Expression
Unary ::= UnaryOp Expression

# 5.2 GetReference
GetReference ::= IDENTIFIER | New { "." IDENTIFIER | Call }

# 5.3 Functions
Call ::= GetReference "(" [ ArgumentList ] ")"
New ::= "new" GetReference "(" [ ArgumentList ] ")"
ArgumentList ::= Expression { "," Expression }

# 5.4 Type-Related Expressions
TypeReference ::= IDENTIFIER { "." IDENTIFIER }
IsExpr ::= Expression "is" TypeReference
AsExpr ::= Expression "as" TypeReference

# 5.5 Heap Allocation
HeapAlloc ::= "heapalloc" "(" Expression ")"

# 5.6 Keywords
Keyword ::= "true"
          | "false"
          | "null"
          | "break"
          | "continue"

# 5.7 Literals
Literal ::= INTEGER
          | FLOATING
          | STRING
          | CHAR

# 6. Operators
BinaryOp ::= "+" | "-" | "*" | "/" | "%"
           | "==" | "!=" | "<" | "<=" | ">" | ">="
           | "&&" | "||"
           | "&" | "|" | "^" | "<<" | ">>"

UnaryOp ::= "+" | "-" | "!" | "~"
