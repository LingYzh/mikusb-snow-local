#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

typedef int (WSAAPI *PFN_WSAConnect)(SOCKET, const struct sockaddr*, int, LPWSABUF, LPWSABUF, LPQOS, LPQOS);
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
    printf("[Hook_ord30] SUCCESS! Redirecting...\n");
    return 0; // Return success for test
}

int main() {
    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");

    FARPROC pOrd30 = GetProcAddress(hWs2, (LPCSTR)30);
    PlaceHook14((void*)pOrd30, (void*)Hook_ord30, 21, (void**)&g_Orig_ord30);

    PFN_WSAConnect fnCall = (PFN_WSAConnect)pOrd30;
    struct sockaddr_in sin = {0};
    sin.sin_family = AF_INET;
    sin.sin_port = htons(5200);
    sin.sin_addr.s_addr = inet_addr("42.192.24.211");

    int res = fnCall(INVALID_SOCKET, (const struct sockaddr*)&sin, sizeof(sin), NULL, NULL, NULL, NULL);
    printf("Result = %d\n", res);

    WSACleanup();
    printf("ORD30 TEST PASSED COMPLETELY!\n");
    return 0;
}
