from get_operand import Operand
import write_instruction as wI
import table_types


unsized_includes_8bit = [
    'LEA'
]

def expand_unsized_operand(
        instructions: table_types.instruction_table, 
        instruction_name: str, 
        operands: list[Operand], 
        opcode: str, 
        encodingType: set[str], 
        opcodeExt: int
    ):

    unsizedOperands = [operand for operand in operands if operand.size == -2]

    for operand in unsizedOperands:
        for size in ([8] if instruction_name in unsized_includes_8bit else []) + [ 16, 32, 64 ]:
            operand.size = size
            wI.write_instruction(
                instructions, 
                instruction_name,
                operands,
                opcode,
                encodingType,
                opcodeExt
            )

    return unsizedOperands
