"""Keeps the inline Turkish/English literal pairs in C# in sync with the .resw
string tables.

Most call sites use the keyed helpers - Text(key, fallback) or Get(key) - which
already read the tables, so a new culture only needs Resources.resw translated.
Two patterns are different because they carry both literals in C#:

  * IAppLocalizationService.Text(turkish, english), including the local L(...)
    aliases that wrap it.
  * The settings navigation and search helpers, which pass Turkish/English
    literals positionally and hand them to Text(...) further down.

Listing those literals in XamlText.resw lets the service translate them like any
other authored string, so a third language is served from the tables.

A Turkish literal that maps to two different English strings cannot be a lookup
key. The report names those call sites so they can be disambiguated instead of
being silently mistranslated.

Run from the repository root:
    python scripts/localization/Sync-InlineTextPairs.py            # report only
    python scripts/localization/Sync-InlineTextPairs.py --append   # add missing
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from xml.sax.saxutils import escape

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = REPOSITORY_ROOT / "src"
STRINGS = REPOSITORY_ROOT / "src" / "MiaDock.App" / "Strings"
SOURCE_CULTURE = "tr-TR"
KEYED_TABLE = "Resources.resw"
XAML_TABLE = "XamlText.resw"

LITERAL = r'"((?:[^"\\]|\\.)*)"'
# Text(...) is the interface method; L(...) is the local alias the settings view
# model and window use to keep their option and category lists readable.
CALL = re.compile(r"(?<![A-Za-z0-9_])(?:Text|L)\(\s*" + LITERAL + r"\s*,\s*" + LITERAL + r"\s*\)")

# The navigation helpers take their literals positionally. Each entry is the
# helper name followed by the indexes of the Turkish/English pairs among the
# leading literal arguments.
POSITIONAL = {
    "Category": (5, ((1, 2), (3, 4))),
    "Subpage": (6, ((2, 3), (4, 5))),
    "Search": (4, ((0, 1), (2, 3))),
}
ENTRY = re.compile(
    r'<data name="([^"]+)" xml:space="preserve">\s*<value>(.*?)</value>',
    re.DOTALL)
UNESCAPE = {"n": "\n", "t": "\t", "r": "\r"}


def unescape(literal: str) -> str:
    parts: list[str] = []
    index = 0
    while index < len(literal):
        character = literal[index]
        if character == "\\":
            following = literal[index + 1]
            parts.append(UNESCAPE.get(following, following))
            index += 2
            continue
        parts.append(character)
        index += 1
    return "".join(parts)


def positional_pattern(helper: str, count: int) -> re.Pattern[str]:
    literals = r"\s*,\s*".join([LITERAL] * count)
    return re.compile(r"(?<![A-Za-z0-9_])" + helper + r"\(\s*" + literals)


def read_table(culture: str, table: str) -> dict[str, str]:
    text = (STRINGS / culture / table).read_text(encoding="utf-8")
    return dict(ENTRY.findall(text))


def call_sites(keys: set[str]) -> dict[str, dict[str, list[str]]]:
    sites: dict[str, dict[str, list[str]]] = {}

    def record(turkish: str, english: str, origin: str) -> None:
        # Skip the keyed overload, whose first argument is a resource key and
        # whose second argument is only an in-source fallback.
        if turkish in keys or not turkish or not english or turkish == english:
            return
        sites.setdefault(turkish, {}).setdefault(english, []).append(origin)

    patterns = [(pairs, positional_pattern(helper, count))
                for helper, (count, pairs) in POSITIONAL.items()]

    for file in sorted(SOURCE.rglob("*.cs")):
        text = file.read_text(encoding="utf-8")
        origin = file.relative_to(REPOSITORY_ROOT).as_posix()
        for match in CALL.finditer(text):
            record(unescape(match.group(1)), unescape(match.group(2)), origin)
        for pairs, pattern in patterns:
            for match in pattern.finditer(text):
                groups = [unescape(group) for group in match.groups()]
                for turkish_index, english_index in pairs:
                    record(groups[turkish_index], groups[english_index], origin)

    return sites


def next_number(existing: dict[str, str]) -> int:
    numbers = [int(name.split(".")[1]) for name in existing if name.startswith("XamlText.")]
    return max(numbers, default=0) + 1


def append(culture: str, entries: list[tuple[str, str]]) -> None:
    path = STRINGS / culture / XAML_TABLE
    block = "".join(
        f'  <data name="{escape(name)}" xml:space="preserve">\n'
        f"    <value>{escape(value)}</value>\n"
        "  </data>\n"
        for name, value in entries)
    text = path.read_text(encoding="utf-8")
    path.write_text(text.replace("</root>", block + "</root>"), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--append", action="store_true",
                        help="add the missing literals to the tr-TR and en-US tables")
    arguments = parser.parse_args()

    keys = set(read_table(SOURCE_CULTURE, KEYED_TABLE))
    sites = call_sites(keys)
    ambiguous = {turkish: variants for turkish, variants in sites.items() if len(variants) > 1}
    unambiguous = {turkish: next(iter(variants))
                   for turkish, variants in sites.items() if len(variants) == 1}

    source = read_table(SOURCE_CULTURE, XAML_TABLE)
    authored = set(source.values())
    missing = {turkish: english for turkish, english in unambiguous.items()
               if turkish not in authored}

    print(f"Inline literal pairs: {len(sites)}; "
          f"already in {XAML_TABLE}: {len(unambiguous) - len(missing)}; "
          f"missing: {len(missing)}; ambiguous: {len(ambiguous)}")

    for turkish, variants in sorted(ambiguous.items()):
        print(f"  AMBIGUOUS {turkish!r}:")
        for english, files in sorted(variants.items()):
            print(f"    {english!r} in {', '.join(sorted(set(files)))}")

    if not missing or not arguments.append:
        for turkish in sorted(missing):
            print(f"  missing {turkish!r} -> {missing[turkish]!r}")
        return 1 if ambiguous else 0

    start = next_number(source)
    ordered = sorted(missing)
    names = [f"XamlText.{start + offset:04d}" for offset in range(len(ordered))]
    append(SOURCE_CULTURE, list(zip(names, ordered)))
    append("en-US", [(name, missing[turkish]) for name, turkish in zip(names, ordered)])
    print(f"Appended {len(ordered)} entries as {names[0]}..{names[-1]}.")
    print("Translate the same names in every other culture folder.")
    return 1 if ambiguous else 0


if __name__ == "__main__":
    sys.exit(main())
