#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

typedef int (WSAAPI *PFN_connect)(SOCKET, const struct sockaddr*, int);
typedef int (WSAAPI *PFN_WSAConnect)(SOCKET, const struct sockaddr*, int, LPWSABUF, LPWSABUF, LPQOS, LPQOS);

static PFN_connect    g_Orig_connect = NULL;
static PFN_connect    g_Orig_ord4 = NULL;
static PFN_WSAConnect g_Orig_WSAConnect = NULL;
static PFN_WSAConnect g_Orig_ord30 = NULL;

static void* CreateTrampoline(void* targetFunc, size_t stolenBytes) {
    BYTE* tramp = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    if (!tramp) return NULL;
    memcpy(tramp, targetFunc, stolenBytes);
    tramp[stolenBytes] = 0xFF;
    tramp[stolenBytes + 1] = 0x25;
    tramp[stolenBytes + 2] = 0x00;
    tramp[stolenBytes + 3] = 0x00;
    tramp[stolenBytes + 4] = 0x00;
    tramp[stolenBytes + 5] = 0x00;
    ULONG_PTR retAddr = (ULONG_PTR)targetFunc + stolenBytes;
    memcpy(&tramp[stolenBytes + 6], &retAddr, 8);
    return tramp;
}

static BOOL PlaceHook14(void* targetFunc, void* hookFunc, size_t stolenBytes, void** ppTrampoline) {
    if (!targetFunc || !hookFunc || stolenBytes < 14) return FALSE;
    *ppTrampoline = CreateTrampoline(targetFunc, stolenBytes);
    if (!*ppTrampoline) return FALSE;

    DWORD oldProtect;
    if (!VirtualProtect(targetFunc, stolenBytes, PAGE_EXECUTE_READWRITE, &oldProtect)) return FALSE;

    BYTE jmpCode[32];
    jmpCode[0] = 0xFF;
    jmpCode[1] = 0x25;
    jmpCode[2] = 0x00;
    jmpCode[3] = 0x00;
    jmpCode[4] = 0x00;
    jmpCode[5] = 0x00;
    ULONG_PTR targetAddr = (ULONG_PTR)hookFunc;
    memcpy(&jmpCode[6], &targetAddr, 8);
    for (size_t i = 14; i < stolenBytes; i++) jmpCode[i] = 0x90;

    memcpy(targetFunc, jmpCode, stolenBytes);
    VirtualProtect(targetFunc, stolenBytes, oldProtect, &oldProtect);
    FlushInstructionCache(GetCurrentProcess(), targetFunc, stolenBytes);
    return TRUE;
}

static int WSAAPI Hook_ord30(SOCKET s, const struct sockaddr* name, int namelen, LPWSABUF a, LPWSABUF b, LPQOS c, LPQOS d) {
    printf("[Hook_ord30] called! redirecting to 127.0.0.1:21000\n");
    struct sockaddr_in sin;
    memcpy(&sin, name, sizeof(sin));
    sin.sin_addr.s_addr = inet_addr("127.0.0.1");
    sin.sin_port = htons(21000);
    return g_Orig_ord30(s, (const struct sockaddr*)&sin, sizeof(sin), a, b, c, d);
}

int main() {
    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");

    FARPROC pOrd30 = GetProcAddress(hWs2, (LPCSTR)30);
    PlaceHook14((void*)pOrd30, (void*)Hook_ord30, 21, (void**)&g_Orig_ord30);

    printf("Testing ord30 call...\n");
    typedef int (WSAAPI *PFN_call)(SOCKET, const struct sockaddr*, int, LPWSABUF, LPWSABUF, LPQOS, LPQOS);
    PFN_call fnCall = (PFN_call)pOrd30;

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    struct sockaddr_in sin = {0};
    sin.sin_family = AF_INET;
    sin.sin_port = htons(5200);
    sin.sin_addr.s_addr = inet_addr("42.192.24.211");

    fnCall(s, (const struct sockaddr*)&sin, sizeof(sin), NULL, NULL, NULL, NULL);
    closesocket(s);
    WSACleanup();

    printf("ORD30 HOOK WORKS PERFECTLY!\n");
    return 0;
}
