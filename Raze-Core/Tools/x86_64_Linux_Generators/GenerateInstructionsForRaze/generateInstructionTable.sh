INTEL_SDM_VOL2_ABCD_URL="https://cdrdv2-public.intel.com/825760/325383-sdm-vol-2abcd.pdf"

OUTPUT_DIR=$(pwd)

dir=$(mktemp -d)
cd $dir

echo "Installing latest version of Intel SDM Vol 2..."
curl -s $INTEL_SDM_VOL2_ABCD_URL > "sdm.pdf"
echo "Done"

# Dependencies:
#   Python:
#       python3.10+, tabula-py, tabula-py[jpype], tabulate
#   Java:
#       default-jre, openjdk-11-jre-headless, openjdk-8-jre-headless


echo "Scraping SDM PDF. This may take some time..."
python3 "$OUTPUT_DIR/GenerateInstructionTable/generate_instruction_table.py" "./sdm.pdf" > /dev/null
echo "Done"

echo "Correcting instructions..."
"$OUTPUT_DIR/GenerateInstructionTable/corrections.sh"
echo "Done"

echo "Parsing instructions..."
instructions=$(python3.10 "$OUTPUT_DIR/GenerateRazeInstructions/parse_instruction_table.py" "instructions.tsv" 2> /dev/null) 
echo "Done"

echo "Validating..."
a=("ADD,SUB,MOV,CALL,PUSH,LEA,CMP,SETE,JMP,JE,JNE,TEST,SYSCALL,SETNE,SETG,SETL,SETGE,SETLE,SETA,SETAE,SETB,SETBE,JG,JL,JGE,JLE,JA,JAE,JB,JBE,INC,DEC,NEG,SHR,SHL,IDIV,DIV,IMUL,MUL,NOT,OR,AND,XOR,MOVSX,MOVZX,SAL,SAR,RET,POP,LEAVE,CWD,CDQ,CQO,CMOVNZ,MOVSS,ADDSS,CVTTSS2SI,MOVD,MOVQ,CBW,CWDE,CDQE,CVTSS2SD,ADDSD,CVTSD2SI,CVTSD2SS")
IFS=', ' read -r -a array <<< "$a" 
for i in "${array[@]}" 
do 
    lines=$(echo $instructions | grep "\"$i\":" -o | wc -l)
    if [[ "$lines" == "0" ]]; then
        echo "WARNING! Instruction "$i" not parsed!"
    fi
done
echo "Done"

echo $instructions > "$OUTPUT_DIR/output.json"

rm -r $dir 
