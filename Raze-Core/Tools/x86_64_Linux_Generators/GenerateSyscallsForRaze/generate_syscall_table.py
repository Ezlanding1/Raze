import os
import sys
import re
import json

RESOURCES_DIR = sys.argv[1]
INPUT_FILE = sys.argv[2]

with open(os.path.join(RESOURCES_DIR, 'types.json')) as types:
    typeConversionTable = json.load(types)

def format_arguments(arguments: list[str]):
    typeInfo = [(8,)]
    formattedArgs = []

    if len(arguments) == 1 and arguments[0] == 'void':
        return (typeInfo, '')

    for i in range(len(arguments)):
        argument = arguments[i]

        *type, name = argument.split()

        if not type:
            type = [name]
            name = "parameter" + str(i+1)

        type = ' '.join(type).removesuffix(' __user')

        if name[0] == '*':
            type += ' *'
            name = name[1:]

        if type not in typeConversionTable:
            print("WARNING: Type '" + type + "' not found. Skipping...")
            return ([], None)
        
        argTypeInfo = typeConversionTable[type]
        typeInfo.append(argTypeInfo[1:])
        formattedArgs.append(f'{argTypeInfo[0]} {name}')

    return (typeInfo, ', '.join(formattedArgs))

argRegs = [ 
    ('rax', 'eax', 'ax', 'al', 'ah'),
    ('rdi', 'edi', 'di', 'dil'), 
    ('rsi', 'esi', 'si', 'sil'),
    ('rdx', 'edx', 'dx', 'dl', 'dh'),
    ('r10', 'r10d', 'r10w', 'r10b'),
    ('r8', 'r8d', 'r8w', 'r8b'), 
    ('r9', 'r9d', 'r9w', 'r9b')
]
bytesToIndex = { 
    0 : 4, 
    1 : 3, 
    2 : 2, 
    4 : 1, 
    8 : 0
}

def get_inline_asm_instructions(typeInfo, args, function_return_type):
    return_type_name = ''
    result = ''
    for arg in enumerate(args):

        if arg[1] == 'void':
            continue
        
        idx = bytesToIndex[typeInfo[arg[0]][0]]
        register = argRegs[arg[0]][idx].upper()

        argName = arg[1].split()[-1]

        if argName[0] == '*':
            argName = argName[1:]

        if not argName[0].isnumeric():
            argName = '$' + argName
        result += f'\t\tMOV {register}, {argName};\n'
    result += '\t\tSYSCALL;\n'

    if function_return_type != 'void':
        if function_return_type not in typeConversionTable:
            print("WARNING: Return type '" + function_return_type + "' not found. Skipping...")
        else:
            argTypeInfo = typeConversionTable[function_return_type]
            register = argRegs[0][bytesToIndex[argTypeInfo[1]]].upper()
            result += f'\t\treturn {register};\n'
            return_type_name = argTypeInfo[0] + ' '

    return (return_type_name, result)


lastNum = '0'

def generate_syscall_function(syscall: list[str]) -> str:
    global lastNum

    syscall = syscall[:-1]

    if len(syscall) == 1:
        print("WARNING: No information on syscall #" + syscall[0] + ". Skipping...")
        return ''
    
    if not syscall[0].split()[0].isnumeric():
        syscall.insert(0, lastNum)
    else:
        lastNum = syscall[0].split()[0]

    _, function_return_type, function_name = syscall[1].split()
    function_name = function_name.upper()
    
    typeInfo, function_arguments = format_arguments(syscall[2:])

    if function_arguments == None:
        return ''

    return_type_name, function_instructions = get_inline_asm_instructions(typeInfo, [syscall[0]] + syscall[2:], function_return_type)

    return ( ''
        f'function unsafe static {return_type_name}{function_name}({function_arguments})\n'
        f'{{\n'
        f'\tasm {{\n'
        f'{function_instructions}'
        f'\t}}\n'
        f'}}\n\n'
    )


def get_class_fields(fields: list[str]) -> list[str]:
    result = []

    for field in fields:
        field_type, field_name = field.split(' ')

        if field_type not in typeConversionTable:
            print("WARNING: Type '" + field_type + "' not found. Skipping...")
            return ''
        
        raze_name, size = typeConversionTable[field_type]

        result.append(f'{raze_name} {field_name}')

    return result

def generate_class_definition(name: str, fields: list[str]) -> str:
    name = name.capitalize()
    class_fields = get_class_fields(fields)
    class_fields_decls = ''.join([ ('\t' + x + ';\n') for x in class_fields ])
    class_fields_ctor_params = ', '.join(class_fields)
    class_fields_ctor_asn = ''.join([ ('\t\tthis.' + x.split(' ')[1] + ' = ' + x.split(' ')[1] + ';\n') for x in class_fields ])

    return ( ''
        f'class {name}\n'
        f'{{\n'
        f'{class_fields_decls}\n'
        f'\tfunction {name}({class_fields_ctor_params})\n'
        f'\t{{\n'
        f'{class_fields_ctor_asn}'
        f'\t}}\n'
        f'}}\n\n'
    )


with open(INPUT_FILE) as fd, open('output.rz', 'w') as output:
    with open(os.path.join(RESOURCES_DIR, 'class_definitions.json')) as class_defs_fd:
        class_defs = json.load(class_defs_fd)
        for name, fields in class_defs.items():
            output.write(generate_class_definition(name, fields))

    for line in fd.readlines():
        syscall = [x.strip() for x in re.split(r'\t|,|\(|\)|\]\]', line)]
        output.write(generate_syscall_function(syscall))
