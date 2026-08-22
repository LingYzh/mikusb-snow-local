#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <mswsock.h>
#include <windows.h>
#include <stdio.h>
#pragma comment(lib, "ws2_32.lib")

typedef int (WSAAPI *PFN_WSAIoctl)(SOCKET, DWORD, LPVOID, DWORD, LPVOID, DWORD, LPDWORD, LPWSAOVERLAPPED, LPWSAOVERLAPPED_COMPLETION_ROUTINE);
typedef BOOL (PASCAL *PFN_ConnectEx)(SOCKET, const struct sockaddr*, int, PVOID, DWORD, LPDWORD, LPOVERLAPPED);

static PFN_WSAIoctl g_Orig_WSAIoctl = NULL;
static PFN_WSAIoctl g_Orig_ord66 = NULL;
static PFN_ConnectEx g_Orig_ConnectEx = NULL;

static BOOL PASCAL Hook_ConnectEx(
    SOCKET s,
    const struct sockaddr* name,
    int namelen,
    PVOID lpSendBuffer,
    DWORD dwSendDataLength,
    LPDWORD lpdwBytesSent,
    LPOVERLAPPED lpOverlapped)
{
    printf("[Hook_ConnectEx] CALLED! Intercepted socket connect!\n");
    return TRUE;
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
    PFN_WSAIoctl fn = g_Orig_WSAIoctl ? g_Orig_WSAIoctl : (g_Orig_ord66 ? g_Orig_ord66 : WSAIoctl);
    int ret = fn(s, dwIoControlCode, lpvInBuffer, cbInBuffer, lpvOutBuffer, cbOutBuffer, lpcbBytesReturned, lpOverlapped, lpCompletionRoutine);

    if (ret == 0 && dwIoControlCode == SIO_GET_EXTENSION_FUNCTION_POINTER && lpvInBuffer && cbInBuffer >= sizeof(GUID) && lpvOutBuffer && cbOutBuffer >= sizeof(void*)) {
        GUID* pGuid = (GUID*)lpvInBuffer;
        GUID guidConnectEx = WSAID_CONNECTEX;
        if (memcmp(pGuid, &guidConnectEx, sizeof(GUID)) == 0) {
            LPFN_CONNECTEX* ppFn = (LPFN_CONNECTEX*)lpvOutBuffer;
            if (*ppFn && *ppFn != Hook_ConnectEx) {
                g_Orig_ConnectEx = *ppFn;
                *ppFn = Hook_ConnectEx;
                printf("[Hook_WSAIoctl] CONNECTEX EXTENSION INTERCEPTED SUCCESSFULLY!\n");
            }
        }
    }
    return ret;
}

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

int main() {
    WSADATA wsa;
    WSAStartup(MAKEWORD(2,2), &wsa);
    HMODULE hWs2 = LoadLibraryA("ws2_32.dll");

    PlaceHook14((void*)GetProcAddress(hWs2, "WSAIoctl"), (void*)Hook_WSAIoctl, 16, (void**)&g_Orig_WSAIoctl);
    PlaceHook14((void*)GetProcAddress(hWs2, (LPCSTR)66), (void*)Hook_WSAIoctl, 16, (void**)&g_Orig_ord66);

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    GUID guidConnectEx = WSAID_CONNECTEX;
    LPFN_CONNECTEX lpfnConnectEx = NULL;
    DWORD dwBytes = 0;

    WSAIoctl(s, SIO_GET_EXTENSION_FUNCTION_POINTER,
             &guidConnectEx, sizeof(guidConnectEx),
             &lpfnConnectEx, sizeof(lpfnConnectEx),
             &dwBytes, NULL, NULL);

    struct sockaddr_in sin = {0};
    sin.sin_family = AF_INET;
    sin.sin_port = htons(5200);
    sin.sin_addr.s_addr = inet_addr("42.192.24.211");

    lpfnConnectEx(s, (const struct sockaddr*)&sin, sizeof(sin), NULL, 0, NULL, NULL);
    closesocket(s);
    WSACleanup();

    printf("ALL CONNECTEX TESTS PASSED!\n");
    return 0;
}
