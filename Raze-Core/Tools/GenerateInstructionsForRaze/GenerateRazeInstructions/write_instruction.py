import expand_unsized_operand as eU 
from get_operand import Operand
import table_types


def write_instruction(
        instructions: table_types.instruction_table, 
        instruction_name: str, 
        operands: list[Operand],
        opcode: str, 
        encodingType: str, 
        opCodeExt: int
    ) -> None:
    
    if eU.expand_unsized_operand(instructions, instruction_name, operands, opcode, encodingType, opCodeExt):
        return
    else:
        _write_instruction(
            instructions, 
            instruction_name,
            operands,
            opcode,
            encodingType,
            opCodeExt
        )

def _write_instruction(
        instructions: table_types.instruction_table, 
        instruction_name: str, 
        operands: list[Operand], 
        opcode: str, 
        encodingType: str, 
        opCodeExt: int
    ) -> None:
    
    if instruction_name not in instructions:
        instructions[instruction_name] = []

    instructions[instruction_name].append({
        'Instruction': instruction_name + (' ' if operands else '') + ', '.join([str(x) for x in operands]),
        'OpCode': opcode
    })

    if encodingType:
        instructions[instruction_name][-1]['EncodingType'] = encodingType
        
    if opCodeExt != -1:
        instructions[instruction_name][-1]['OpCodeExtension'] = opCodeExt
