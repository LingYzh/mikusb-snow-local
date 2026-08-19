#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <string.h>

int main() {
    HANDLE h = CreateFileA("D:\\Snow\\data\\game\\Game\\Binaries\\Win64\\Game.exe", GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL);
    HANDLE map = CreateFileMappingA(h, NULL, PAGE_READONLY, 0, 0, NULL);
    BYTE* data = (BYTE*)MapViewOfFile(map, FILE_MAP_READ, 0, 0, 0);
    LARGE_INTEGER sz; GetFileSizeEx(h, &sz);

    unsigned long long offs[] = { 0x5B58FF2, 0x5B59360, 0x5FA931B, 0x5FA76A6 };
    for (int i = 0; i < 4; i++) {
        unsigned long long off = offs[i];
        printf("==== context @ 0x%llX ====\n", off);
        BYTE* p = data + off - 200;
        if (p < data) p = data;
        BYTE* end = data + off + 400;
        if (end > data + sz.QuadPart) end = data + sz.QuadPart;
        for (; p < end; p++) {
            char c = (*p >= 32 && *p < 127) ? (char)*p : (*p == 0 ? '|' : '.');
            putchar(c);
        }
        putchar('\n');
    }

    // also scan for 42.192 as utf16 pieces and dotted IP-like
    printf("==== ascii IPs 42. ====\n");
    int found = 0;
    for (BYTE* p = data; p < data + sz.QuadPart - 8; p++) {
        if (p[0]=='4' && p[1]=='2' && p[2]=='.') {
            int ok = 1;
            for (int i = 0; i < 12 && p[i]; i++) {
                if (!((p[i]>='0'&&p[i]<='9') || p[i]=='.')) { ok = 0; break; }
            }
            if (ok) {
                printf("  @0x%llX %s\n", (unsigned long long)(p-data), p);
                if (++found >= 20) break;
            }
        }
    }
    printf("==== utf16 IPs 42. ====\n");
    found = 0;
    for (BYTE* p = data; p < data + sz.QuadPart - 16; p++) {
        if (p[0]=='4' && p[1]==0 && p[2]=='2' && p[3]==0 && p[4]=='.' && p[5]==0) {
            printf("  @0x%llX ", (unsigned long long)(p-data));
            for (int i = 0; i < 40; i+=2) {
                if (!p[i]) break;
                putchar(p[i]);
            }
            putchar('\n');
            if (++found >= 20) break;
        }
    }
    return 0;
}
