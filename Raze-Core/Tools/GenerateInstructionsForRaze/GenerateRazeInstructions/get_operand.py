from parse_exception import ParseException


class Operand:
    def __init__(self, name: str, size: int, displaySize: bool = True) -> None:
        self.name = name
        self.size = size
        self.displaySize = displaySize
    
    def __str__(self) -> str:
        return self.name + (str(self.size) if self.displaySize else '')

argTable = {
    "r" : "R",
    "m" : "M",
    "imm" : "IMM",
    "r/m" : "RM",
    'xmm/m' : 'XMMRM',
    'moffs' : 'MOFFS',
    'mm/m' : 'MMXRM',
    'regna' : 'RNA',
}

regSize = { 
    'xmm' : ('XMM', 16),
    'mm' : ('MMX', 8),
    'AL' : ('AL', 1),
    'AX' : ('AX', 2),
    'EAX' : ('EAX', 4),
    'RAX' : ('RAX', 8),
    'DL' : ('DL', 1),
    'DX' : ('DX', 2),
    'EDX' : ('EDX', 4),
    'RDX' : ('RDX', 8),
    'CL' : ('CL', 1),
    'CX' : ('CX', 2),
    'ECX' : ('ECX', 4),
    'RCX' : ('RCX', 8),
    'creg' : ('CR', 8),
    'dreg' : ('DR', 8),
    'treg' : ('TR', 8),
    'Sreg' : ('SEG', 2),
    'CS' : ('CS', 2),
    'FS' : ('FS', 2), 
    'GS' : ('GS', 2),
    'mm' : ('MMX', 8),
}

def reg_to_operand(reg: str) -> Operand:
    name, size = regSize[reg]
    return Operand(name, size * 8, False)


def get_operand(arg: str, encodingType: set[str]) -> Operand:
    if arg.endswith('int'):
        arg = arg[:-3]
    elif arg.endswith('fp'):
        arg = arg[:-2]

    argType = arg.rstrip('1234567890')
    argSize = int(arg[len(argType):]) if len(argType) != len(arg) else -1 
    
    if argType == 'rel':
        encodingType.add('RelativeJump')
        argType = 'imm'

    if argType == '' and argSize == 1:
        return Operand('1', '1', False)
    
    if argSize == -1:
        if argType == 'reg':
            return Operand(argTable['r'], -2)
        if argType == 'm':
            return Operand(argTable['m'], -2)
        else:
            return reg_to_operand(argType)
    
    return Operand(argTable[argType], argSize)
