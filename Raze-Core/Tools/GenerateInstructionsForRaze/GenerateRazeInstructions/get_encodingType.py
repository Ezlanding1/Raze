from get_operand import Operand


def get_encodingType(operands: list[Operand], operand_encoding: str, encodingType: set[str]) -> str:

    if len(operands) == 2 and operand_encoding == 'I':
        encodingType.add('NoModRegRM')

    return ' | '.join(encodingType)
