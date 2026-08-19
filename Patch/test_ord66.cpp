#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    BYTE* code = (BYTE*)GetProcAddress(hWs2, (LPCSTR)66);
    printf("ord66 bytes: ");
    for(int i=0; i<32; i++) printf("%02X ", code[i]);
    printf("\n");
    return 0;
}
