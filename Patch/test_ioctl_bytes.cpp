#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    FARPROC pWSAIoctl = GetProcAddress(hWs2, "WSAIoctl");
    FARPROC pWSAIoctlOrd = GetProcAddress(hWs2, (LPCSTR)66);

    printf("WSAIoctl name: %p, ord66: %p\n", pWSAIoctl, pWSAIoctlOrd);
    BYTE* code = (BYTE*)pWSAIoctl;
    printf("WSAIoctl bytes: ");
    for(int i=0; i<16; i++) printf("%02X ", code[i]);
    printf("\n");
    return 0;
}
