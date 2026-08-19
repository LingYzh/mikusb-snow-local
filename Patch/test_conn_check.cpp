#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    FARPROC pConnName = GetProcAddress(hWs2, "connect");
    FARPROC pConnOrd = GetProcAddress(hWs2, (LPCSTR)4);
    FARPROC pWSAConnName = GetProcAddress(hWs2, "WSAConnect");
    FARPROC pWSAConnOrd = GetProcAddress(hWs2, (LPCSTR)30);

    printf("connect name=%p ord4=%p\n", pConnName, pConnOrd);
    printf("WSAConnect name=%p ord30=%p\n", pWSAConnName, pWSAConnOrd);

    BYTE* code = (BYTE*)pConnName;
    printf("connect bytes: ");
    for(int i=0; i<16; i++) printf("%02X ", code[i]);
    printf("\n");

    return 0;
}
