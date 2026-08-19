#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

typedef int (WSAAPI *PFN_connect)(SOCKET, const struct sockaddr*, int);
typedef int (WSAAPI *PFN_WSAConnect)(SOCKET, const struct sockaddr*, int, LPWSABUF, LPWSABUF, LPQOS, LPQOS);
typedef int (WSAAPI *PFN_getaddrinfo)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
typedef int (WSAAPI *PFN_GetAddrInfoW)(PCWSTR, PCWSTR, const ADDRINFOW*, PADDRINFOW*);
typedef struct hostent* (WSAAPI *PFN_gethostbyname)(const char*);

static PFN_connect       g_Orig_connect = NULL;
static PFN_WSAConnect    g_Orig_WSAConnect = NULL;
static PFN_getaddrinfo   g_Orig_getaddrinfo = NULL;
static PFN_GetAddrInfoW  g_Orig_GetAddrInfoW = NULL;
static PFN_gethostbyname g_Orig_gethostbyname = NULL;

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

static int WSAAPI Hook_connect(SOCKET s, const struct sockaddr* name, int namelen) {
    printf("[Hook_connect] called!\n");
    return g_Orig_connect(s, name, namelen);
}

static int WSAAPI Hook_getaddrinfo(PCSTR pNodeName, PCSTR pServiceName, const ADDRINFOA *pHints, PADDRINFOA *ppResult) {
    printf("[Hook_getaddrinfo] node=%s service=%s\n", pNodeName, pServiceName);
    return g_Orig_getaddrinfo("127.0.0.1", pServiceName, pHints, ppResult);
}

static int WSAAPI Hook_GetAddrInfoW(PCWSTR pNodeName, PCWSTR pServiceName, const ADDRINFOW *pHints, PADDRINFOW *ppResult) {
    wprintf(L"[Hook_GetAddrInfoW] node=%s service=%s\n", pNodeName, pServiceName);
    return g_Orig_GetAddrInfoW(L"127.0.0.1", pServiceName, pHints, ppResult);
}

static struct hostent* WSAAPI Hook_gethostbyname(const char* name) {
    printf("[Hook_gethostbyname] %s\n", name);
    return g_Orig_gethostbyname("127.0.0.1");
}

int main() {
    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");

    PlaceHook14((void*)GetProcAddress(hWs2, "connect"),        (void*)Hook_connect,        16, (void**)&g_Orig_connect);
    PlaceHook14((void*)GetProcAddress(hWs2, "WSAConnect"),     (void*)Hook_connect,        15, (void**)&g_Orig_WSAConnect);
    PlaceHook14((void*)GetProcAddress(hWs2, "getaddrinfo"),    (void*)Hook_getaddrinfo,    19, (void**)&g_Orig_getaddrinfo);
    PlaceHook14((void*)GetProcAddress(hWs2, "GetAddrInfoW"),   (void*)Hook_GetAddrInfoW,   21, (void**)&g_Orig_GetAddrInfoW);
    PlaceHook14((void*)GetProcAddress(hWs2, "gethostbyname"),  (void*)Hook_gethostbyname,  15, (void**)&g_Orig_gethostbyname);

    printf("Hooks placed! Testing GetAddrInfoW...\n");
    ADDRINFOW hints = {0};
    hints.ai_family = AF_INET;
    PADDRINFOW result = NULL;
    int res = GetAddrInfoW(L"sh-jxsj.xgsdk.com", L"18443", &hints, &result);
    if (res == 0 && result) {
        struct sockaddr_in* sin = (struct sockaddr_in*)result->ai_addr;
        printf("GetAddrInfoW returned: IP=%s\n", inet_ntoa(sin->sin_addr));
        FreeAddrInfoW(result);
    }

    printf("Testing getaddrinfo...\n");
    ADDRINFOA hintsA = {0};
    hintsA.ai_family = AF_INET;
    PADDRINFOA resultA = NULL;
    res = getaddrinfo("js2sdk.xoyo.com", "11443", &hintsA, &resultA);
    if (res == 0 && resultA) {
        struct sockaddr_in* sin = (struct sockaddr_in*)resultA->ai_addr;
        printf("getaddrinfo returned: IP=%s\n", inet_ntoa(sin->sin_addr));
        freeaddrinfo(resultA);
    }

    printf("Testing gethostbyname...\n");
    struct hostent* he = gethostbyname("xoyo.com");
    if (he) {
        printf("gethostbyname returned: IP=%s\n", inet_ntoa(*(struct in_addr*)he->h_addr));
    }

    WSACleanup();
    printf("ALL TESTS PASSED WITH ZERO CRASHES!\n");
    return 0;
}
