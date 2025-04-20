SYSCALL_TABLES="https://raw.githubusercontent.com/torvalds/linux/v6.7/arch/x86/entry/syscalls/syscall_64.tbl"
SYSCALL_INTERFACES="https://raw.githubusercontent.com/torvalds/linux/master/include/linux/syscalls.h"

OUTPUT_DIR=$(pwd)

dir=$(mktemp -d)
cd $dir

# Dependencies:
#   Python 3.9+, ripgrep 

echo "Installing latest syscall table version..."
curl -s $SYSCALL_TABLES > syscall_64.tbl
echo "Done"

echo "Installing latest version of syscall interfaces..."
curl -s $SYSCALL_INTERFACES > syscalls.h
echo "Done"

echo "Parsing files..."
MANPAGE_NAMES=$(rg "[0-9]+\s+(common|64)\s+([a-zA-Z_0-9]+)\s+([a-zA-Z_0-9]+)" -or '$3' syscall_64.tbl)
MANPAGE_NUMS=$(rg "([0-9]+)\s+(common|64)\s+([a-zA-Z_0-9]+)\s+[a-zA-Z_0-9]+" -or '$1' syscall_64.tbl)
echo "Done"

names=( $MANPAGE_NAMES )
nums=( $MANPAGE_NUMS )

> input.txt

echo "Scraping syscall interfaces..."

for i in "${!names[@]}"
do
    echo -ne "${nums[$i]}\t" >> input.txt
    echo "${names[$i]}"
    PREFIX="asmlinkage"
    NAME="${names[$i]}"

    case "${names[$i]}" in
        "sys_mmap")
            PREFIX=""
            NAME="ksys_mmap_pgoff"
            ;;

        "sys_rt_sigreturn")
            echo -e "\0" >> input.txt
            continue
            ;;

        "sys_modify_ldt")
            echo -e "asmlinkage long sys_modify_ldt(int func, void *ptr, unsigned long bytecount);\0" >> input.txt
            continue
            ;;
        "sys_arch_prctl")
            echo -e "asmlinkage long sys_arch_prctl(int option, unsigned long arg2);\0" >> input.txt
            continue
            ;;
        "sys_iopl")
            echo -e "asmlinkage long sys_iopl(unsigned int level);\0" >> input.txt
            continue
            ;;
    esac
    
    grep -Pzo "$PREFIX.*$NAME[^a-zA-Z_0-9][\s\S]*?\);" $dir/syscalls.h >> input.txt || echo "WARNING: Syscall #${nums[$i]} '$NAME' not found"

done

tr -d '\n' < input.txt | tr '\0' '\n' > newfile && mv newfile input.txt
sed -i -E "s/,\s+/, /g" input.txt
sed -i -E "s/\(\s+/\(/g" input.txt

cp "$OUTPUT_DIR/class_definitions.json" ./
cp "$OUTPUT_DIR/types.json" ./
python3.10 "$OUTPUT_DIR/generate_syscall_table.py" "$OUTPUT_DIR" "./input.txt" 
cp "./output.rz" "$OUTPUT_DIR/output.rz"

echo "Done"

rm -r $dir 
