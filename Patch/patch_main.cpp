#define WIN32_LEAN_AND_MEAN
#define _WIN32_WINNT 0x0601
#define NTDDI_VERSION 0x06010000
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS

#include <winsock2.h>
#include <mswsock.h>
#include <windows.h>
#include <ws2tcpip.h>
#include <psapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "mswsock.lib")
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "user32.lib")

static FILE* g_LogFile = NULL;
static CRITICAL_SECTION g_LogLock;
static HMODULE g_hWs2 = NULL;

static void InitLogging() {
    InitializeCriticalSection(&g_LogLock);
    fopen_s(&g_LogFile, "Patch\\Patch.log", "w");
    if (!g_LogFile) {
        fopen_s(&g_LogFile, "Patch.log", "w");
    }
}

static void Log(const char* format, ...) {
    EnterCriticalSection(&g_LogLock);
    if (g_LogFile) {
        va_list args;
        va_start(args, format);
        vfprintf(g_LogFile, format, args);
        fprintf(g_LogFile, "\n");
        fflush(g_LogFile);
        va_end(args);
    }
    LeaveCriticalSection(&g_LogLock);
}

static void LogW(const wchar_t* format, ...) {
    EnterCriticalSection(&g_LogLock);
    if (g_LogFile) {
        va_list args;
        va_start(args, format);
        vfwprintf(g_LogFile, format, args);
        fwprintf(g_LogFile, L"\n");
        fflush(g_LogFile);
        va_end(args);
    }
    LeaveCriticalSection(&g_LogLock);
}

// DNS rewrite only. XGSDK hosts must NOT be rewritten here: curl treats
// 127.0.0.1 as localhost, skips SOCKS5h, and getGameConfig then dies.
// Serverlist CDN still goes to loopback (hosts file + this list).
static const char* const DNS_REDIRECT_DOMAINS[] = {
    "xoyocdn.com",
    NULL
};

static BOOL IsOfficialIpA(const char* host) {
    if (!host || !*host) return FALSE;
    if (strncmp(host, "42.192.", 7) == 0) return TRUE;
    if (strncmp(host, "124.156.", 8) == 0) return TRUE;
    if (strncmp(host, "43.129.", 7) == 0) return TRUE;
    if (strncmp(host, "150.109.", 8) == 0) return TRUE;
    if (strncmp(host, "198.18.", 7) == 0) return TRUE;
    if (strncmp(host, "198.19.", 7) == 0) return TRUE;
    return FALSE;
}

static BOOL ShouldRedirectA(const char* host) {
    if (!host || !*host) return FALSE;
    if (_stricmp(host, "127.0.0.1") == 0 || _stricmp(host, "localhost") == 0 || _stricmp(host, "::1") == 0) return FALSE;
    if (IsOfficialIpA(host)) return TRUE;

    for (int i = 0; DNS_REDIRECT_DOMAINS[i]; i++) {
        const char* domain = DNS_REDIRECT_DOMAINS[i];
        size_t hostLen = strlen(host);
        size_t domLen = strlen(domain);
        if (hostLen == domLen) {
            if (_stricmp(host, domain) == 0) return TRUE;
        } else if (hostLen > domLen) {
            if (host[hostLen - domLen - 1] == '.' &&
                _stricmp(host + (hostLen - domLen), domain) == 0) {
                return TRUE;
            }
        }
    }
    return FALSE;
}

static BOOL ShouldRedirectW(const wchar_t* host) {
    if (!host || !*host) return FALSE;
    char hostA[512] = {0};
    WideCharToMultiByte(CP_UTF8, 0, host, -1, hostA, sizeof(hostA) - 1, NULL, NULL);
    return ShouldRedirectA(hostA);
}

typedef int (WSAAPI *PFN_connect)(SOCKET, const struct sockaddr*, int);
typedef int (WSAAPI *PFN_WSAConnect)(SOCKET, const struct sockaddr*, int, LPWSABUF, LPWSABUF, LPQOS, LPQOS);
typedef int (WSAAPI *PFN_getaddrinfo)(PCSTR, PCSTR, const ADDRINFOA*, PADDRINFOA*);
typedef int (WSAAPI *PFN_GetAddrInfoW)(PCWSTR, PCWSTR, const ADDRINFOW*, PADDRINFOW*);
typedef INT (WSAAPI *PFN_GetAddrInfoExA)(
    PCSTR, PCSTR, DWORD, LPGUID, const ADDRINFOEXA*, PADDRINFOEXA*,
    struct timeval*, LPOVERLAPPED, LPLOOKUPSERVICE_COMPLETION_ROUTINE, LPHANDLE);
typedef INT (WSAAPI *PFN_GetAddrInfoExW)(
    PCWSTR, PCWSTR, DWORD, LPGUID, const ADDRINFOEXW*, PADDRINFOEXW*,
    struct timeval*, LPOVERLAPPED, LPLOOKUPSERVICE_COMPLETION_ROUTINE, LPHANDLE);
