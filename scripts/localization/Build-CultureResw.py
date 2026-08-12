"""Exports translation worksheets and builds a culture's .resw tables from them.

The tr-TR folder is the source of truth for which names exist. Translating a
language therefore has two steps:

    python scripts/localization/Build-CultureResw.py --export az-Latn-AZ
    # fill in artifacts/localization/translations/az-Latn-AZ-<table>-<nn>.tsv
    python scripts/localization/Build-CultureResw.py --build az-Latn-AZ

The worksheet keeps the Turkish and English text side by side so a translator
sees both. The build step refuses to write a table unless every name is present,
no value is blank, and the format placeholders match the Turkish text, which is
what the .resw tests assert later.
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from xml.sax.saxutils import escape

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
STRINGS = REPOSITORY_ROOT / "src" / "MiaDock.App" / "Strings"
WORK = REPOSITORY_ROOT / "artifacts" / "localization"
WORKSHEETS = WORK / "worksheet"
TRANSLATIONS = WORK / "translations"
SOURCE_CULTURE = "tr-TR"
REFERENCE_CULTURE = "en-US"
TABLES = ("Resources.resw", "XamlText.resw")
CHUNK = 120

ENTRY = re.compile(
    r'<data name="([^"]+)" xml:space="preserve">\s*<value>(.*?)</value>',
    re.DOTALL)
BANNER = re.compile(r"<!--(.*?)-->", re.DOTALL)
PLACEHOLDER = re.compile(r"\{\d+(?::[^}]*)?\}")
RESHEADERS = """  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value>
  </resheader>"""


def unescape_xml(value: str) -> str:
    return (value.replace("&lt;", "<").replace("&gt;", ">")
            .replace("&quot;", '"').replace("&apos;", "'")
            .replace("&amp;", "&"))


def read_table(culture: str, table: str) -> dict[str, str]:
    path = STRINGS / culture / table
    if not path.exists():
        return {}
    return {name: unescape_xml(value)
            for name, value in ENTRY.findall(path.read_text(encoding="utf-8"))}


def banner(table: str) -> str:
    text = (STRINGS / SOURCE_CULTURE / table).read_text(encoding="utf-8")
    found = BANNER.search(text)
    return found.group(1).strip() if found else table


def encode(value: str) -> str:
    return value.replace("\\", "\\\\").replace("\t", "\\t").replace("\n", "\\n")


def decode(value: str) -> str:
    parts: list[str] = []
    index = 0
    while index < len(value):
        character = value[index]
        if character == "\\" and index + 1 < len(value):
            following = value[index + 1]
            parts.append({"n": "\n", "t": "\t", "\\": "\\"}.get(following, following))
            index += 2
            continue
        parts.append(character)
        index += 1
    return "".join(parts)


def export(culture: str) -> int:
    WORKSHEETS.mkdir(parents=True, exist_ok=True)
    TRANSLATIONS.mkdir(parents=True, exist_ok=True)
    for stale in WORKSHEETS.glob(f"{culture}-*.tsv"):
        stale.unlink()

    total = 0
    for table in TABLES:
        source = read_table(SOURCE_CULTURE, table)
        reference = read_table(REFERENCE_CULTURE, table)
        existing = read_table(culture, table)
        pending = [name for name in source if name not in existing]
        total += len(pending)
        stem = table.removesuffix(".resw")
        for index in range(0, len(pending), CHUNK):
            names = pending[index:index + CHUNK]
            path = WORKSHEETS / f"{culture}-{stem}-{index // CHUNK + 1:02d}.tsv"
            lines = ["name\ttr\ten"]
            lines += [f"{name}\t{encode(source[name])}\t{encode(reference.get(name, ''))}"
                      for name in names]
            path.write_text("\n".join(lines) + "\n", encoding="utf-8")
            print(f"{path.relative_to(REPOSITORY_ROOT)}: {len(names)} entries")

    print(f"{culture}: {total} entries need translation.")
    print(f"Write name<TAB>translation files into "
          f"{TRANSLATIONS.relative_to(REPOSITORY_ROOT)}.")
    return 0


def read_translations(culture: str, table: str) -> dict[str, str]:
    stem = table.removesuffix(".resw")
    values: dict[str, str] = {}
    for path in sorted(TRANSLATIONS.glob(f"{culture}-{stem}-*.tsv")):
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            if not line.strip() or line.startswith("name\t"):
                continue
            name, separator, value = line.partition("\t")
            if not separator:
                raise ValueError(f"{path.name}:{number} has no tab separator.")
            values[name.strip()] = decode(value)
    return values


def build(culture: str) -> int:
    problems: list[str] = []
    written: list[str] = []

    for table in TABLES:
        source = read_table(SOURCE_CULTURE, table)
        translated = read_translations(culture, table)
        translated.update({name: value for name, value in read_table(culture, table).items()
                           if name not in translated})

        missing = [name for name in source if name not in translated]
        unknown = [name for name in translated if name not in source]
        blank = [name for name in translated if not translated[name].strip()]
        mismatched = [name for name in source
                      if name in translated
                      and set(PLACEHOLDER.findall(source[name]))
                      != set(PLACEHOLDER.findall(translated[name]))]

        for label, names in (("missing", missing), ("unknown name", unknown),
                             ("blank", blank), ("placeholder mismatch", mismatched)):
            if names:
                shown = ", ".join(names[:8]) + ("..." if len(names) > 8 else "")
                problems.append(f"{table}: {len(names)} {label}: {shown}")

        if problems:
            continue

        lines = ['<?xml version="1.0" encoding="utf-8"?>', "<root>",
                 f"  <!-- {banner(table)} -->", RESHEADERS]
        for name in source:
            lines.append(f'  <data name="{escape(name)}" xml:space="preserve">')
            lines.append(f"    <value>{escape(translated[name])}</value>")
            lines.append("  </data>")
        lines.append("</root>")

        path = STRINGS / culture / table
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        written.append(f"{path.relative_to(REPOSITORY_ROOT)}: {len(source)} entries")

    if problems:
        print("\n".join(problems))
        return 1

    print("\n".join(written))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--export", metavar="CULTURE")
    parser.add_argument("--build", metavar="CULTURE")
    arguments = parser.parse_args()

    if arguments.export:
        return export(arguments.export)
    if arguments.build:
        return build(arguments.build)

    parser.print_help()
    return 1


if __name__ == "__main__":
    sys.exit(main())
