#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hMod = LoadLibraryExA("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\GameBase.dll", NULL, DONT_RESOLVE_DLL_REFERENCES);
    if (!hMod) {
        printf("Failed to load GameBase.dll: %lu\n", GetLastError());
        return 1;
    }

    PIMAGE_DOS_HEADER pDos = (PIMAGE_DOS_HEADER)hMod;
    PIMAGE_NT_HEADERS pNt = (PIMAGE_NT_HEADERS)((BYTE*)hMod + pDos->e_lfanew);
    IMAGE_DATA_DIRECTORY importDir = pNt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];

    PIMAGE_IMPORT_DESCRIPTOR pImport = (PIMAGE_IMPORT_DESCRIPTOR)((BYTE*)hMod + importDir.VirtualAddress);
    for (; pImport->Name != 0; pImport++) {
        const char* dllName = (const char*)((BYTE*)hMod + pImport->Name);
        printf("DLL: %s\n", dllName);
        if (_strnicmp(dllName, "ws", 2) == 0) {
            PIMAGE_THUNK_DATA pOrigThunk = (PIMAGE_THUNK_DATA)((BYTE*)hMod + (pImport->OriginalFirstThunk ? pImport->OriginalFirstThunk : pImport->FirstThunk));
            for (; pOrigThunk->u1.AddressOfData != 0; pOrigThunk++) {
                if (IMAGE_SNAP_BY_ORDINAL(pOrigThunk->u1.Ordinal)) {
                    printf("  Ordinal: %llu\n", IMAGE_ORDINAL(pOrigThunk->u1.Ordinal));
                } else {
                    PIMAGE_IMPORT_BY_NAME pName = (PIMAGE_IMPORT_BY_NAME)((BYTE*)hMod + pOrigThunk->u1.AddressOfData);
                    printf("  Name: %s (hint=%u)\n", pName->Name, pName->Hint);
                }
            }
        }
    }
    return 0;
}