typedef struct hostent* (WSAAPI *PFN_gethostbyname)(const char*);
typedef int (WSAAPI *PFN_WSAIoctl)(SOCKET, DWORD, LPVOID, DWORD, LPVOID, DWORD, LPDWORD, LPWSAOVERLAPPED, LPWSAOVERLAPPED_COMPLETION_ROUTINE);
typedef unsigned long (WSAAPI *PFN_inet_addr)(const char*);
typedef INT (WSAAPI *PFN_WSAStringToAddressA)(LPSTR, INT, LPWSAPROTOCOL_INFOA, LPSOCKADDR, LPINT);
typedef INT (WSAAPI *PFN_WSAStringToAddressW)(LPWSTR, INT, LPWSAPROTOCOL_INFOW, LPSOCKADDR, LPINT);
typedef SOCKET (WSAAPI *PFN_socket)(int, int, int);
typedef BOOL (PASCAL *PFN_ConnectEx)(SOCKET, const struct sockaddr*, int, PVOID, DWORD, LPDWORD, LPOVERLAPPED);

typedef LONG NTSTATUS;
typedef struct _IO_STATUS_BLOCK {
    union {
        NTSTATUS Status;
        PVOID Pointer;
    };
    ULONG_PTR Information;
} IO_STATUS_BLOCK, *PIO_STATUS_BLOCK;
typedef VOID (NTAPI *PIO_APC_ROUTINE)(PVOID, PIO_STATUS_BLOCK, ULONG);
typedef NTSTATUS (NTAPI *PFN_NtDeviceIoControlFile)(
    HANDLE, HANDLE, PIO_APC_ROUTINE, PVOID, PIO_STATUS_BLOCK,
    ULONG, PVOID, ULONG, PVOID, ULONG);

static PFN_connect       g_Orig_connect       = NULL;
static PFN_WSAConnect    g_Orig_WSAConnect    = NULL;
static PFN_getaddrinfo   g_Orig_getaddrinfo   = NULL;
static PFN_GetAddrInfoW  g_Orig_GetAddrInfoW  = NULL;
static PFN_GetAddrInfoExA g_Orig_GetAddrInfoExA = NULL;
static PFN_GetAddrInfoExW g_Orig_GetAddrInfoExW = NULL;
static PFN_gethostbyname g_Orig_gethostbyname = NULL;
static PFN_WSAIoctl      g_Orig_WSAIoctl      = NULL;
static PFN_ConnectEx     g_Orig_ConnectEx     = NULL;
static PFN_NtDeviceIoControlFile g_Orig_NtDeviceIoControlFile = NULL;
static PFN_inet_addr     g_Orig_inet_addr     = NULL;
static PFN_WSAStringToAddressA g_Orig_WSAStringToAddressA = NULL;
static PFN_WSAStringToAddressW g_Orig_WSAStringToAddressW = NULL;
static PFN_socket        g_Orig_socket        = NULL;
static volatile LONG     g_ConnectExHooked    = 0;

typedef struct {
    void* target;
    void* detour;
    void** orig;
    size_t stolen;
    const char* name;
} SavedHook;
static SavedHook g_SavedHooks[20];
static int g_SavedHookCount = 0;

static void InstallConnectExHook();

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

static BOOL PlaceHookNamed(void* targetFunc, void* hookFunc, size_t stolenBytes, void** ppTrampoline, const char* name) {
    if (!targetFunc) {
        Log("[MikuSB-Patch] Hook %s FAILED: null func", name);
        return FALSE;
    }
    BYTE* p = (BYTE*)targetFunc;
    Log("[MikuSB-Patch] Hook %s func=%p bytes=%02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X",
        name, targetFunc,
        p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7],
        p[8], p[9], p[10], p[11], p[12], p[13], p[14], p[15]);
    BOOL ok = PlaceHook14(targetFunc, hookFunc, stolenBytes, ppTrampoline);
    Log("[MikuSB-Patch] Hook %s %s stolen=%zu tramp=%p",
        name, ok ? "OK" : "FAILED", stolenBytes, ppTrampoline ? *ppTrampoline : NULL);
    if (ok && g_SavedHookCount < (int)(sizeof(g_SavedHooks) / sizeof(g_SavedHooks[0]))) {
        g_SavedHooks[g_SavedHookCount].target = targetFunc;
        g_SavedHooks[g_SavedHookCount].detour = hookFunc;
        g_SavedHooks[g_SavedHookCount].orig = ppTrampoline;
        g_SavedHooks[g_SavedHookCount].stolen = stolenBytes;
        g_SavedHooks[g_SavedHookCount].name = name;
        g_SavedHookCount++;
    }
    return ok;
}

static void LogSockAddr(const char* api, const struct sockaddr* name, int namelen) {
    if (!name) {
        Log("[MikuSB-Patch] %s name=NULL", api);
        return;
    }
    if (name->sa_family == AF_INET && namelen >= (int)sizeof(struct sockaddr_in)) {
        const struct sockaddr_in* sin = (const struct sockaddr_in*)name;
        unsigned short port = ntohs(sin->sin_port);
        if (port == 18888) return;
        Log("[MikuSB-Patch] %s AF_INET %s:%d", api, inet_ntoa(sin->sin_addr), port);
        return;
    }
    if (name->sa_family == AF_INET6 && namelen >= (int)sizeof(struct sockaddr_in6)) {
        const struct sockaddr_in6* sin6 = (const struct sockaddr_in6*)name;
        unsigned short port = ntohs(sin6->sin6_port);
        if (IN6_IS_ADDR_V4MAPPED(&sin6->sin6_addr)) {
            struct in_addr v4;
            memcpy(&v4, &sin6->sin6_addr.s6_addr[12], 4);
            Log("[MikuSB-Patch] %s AF_INET6-v4mapped %s:%d", api, inet_ntoa(v4), port);
            return;
        }
        char ip[64] = {0};
        inet_ntop(AF_INET6, &sin6->sin6_addr, ip, sizeof(ip));
        Log("[MikuSB-Patch] %s AF_INET6 %s:%d", api, ip, port);
        return;
    }
    Log("[MikuSB-Patch] %s family=%d namelen=%d", api, name->sa_family, namelen);
}

