import os
import subprocess
import struct
import time

RECUP_DIR = os.path.dirname(os.path.abspath(__file__))
EXE_PATH = os.path.join(RECUP_DIR, 'RecupBD.exe')
CS_PATH = os.path.join(RECUP_DIR, 'RecupBD.cs')
CSC_PATH = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if not os.path.exists(CSC_PATH):
    CSC_PATH = r'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'

def stop_process():
    subprocess.run(['powershell', '-Command', 'Get-Process RecupBD -ErrorAction SilentlyContinue | Stop-Process -Force'], capture_output=True)
    time.sleep(0.5)

def compile_cs():
    cmd = [
        CSC_PATH,
        '/nologo',
        '/codepage:65001',
        '/target:winexe',
        '/win32icon:app_icon.ico',
        '/win32manifest:app.manifest',
        '/r:System.ServiceProcess.dll',
        f'/out:{EXE_PATH}',
        CS_PATH
    ]
    res = subprocess.run(cmd, cwd=RECUP_DIR, capture_output=True, text=True)
    if res.returncode != 0:
        print('Compile error:', res.stderr or res.stdout)
        return False
    print('Compilation succeeded.')
    return True

def set_language_pt_br():
    with open(EXE_PATH, 'rb') as f:
        data = bytearray(f.read())

    pe_offset = struct.unpack_from('<I', data, 0x3c)[0]
    opt_header_offset = pe_offset + 24
    rsrc_rva = struct.unpack_from('<I', data, opt_header_offset + 96 + 2*8)[0]
    num_sections = struct.unpack_from('<H', data, pe_offset + 6)[0]
    sec_offset = pe_offset + 24 + struct.unpack_from('<H', data, pe_offset + 20)[0]

    rsrc_raw = 0
    for i in range(num_sections):
        v_size, v_rva, r_size, r_offset = struct.unpack_from('<IIII', data, sec_offset + i*40 + 8)
        if v_rva == rsrc_rva:
            rsrc_raw = r_offset
            break

    def parse_dir(offset):
        num_named, num_id = struct.unpack_from('<HH', data, offset + 12)
        entries = []
        base = offset + 16
        for i in range(num_named + num_id):
            name_id, sub_off = struct.unpack_from('<II', data, base + i*8)
            entries.append((name_id, sub_off, base + i*8))
        return entries

    for tid, t_sub, _ in parse_dir(rsrc_raw):
        if tid == 16: # RT_VERSION
            for nid, n_sub, _ in parse_dir(rsrc_raw + (t_sub & 0x7FFFFFFF)):
                for lid, l_sub, l_ent_off in parse_dir(rsrc_raw + (n_sub & 0x7FFFFFFF)):
                    struct.pack_into('<I', data, l_ent_off, 0x0416) # LangID = 0x0416 (pt-BR)

    pos_trans = data.find(b'T\x00r\x00a\x00n\x00s\x00l\x00a\x00t\x00i\x00o\x00n\x00')
    if pos_trans != -1:
        t_val = data.find(b'\x00\x00\xb0\x04', pos_trans)
        if t_val != -1:
            data[t_val : t_val + 4] = b'\x16\x04\xb0\x04'

    pos_str = data.find(b'0\x000\x000\x000\x000\x004\x00b\x000\x00')
    if pos_str != -1:
        data[pos_str : pos_str + 16] = b'0\x004\x001\x006\x000\x004\x00b\x000\x00'

    with open(EXE_PATH, 'wb') as f:
        f.write(data)

    print('Language set to Português (Brasil) [0x0416].')

if __name__ == '__main__':
    stop_process()
    if compile_cs():
        set_language_pt_br()
