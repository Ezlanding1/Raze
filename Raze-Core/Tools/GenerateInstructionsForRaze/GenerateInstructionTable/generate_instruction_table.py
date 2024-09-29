import sys
from typing import List, Dict
from tabula import read_pdf 
from tabulate import tabulate 
import pandas as pd
from pandas import DataFrame, Series
import csv
import re


if len(sys.argv) != 2:
    print('Usage: generateInstructions.py [Intel® 64 and IA-32 Architectures Software Developer\'s Manual Combined Volumes 2A, 2B, 2C, and 2D: Instruction Set Reference, A-Z]')
    exit(65)

def to_header(raw_header: str) -> str:
    return re.sub('[^a-zA-Z]+', '', raw_header).lower()

file = sys.argv[1]
PAGE_BEGIN = 118
PAGE_END = 2341
TABLE_HEADER_0_START='Opcode'
pd.set_option('display.max_colwidth', None)
EXPORT_DESCRIPTIONS=False
PARSE_FLAGS=True
HEADERS = ['Opcode', 'Instruction', 'Op/En', '64/32-Bit Mode Support', 'CPUID Feature Flag', 'Flags'] + (['Description'] if EXPORT_DESCRIPTIONS else [])
FORMATTED_HEADERS = [to_header(header) for header in HEADERS]
NO_DATA=''
OUTPUT_FILENAME = 'instructions.tsv'

df: DataFrame = read_pdf(file, pages=f'{PAGE_BEGIN}-{PAGE_END}', lattice=True)

to_flags = {
    x : {} for x in FORMATTED_HEADERS
}
to_flags['instruction'] = [
    ('8*', 'NoUpper8BitEncoding', '8'),
    ('81', 'NoUpper8BitEncoding', '8'),
    ('8**', 'NoUpper8BitEncoding', '8'),
    ('r32a', '', 'r32'),
    ('r32b', '', 'r32'),
    ('r64a', '', 'r64'),
    ('r64b', '', 'r32'),
    ('Sreg2', '', 'Sreg'),
    ('r/m82', '', 'r/m8'),
    ('r/m162', '', 'r/m16'),
    ('r/m642', '', 'r/m64'),
    ('moffs83', '', 'moffs8'),
    ('moffs163', '', 'moffs16'),
    ('moffs323', '', 'moffs32'),
    ('moffs643', '', 'moffs64'),
    ('r16/r32/r64', '', 'reg'),
    (' mm1/m', '', ' mm/m'),
    (' mm2/m', '', ' mm/m'),
    (' mm3/m', '', ' mm/m'),
    (' mm1', '', ' mm'),
    (' mm2', '', ' mm'),
    (' mm3', '', ' mm'),
    ('xmm1/m', '', 'xmm/m'),
    ('xmm2/m', '', 'xmm/m'),
    ('xmm3/m', '', 'xmm/m'),
    ('xmm1', '', 'xmm'),
    ('xmm2', '', 'xmm'),
    ('xmm3', '', 'xmm'),
    ('ymm1/m', '', 'ymm/m'),
    ('ymm2/m', '', 'ymm/m'),
    ('ymm3/m', '', 'ymm/m'),
    ('ymm1', '', 'ymm'),
    ('ymm2', '', 'ymm'),
    ('ymm3', '', 'ymm'),
    ('zmm1/m', '', 'zmm/m'),
    ('zmm2/m', '', 'zmm/m'),
    ('zmm3/m', '', 'zmm/m'),
    ('zmm1', '', 'zmm'),
    ('zmm2', '', 'zmm'),
    ('zmm3', '',' zmm')
]
to_flags['opcode'] = [
    ('REX + ', '', 'REX '),
    ('REX.W + ', '', 'REX.W '),
    ('REX.w ', '', 'REX.W '),
    ('/r1', '', '/r'),
    ('/r2', '', '/r'),
    ('+ rb', '' , '+rb'), 
    ('+ rw', '' , '+rw'), 
    ('+ rd', '' , '+rd'), 
    ('+ ro', '' , '+ro'),
    (' +rb', '' , '+rb'), 
    (' +rw', '' , '+rw'), 
    (' +rd', '' , '+rd'), 
    (' +ro', '' , '+ro'),
    ('/05', '', '/5'),
    ('01/7', '', '01 /7'),
    ('0F3A', '', '0F 3A'),
    ('0F38', '', '0F 38'),
    ('ib1', '', 'ib'),
    ('13/r', '', '13 /r')
]
to_flags['description'] = [
    ('sign extend', 'SignExtends', 'sign extend'),
    ('sign-extend', 'SignExtends', 'sign-extend'),
    ('signextend', 'SignExtends', 'signextend'),
    ('sign-extension', 'SignExtends', 'sign-extension'),
    ('sign- extension', 'SignExtends', 'sign-extension'),
    ('zero extend', 'ZeroExtends', 'zero extend'),
    ('zero-extend', 'ZeroExtends', 'zero-extend'),
    ('zeroextend', 'ZeroExtends', 'zeroextend'),
    ('zero-extension', 'ZeroExtends', 'zero-extension'),
    ('zero- extension', 'ZeroExtends', 'zero-extension'),
    ('Jump far', 'FarJump', 'Jump far')
]

