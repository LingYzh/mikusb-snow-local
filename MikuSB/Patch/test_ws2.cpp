#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    void* pConnect = (void*)GetProcAddress(hWs2, "connect");
    void* pWSAConnect = (void*)GetProcAddress(hWs2, "WSAConnect");
    void* pGetAddrInfo = (void*)GetProcAddress(hWs2, "getaddrinfo");
    unsigned char* b = (unsigned char*)pConnect;
    printf("connect: %p -> ", pConnect);
    for(int i=0; i<16; i++) printf("%02X ", b[i]);
    printf("\n");
    b = (unsigned char*)pWSAConnect;
    printf("WSAConnect: %p -> ", pWSAConnect);
    for(int i=0; i<16; i++) printf("%02X ", b[i]);
    printf("\n");
    b = (unsigned char*)pGetAddrInfo;
    printf("getaddrinfo: %p -> ", pGetAddrInfo);
    for(int i=0; i<16; i++) printf("%02X ", b[i]);
    printf("\n");
    return 0;
}
