#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <mswsock.h>
#include <windows.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "mswsock.lib")

int main() {
    HMODULE hMswsock = LoadLibraryA("mswsock.dll");
    FARPROC pConnectEx = GetProcAddress(hMswsock, "ConnectEx");
    printf("mswsock.dll ConnectEx export: %p\n", pConnectEx);

    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    GUID guidConnectEx = WSAID_CONNECTEX;
    LPFN_CONNECTEX lpfnConnectEx = NULL;
    DWORD dwBytes = 0;

    int res = WSAIoctl(s, SIO_GET_EXTENSION_FUNCTION_POINTER,
                       &guidConnectEx, sizeof(guidConnectEx),
                       &lpfnConnectEx, sizeof(lpfnConnectEx),
                       &dwBytes, NULL, NULL);

    printf("WSAIoctl ConnectEx pointer: %p, result=%d\n", lpfnConnectEx, res);
    closesocket(s);
    WSACleanup();
    return 0;
}
