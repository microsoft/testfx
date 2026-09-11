#!/usr/bin/env python3
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""Prepare an isolated, unsigned CodeCoverage package for Option 1 CI validation.

Only the pinned targets file and the nuspec version change. Unmodified ZIP
records are copied verbatim; modified entries use deterministic stored DEFLATE
blocks, avoiding compressor-version differences while retaining their original
compression method. ZIP timestamps, metadata, and entry order are preserved.
The raw GitHub target uses LF; the requested target digest uses CRLF. Both
representations are hash-pinned, with only that newline conversion permitted.
Nothing is installed, uploaded, or written outside the requested checkout folder.
"""

from __future__ import annotations

import argparse
import binascii
import hashlib
import io
import json
import os
from pathlib import Path
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


PACKAGE_ID = "Microsoft.CodeCoverage"
STOCK_VERSION = "18.10.0"
VERSION = "18.10.1-option1.9017a6fc"
COMMIT = "9017a6fcfd0dca983d4a0afc1c646b3f8ce5770c"
STOCK_SHA256 = "0ABB8709BB3D3F33B79B6FE5CA18D96E30DF31535C3BDF5B74CA21721BEF848E"
RAW_FIX_SHA256 = "02AEFF7E5E93EFD61CC9154118230008C80AD8512563ABD36A575565E24C2E2A"
FIX_SHA256 = "FB9D4C1A52897F3CD0524AC3EB053B56CBAD981D8E67BFBD20C7F0981533A519"
INDEX_URL = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"
FIX_URL = (
    f"https://raw.githubusercontent.com/microsoft/vstest/{COMMIT}/"
    "src/package/Microsoft.CodeCoverage/Microsoft.CodeCoverage.targets"
)
TARGET_ENTRY = "build/netstandard2.0/Microsoft.CodeCoverage.targets"
NUSPEC_ENTRY = "Microsoft.CodeCoverage.nuspec"
SIGNATURE_ENTRY = ".signature.p7s"
PACKAGE_FILENAME = f"{PACKAGE_ID}.{VERSION}.nupkg"
PROVENANCE_FILENAME = f"{PACKAGE_ID}.{VERSION}.provenance.json"
REPO_ROOT = Path(__file__).resolve().parents[2]
END_RECORD = struct.Struct("<4s4H2IH")
CENTRAL_HEADER_SIZE = 46
LOCAL_HEADER_SIZE = 30
METADATA_FIELDS = (
    "date_time", "compress_type", "comment", "extra", "create_system",
    "create_version", "extract_version", "reserved", "flag_bits", "volume",
    "internal_attr", "external_attr",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def verify_hash(data: bytes, expected: str, label: str) -> None:
    actual = sha256(data)
    require(actual == expected, f"{label} SHA256 mismatch: expected {expected}, got {actual}")


def download(url: str) -> bytes:
    require(urllib.parse.urlsplit(url).scheme == "https", f"Non-HTTPS source: {url}")
    for attempt in range(3):
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "testfx-option1-validation"})
            with urllib.request.urlopen(request, timeout=180) as response:
                require(
                    urllib.parse.urlsplit(response.url).scheme == "https",
                    f"Non-HTTPS download redirect: {response.url}",
                )
                return response.read()
        except (urllib.error.URLError, TimeoutError) as error:
            if isinstance(error, urllib.error.HTTPError) and error.code not in (408, 429, 500, 502, 503, 504):
                raise
            if attempt == 2:
                raise
            time.sleep(2 ** attempt)
    raise RuntimeError("Download attempts exhausted")


def stock_package_url() -> tuple[str, str]:
    index = json.loads(download(INDEX_URL))
    bases = []
    for resource in index["resources"]:
        types = resource.get("@type", [])
        if isinstance(types, str):
            types = [types]
        if "PackageBaseAddress/3.0.0" in types:
            bases.append(resource["@id"].rstrip("/"))
    require(len(set(bases)) == 1, "Expected one PackageBaseAddress/3.0.0 in dotnet-public index")
    base = bases[0]
    filename = f"{PACKAGE_ID}.{STOCK_VERSION}.nupkg".lower()
    return base, f"{base}/{PACKAGE_ID.lower()}/{STOCK_VERSION}/{filename}"


def read_records(data: bytes) -> tuple[list[tuple[zipfile.ZipInfo, bytes, bytes]], bytes]:
    """Read the pinned package's ordinary, single-disk ZIP without normalizing headers."""
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        entries = archive.infolist()
        require(len({entry.filename for entry in entries}) == len(entries), "Duplicate ZIP entries")
        end_offset = len(data) - END_RECORD.size - len(archive.comment)
        end = data[end_offset:]
        signature, disk, central_disk, disk_count, count, central_size, central_offset, comment_size = (
            END_RECORD.unpack_from(end)
        )
        require(signature == b"PK\x05\x06", "Missing ZIP end record")
        require(disk == central_disk == 0 and disk_count == count == len(entries), "Unsupported ZIP layout")
        require(count < 0xFFFF and central_offset < 0xFFFFFFFF, "ZIP64 is not supported")
        require(central_offset + central_size == end_offset, "Unexpected ZIP central directory layout")
        require(comment_size == len(archive.comment), "Invalid ZIP comment length")

        records = []
        cursor = central_offset
        local_cursor = 0
        for entry in entries:
            require(entry.header_offset == local_cursor, f"Unexpected ZIP entry order: {entry.filename}")
            require(entry.flag_bits == 0, f"Unsupported ZIP flags: {entry.filename}")
            require(entry.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED), "Unsupported compression")
            require(data[cursor:cursor + 4] == b"PK\x01\x02", "Missing central header")
            name_size, extra_size, comment_size = struct.unpack_from("<3H", data, cursor + 28)
            record_size = CENTRAL_HEADER_SIZE + name_size + extra_size + comment_size
            central = data[cursor:cursor + record_size]
            require(len(central) == record_size, "Truncated central header")
            cursor += record_size

            require(data[local_cursor:local_cursor + 4] == b"PK\x03\x04", "Missing local header")
            name_size, extra_size = struct.unpack_from("<2H", data, local_cursor + 26)
            local_size = LOCAL_HEADER_SIZE + name_size + extra_size + entry.compress_size
            local = data[local_cursor:local_cursor + local_size]
            require(len(local) == local_size, "Truncated local record")
            require(
                struct.unpack_from("<3I", local, 14) == (entry.CRC, entry.compress_size, entry.file_size),
                f"Local/central size or checksum disagreement: {entry.filename}",
            )
            local_cursor += local_size
            records.append((entry, local, central))
        require(cursor == end_offset and local_cursor == central_offset, "Unexpected data between ZIP records")
        return records, end


