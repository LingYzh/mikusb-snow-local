#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <string.h>

static void DumpImports(const char* path) {
    HMODULE hMod = LoadLibraryExA(path, NULL, DONT_RESOLVE_DLL_REFERENCES);
    if (!hMod) {
        printf("load fail %s %lu\n", path, GetLastError());
        return;
    }
    PIMAGE_DOS_HEADER pDos = (PIMAGE_DOS_HEADER)hMod;
    PIMAGE_NT_HEADERS pNt = (PIMAGE_NT_HEADERS)((BYTE*)hMod + pDos->e_lfanew);
    printf("==== %s imports ====\n", path);

    IMAGE_DATA_DIRECTORY importDir = pNt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (importDir.VirtualAddress) {
        PIMAGE_IMPORT_DESCRIPTOR pImport = (PIMAGE_IMPORT_DESCRIPTOR)((BYTE*)hMod + importDir.VirtualAddress);
        for (; pImport->Name != 0; pImport++) {
            const char* dllName = (const char*)((BYTE*)hMod + pImport->Name);
            printf("IAT %s\n", dllName);
            if (_strnicmp(dllName, "ws", 2) == 0 || _stricmp(dllName, "mswsock.dll") == 0 || _stricmp(dllName, "ntdll.dll") == 0) {
                PIMAGE_THUNK_DATA pOrigThunk = (PIMAGE_THUNK_DATA)((BYTE*)hMod + (pImport->OriginalFirstThunk ? pImport->OriginalFirstThunk : pImport->FirstThunk));
                for (; pOrigThunk->u1.AddressOfData != 0; pOrigThunk++) {
                    if (IMAGE_SNAP_BY_ORDINAL(pOrigThunk->u1.Ordinal))
                        printf("  ord %llu\n", IMAGE_ORDINAL(pOrigThunk->u1.Ordinal));
                    else {
                        PIMAGE_IMPORT_BY_NAME pName = (PIMAGE_IMPORT_BY_NAME)((BYTE*)hMod + pOrigThunk->u1.AddressOfData);
                        printf("  %s\n", pName->Name);
                    }
                }
            }
        }
    }

    IMAGE_DATA_DIRECTORY delayDir = pNt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT];
    if (delayDir.VirtualAddress) {
        PIMAGE_DELAYLOAD_DESCRIPTOR pDelay = (PIMAGE_DELAYLOAD_DESCRIPTOR)((BYTE*)hMod + delayDir.VirtualAddress);
        for (; pDelay->DllNameRVA != 0; pDelay++) {
            const char* dllName = (const char*)((BYTE*)hMod + pDelay->DllNameRVA);
            printf("DELAY %s\n", dllName);
            if (_strnicmp(dllName, "ws", 2) == 0 || _stricmp(dllName, "mswsock.dll") == 0) {
                PIMAGE_THUNK_DATA pOrigThunk = (PIMAGE_THUNK_DATA)((BYTE*)hMod + pDelay->ImportNameTableRVA);
                for (; pOrigThunk->u1.AddressOfData != 0; pOrigThunk++) {
                    if (IMAGE_SNAP_BY_ORDINAL(pOrigThunk->u1.Ordinal))
                        printf("  ord %llu\n", IMAGE_ORDINAL(pOrigThunk->u1.Ordinal));
                    else {
                        PIMAGE_IMPORT_BY_NAME pName = (PIMAGE_IMPORT_BY_NAME)((BYTE*)hMod + pOrigThunk->u1.AddressOfData);
                        printf("  %s\n", pName->Name);
                    }
                }
            }
        }
    } else {
        printf("(no delay imports)\n");
    }
}

int main() {
    DumpImports("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\Game.exe");
    DumpImports("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\GameBase.dll");
    return 0;
}
