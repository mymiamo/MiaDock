"""One-shot migration that lifts the hard coded string tables out of
AppLocalizationService.cs into .resw files under src/MiaDock.App/Strings.

Run from the repository root:
    python scripts/localization/Export-LocalizationResw.py
"""

from __future__ import annotations

import pathlib
import sys
from xml.sax.saxutils import escape

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = REPOSITORY_ROOT / "src" / "MiaDock.App" / "Services" / "AppLocalizationService.cs"
STRINGS = REPOSITORY_ROOT / "src" / "MiaDock.App" / "Strings"

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
  </resheader>
"""


def read_string_literal(text: str, index: int) -> tuple[str, int]:
    """Reads the C# string literal starting at `index` (which must be a quote)."""
    if text[index] != '"':
        raise ValueError(f"Expected a string literal at offset {index}.")

    index += 1
    value: list[str] = []
    while True:
        character = text[index]
        if character == "\\":
            following = text[index + 1]
            value.append({"n": "\n", "t": "\t", "r": "\r"}.get(following, following))
            index += 2
            continue
        if character == '"':
            return "".join(value), index + 1
        value.append(character)
        index += 1


def skip(text: str, index: int, expected: str) -> int:
    while text[index].isspace():
        index += 1
    if not text.startswith(expected, index):
        raise ValueError(f"Expected {expected!r} at offset {index}: {text[index:index + 40]!r}")
    return index + len(expected)


def parse_block(text: str, start: int, end: int, tuple_valued: bool) -> list[tuple[str, ...]]:
    entries: list[tuple[str, ...]] = []
    index = start
    while True:
        index = text.find('["', index)
        if index < 0 or index >= end:
            return entries

        key, index = read_string_literal(text, index + 1)
        index = skip(text, index, "]")
        index = skip(text, index, "=")
        while text[index].isspace():
            index += 1

        if tuple_valued:
            index = skip(text, index, "(")
            turkish, index = read_string_literal(text, index)
            index = skip(text, index, ",")
            while text[index].isspace():
                index += 1
            english, index = read_string_literal(text, index)
            index = skip(text, index, ")")
            entries.append((key, turkish, english))
        else:
            value, index = read_string_literal(text, index)
            entries.append((key, value))


def write_resw(path: pathlib.Path, entries: list[tuple[str, str | None]], banner: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<root>",
        f"  <!-- {banner} -->",
        RESHEADERS.rstrip("\n"),
    ]
    for name, value, comment in entries:
        lines.append(f'  <data name="{escape(name)}" xml:space="preserve">')
        lines.append(f"    <value>{escape(value)}</value>")
        if comment:
            lines.append(f"    <comment>{escape(comment)}</comment>")
        lines.append("  </data>")
    lines.append("</root>")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{path.relative_to(REPOSITORY_ROOT)}: {len(entries)} entries")


def main() -> int:
    text = SOURCE.read_text(encoding="utf-8")

    catalog_start = text.index("Catalog =")
    catalog_end = text.index("private static readonly IReadOnlyDictionary<string, string> English")
    literal_end = text.index("public AppLanguage CurrentLanguage")

    catalog = parse_block(text, catalog_start, catalog_end, tuple_valued=True)
    literals = parse_block(text, catalog_end, literal_end, tuple_valued=False)
    if not catalog or not literals:
        raise ValueError("The string tables could not be located.")

    keyed_banner = (
        "Keyed UI strings resolved through ILocalizationService.Get(key). "
        "Add a new language by copying this folder and translating every value."
    )
    literal_banner = (
        "Literal XAML text overrides. The tr-TR value is the string authored in "
        "XAML; every other language translates it under the same name."
    )

    write_resw(
        STRINGS / "tr-TR" / "Resources.resw",
        [(key, turkish, None) for key, turkish, _ in catalog],
        keyed_banner,
    )
    write_resw(
        STRINGS / "en-US" / "Resources.resw",
        [(key, english, turkish) for key, turkish, english in catalog],
        keyed_banner,
    )

    # The C# table used indexer syntax, so repeated literals silently overwrote
    # each other. Collapsing them keeps translators from seeing the same source
    # string twice.
    unique: dict[str, str] = {}
    for turkish, english in literals:
        if turkish in unique and unique[turkish] != english:
            raise ValueError(f"Conflicting translations for {turkish!r}.")
        unique.setdefault(turkish, english)

    numbered = [(f"XamlText.{index:04d}", turkish, english)
                for index, (turkish, english) in enumerate(unique.items(), start=1)]
    write_resw(
        STRINGS / "tr-TR" / "XamlText.resw",
        [(name, turkish, None) for name, turkish, _ in numbered],
        literal_banner,
    )
    write_resw(
        STRINGS / "en-US" / "XamlText.resw",
        [(name, english, turkish) for name, turkish, english in numbered],
        literal_banner,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