def stored_deflate(data: bytes) -> bytes:
    """Encode method 8 with RFC 1951 stored blocks, independent of zlib versions."""
    blocks = []
    for offset in range(0, max(1, len(data)), 65535):
        block = data[offset:offset + 65535]
        final = int(offset + len(block) == len(data))
        blocks.append(struct.pack("<BHH", final, len(block), len(block) ^ 0xFFFF) + block)
    return b"".join(blocks)


def replace_nuspec_version(data: bytes) -> bytes:
    metadata = ET.fromstring(data).find("{*}metadata")
    require(metadata is not None, "Missing nuspec metadata")
    require(metadata.findtext("{*}id") == PACKAGE_ID, "Unexpected nuspec package ID")
    require(metadata.findtext("{*}version") == STOCK_VERSION, "Unexpected stock nuspec version")
    original = f"<version>{STOCK_VERSION}</version>".encode("ascii")
    replacement = f"<version>{VERSION}</version>".encode("ascii")
    require(data.count(original) == 1, "Expected exactly one stock nuspec version element")
    return data.replace(original, replacement, 1)


def repack(stock: bytes, replacements: dict[str, bytes]) -> bytes:
    records, end = read_records(stock)
    require(
        {TARGET_ENTRY, NUSPEC_ENTRY, SIGNATURE_ENTRY}.issubset(entry.filename for entry, _, _ in records),
        "Stock package is missing the expected target, nuspec, or signature",
    )
    local_records = []
    central_records = []
    offset = 0
    for entry, local, central in records:
        if entry.filename == SIGNATURE_ENTRY:
            continue
        central = bytearray(central)
        if entry.filename in replacements:
            contents = replacements[entry.filename]
            compressed = stored_deflate(contents) if entry.compress_type == zipfile.ZIP_DEFLATED else contents
            name_size, extra_size = struct.unpack_from("<2H", local, 26)
            header_size = LOCAL_HEADER_SIZE + name_size + extra_size
            header = bytearray(local[:header_size])
            sizes = (binascii.crc32(contents) & 0xFFFFFFFF, len(compressed), len(contents))
            struct.pack_into("<3I", header, 14, *sizes)
            struct.pack_into("<3I", central, 16, *sizes)
            local = bytes(header) + compressed
        struct.pack_into("<I", central, 42, offset)
        local_records.append(local)
        central_records.append(bytes(central))
        offset += len(local)
    central_directory = b"".join(central_records)
    end = bytearray(end)
    struct.pack_into("<2H2I", end, 8, len(central_records), len(central_records), len(central_directory), offset)
    return b"".join(local_records) + central_directory + bytes(end)


