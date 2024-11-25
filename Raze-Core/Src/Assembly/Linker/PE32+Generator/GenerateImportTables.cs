using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Raze;

public partial class Linker
{
    internal static partial class PE32PlusGenerator
    {
        public static (PE32Plus.IMAGE_IMPORT_DESCRIPTOR[], PE32Plus.IMPORT_LOOKUP[], byte[]) GenerateImportTables(Assembler assembler, SystemInfo systemInfo, uint currentSectionRva)
        {
            if (assembler.symbolTable.includes.Count == 0)
                return ([], [], []);

            List<PE32Plus.IMAGE_IMPORT_DESCRIPTOR> importDescriptorTable = [];
            List<PE32Plus.IMPORT_LOOKUP> importLookupTable = [];
            List<byte> imageImportByName = [];

            currentSectionRva = PeUtils.GetIDataSectionRVA(assembler, systemInfo);

            var groupedIncludes = PeUtils.GroupIncludesByDllName(assembler.symbolTable);

            (uint iltTablesLocation, uint nameTablesLocation, uint iatTablesLocation) = 
                PeUtils.GetIDataSectionTablesLocations(assembler.symbolTable);

            uint currentIltTableOffset = 0;
            uint currentNameTableOffset = 0;
            uint currentIatOffset = 0;

            foreach (var includeGroup in groupedIncludes)
            {
                var lastIltTableOffset = currentIltTableOffset;

                foreach (var include in includeGroup)
                {
                    importLookupTable.Add(
                        new PE32Plus.IMPORT_LOOKUP(
                            // RVA of Name table (RVA to corresponding PE32Plus.IMAGE_IMPORT_BY_NAME in the name table, which has the function's Hint/Name)
                            currentSectionRva + nameTablesLocation + currentNameTableOffset
                        )
                    );

                    // Currently hint value is always set to '0'. In the future, add a compiler option to resolve a hint value from a local dll
                    imageImportByName.AddRange(ToReadOnlySpan(new PE32Plus.IMAGE_IMPORT_BY_NAME(0)).ToArray());
                    imageImportByName.AddRange(Encoding.ASCII.GetBytes(include.importedFunctionName + "\0"));

                    currentNameTableOffset += (uint)(Marshal.SizeOf<PE32Plus.IMAGE_IMPORT_BY_NAME>() + include.importedFunctionName.Length + 1);
                }
                importLookupTable.Add(new());
                currentIltTableOffset += (uint)(Marshal.SizeOf<PE32Plus.IMPORT_LOOKUP>() * (includeGroup.Count() + 1));


                importDescriptorTable.Add(
                    new PE32Plus.IMAGE_IMPORT_DESCRIPTOR(
                        // RVA of ILT table
                        currentSectionRva + iltTablesLocation + lastIltTableOffset,
                        0,
                        0,
                        // RVA to the dll's name
                        currentSectionRva + nameTablesLocation + currentNameTableOffset,
                        // RVA to IAT Table
                        currentSectionRva + iatTablesLocation + currentIatOffset
                    )
                );

                currentIatOffset += (uint)(0x8 * (includeGroup.Count() + 1));

                imageImportByName.AddRange(Encoding.ASCII.GetBytes(includeGroup.Key + "\0"));
                currentNameTableOffset += (uint)(includeGroup.Key.Length + 1);
            }
            importDescriptorTable.Add(new());

            return (importDescriptorTable.ToArray(), importLookupTable.ToArray(), imageImportByName.ToArray());
        }
    }
}
