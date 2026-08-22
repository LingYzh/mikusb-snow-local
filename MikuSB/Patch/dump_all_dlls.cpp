#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hMod = LoadLibraryExA("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\Game.exe", NULL, DONT_RESOLVE_DLL_REFERENCES);
    if (!hMod) return 1;

    PIMAGE_DOS_HEADER pDos = (PIMAGE_DOS_HEADER)hMod;
    PIMAGE_NT_HEADERS pNt = (PIMAGE_NT_HEADERS)((BYTE*)hMod + pDos->e_lfanew);
    IMAGE_DATA_DIRECTORY importDir = pNt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];

    PIMAGE_IMPORT_DESCRIPTOR pImport = (PIMAGE_IMPORT_DESCRIPTOR)((BYTE*)hMod + importDir.VirtualAddress);
    for (; pImport->Name != 0; pImport++) {
        const char* dllName = (const char*)((BYTE*)hMod + pImport->Name);
        printf("DLL: %s\n", dllName);
    }
    return 0;
}
