#include "hde64.h"
#include "table64.h"

unsigned int hde64_disasm(const void *code, hde64s *hs) {
    uint8_t x, c, *p = (uint8_t *)code, cflags, opcode, pref = 0;
    uint8_t *ht = table64, m_mod, m_reg, m_rm, disp_size = 0;

    memset(hs, 0, sizeof(hde64s));

    for (x = 16; x; x--) {
        c = *p++;
        if (c == 0xf3) hs->p_rep = c, pref |= F_PREFIX_REP;
        else if (c == 0xf2) hs->p_rep = c, pref |= F_PREFIX_REPNZ;
        else if (c == 0xf0) hs->p_lock = c, pref |= F_PREFIX_LOCK;
        else if (c == 0x26 || c == 0x2e || c == 0x36 || c == 0x3e || c == 0x64 || c == 0x65)
            hs->p_seg = c, pref |= F_PREFIX_SEG;
        else if (c == 0x66) hs->p_66 = c, pref |= F_PREFIX_66;
        else if (c == 0x67) hs->p_67 = c, pref |= F_PREFIX_67;
        else {
            if ((c & 0xf0) == 0x40) {
                hs->rex = c;
                hs->rex_w = (c & 8) >> 3;
                hs->rex_r = (c & 4) >> 2;
                hs->rex_x = (c & 2) >> 1;
                hs->rex_b = c & 1;
                if (x == 16) {
                    pref |= F_PREFIX_REX;
                    continue;
                }
            }
            break;
        }
    }

    hs->flags = pref;
    if (x == 0) { hs->flags |= F_ERROR | F_ERROR_LENGTH; return 0; }

    opcode = c;
    hs->opcode = opcode;

    if (opcode == 0x0f) {
        c = *p++;
        hs->opcode2 = c;
        ht += 256;
        opcode = c;
    }

    cflags = ht[opcode];

    if (cflags & C_MODRM) {
        c = *p++;
        hs->modrm = c;
        hs->modrm_mod = m_mod = c >> 6;
        hs->modrm_reg = m_reg = (c & 0x38) >> 3;
        hs->modrm_rm  = m_rm  = c & 7;

        if (m_mod != 3 && m_rm == 4) {
            c = *p++;
            hs->sib = c;
            hs->sib_scale = c >> 6;
            hs->sib_index = (c & 0x38) >> 3;
            hs->sib_base  = c & 7;
            if (hs->sib_base == 5 && m_mod == 0) disp_size = 4;
        }

        if (m_mod == 0 && m_rm == 5) disp_size = 4, hs->flags |= F_RELATIVE;
        else if (m_mod == 1) disp_size = 1;
        else if (m_mod == 2) disp_size = 4;

        if (disp_size) {
            hs->disp.disp32 = 0;
            memcpy(&hs->disp, p, disp_size);
            p += disp_size;
            hs->flags |= (disp_size == 1) ? F_DISP8 : F_DISP32;
        }
    }

    if (cflags & C_IMM_P66) {
        if (hs->p_66) {
            memcpy(&hs->imm.imm16, p, 2);
            p += 2;
            hs->flags |= F_IMM16;
        } else {
            memcpy(&hs->imm.imm32, p, 4);
            p += 4;
            hs->flags |= F_IMM32;
        }
    } else if (cflags & C_IMM16) {
        memcpy(&hs->imm.imm16, p, 2);
        p += 2;
        hs->flags |= F_IMM16;
    } else if (cflags & C_IMM8) {
        hs->imm.imm8 = *p++;
        hs->flags |= F_IMM8;
    }

    hs->len = (uint8_t)(p - (uint8_t *)code);
    return hs->len;
}
