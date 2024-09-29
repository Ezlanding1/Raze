import re
from parse_exception import ParseException


HEX_DIGIT=r'[0-9a-fA-F]'

def get_opcode_extension(opcode: str) -> int:
    opCodeExt = -1
    if m := re.search('/([0-7])', opcode):
        opCodeExt = int(m.group(1))

    return opCodeExt

def get_opcode(opcode: str, encodingType: set[str]) -> str:
    result = ''
    
    for opcode_byte in opcode.split():
        if opcode_byte in ['cb', 'cw', 'cd', 'cp', 'co', 'ct', 'ib', 'iw', 'id', 'io', '/r']:
            continue
        elif re.match(HEX_DIGIT + '{2}', opcode_byte):
            if len(opcode_byte) == 2:
                result += ' ' + opcode_byte.upper()
            elif opcode_byte.endswith(('+rb', '+rw', '+rd', '+ro')):
                encodingType.add('AddRegisterToOpCode')
                encodingType.add('NoModRegRM')
                result += ' ' + opcode_byte[:-3].upper()
            else:
                raise ParseException("Unrecognized opcode-byte: " + opcode_byte)                
        elif re.match('/' + HEX_DIGIT + '$', opcode_byte):
            continue
        elif opcode_byte.startswith('REX'):
            if opcode_byte.endswith('.W'):
                encodingType.add('RexWPrefix')
            elif opcode_byte.endswith('.R'):
                encodingType.add('RexRPrefix')
            else:
                encodingType.add('RexPrefix')
        elif opcode_byte in [ 'NP', 'NFx' ]:
            pass
        else:
            raise ParseException("Unrecognized opcode-byte: " + opcode_byte)
        
    return result[1:]
