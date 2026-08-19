#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    FARPROC pOrd30 = GetProcAddress(hWs2, (LPCSTR)30);
    FARPROC pOrd4 = GetProcAddress(hWs2, (LPCSTR)4);

    BYTE* code30 = (BYTE*)pOrd30;
    printf("ord30 bytes: ");
    for(int i=0; i<16; i++) printf("%02X ", code30[i]);
    printf("\n");

    BYTE* code4 = (BYTE*)pOrd4;
    printf("ord4 bytes: ");
    for(int i=0; i<16; i++) printf("%02X ", code4[i]);
    printf("\n");

    return 0;
}
