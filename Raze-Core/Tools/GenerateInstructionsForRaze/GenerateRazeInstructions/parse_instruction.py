import sys
from get_encodingType import get_encodingType
from get_opcode import get_opcode, get_opcode_extension
from to_raze_args import to_raze_args
from write_instruction import write_instruction
import table_types

class Result:
    def __init__(self) -> None:
        self.total = 0
        self.passed = 0
        self.failed = 0
    
    def print_stats(self, instructions) -> None:
        print(f"\n\n\nRESULT:\nTotal: {self.total}, Passed: {self.passed}, Skipped: {self.total-self.passed-self.failed} Failed: {self.failed}")
        print(f"STATS:\nUnqiue-Instructions: {len(instructions)}, Percent Passed: {(self.passed/(self.passed+self.failed))*100:.2f}%, Percent Parsed: {((self.failed+self.passed)/self.total)*100:.2f}%")
        
        
def parse_instruction(
        instructions: table_types.instruction_table, 
        r_opcode: str, 
        instruction: str, 
        operandEncoding: str, 
        flags: str, 
        result: Result
    ) -> None:

    encodingType: set[str] = set()

    if flags: 
        for flag in flags.split(', '):
            encodingType.add(flag)

    try:
        instruction_name, operands = to_raze_args(instruction, encodingType)
        opcode = get_opcode(r_opcode, encodingType)
        encodingType = get_encodingType(operands, operandEncoding, encodingType)
        opcodeExt = get_opcode_extension(r_opcode)
    except Exception as e:
        print("Instruction not parsed:\n", r_opcode, instruction, flags, "Reason: " + str(e), file=sys.stderr)
        result.failed += 1
        return

    write_instruction(
        instructions, 
        instruction_name,
        operands,
        opcode,
        encodingType,
        opcodeExt
    )
    result.passed += 1
