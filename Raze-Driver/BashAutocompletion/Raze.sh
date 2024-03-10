declare -A commands=( 
    ["compile"] = "-e --example1 -2 --example2"
    ["run"] = "-r --runExample1 -2 --runExample2"
)
defualt="compile run -h --help -v --version"

get_options()
{
    if [[ "${#COMP_WORDS[@]}" -le 2 ]]; then
        eval "$1='$defualt'"
        return
    fi

    local command="${COMP_WORDS[1]}"

    if [[ -n "${commands[$command]+1}"  ]]; then
        eval "$1='${commands[$command]}'"
    else

        eval "$1='$defualt'"
    fi
}

_raze()
{
    get_options opts

    local cur prev
    COMPREPLY=()
    cur="${COMP_WORDS[COMP_CWORD]}"
    prev="${COMP_WORDS[COMP_CWORD-1]}"

    COMPREPLY=( $(compgen -W "${opts}" -- ${cur}) )
    return 0

}
complete -F _raze Raze