def format_bitmode_val(value):
    return {
        'vv' : 'Valid/Valid',
        'v' : 'Valid',
        'i' : 'Invalid',
        'invalid' : 'Invalid',
        'inv' : 'Invalid',
        'valid' : 'Valid',
        'ne' : 'N.E.',
        'np' : 'N.P.',
        'ni' : 'N.I.',
        'ns' : 'N.S.',
        '' : ''
    }[to_header(value)]

def add_to_instructions(instructions: List[Dict[str, str]], idx: int, header: str, value: str):
    
    if header == 'instruction':
        value, *operands = value.split(' ', maxsplit=1)
        if operands:
            value += ' ' + ', '.join([x.strip() for x in operands[0].split(',')])
    if header == 'bitmodesupport':
        value = '/'.join([format_bitmode_val(x) for x in value.split('/')])

    if PARSE_FLAGS:
        for k, v, r in to_flags[header]:
            if k in value and v not in instructions[idx]['flags']:
                instructions[idx]['flags'] += (v + ', ') if v else ''
            value = value.replace(k, r)

    if not EXPORT_DESCRIPTIONS and header == 'description':
        return
    
    instructions[idx][header] = value
    
aliases = { 'cpuid' : 'cpuidfeatureflag', 'bitmode' : 'bitmodesupport' }

def add_item(instructions: List[Dict[str, str]], idx: int, item: List[str], header: str, series: Series):

    value = ' '.join(item)

    if header in aliases:
        header = aliases[header]
    
    if header in FORMATTED_HEADERS:
        if header == 'bitmodesupport' and value == 'VV':
            value = 'V/V'
            
        add_to_instructions(instructions, idx, header, value)
        return
    elif header == 'opcodeinstruction':
        dataIdx = str(series.astype(str)).split('\n')[idx].lstrip('0123456789').strip().find('\\r')
        data = [value[:dataIdx], value[dataIdx+1:]]
        
        add_to_instructions(instructions, idx, 'opcode', data[0])
        add_to_instructions(instructions, idx, 'instruction', data[1])
        return
    elif header == 'modesupport':
        instructions[idx]['bitmodesupport'] = value
        return
    elif header == 'compatlegmode':
        if value != 'N/A':
            add_to_instructions(instructions, idx, 'bitmodesupport', instructions[idx]['bitmodesupport'] + '/' + value)
        return
    elif header == 'description':
        add_to_instructions(instructions, idx, header, value)
        return
    elif header == 'unnamed':
        return
    
    print(series.astype(str).str.split())
    raise Exception("Unrecognized header type: " + header)

def get_instructions_from_table(table: DataFrame) -> List[Dict[str, str]]: 
    instructions = []

    for row in table:
        series = table[row]
        header = to_header(row)
        idx = 0
        for item in series.astype(str).str.split():
            if ''.join(item).lower() in ['notes:', 'nan']:
                continue
            if len(instructions) <= idx:
                instructions.append({x : NO_DATA for x in FORMATTED_HEADERS})

            add_item(instructions, idx, item, header, series)
            idx += 1

    if PARSE_FLAGS:   
        for instruction in instructions:
            instruction['flags'] = instruction['flags'][:-2]
            
    return instructions

with open(OUTPUT_FILENAME, 'w', newline='', encoding="utf-8") as output:
    writer = csv.DictWriter(output, fieldnames=FORMATTED_HEADERS, delimiter='\t')
    writer.writerow({ to_header(x) : x for x in HEADERS })

    for i in range(len(df)):
        table = df[i]
        if not table.empty and list(table)[0].strip().startswith(TABLE_HEADER_0_START):
            writer.writerows(get_instructions_from_table(table))

with open(OUTPUT_FILENAME, 'r', encoding="utf-8") as inp:
    print(tabulate(csv.reader(inp, delimiter='\t'), tablefmt="grid"))
