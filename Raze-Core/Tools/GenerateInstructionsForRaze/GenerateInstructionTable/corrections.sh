INPUT='instructions.tsv'

# Undocumented Instructions:

# Corrections:
sed  -i \
    -e 's/VFPCLASSPHk1/VFPCLASSPH k1/g' \
    -e 's/VREDUCEPHxmm/VREDUCEPH xmm/g' \
    -e 's/VREDUCEPHymm/VREDUCEPH ymm/g' \
    -e 's/VGETEXPPHzmm/VGETEXPPH zmm/g' \
    -e 's/\/ /\//g' \
    -e 's/mm{/mm {/g' \
    -e 's/,x/, x/g' \
    -e 's/,y/, y/g' \
    -e 's/,z/, z/g' \
    $INPUT
