import sys
import csv
import json
from parse_instruction import parse_instruction, Result
from sort_instructions import sort_instructions
import table_types


table = sys.argv[1]

instructions: table_types.instruction_table = {}
result: Result = Result()

with open(table) as fd:
    reader = csv.reader(fd, delimiter='\t')
    next(reader)

    for row in reader:
        result.total += 1

        opcode, instruction, operandEncoding, bit_mode, featureFlag, flags = row

        # Blacklist - unsupported features
        if any([x in featureFlag for x in [ 'AVX512F', 'AVX512PF', 'AVX512VL', 'AVX', 'AVX2', 'AVX512DQ', 'FMA', 'WAITPKG' ]]):
            continue

        if bit_mode.split('/')[0] != 'Valid':
            continue
        
        parse_instruction(instructions, opcode, instruction, operandEncoding, flags, result)

    for instruction in instructions:
        instructions[instruction] = sort_instructions(instructions[instruction]) 


print(json.dumps(instructions))
result.print_stats(instructions)