static void RedirectSockAddr(struct sockaddr_in* sin) {
    if (!sin) return;
    unsigned short port = ntohs(sin->sin_port);
    unsigned long localIp = inet_addr("127.0.0.1");
    char origIp[32];
    strcpy_s(origIp, inet_ntoa(sin->sin_addr));

    // GameServer port redirection (5200, 5100, 5201, 21000, 21001)
    if (port == 5200 || port == 5100 || port == 5201 || port == 21000 || port == 21001) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(21000);
        Log("[MikuSB-Patch] Redirect GameServer TCP: %s:%d -> 127.0.0.1:21000", origIp, port);
        return;
    }

    // HTTPS / SDK Ports. 18443 is getGameConfig; fold it onto 13443 so login
    // still works if Hyper-V excluded 18443 or Kestrel did not bind it.
    if (port == 18443) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(13443);
        Log("[MikuSB-Patch] Redirect SDK HTTPS: port 18443 -> 127.0.0.1:13443");
        return;
    }
    if (port == 11443 || port == 13443 || port == 19443 || port == 31443) {
        sin->sin_addr.s_addr = localIp;
        Log("[MikuSB-Patch] Redirect SDK HTTPS: port %d -> 127.0.0.1:%d", port, port);
        return;
    }

    if (port == 80) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(21500);
        Log("[MikuSB-Patch] Redirect HTTP: port 80 -> 127.0.0.1:21500");
        return;
    }

    if (port == 443) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(13443);
        Log("[MikuSB-Patch] Redirect HTTPS: port 443 -> 127.0.0.1:13443");
        return;
    }

    if (port == 893) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(31443);
        Log("[MikuSB-Patch] Redirect BI: port 893 -> 127.0.0.1:31443");
        return;
    }

    // Official IP ranges
    unsigned char b1 = (unsigned char)(sin->sin_addr.s_addr & 0xFF);
    unsigned char b2 = (unsigned char)((sin->sin_addr.s_addr >> 8) & 0xFF);
    if ((b1 == 42 && b2 == 192) || (b1 == 124 && b2 == 156) || (b1 == 43 && b2 == 129) || (b1 == 150 && b2 == 109)) {
        sin->sin_addr.s_addr = localIp;
        sin->sin_port = htons(21000);
        Log("[MikuSB-Patch] Redirect Official IP range (%d.%d.*.*) -> 127.0.0.1:21000", b1, b2);
    }
}

static BOOL PatchSockaddrBytes(BYTE* p, ULONG remaining) {
    if (!p || remaining < 8) return FALSE;
    unsigned short family = (unsigned short)(p[0] | (p[1] << 8));
    if (family != AF_INET) return FALSE;
    unsigned short port = (unsigned short)((p[2] << 8) | p[3]);
    unsigned char b1 = p[4], b2 = p[5];
    BOOL gamePort = (port == 5200 || port == 5100 || port == 5201 || port == 21000 || port == 21001);
    BOOL officialIp = (b1 == 42 && b2 == 192) || (b1 == 124 && b2 == 156) || (b1 == 43 && b2 == 129) || (b1 == 150 && b2 == 109);
    if (!gamePort && !officialIp) return FALSE;
    if (p[4] == 127 && p[5] == 0 && p[6] == 0 && p[7] == 1 && port == 21000) return FALSE;
    Log("[MikuSB-Patch] AFD rewrite %d.%d.%d.%d:%d -> 127.0.0.1:21000", p[4], p[5], p[6], p[7], port);
    p[2] = 0x52;
    p[3] = 0x08;
    p[4] = 127;
    p[5] = 0;
    p[6] = 0;
    p[7] = 1;
    return TRUE;
}

static void PatchAfdBuffer(PVOID buffer, ULONG length) {
    if (!buffer || length < 8) return;
    BYTE* p = (BYTE*)buffer;
    BYTE officialIp[4] = { 42, 192, 24, 211 };
    BYTE localIp[4] = { 127, 0, 0, 1 };
    for (ULONG i = 0; i + 4 <= length && i < 256; i++) {
        if (memcmp(p + i, officialIp, 4) == 0) {
            Log("[MikuSB-Patch] AFD rewrite raw IP 42.192.24.211 at offset %u", i);
            memcpy(p + i, localIp, 4);
        }
        PatchSockaddrBytes(p + i, length - i);
    }
}

static NTSTATUS NTAPI Hook_NtDeviceIoControlFile(
    HANDLE FileHandle,
    HANDLE Event,
    PIO_APC_ROUTINE ApcRoutine,
    PVOID ApcContext,
    PIO_STATUS_BLOCK IoStatusBlock,
    ULONG IoControlCode,
    PVOID InputBuffer,
    ULONG InputBufferLength,
    PVOID OutputBuffer,
    ULONG OutputBufferLength)
{
    if (InputBuffer && InputBufferLength >= 8 && InputBufferLength <= 4096
        && ((IoControlCode >> 16) & 0xFFFF) == 0x12)
        PatchAfdBuffer(InputBuffer, InputBufferLength);
    return g_Orig_NtDeviceIoControlFile
        ? g_Orig_NtDeviceIoControlFile(FileHandle, Event, ApcRoutine, ApcContext, IoStatusBlock,
                                       IoControlCode, InputBuffer, InputBufferLength, OutputBuffer, OutputBufferLength)
        : (NTSTATUS)0xC0000002;
}

