"""MystTiq: read the English (or another language's) item and Pal display names from the server's own game pak.

Read-only. Parses the Unreal pak (version 11) index, extracts two localisation tables, and prints JSON:
    {"version": 1, "lang": "en", "items": {"<id>": "<name>"}, "pals": {"<id>": "<name>"}}
The tables' keys are ITEM_NAME_<id>_TextData and PAL_NAME_<id>_TextData, where <id> is the id the world save and the
give commands use. Needs Python 3 and the "ooz" module (Oodle), which the PlM/Oodle save tooling already installs.
Nothing from the game is stored by MystTiq except this name list, written next to MystTiq's own runtime data.

Exit codes: 0 ok, 2 ooz missing, 3 pak not supported, 4 tables not found, 5 read error.
"""
import argparse, json, struct, sys

TABLE_DIR = "Pal/Content/L10N/{lang}/Pal/DataTable/Text/"
TABLES = {"items": ("DT_ItemNameText_Common", "ITEM_NAME_"), "pals": ("DT_PalNameText_Common", "PAL_NAME_")}
PAK_MAGIC = 0x5A6F12E1
FOOTER_SIZE = 221  # v11: guid 16, encrypted 1, magic 4, version 4, offset 8, size 8, hash 20, 5 x 32 method names


class Reader:
    def __init__(self, data, offset=0): self.data, self.o = data, offset
    def take(self, fmt):
        v = struct.unpack_from(fmt, self.data, self.o)[0]; self.o += struct.calcsize(fmt); return v
    def raw(self, n): v = self.data[self.o:self.o + n]; self.o += n; return v
    def fstring(self):
        n = self.take("<i")
        if n == 0: return ""
        if n > 0: return self.raw(n)[:-1].decode("utf-8", "replace")
        return self.raw(-n * 2)[:-2].decode("utf-16-le", "replace")


class Fail(Exception):
    def __init__(self, code, message): super().__init__(message); self.code = code


def read_index(f):
    f.seek(0, 2); size = f.tell()
    if size < FOOTER_SIZE: raise Fail(3, "the file is too small to be a pak")
    f.seek(size - FOOTER_SIZE); foot = Reader(f.read(FOOTER_SIZE))
    foot.raw(16); encrypted = foot.take("<B"); magic = foot.take("<I"); version = foot.take("<i")
    index_offset = foot.take("<q"); index_size = foot.take("<q"); foot.raw(20)
    methods = [foot.raw(32).split(b"\0")[0].decode("ascii", "replace") for _ in range(5)]
    if magic != PAK_MAGIC: raise Fail(3, "not an Unreal pak (magic mismatch)")
    if version != 11: raise Fail(3, f"pak version {version} is not supported (only 11)")
    if encrypted: raise Fail(3, "the pak index is encrypted")
    f.seek(index_offset); idx = Reader(f.read(index_size))
    idx.fstring(); idx.take("<i"); idx.take("<Q")
    if idx.take("<I"): idx.take("<q"); idx.take("<q"); idx.raw(20)
    if not idx.take("<I"): raise Fail(3, "the pak has no full directory index")
    dir_offset = idx.take("<q"); dir_size = idx.take("<q"); idx.raw(20)
    encoded = idx.raw(idx.take("<i"))
    f.seek(dir_offset); d = Reader(f.read(dir_size))
    files = {}
    for _ in range(d.take("<i")):
        directory = d.fstring()
        for _ in range(d.take("<i")):
            name = d.fstring(); files[directory + name] = d.take("<i")
    return files, encoded, methods


def entry_offset(encoded, position):
    r = Reader(encoded, position); bits = r.take("<I")
    if (bits & 0x3F) == 0x3F: r.take("<I")
    return r.take("<I") if bits & (1 << 31) else r.take("<Q")


def read_file(f, offset, methods):
    f.seek(offset); r = Reader(f.read(4096))
    r.take("<q"); r.take("<q"); size = r.take("<q"); method = r.take("<I"); r.raw(20)
    blocks = [(r.take("<q"), r.take("<q")) for _ in range(r.take("<I"))] if method else []
    r.take("<B"); block_size = r.take("<I")
    if not method:
        f.seek(offset + r.o); return f.read(size)
    if methods[method - 1] != "Oodle": raise Fail(3, f"compression {methods[method - 1]} is not supported")
    try:
        import ooz  # only needed for Oodle-compressed files (all of them in a real game pak)
    except ImportError:
        ooz = None
    # A folder named ooz without the built module imports as an empty namespace package (seen on the Linux test VM).
    if ooz is None or not hasattr(ooz, "decompress"):
        raise Fail(2, "the Python module 'ooz' (Oodle) is not installed")
    out = bytearray()
    for start, end in blocks:
        f.seek(offset + start)
        out += ooz.decompress(f.read(end - start), min(block_size, size - len(out)))
    if len(out) != size: raise Fail(5, "a table did not decompress to its recorded size")
    return bytes(out)


def fstring_at(data, o):
    n = struct.unpack_from("<i", data, o)[0]; o += 4
    if n > 0: return data[o:o + n - 1].decode("utf-8", "replace"), o + n
    if n < 0: return data[o:o - n * 2 - 2].decode("utf-16-le", "replace"), o - n * 2
    return "", o


# Each row's text is an FText saved as (namespace, key, source string); the namespace is the table's own name.
def names_from_table(data, namespace, prefix):
    ns = namespace.encode() + b"\0"; marker = struct.pack("<i", len(ns)) + ns
    out, i = {}, data.find(marker)
    while i >= 0:
        key, o = fstring_at(data, i + len(marker)); text, o = fstring_at(data, o)
        if key.startswith(prefix) and key.endswith("_TextData") and text.strip():
            out[key[len(prefix):-len("_TextData")]] = text.strip()
        i = data.find(marker, o)
    return out


def main():
    p = argparse.ArgumentParser(); p.add_argument("--pak", required=True); p.add_argument("--lang", default="en")
    a = p.parse_args()
    try:
        with open(a.pak, "rb") as f:
            files, encoded, methods = read_index(f)
            result = {"version": 1, "lang": a.lang}
            for kind, (table, prefix) in TABLES.items():
                path = TABLE_DIR.format(lang=a.lang) + table + ".uexp"
                if path not in files: raise Fail(4, f"{path} is not in the pak")
                result[kind] = names_from_table(read_file(f, entry_offset(encoded, files[path]), methods), table, prefix)
        json.dump(result, sys.stdout, ensure_ascii=True, sort_keys=True)
        return 0
    except Fail as e:
        print(e, file=sys.stderr); return e.code
    except (OSError, struct.error, ValueError) as e:
        print(f"read error: {e}", file=sys.stderr); return 5


if __name__ == "__main__":
    sys.exit(main())
