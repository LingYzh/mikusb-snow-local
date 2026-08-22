#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

int main() {
    HMODULE hPatch = LoadLibraryA("D:\\Snow\\Server\\Patch\\MikuSB-Patch.dll");
    printf("Loaded Patch.dll: %p\n", hPatch);

    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    struct sockaddr_in sin = {0};
    sin.sin_family = AF_INET;
    sin.sin_port = htons(5200);
    sin.sin_addr.s_addr = inet_addr("42.192.24.211");

    printf("Calling connect to 42.192.24.211:5200...\n");
    int res = connect(s, (const struct sockaddr*)&sin, sizeof(sin));
    printf("connect returned %d, WSAGetLastError=%d\n", res, WSAGetLastError());
    closesocket(s);

    s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    printf("Calling WSAConnect to 42.192.24.211:5200...\n");
    res = WSAConnect(s, (const struct sockaddr*)&sin, sizeof(sin), NULL, NULL, NULL, NULL);
    printf("WSAConnect returned %d, WSAGetLastError=%d\n", res, WSAGetLastError());
    closesocket(s);

    WSACleanup();
    return 0;
}