static void ApplyRedirect(const struct sockaddr* name, int namelen, struct sockaddr_storage* out, int* outlen) {
    memset(out, 0, sizeof(*out));
    if (!name || namelen <= 0) {
        *outlen = 0;
        return;
    }
    int copyLen = namelen;
    if (copyLen > (int)sizeof(*out)) copyLen = (int)sizeof(*out);
    memcpy(out, name, copyLen);
    *outlen = copyLen;

    if (name->sa_family == AF_INET && namelen >= (int)sizeof(struct sockaddr_in)) {
        RedirectSockAddr((struct sockaddr_in*)out);
        *outlen = sizeof(struct sockaddr_in);
        return;
    }
    if (name->sa_family == AF_INET6 && namelen >= (int)sizeof(struct sockaddr_in6)) {
        struct sockaddr_in6* sin6 = (struct sockaddr_in6*)out;
        struct sockaddr_in sin;
        memset(&sin, 0, sizeof(sin));
        sin.sin_family = AF_INET;
        sin.sin_port = sin6->sin6_port;
        if (IN6_IS_ADDR_V4MAPPED(&sin6->sin6_addr)) {
            memcpy(&sin.sin_addr, &sin6->sin6_addr.s6_addr[12], 4);
        } else {
            unsigned short port = ntohs(sin6->sin6_port);
            BOOL sdkPort = (port == 80 || port == 443 || port == 11443 || port == 13443
                || port == 18443 || port == 19443 || port == 31443);
            if (port != 5200 && port != 5100 && port != 5201 && port != 21000 && port != 21001 && !sdkPort)
                return;
            sin.sin_addr.s_addr = inet_addr("127.0.0.1");
        }
        unsigned long before = sin.sin_addr.s_addr;
        unsigned short beforePort = sin.sin_port;
        RedirectSockAddr(&sin);
        if (sin.sin_addr.s_addr != before || sin.sin_port != beforePort || IN6_IS_ADDR_V4MAPPED(&sin6->sin6_addr)) {
            memcpy(out, &sin, sizeof(sin));
            *outlen = sizeof(sin);
        }
    }
}

static BOOL PASCAL Hook_ConnectEx(
    SOCKET s,
    const struct sockaddr* name,
    int namelen,
    PVOID lpSendBuffer,
    DWORD dwSendDataLength,
    LPDWORD lpdwBytesSent,
    LPOVERLAPPED lpOverlapped)
{
    LogSockAddr("ConnectEx", name, namelen);
    struct sockaddr_storage redirected;
    int redirectedLen = namelen;
    ApplyRedirect(name, namelen, &redirected, &redirectedLen);
    return g_Orig_ConnectEx
        ? g_Orig_ConnectEx(s, (const struct sockaddr*)&redirected, redirectedLen, lpSendBuffer, dwSendDataLength, lpdwBytesSent, lpOverlapped)
        : FALSE;
}

static int WSAAPI Hook_WSAIoctl(
    SOCKET s,
    DWORD dwIoControlCode,
    LPVOID lpvInBuffer,
    DWORD cbInBuffer,
    LPVOID lpvOutBuffer,
    DWORD cbOutBuffer,
    LPDWORD lpcbBytesReturned,
    LPWSAOVERLAPPED lpOverlapped,
    LPWSAOVERLAPPED_COMPLETION_ROUTINE lpCompletionRoutine)
{
    PFN_WSAIoctl fn = g_Orig_WSAIoctl ? g_Orig_WSAIoctl : WSAIoctl;
    int ret = fn(s, dwIoControlCode, lpvInBuffer, cbInBuffer, lpvOutBuffer, cbOutBuffer, lpcbBytesReturned, lpOverlapped, lpCompletionRoutine);

    if (dwIoControlCode == SIO_GET_EXTENSION_FUNCTION_POINTER && lpvInBuffer && cbInBuffer >= sizeof(GUID)) {
        GUID* pGuid = (GUID*)lpvInBuffer;
        GUID guidConnectEx = WSAID_CONNECTEX;
        Log("[MikuSB-Patch] WSAIoctl SIO_GET_EXTENSION_FUNCTION_POINTER ret=%d guid={%08lX-%04X-...} out=%p",
            ret, pGuid->Data1, pGuid->Data2, lpvOutBuffer);
        if (ret == 0 && memcmp(pGuid, &guidConnectEx, sizeof(GUID)) == 0 && lpvOutBuffer && cbOutBuffer >= sizeof(void*)) {
            LPFN_CONNECTEX* ppFn = (LPFN_CONNECTEX*)lpvOutBuffer;
            if (*ppFn && *ppFn != Hook_ConnectEx) {
                if (!g_Orig_ConnectEx) g_Orig_ConnectEx = *ppFn;
                *ppFn = Hook_ConnectEx;
                Log("[MikuSB-Patch] Intercepted UE ConnectEx from WSAIoctl! orig=%p", g_Orig_ConnectEx);
            }
        }
    }
    return ret;
}