def verify_package(stock: bytes, output: bytes, replacements: dict[str, bytes]) -> dict:
    old_records, old_end = read_records(stock)
    new_records, new_end = read_records(output)
    old_by_name = {entry.filename: (entry, local, central) for entry, local, central in old_records}
    expected_names = [entry.filename for entry, _, _ in old_records if entry.filename != SIGNATURE_ENTRY]
    require([entry.filename for entry, _, _ in new_records] == expected_names, "Output entry list/order differs")
    require(old_end[:8] == new_end[:8] and old_end[20:] == new_end[20:], "ZIP archive metadata changed")
    changes = []
    unchanged_bytes = 0
    with zipfile.ZipFile(io.BytesIO(stock)) as before, zipfile.ZipFile(io.BytesIO(output)) as after:
        for entry, local, central in new_records:
            old_entry, old_local, old_central = old_by_name[entry.filename]
            original = before.read(old_entry)
            actual = after.read(entry)
            expected = replacements.get(entry.filename, original)
            require(actual == expected, f"Unexpected content: {entry.filename}")
            for field in METADATA_FIELDS:
                require(
                    getattr(old_entry, field) == getattr(entry, field),
                    f"ZIP {field} changed: {entry.filename}",
                )
            require(
                old_central[:16] == central[:16] and old_central[28:42] == central[28:42]
                and old_central[46:] == central[46:],
                f"Central ZIP metadata changed: {entry.filename}",
            )
            require(
                old_local[:14] == local[:14] and old_local[26:30] == local[26:30],
                f"Local ZIP metadata changed: {entry.filename}",
            )
            name_size, extra_size = struct.unpack_from("<2H", local, 26)
            header_size = LOCAL_HEADER_SIZE + name_size + extra_size
            require(old_local[30:header_size] == local[30:header_size], f"Local name/extra changed: {entry.filename}")
            if entry.filename in replacements:
                require(actual != original, f"Expected modified entry is unchanged: {entry.filename}")
                changes.append({
                    "entry": entry.filename,
                    "change": "modified",
                    "stock_sha256": sha256(original),
                    "output_sha256": sha256(actual),
                    "stock_bytes": len(original),
                    "output_bytes": len(actual),
                    "zip_timestamp": list(entry.date_time),
                })
            else:
                require(old_local == local, f"Unmodified compressed record changed: {entry.filename}")
                require(old_central[:42] == central[:42], f"Unmodified central header changed: {entry.filename}")
                unchanged_bytes += len(actual)
        signature = before.read(SIGNATURE_ENTRY)
        changes.append({
            "entry": SIGNATURE_ENTRY,
            "change": "removed",
            "stock_sha256": sha256(signature),
            "stock_bytes": len(signature),
        })
    require(len(changes) == 3, "Expected exactly two modified entries and one removed signature")
    return {
        "changed_entries": changes,
        "counts": {
            "stock_entries": len(old_records),
            "output_entries": len(new_records),
            "modified_entries": 2,
            "removed_entries": 1,
            "added_entries": 0,
            "unchanged_entries": len(new_records) - 2,
            "unchanged_uncompressed_bytes": unchanged_bytes,
            "preserved_timestamp_entries": len(new_records),
            "preserved_metadata_entries": len(new_records),
        },
        "verification": {
            "content_equivalence": True,
            "timestamp_equivalence": True,
            "metadata_equivalence": True,
            "entry_order_preserved": True,
            "unmodified_compressed_records_preserved": True,
            "signature_removed": True,
        },
    }


