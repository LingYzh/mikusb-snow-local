#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <string.h>

static void PrintUtf16Around(BYTE* data, LARGE_INTEGER sz, const wchar_t* needle) {
    size_t nlen = wcslen(needle) * 2;
    BYTE* end = data + sz.QuadPart - (long long)nlen;
    int found = 0;
    for (BYTE* p = data; p < end; p++) {
        if (memcmp(p, needle, nlen) == 0) {
            printf("==== utf16 '%ls' @ 0x%llX ====\n", needle, (unsigned long long)(p - data));
            BYTE* start = p - 120;
            if (start < data) start = data;
            BYTE* stop = p + nlen + 200;
            if (stop > data + sz.QuadPart) stop = data + sz.QuadPart;
            for (BYTE* q = start; q < stop; q += 2) {
                wchar_t c = *(wchar_t*)q;
                if (c >= 32 && c < 127) putchar((char)c);
                else if (c == 0) putchar('|');
                else putchar('.');
            }
            putchar('\n');
            if (++found >= 4) break;
        }
    }
    if (!found) printf("not found: %ls\n", needle);
}

int main() {
    HANDLE h = CreateFileA("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\Game.exe", GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    HANDLE map = CreateFileMappingA(h, NULL, PAGE_READONLY, 0, 0, NULL);
    BYTE* data = (BYTE*)MapViewOfFile(map, FILE_MAP_READ, 0, 0, 0);
    LARGE_INTEGER sz; GetFileSizeEx(h, &sz);

    PrintUtf16Around(data, sz, L"servers.txt");
    PrintUtf16Around(data, sz, L"ForceWithDevelopmentServers");
    PrintUtf16Around(data, sz, L"OnIpRegionReady");
    PrintUtf16Around(data, sz, L"CachedLoginServer");
    PrintUtf16Around(data, sz, L"development");
    PrintUtf16Around(data, sz, L"127.0.0.1");
    PrintUtf16Around(data, sz, L"localhost");
    PrintUtf16Around(data, sz, L"LoadSetting");
    PrintUtf16Around(data, sz, L"FConnection::Connect");
    PrintUtf16Around(data, sz, L"already registered handler");
    return 0;
}