static int WSAAPI Hook_connect(SOCKET s, const struct sockaddr *name, int namelen) {
    LogSockAddr("connect", name, namelen);
    struct sockaddr_storage redirected;
    int redirectedLen = namelen;
    ApplyRedirect(name, namelen, &redirected, &redirectedLen);
    int ret = g_Orig_connect
        ? g_Orig_connect(s, (const struct sockaddr*)&redirected, redirectedLen)
        : SOCKET_ERROR;
    int wsa = WSAGetLastError();
    if (redirected.ss_family == AF_INET) {
        struct sockaddr_in* sin = (struct sockaddr_in*)&redirected;
        unsigned short port = ntohs(sin->sin_port);
        if (port != 18888 || (ret != 0 && wsa != 10035 && wsa != 10036))
            Log("[MikuSB-Patch] connect() -> %s:%d result=%d wsa=%d",
                inet_ntoa(sin->sin_addr), port, ret, wsa);
    }
    return ret;
}

static int WSAAPI Hook_WSAConnect(
    SOCKET s,
    const struct sockaddr *name,
    int namelen,
    LPWSABUF lpCallerData,
    LPWSABUF lpCalleeData,
    LPQOS lpSQOS,
    LPQOS lpGQOS)
{
    LogSockAddr("WSAConnect", name, namelen);
    struct sockaddr_storage redirected;
    int redirectedLen = namelen;
    ApplyRedirect(name, namelen, &redirected, &redirectedLen);
    if (g_Orig_WSAConnect)
        return g_Orig_WSAConnect(s, (const struct sockaddr*)&redirected, redirectedLen, lpCallerData, lpCalleeData, lpSQOS, lpGQOS);
    return SOCKET_ERROR;
}

static int WSAAPI Hook_getaddrinfo(
    PCSTR pNodeName,
    PCSTR pServiceName,
    const ADDRINFOA *pHints,
    PADDRINFOA *ppResult)
{
    if (pNodeName && ShouldRedirectA(pNodeName)) {
        Log("[MikuSB-Patch] Redirect getaddrinfo: %s:%s -> 127.0.0.1:%s",
            pNodeName, pServiceName ? pServiceName : "(null)", pServiceName ? pServiceName : "(null)");
        return g_Orig_getaddrinfo ? g_Orig_getaddrinfo("127.0.0.1", pServiceName, pHints, ppResult)
                                  : getaddrinfo("127.0.0.1", pServiceName, pHints, ppResult);
    }
    return g_Orig_getaddrinfo ? g_Orig_getaddrinfo(pNodeName, pServiceName, pHints, ppResult)
                              : getaddrinfo(pNodeName, pServiceName, pHints, ppResult);
}

static int WSAAPI Hook_GetAddrInfoW(
    PCWSTR pNodeName,
    PCWSTR pServiceName,
    const ADDRINFOW *pHints,
    PADDRINFOW *ppResult)
{
    if (pNodeName && ShouldRedirectW(pNodeName)) {
        LogW(L"[MikuSB-Patch] Redirect GetAddrInfoW: %s:%s -> 127.0.0.1:%s",
            pNodeName, pServiceName ? pServiceName : L"(null)", pServiceName ? pServiceName : L"(null)");
        return g_Orig_GetAddrInfoW ? g_Orig_GetAddrInfoW(L"127.0.0.1", pServiceName, pHints, ppResult)
                                   : GetAddrInfoW(L"127.0.0.1", pServiceName, pHints, ppResult);
    }
    return g_Orig_GetAddrInfoW ? g_Orig_GetAddrInfoW(pNodeName, pServiceName, pHints, ppResult)
                               : GetAddrInfoW(pNodeName, pServiceName, pHints, ppResult);
}

static INT WSAAPI Hook_GetAddrInfoExA(
    PCSTR pName, PCSTR pServiceName, DWORD dwNameSpace, LPGUID lpNspId,
    const ADDRINFOEXA *hints, PADDRINFOEXA *ppResult, struct timeval *timeout,
    LPOVERLAPPED lpOverlapped, LPLOOKUPSERVICE_COMPLETION_ROUTINE lpCompletionRoutine, LPHANDLE lpHandle)
{
    PCSTR name = pName;
    if (pName && ShouldRedirectA(pName)) {
        Log("[MikuSB-Patch] Redirect GetAddrInfoExA: %s -> 127.0.0.1", pName);
        name = "127.0.0.1";
    }
    return g_Orig_GetAddrInfoExA
        ? g_Orig_GetAddrInfoExA(name, pServiceName, dwNameSpace, lpNspId, hints, ppResult,
                                timeout, lpOverlapped, lpCompletionRoutine, lpHandle)
        : WSAHOST_NOT_FOUND;
}

static INT WSAAPI Hook_GetAddrInfoExW(
    PCWSTR pName, PCWSTR pServiceName, DWORD dwNameSpace, LPGUID lpNspId,
    const ADDRINFOEXW *hints, PADDRINFOEXW *ppResult, struct timeval *timeout,
    LPOVERLAPPED lpOverlapped, LPLOOKUPSERVICE_COMPLETION_ROUTINE lpCompletionRoutine, LPHANDLE lpHandle)
{
    PCWSTR name = pName;
    if (pName && ShouldRedirectW(pName)) {
        LogW(L"[MikuSB-Patch] Redirect GetAddrInfoExW: %s -> 127.0.0.1", pName);
        name = L"127.0.0.1";
    }
    return g_Orig_GetAddrInfoExW
        ? g_Orig_GetAddrInfoExW(name, pServiceName, dwNameSpace, lpNspId, hints, ppResult,
                                timeout, lpOverlapped, lpCompletionRoutine, lpHandle)
        : WSAHOST_NOT_FOUND;
}

