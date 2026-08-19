#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    void* pGetAddrInfoW = (void*)GetProcAddress(hWs2, "GetAddrInfoW");
    void* pGhbName = (void*)GetProcAddress(hWs2, "gethostbyname");
    unsigned char* b = (unsigned char*)pGetAddrInfoW;
    printf("GetAddrInfoW: %p -> ", pGetAddrInfoW);
    for(int i=0; i<16; i++) printf("%02X ", b[i]);
    printf("\n");
    b = (unsigned char*)pGhbName;
    printf("gethostbyname: %p -> ", pGhbName);
    for(int i=0; i<16; i++) printf("%02X ", b[i]);
    printf("\n");
    return 0;
}
