"""Fill romance-language translation TSVs from English worksheet columns.

Reads artifacts/localization/worksheet/<culture>-*.tsv (name/tr/en) and writes
artifacts/localization/translations/<culture>-<table>-<nn>.tsv (name/translation).

es-MX starts from es-ES with a small regional lexicon pass. Placeholders are
forced to match the Turkish source strings so Build-CultureResw validation passes.
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
import time

from deep_translator import GoogleTranslator

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
WORK = REPOSITORY_ROOT / "artifacts" / "localization"
WORKSHEETS = WORK / "worksheet"
TRANSLATIONS = WORK / "translations"
CHUNK = 120
PLACEHOLDER = re.compile(r"\{\d+(?::[^}]*)?\}")

# Brand / proper nouns that must stay intact.
PROTECTED = [
    "MiaDock",
    "Windows",
    "WinUI",
    "Mica",
    "Acrylic",
    "StartupTask",
    "mymiamo.net",
    "mymiamo.net/bug",
    "Segoe",
]

# European Spanish -> Mexican Spanish UI lexicon.
ES_TO_MX = [
    (re.compile(r"\borderador\b", re.I), "computadora"),
    (re.compile(r"\borderadores\b", re.I), "computadoras"),
    (re.compile(r"\bmóvil\b", re.I), "celular"),
    (re.compile(r"\bmóviles\b", re.I), "celulares"),
    (re.compile(r"\barchivo comprimido\b", re.I), "archivo zip"),
    (re.compile(r"\bordenador portátil\b", re.I), "laptop"),
    (re.compile(r"\bvosotros\b", re.I), "ustedes"),
    (re.compile(r"\bvuestro\b", re.I), "su"),
    (re.compile(r"\bvuestra\b", re.I), "su"),
]


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


def protect(text: str) -> tuple[str, dict[str, str]]:
    mapping: dict[str, str] = {}
    protected = text
    for index, token in enumerate(PROTECTED):
        if token in protected:
            marker = f"⟦P{index}⟧"
            mapping[marker] = token
            protected = protected.replace(token, marker)
    return protected, mapping


def unprotect(text: str, mapping: dict[str, str]) -> str:
    restored = text
    for marker, token in mapping.items():
        restored = restored.replace(marker, token)
    return restored


def align_placeholders(source_tr: str, translated: str) -> str:
    source_ph = PLACEHOLDER.findall(source_tr)
    translated_ph = PLACEHOLDER.findall(translated)
    if source_ph == translated_ph:
        return translated
    if not source_ph:
        return PLACEHOLDER.sub("", translated)
    # Replace translated placeholders in order, then append any missing ones.
    result = translated
    for index, match in enumerate(PLACEHOLDER.finditer(translated)):
        if index >= len(source_ph):
            break
        result = result.replace(match.group(0), source_ph[index], 1)
    missing = source_ph[len(PLACEHOLDER.findall(result)) :]
    if missing:
        result = result.rstrip() + " " + " ".join(missing)
    return result


def apply_mx(text: str) -> str:
    result = text
    for pattern, replacement in ES_TO_MX:
        result = pattern.sub(replacement, result)
    return result


def read_worksheets(culture: str) -> dict[str, list[tuple[str, str, str]]]:
    """table stem -> list of (name, tr, en)."""
    tables: dict[str, list[tuple[str, str, str]]] = {}
    for path in sorted(WORKSHEETS.glob(f"{culture}-*.tsv")):
        stem = path.name[len(culture) + 1 :]  # Resources-01.tsv
        table = "Resources.resw" if stem.startswith("Resources") else "XamlText.resw"
        rows = tables.setdefault(table, [])
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            if number == 1 and line.startswith("name\t"):
                continue
            if not line.strip():
                continue
            parts = line.split("\t")
            if len(parts) < 3:
                raise ValueError(f"{path.name}:{number} expected name/tr/en")
            name, tr, en = parts[0], decode(parts[1]), decode(parts[2])
            rows.append((name, tr, en))
    return tables


class TranslatorCache:
    def __init__(self, target: str) -> None:
        self._target = target
        self._translator = GoogleTranslator(source="en", target=target)
        self._cache: dict[str, str] = {}

    def translate(self, english: str) -> str:
        if english in self._cache:
            return self._cache[english]
        if not english.strip():
            self._cache[english] = english
            return english

        protected, mapping = protect(english)
        attempt = 0
        while True:
            try:
                translated = self._translator.translate(protected)
                break
            except Exception as error:  # noqa: BLE001 - network/rate limits
                attempt += 1
                if attempt >= 8:
                    raise RuntimeError(f"Translation failed for {english!r}: {error}") from error
                time.sleep(min(2 ** attempt, 20))
        restored = unprotect(translated or protected, mapping)
        self._cache[english] = restored
        time.sleep(0.05)
        return restored


def write_translation_chunks(culture: str, table: str, rows: list[tuple[str, str]]) -> None:
    stem = table.removesuffix(".resw")
    for path in TRANSLATIONS.glob(f"{culture}-{stem}-*.tsv"):
        path.unlink()
    for index in range(0, len(rows), CHUNK):
        chunk = rows[index : index + CHUNK]
        path = TRANSLATIONS / f"{culture}-{stem}-{index // CHUNK + 1:02d}.tsv"
        lines = ["name\ttranslation"]
        lines += [f"{name}\t{encode(value)}" for name, value in chunk]
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"{path.relative_to(REPOSITORY_ROOT)}: {len(chunk)} entries")


def generate(culture: str, google_target: str, base_es: dict[str, str] | None = None) -> dict[str, str]:
    TRANSLATIONS.mkdir(parents=True, exist_ok=True)
    tables = read_worksheets(culture)
    translator = None if base_es is not None else TranslatorCache(google_target)
    all_by_name: dict[str, str] = {}

    for table, entries in tables.items():
        output_rows: list[tuple[str, str]] = []
        for name, tr, en in entries:
            if base_es is not None:
                translated = apply_mx(base_es[name])
            else:
                assert translator is not None
                translated = translator.translate(en)
            translated = align_placeholders(tr, translated)
            if not translated.strip():
                translated = en
            translated = align_placeholders(tr, translated)
            output_rows.append((name, translated))
            all_by_name[name] = translated
            if len(all_by_name) % 50 == 0:
                print(f"{culture}: {len(all_by_name)} translated...", flush=True)
        write_translation_chunks(culture, table, output_rows)

    print(f"{culture}: {len(all_by_name)} entries ready.")
    return all_by_name


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--culture", action="append", dest="cultures")
    args = parser.parse_args()
    cultures = args.cultures or ["es-ES", "es-MX", "pt-BR"]

    es_map: dict[str, str] | None = None
    if "es-ES" in cultures:
        es_map = generate("es-ES", "es")
    if "es-MX" in cultures:
        if es_map is None:
            # Allow generating MX alone by translating to es first from worksheets.
            es_map = generate("es-ES", "es")
        generate("es-MX", "es", base_es=es_map)
    if "pt-BR" in cultures:
        generate("pt-BR", "pt")
    return 0


if __name__ == "__main__":
    sys.exit(main())
