#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <stdio.h>
#include "hde64.h"
#include "table64.h"

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    FARPROC pOrd30 = GetProcAddress(hWs2, (LPCSTR)30);
    FARPROC pConn = GetProcAddress(hWs2, "connect");

    printf("Disassembling ord30:\n");
    BYTE* code = (BYTE*)pOrd30;
    size_t len = 0;
    while (len < 32) {
        hde64s hs;
        hde64_disasm(code + len, &hs);
        printf("offset +%zu (len=%u, flags=0x%x): ", len, hs.len, hs.flags);
        for(int i=0; i<hs.len; i++) printf("%02X ", code[len + i]);
        printf("\n");
        if (hs.flags & F_ERROR) { printf("ERROR\n"); break; }
        len += hs.len;
    }

    printf("Disassembling connect:\n");
    code = (BYTE*)pConn;
    len = 0;
    while (len < 32) {
        hde64s hs;
        hde64_disasm(code + len, &hs);
        printf("offset +%zu (len=%u, flags=0x%x): ", len, hs.len, hs.flags);
        for(int i=0; i<hs.len; i++) printf("%02X ", code[len + i]);
        printf("\n");
        if (hs.flags & F_ERROR) { printf("ERROR\n"); break; }
        len += hs.len;
    }
    return 0;
}
