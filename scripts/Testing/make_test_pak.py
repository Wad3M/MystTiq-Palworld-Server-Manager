"""MystTiq test fixture (v0.8.13.0): write a tiny Unreal pak (version 11, uncompressed) holding the two English name
tables the game-name extractor reads, with made-up rows. Used by Test-v0.8.13.0-RouteSmoke.ps1 so the smoke needs no
game files and no Oodle module.

    python make_test_pak.py <out.pak> <items-json> <pals-json>
"""
import json, struct, sys

MAGIC = 0x5A6F12E1
TABLE_DIR = "Pal/Content/L10N/en/Pal/DataTable/Text/"


def fstring(s):
    b = s.encode("utf-8") + b"\0"
    return struct.pack("<i", len(b)) + b


def table(namespace, prefix, rows):
    body = b"\x00" * 16  # stands in for the export header the extractor skips
    for rid, text in rows.items():
        body += fstring(namespace) + fstring(f"{prefix}{rid}_TextData") + fstring(text) + b"\x00" * 9
    return body


def main(out, items, pals):
    files = {
        TABLE_DIR + "DT_ItemNameText_Common.uexp": table("DT_ItemNameText_Common", "ITEM_NAME_", items),
        TABLE_DIR + "DT_PalNameText_Common.uexp": table("DT_PalNameText_Common", "PAL_NAME_", pals),
    }
    data = bytearray(); encoded = bytearray(); positions = {}
    for path, content in files.items():
        offset = len(data)
        # Serialized FPakEntry: offset, size, uncompressed size, method 0, hash, flags, block size; then the bytes.
        data += struct.pack("<qqqI", offset, len(content), len(content), 0) + b"\x00" * 20 + struct.pack("<BI", 0, 0)
        data += content
        positions[path] = len(encoded)
        # Encoded entry: offset, uncompressed size and size all 32-bit safe, no compression, no blocks.
        encoded += struct.pack("<III", (1 << 31) | (1 << 30) | (1 << 29), offset, len(content))
    dirs = {}
    for path, pos in positions.items():
        d, name = path.rsplit("/", 1)
        dirs.setdefault(d + "/", []).append((name, pos))
    fdi = struct.pack("<i", len(dirs))
    for d, entries in dirs.items():
        fdi += fstring(d) + struct.pack("<i", len(entries))
        for name, pos in entries: fdi += fstring(name) + struct.pack("<i", pos)
    index_offset = len(data)
    fdi_offset = index_offset + 0  # patched below once the primary index size is known
    primary = fstring("../../../") + struct.pack("<iQI", len(files), 0, 0)
    head_len = len(primary) + 4 + 8 + 8 + 20 + 4 + len(encoded) + 4
    fdi_offset = index_offset + head_len
    primary += struct.pack("<Iqq", 1, fdi_offset, len(fdi)) + b"\x00" * 20
    primary += struct.pack("<i", len(encoded)) + bytes(encoded) + struct.pack("<i", 0)
    assert len(primary) == head_len
    data += primary + fdi
    footer = b"\x00" * 16 + struct.pack("<BIiqq", 0, MAGIC, 11, index_offset, len(primary)) + b"\x00" * 20
    footer += b"Oodle".ljust(32, b"\x00") + b"\x00" * 128
    assert len(footer) == 221
    data += footer
    open(out, "wb").write(bytes(data))


if __name__ == "__main__":
    main(sys.argv[1], json.loads(sys.argv[2]), json.loads(sys.argv[3]))