def prepare(output_directory: Path) -> dict:
    output_directory = output_directory.resolve()
    require(REPO_ROOT in output_directory.parents, "Output directory must be inside this checkout, not a shared cache")
    base, stock_url = stock_package_url()
    stock = download(stock_url)
    verify_hash(stock, STOCK_SHA256, "Stock package")
    raw_fixed_target = download(FIX_URL)
    verify_hash(raw_fixed_target, RAW_FIX_SHA256, "Raw fixed target")
    fixed_target = raw_fixed_target.replace(b"\n", b"\r\n")
    verify_hash(fixed_target, FIX_SHA256, "Fixed target")
    with zipfile.ZipFile(io.BytesIO(stock)) as archive:
        stock_target_hash = sha256(archive.read(TARGET_ENTRY))
        replacements = {
            TARGET_ENTRY: fixed_target,
            NUSPEC_ENTRY: replace_nuspec_version(archive.read(NUSPEC_ENTRY)),
        }
    output = repack(stock, replacements)
    output_hash = sha256(output)
    output_directory.mkdir(parents=True, exist_ok=True)
    package_path = output_directory / PACKAGE_FILENAME
    provenance_path = output_directory / PROVENANCE_FILENAME
    if package_path.exists():
        verify_hash(package_path.read_bytes(), output_hash, "Existing validation package")
    pending_package = output_directory / f".{PACKAGE_FILENAME}.{os.getpid()}.partial"
    pending_provenance = output_directory / f".{PROVENANCE_FILENAME}.{os.getpid()}.partial"
    try:
        with pending_package.open("xb") as stream:
            stream.write(output)
        written = pending_package.read_bytes()
        verify_hash(written, output_hash, "Written validation package")
        verification = verify_package(stock, written, replacements)
        provenance = {
            "schema_version": 1,
            "package_id": PACKAGE_ID,
            "package_version": VERSION,
            "stock": {
                "version": STOCK_VERSION,
                "index_url": INDEX_URL,
                "package_base_address": base,
                "url": stock_url,
                "sha256": sha256(stock),
                "expected_sha256": STOCK_SHA256,
                "bytes": len(stock),
                "target_entry": TARGET_ENTRY,
                "target_sha256": stock_target_hash,
            },
            "fixed_target": {
                "commit": COMMIT,
                "url": FIX_URL,
                "sha256": sha256(fixed_target),
                "expected_sha256": FIX_SHA256,
                "bytes": len(fixed_target),
                "raw_sha256": sha256(raw_fixed_target),
                "expected_raw_sha256": RAW_FIX_SHA256,
                "raw_bytes": len(raw_fixed_target),
                "newline_conversion": "LF to CRLF; no other target changes",
            },
            "output": {
                "filename": PACKAGE_FILENAME,
                "sha256": output_hash,
                "bytes": len(written),
                "signed": False,
                "repack_algorithm": "verbatim ZIP records; stored DEFLATE blocks for changed entries",
            },
            **verification,
        }
        provenance_bytes = (json.dumps(provenance, indent=2, sort_keys=True) + "\n").encode("utf-8")
        with pending_provenance.open("xb") as stream:
            stream.write(provenance_bytes)
        pending_package.replace(package_path)
        pending_provenance.replace(provenance_path)
    finally:
        pending_package.unlink(missing_ok=True)
        pending_provenance.unlink(missing_ok=True)
    return provenance


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-directory", type=Path, required=True,
        help="Local feed directory inside this checkout (never a shared NuGet cache).",
    )
    args = parser.parse_args()
    try:
        provenance = prepare(args.output_directory)
    except (OSError, ValueError, KeyError, struct.error, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"Option 1 package preparation FAILED: {error}", file=sys.stderr)
        return 1
    print(json.dumps(provenance, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
