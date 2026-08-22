#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    BYTE* pName = (BYTE*)GetProcAddress(hWs2, "WSAConnect");
    BYTE* pOrd = (BYTE*)GetProcAddress(hWs2, (LPCSTR)30);

    printf("WSAConnect name bytes:\n");
    for(int i=0; i<32; i++) printf("%02X ", pName[i]);
    printf("\n");

    printf("WSAConnect ord30 bytes:\n");
    for(int i=0; i<32; i++) printf("%02X ", pOrd[i]);
    printf("\n");
    return 0;
}