static struct hostent* WSAAPI Hook_gethostbyname(const char* name) {
    if (name && ShouldRedirectA(name)) {
        Log("[MikuSB-Patch] Redirect gethostbyname: %s -> 127.0.0.1", name);
        return g_Orig_gethostbyname ? g_Orig_gethostbyname("127.0.0.1")
                                    : gethostbyname("127.0.0.1");
    }
    return g_Orig_gethostbyname ? g_Orig_gethostbyname(name)
                                : gethostbyname(name);
}

static unsigned long WSAAPI Hook_inet_addr(const char* cp) {
    if (cp && ShouldRedirectA(cp)) {
        Log("[MikuSB-Patch] inet_addr %s -> 127.0.0.1", cp);
        cp = "127.0.0.1";
    }
    return g_Orig_inet_addr ? g_Orig_inet_addr(cp) : INADDR_NONE;
}

static INT WSAAPI Hook_WSAStringToAddressA(LPSTR AddressString, INT AddressFamily, LPWSAPROTOCOL_INFOA lpProtocolInfo, LPSOCKADDR lpAddress, LPINT lpAddressLength) {
    if (AddressString && ShouldRedirectA(AddressString)) {
        Log("[MikuSB-Patch] WSAStringToAddressA %s -> 127.0.0.1", AddressString);
        AddressString = (LPSTR)"127.0.0.1";
    }
    return g_Orig_WSAStringToAddressA
        ? g_Orig_WSAStringToAddressA(AddressString, AddressFamily, lpProtocolInfo, lpAddress, lpAddressLength)
        : SOCKET_ERROR;
}

static INT WSAAPI Hook_WSAStringToAddressW(LPWSTR AddressString, INT AddressFamily, LPWSAPROTOCOL_INFOW lpProtocolInfo, LPSOCKADDR lpAddress, LPINT lpAddressLength) {
    if (AddressString && ShouldRedirectW(AddressString)) {
        LogW(L"[MikuSB-Patch] WSAStringToAddressW %s -> 127.0.0.1", AddressString);
        AddressString = (LPWSTR)L"127.0.0.1";
    }
    return g_Orig_WSAStringToAddressW
        ? g_Orig_WSAStringToAddressW(AddressString, AddressFamily, lpProtocolInfo, lpAddress, lpAddressLength)
        : SOCKET_ERROR;
}

static SOCKET WSAAPI Hook_socket(int af, int type, int protocol) {
    SOCKET s = g_Orig_socket ? g_Orig_socket(af, type, protocol) : INVALID_SOCKET;
    if (type == SOCK_STREAM)
        Log("[MikuSB-Patch] socket() STREAM af=%d proto=%d s=%llu", af, protocol, (unsigned long long)s);
    return s;
}

static void PatchIATForModule(HMODULE hMod) {
    if (!hMod) return;
    __try {
        PIMAGE_DOS_HEADER pDos = (PIMAGE_DOS_HEADER)hMod;
        if (pDos->e_magic != IMAGE_DOS_SIGNATURE) return;
        PIMAGE_NT_HEADERS pNt = (PIMAGE_NT_HEADERS)((BYTE*)hMod + pDos->e_lfanew);
        if (pNt->Signature != IMAGE_NT_SIGNATURE) return;

        IMAGE_DATA_DIRECTORY importDir = pNt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
        if (importDir.VirtualAddress == 0 || importDir.Size == 0) return;

        PIMAGE_IMPORT_DESCRIPTOR pImport = (PIMAGE_IMPORT_DESCRIPTOR)((BYTE*)hMod + importDir.VirtualAddress);
        for (; pImport->Name != 0; pImport++) {
            const char* dllName = (const char*)((BYTE*)hMod + pImport->Name);
            if (!dllName) continue;
            if (_stricmp(dllName, "ws2_32.dll") != 0 && _stricmp(dllName, "wsock32.dll") != 0 && _stricmp(dllName, "ws2_32") != 0) {
                continue;
            }

            PIMAGE_THUNK_DATA pThunk = (PIMAGE_THUNK_DATA)((BYTE*)hMod + pImport->FirstThunk);
            for (; pThunk->u1.Function != 0; pThunk++) {
                void* curFunc = (void*)pThunk->u1.Function;
                if (curFunc == (void*)Hook_connect || curFunc == (void*)Hook_WSAConnect ||
                    curFunc == (void*)Hook_WSAIoctl ||
                    curFunc == (void*)Hook_getaddrinfo || curFunc == (void*)Hook_GetAddrInfoW ||
                    curFunc == (void*)Hook_GetAddrInfoExA || curFunc == (void*)Hook_GetAddrInfoExW ||
                    curFunc == (void*)Hook_gethostbyname) {
                    continue;
                }

                if (curFunc == (void*)g_Orig_connect || curFunc == (void*)GetProcAddress(g_hWs2, "connect") || curFunc == (void*)GetProcAddress(g_hWs2, (LPCSTR)4)) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_connect;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (curFunc == (void*)g_Orig_WSAConnect || curFunc == (void*)GetProcAddress(g_hWs2, "WSAConnect")) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_WSAConnect;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (curFunc == (void*)g_Orig_WSAIoctl || curFunc == (void*)GetProcAddress(g_hWs2, "WSAIoctl")) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_WSAIoctl;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (curFunc == (void*)g_Orig_getaddrinfo || curFunc == (void*)GetProcAddress(g_hWs2, "getaddrinfo")) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_getaddrinfo;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (curFunc == (void*)g_Orig_GetAddrInfoW || curFunc == (void*)GetProcAddress(g_hWs2, "GetAddrInfoW")) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_GetAddrInfoW;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (curFunc == (void*)g_Orig_gethostbyname || curFunc == (void*)GetProcAddress(g_hWs2, "gethostbyname") || curFunc == (void*)GetProcAddress(g_hWs2, (LPCSTR)52)) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_gethostbyname;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (g_Orig_GetAddrInfoExA && (curFunc == (void*)g_Orig_GetAddrInfoExA || curFunc == (void*)GetProcAddress(g_hWs2, "GetAddrInfoExA"))) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_GetAddrInfoExA;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
                else if (g_Orig_GetAddrInfoExW && (curFunc == (void*)g_Orig_GetAddrInfoExW || curFunc == (void*)GetProcAddress(g_hWs2, "GetAddrInfoExW"))) {
                    DWORD oldProt;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProt);
                    pThunk->u1.Function = (ULONG_PTR)Hook_GetAddrInfoExW;
                    VirtualProtect(&pThunk->u1.Function, sizeof(void*), oldProt, &oldProt);
                }
            }
        }
    } __except(EXCEPTION_EXECUTE_HANDLER) {}
}

