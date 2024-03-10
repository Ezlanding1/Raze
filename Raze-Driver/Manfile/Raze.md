% RAZE(1) Version 0.0 | Raze Documentation

# NAME

**Raze** — The Raze programming language compiler

# SYNOPSIS

| **Raze** **compile**|**run** _file_ \[**-o**|**\--output**] \[**-a**|**\--output-assembly**]
| **Raze** \[**-h**|**\--help**|**-v**|**\--version**]

# DESCRIPTION

Raze is a Compiled, Object Oriented, Statically & Strictly typed toy programming language that combines all the intuitive features from languages such as C, C#, and Python into a neat syntax, while also offering access to lower level programming.

# COMMAND LINE OPTIONS

**compile** _file_

: Compiles the given file

    **-o**, \--output="_fileName_"

    :   Outputs the binary with the given filename. Default is "a.out".

    **-a**, \--output-assembly="[intel|at&t]" 

    :   Outputs a corresponding assembly file for the binary in the specified syntax. Defualt is intel syntax.

    **-l**, \--output-linker-objects" 

    :   Outputs the corresponding object file(s) for the binary.

    **-i**, \--no-inline

    :   Forces all calls to not be inlined

    **-d**, \--dry-run

    :   Supresses output of binary file. Will still output any other specified files.


**run** _file_

: Compiles and runs the given file

    **-i**, \--no-inline

    :   Forces all calls to not be inlined
    
    **-d**, \--dry-run

    :   Supresses output of binary file. Will still output any other specified files.



**-h**, \--help

:   Prints usage information. 

**-v**, \--version

:   Prints the current version number.

# EXAMPLES

**Raze** \--version
:   Prints version number of compiler

**Raze** **compile** ./myProgram.rz -o a.out \--output-assembly="at&t"
:   Compiles "myProgram.rz" and outputs the binary as "a.out". Also outputs the relative assembly in AT&T syntax.

# FILES

*~/.raze/config.json*

:   Per-user default configuration file.

*/etc/raze/config.json*

:   Global default configuration file.


# EXIT STATUS

Raze compile _file_

**0** 
:   Successful compilation

**65**
:   User error

**70**
:   Internal error - compiler panic

Raze run _file_

**0-255** 
:   Exit status of input program


# BUGS

See GitHub Issues: <https://github.com/Ezlanding1/Raze/issues>

# AUTHOR

Ezlanding1 <!-- <foo@example.org> -->