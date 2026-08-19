#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>

int main() {
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");
    printf("ws2_32 loaded at %p\n", hWs2);
    FARPROC pConnect = GetProcAddress(hWs2, "connect");
    FARPROC pWSAConnect = GetProcAddress(hWs2, "WSAConnect");
    FARPROC pOrd4 = GetProcAddress(hWs2, (LPCSTR)4);
    FARPROC pOrd30 = GetProcAddress(hWs2, (LPCSTR)30);

    printf("connect: %p\n", pConnect);
    printf("WSAConnect: %p\n", pWSAConnect);
    printf("ord4: %p\n", pOrd4);
    printf("ord30: %p\n", pOrd30);
    return 0;
}