static void PatchAllModules() {
    HMODULE hMods[1024];
    DWORD cbNeeded;
    HANDLE hProcess = GetCurrentProcess();
    if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded)) {
        DWORD count = cbNeeded / sizeof(HMODULE);
        for (DWORD i = 0; i < count; i++) {
            PatchIATForModule(hMods[i]);
        }
    }
}

static void ReapplyHooks() {
    for (int i = 0; i < g_SavedHookCount; i++) {
        BYTE* p = (BYTE*)g_SavedHooks[i].target;
        if (!p) continue;
        if (p[0] == 0xFF && p[1] == 0x25) continue;
        Log("[MikuSB-Patch] Hook %s was restored, re-applying", g_SavedHooks[i].name);
        PlaceHook14(g_SavedHooks[i].target, g_SavedHooks[i].detour, g_SavedHooks[i].stolen, g_SavedHooks[i].orig);
    }
}

static void PatchOfficialIpInMemory() {
    static const char kFrom[] = "42.192.24.211";
    static const char kTo[] = "127.0.0.1";
    static const wchar_t kFromW[] = L"42.192.24.211";
    static const wchar_t kToW[] = L"127.0.0.1";
    MEMORY_BASIC_INFORMATION mbi;
    unsigned char* addr = NULL;
    int hits = 0;
    SIZE_T scanned = 0;

    while (VirtualQuery(addr, &mbi, sizeof(mbi))) {
        unsigned char* next = (unsigned char*)mbi.BaseAddress + mbi.RegionSize;
        DWORD prot = mbi.Protect & 0xFF;
        BOOL rw = (prot == PAGE_READWRITE || prot == PAGE_WRITECOPY || prot == PAGE_EXECUTE_READWRITE);
        if (mbi.State == MEM_COMMIT && rw && mbi.RegionSize > 0 && mbi.RegionSize <= 32ull * 1024 * 1024) {
            scanned += mbi.RegionSize;
            if (scanned > 256ull * 1024 * 1024) break;
            __try {
                BYTE* p = (BYTE*)mbi.BaseAddress;
                SIZE_T sz = mbi.RegionSize;
                for (SIZE_T i = 0; i + 13 <= sz; i++) {
                    if (p[i] == '4' && memcmp(p + i, kFrom, 13) == 0) {
                        memcpy(p + i, kTo, 10);
                        memset(p + i + 10, 0, 3);
                        hits++;
                    }
                }
                for (SIZE_T i = 0; i + 26 <= sz; i++) {
                    if (p[i] == '4' && p[i + 1] == 0 && memcmp(p + i, kFromW, 26) == 0) {
                        memcpy(p + i, kToW, 20);
                        memset(p + i + 20, 0, 6);
                        hits++;
                    }
                }
                for (SIZE_T i = 0; i + 8 <= sz; i++) {
                    if (p[i] == 2 && p[i + 1] == 0 && p[i + 2] == 0x14 && p[i + 3] == 0x50 &&
                        p[i + 4] == 42 && p[i + 5] == 192 && p[i + 6] == 24 && p[i + 7] == 211) {
                        p[i + 4] = 127;
                        p[i + 5] = 0;
                        p[i + 6] = 0;
                        p[i + 7] = 1;
                        hits++;
                    }
                }
            } __except (EXCEPTION_EXECUTE_HANDLER) {
            }
        }
        if (next <= addr) break;
        addr = next;
    }
    if (hits)
        Log("[MikuSB-Patch] Memory-patched official IP, hits=%d", hits);
}

static DWORD WINAPI WatcherThread(LPVOID lpParam) {
    Sleep(3000);
    for (int i = 0; ; i++) {
        Sleep(i < 120 ? 500 : 2000);
        PatchAllModules();
        ReapplyHooks();
        PatchOfficialIpInMemory();
        if (!g_ConnectExHooked)
            InstallConnectExHook();
    }
    return 0;
}

