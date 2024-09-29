import table_types


def opcode_size(opcode):
    return len(opcode.split())

operand_sizes = {
    'IMM8' : 1,
    'IMM16' : 2,
    'IMM32' : 4,
    'IMM64' : 8,

    'MOFFS8' : 8,
    'MOFFS16' : 8,
    'MOFFS32' : 8,
    'MOFFS64' : 8,
}

def operand_size(instruction: str) -> int:
    _, *operands = instruction.split(' ', maxsplit=1)

    if operands:
        return sum([operand_sizes[operand] for operand in operands[0].split(', ') if operand in operand_sizes])
    return 0
        

encodingtype_sizes = {
    'RexPrefix' : 1,
    'RexWPrefix' : 1,
    'SizePrefix' : 1,
    'NoModRegRM' : -1,
    'SignExtends' : 0,
    'ZeroExtends' : 0,
    'AddRegisterToOpCode' : 0,
    'RelativeJump' : 0,
    'NoUpper8BitEncoding' : 0,
}

def encodingType_size(encodingType: str) -> int:
    return sum(encodingtype_sizes[x] for x in encodingType.split(' | ')) if encodingType != None else 0

def get_instruction_size(instruction: table_types.instruction_table_entry) -> int:
    return opcode_size(instruction['OpCode']) + operand_size(instruction['Instruction']) + encodingType_size(instruction.get('EncodingType'))

def sort_instructions(instructions: table_types.instruction_table):
    return sorted(instructions, key=get_instruction_size)
