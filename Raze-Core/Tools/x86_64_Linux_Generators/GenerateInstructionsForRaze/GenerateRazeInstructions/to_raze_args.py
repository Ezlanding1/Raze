from parse_exception import ParseException
from get_operand import Operand, get_operand

def to_raze_args(instruction: str, encodingType: set[str]) -> tuple[str, list[Operand]]:
    result: list[Operand] = []
    
    # Note: REP/REPE/REPZ/REPNE/REPNZ opcode flags are not currently supported
    if instruction.startswith(('REP', 'REPE', 'REPZ', 'REPNE', 'REPNZ')):
        instruction = instruction.split(' ', maxsplit=1)[1]

    instruction, *operands = instruction.split(' ', maxsplit=1)
    if operands:
        result = [get_operand(operand, encodingType) for operand in operands[0].split(', ')]
    
    for operand in result:
        match operand.size:
            case -1:
                raise ParseException('Unset size')
            case 16:
                encodingType.add('SizePrefix')
            case size if size > 0 and size not in [8, 16, 32, 64, 128]:
                raise ParseException('Unsupported size: ' + size)

    return (instruction, result)