static void InstallWs2Hooks() {
    if (!g_hWs2) return;
    void* pConnect      = (void*)GetProcAddress(g_hWs2, "connect");
    void* pWSAConnect   = (void*)GetProcAddress(g_hWs2, "WSAConnect");
    void* pOrd30        = (void*)GetProcAddress(g_hWs2, (LPCSTR)30);
    void* pWSAIoctl     = (void*)GetProcAddress(g_hWs2, "WSAIoctl");
    void* pOrd66        = (void*)GetProcAddress(g_hWs2, (LPCSTR)66);
    void* pGetAddrInfo  = (void*)GetProcAddress(g_hWs2, "getaddrinfo");
    void* pGetAddrInfoW = (void*)GetProcAddress(g_hWs2, "GetAddrInfoW");
    void* pGhbName      = (void*)GetProcAddress(g_hWs2, "gethostbyname");

    PlaceHookNamed(pConnect,      (void*)Hook_connect,       16, (void**)&g_Orig_connect,      "connect");
    PlaceHookNamed(pWSAConnect,   (void*)Hook_WSAConnect,    16, (void**)&g_Orig_WSAConnect,   "WSAConnect");
    // Do NOT hook ordinal 30/66 by number. On this Windows, ordinal 30 is
    // GetAddrInfoExW — treating it as WSAConnect made login DNS/connect return
    // SOCKET_ERROR with WSA 0 ("Immediate connect fail ... No error").
    if (pOrd30 && pOrd30 != pWSAConnect)
        Log("[MikuSB-Patch] skip ordinal 30 (%p) - not WSAConnect (%p)", pOrd30, pWSAConnect);
    PlaceHookNamed(pWSAIoctl,     (void*)Hook_WSAIoctl,      16, (void**)&g_Orig_WSAIoctl,     "WSAIoctl");
    if (pOrd66 && pOrd66 != pWSAIoctl)
        Log("[MikuSB-Patch] skip ordinal 66 (%p) - not WSAIoctl (%p)", pOrd66, pWSAIoctl);
    PlaceHookNamed(pGetAddrInfo,  (void*)Hook_getaddrinfo,   19, (void**)&g_Orig_getaddrinfo,  "getaddrinfo");
    PlaceHookNamed(pGetAddrInfoW, (void*)Hook_GetAddrInfoW,  21, (void**)&g_Orig_GetAddrInfoW, "GetAddrInfoW");
    PlaceHookNamed(pGhbName,      (void*)Hook_gethostbyname, 15, (void**)&g_Orig_gethostbyname,"gethostbyname");
    g_Orig_GetAddrInfoExA = (PFN_GetAddrInfoExA)GetProcAddress(g_hWs2, "GetAddrInfoExA");
    g_Orig_GetAddrInfoExW = (PFN_GetAddrInfoExW)GetProcAddress(g_hWs2, "GetAddrInfoExW");
    Log("[MikuSB-Patch] GetAddrInfoExA=%p GetAddrInfoExW=%p WSAConnect=%p (IAT only for Ex)",
        g_Orig_GetAddrInfoExA, g_Orig_GetAddrInfoExW, pWSAConnect);

    HMODULE hNtdll = GetModuleHandleA("ntdll.dll");
    if (hNtdll) {
        void* pIoctl = (void*)GetProcAddress(hNtdll, "NtDeviceIoControlFile");
        PlaceHookNamed(pIoctl, (void*)Hook_NtDeviceIoControlFile, 16, (void**)&g_Orig_NtDeviceIoControlFile, "NtDeviceIoControlFile");
    }
}

static void InstallConnectExHook() {
    if (g_ConnectExHooked) return;

    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET) {
        Log("[MikuSB-Patch] ConnectEx resolve: socket() failed wsa=%d", WSAGetLastError());
        return;
    }

    GUID guidConnectEx = WSAID_CONNECTEX;
    PFN_ConnectEx pfn = NULL;
    DWORD bytes = 0;
    PFN_WSAIoctl ioctl = g_Orig_WSAIoctl ? g_Orig_WSAIoctl : WSAIoctl;
    int r = ioctl(s, SIO_GET_EXTENSION_FUNCTION_POINTER,
                  &guidConnectEx, sizeof(guidConnectEx),
                  &pfn, sizeof(pfn), &bytes, NULL, NULL);
    closesocket(s);

    Log("[MikuSB-Patch] Resolve ConnectEx ret=%d pfn=%p", r, pfn);
    if (r != 0 || !pfn) return;

    if (PlaceHookNamed(pfn, (void*)Hook_ConnectEx, 15, (void**)&g_Orig_ConnectEx, "ConnectEx")) {
        InterlockedExchange(&g_ConnectExHooked, 1);
    }
}

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved) {
    if (fdwReason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(hinstDLL);
        InitLogging();

        g_hWs2 = GetModuleHandleA("ws2_32.dll");
        if (!g_hWs2) g_hWs2 = LoadLibraryA("ws2_32.dll");

        InstallWs2Hooks();
        InstallConnectExHook();
        PatchAllModules();
        CreateThread(NULL, 0, WatcherThread, NULL, 0, NULL);
        Log("[MikuSB-Patch] Full interceptor v4.3 (no ord30 smash, 18443->13443) initialized successfully.");
    }
    return TRUE;
}
