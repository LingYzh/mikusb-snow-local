#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <string.h>

static void ScanFile(const char* path) {
    HANDLE h = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        printf("open fail %s err=%lu\n", path, GetLastError());
        return;
    }
    LARGE_INTEGER sz;
    GetFileSizeEx(h, &sz);
    HANDLE map = CreateFileMappingA(h, NULL, PAGE_READONLY, 0, 0, NULL);
    BYTE* data = (BYTE*)MapViewOfFile(map, FILE_MAP_READ, 0, 0, 0);
    printf("==== %s size=%lld\n", path, sz.QuadPart);

    const char* needles[] = {
        "42.192.24.211",
        "servers.txt",
        "ConnectEx",
        "WSAConnect",
        "WSAIoctl",
        "mswsock",
        "LoginServer",
        NULL
    };

    for (int n = 0; needles[n]; n++) {
        const char* needle = needles[n];
        size_t nlen = strlen(needle);
        int found = 0;
        BYTE* p = data;
        BYTE* end = data + (size_t)sz.QuadPart - nlen;
        for (; p < end; p++) {
            if (memcmp(p, needle, nlen) == 0) {
                found++;
                if (found <= 10) {
                    BYTE* start = p - 40;
                    if (start < data) start = data;
                    BYTE* stop = p + nlen + 70;
                    if (stop > data + sz.QuadPart) stop = data + sz.QuadPart;
                    printf("  ascii '%s' @ 0x%llX: ", needle, (unsigned long long)(p - data));
                    for (BYTE* q = start; q < stop; q++) {
                        char c = (*q >= 32 && *q < 127) ? (char)*q : '.';
                        putchar(c);
                    }
                    putchar('\n');
                }
            }
        }
        if (!found)
            printf("  ascii '%s': not found\n", needle);
        else if (found > 10)
            printf("  ascii '%s': %d hits\n", needle, found);

        found = 0;
        p = data;
        end = data + (size_t)sz.QuadPart - nlen * 2;
        for (; p < end; p++) {
            int ok = 1;
            for (size_t i = 0; i < nlen; i++) {
                if (p[i * 2] != (BYTE)needle[i] || p[i * 2 + 1] != 0) {
                    ok = 0;
                    break;
                }
            }
            if (ok) {
                found++;
                if (found <= 6) {
                    printf("  utf16 '%s' @ 0x%llX\n", needle, (unsigned long long)(p - data));
                }
            }
        }
        if (!found)
            printf("  utf16 '%s': not found\n", needle);
        else if (found > 6)
            printf("  utf16 '%s': %d hits\n", needle, found);
    }

    UnmapViewOfFile(data);
    CloseHandle(map);
    CloseHandle(h);
}

int main() {
    ScanFile("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\Game.exe");
    ScanFile("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\GameBase.dll");
    return 0;
}
