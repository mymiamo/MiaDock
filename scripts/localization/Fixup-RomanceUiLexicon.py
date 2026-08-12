"""Correct common machine-translation mistakes for romance UI locales."""

from __future__ import annotations

import pathlib
import re
import sys

REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
STRINGS = REPOSITORY_ROOT / "src" / "MiaDock.App" / "Strings"
TRANSLATIONS = REPOSITORY_ROOT / "artifacts" / "localization" / "translations"

# Keyed Resources.resw overrides per culture.
KEYED = {
    "es-ES": {
        "Common.Close": "Cerrar",
        "Common.Cancel": "Cancelar",
        "Common.Play": "Reproducir",
        "Common.Pause": "Pausar",
        "Common.Available": "Disponible",
        "Common.Unavailable": "No disponible",
        "Common.Unknown": "Desconocido",
        "Tray.Exit": "Salir",
        "Tray.Next": "Siguiente",
        "Tray.Previous": "Anterior",
        "Dock.Show": "Mostrar dock",
        "Dock.Hide": "Ocultar dock",
        "Dock.Settings": "Configuración",
        "Tray.StartWithWindows": "Ejecutar al iniciar Windows",
        "Tray.TemporaryNotifications": "Mostrar notificaciones temporales",
        "Tray.SelectMonitor": "Seleccionar monitor",
        "Tray.FullscreenBehavior": "Comportamiento a pantalla completa",
        "Tray.DefaultMedia": "Aplicación multimedia predeterminada",
        "Tray.MediaNotFound": "No se encontró ninguna aplicación multimedia",
        "Tray.PrimaryMonitor": "Monitor principal",
        "Tray.ActiveMonitor": "Monitor de la ventana activa",
    },
    "es-MX": {
        "Common.Close": "Cerrar",
        "Common.Cancel": "Cancelar",
        "Common.Play": "Reproducir",
        "Common.Pause": "Pausar",
        "Common.Available": "Disponible",
        "Common.Unavailable": "No disponible",
        "Common.Unknown": "Desconocido",
        "Tray.Exit": "Salir",
        "Tray.Next": "Siguiente",
        "Tray.Previous": "Anterior",
        "Dock.Show": "Mostrar dock",
        "Dock.Hide": "Ocultar dock",
        "Dock.Settings": "Configuración",
        "Tray.StartWithWindows": "Ejecutar al iniciar Windows",
        "Tray.TemporaryNotifications": "Mostrar notificaciones temporales",
        "Tray.SelectMonitor": "Seleccionar monitor",
        "Tray.FullscreenBehavior": "Comportamiento en pantalla completa",
        "Tray.DefaultMedia": "Aplicación de medios predeterminada",
        "Tray.MediaNotFound": "No se encontró ninguna aplicación de medios",
        "Tray.PrimaryMonitor": "Monitor principal",
        "Tray.ActiveMonitor": "Monitor de la ventana activa",
    },
    "pt-BR": {
        "Common.Close": "Fechar",
        "Common.Cancel": "Cancelar",
        "Common.Play": "Reproduzir",
        "Common.Pause": "Pausar",
        "Common.Available": "Disponível",
        "Common.Unavailable": "Indisponível",
        "Common.Unknown": "Desconhecido",
        "Tray.Exit": "Sair",
        "Tray.Next": "Próximo",
        "Tray.Previous": "Anterior",
        "Dock.Show": "Mostrar dock",
        "Dock.Hide": "Ocultar dock",
        "Dock.Settings": "Configurações",
        "Tray.StartWithWindows": "Executar na inicialização do Windows",
        "Tray.TemporaryNotifications": "Mostrar notificações temporárias",
        "Tray.SelectMonitor": "Selecionar monitor",
        "Tray.FullscreenBehavior": "Comportamento em tela cheia",
        "Tray.DefaultMedia": "Aplicativo de mídia padrão",
        "Tray.MediaNotFound": "Nenhum aplicativo de mídia encontrado",
        "Tray.PrimaryMonitor": "Monitor principal",
        "Tray.ActiveMonitor": "Monitor da janela ativa",
    },
}

# Exact-value replacements applied to both Resources and XamlText.
VALUE_FIXES = {
    "es-ES": {
        "Cerca": "Cerrar",
        "Salida": "Salir",
        "Ajustes": "Configuración",
    },
    "es-MX": {
        "Cerca": "Cerrar",
        "Salida": "Salir",
        "Ajustes": "Configuración",
    },
    "pt-BR": {
        "Saída": "Sair",
    },
}

DATA_RE = re.compile(
    r'(<data name="([^"]+)"[^>]*>\s*<value>)(.*?)(</value>)',
    re.S,
)


def encode(value: str) -> str:
    return value.replace("\\", "\\\\").replace("\t", "\\t").replace("\n", "\\n")


def patch_resw(path: pathlib.Path, keyed: dict[str, str], values: dict[str, str]) -> int:
    text = path.read_text(encoding="utf-8")
    changed = 0

    def repl(match: re.Match[str]) -> str:
        nonlocal changed
        prefix, name, value, suffix = match.group(1), match.group(2), match.group(3), match.group(4)
        new_value = keyed.get(name, values.get(value, value))
        if new_value != value:
            changed += 1
            return f"{prefix}{new_value}{suffix}"
        return match.group(0)

    updated = DATA_RE.sub(repl, text)
    if changed:
        path.write_text(updated, encoding="utf-8")
    return changed


def patch_translation_tsvs(culture: str, keyed: dict[str, str], values: dict[str, str]) -> int:
    changed = 0
    for path in sorted(TRANSLATIONS.glob(f"{culture}-*.tsv")):
        lines = path.read_text(encoding="utf-8").splitlines()
        out: list[str] = []
        file_changed = False
        for index, line in enumerate(lines):
            if index == 0 and line.startswith("name\t"):
                out.append(line)
                continue
            if not line.strip() or "\t" not in line:
                out.append(line)
                continue
            name, value = line.split("\t", 1)
            # values in TSV are encoded; decode lightly for exact fixes
            raw = value.replace("\\n", "\n").replace("\\t", "\t").replace("\\\\", "\\")
            new_raw = keyed.get(name, values.get(raw, raw))
            if new_raw != raw:
                file_changed = True
                changed += 1
                out.append(f"{name}\t{encode(new_raw)}")
            else:
                out.append(line)
        if file_changed:
            path.write_text("\n".join(out) + "\n", encoding="utf-8")
    return changed


def main() -> int:
    total = 0
    for culture, keyed in KEYED.items():
        values = VALUE_FIXES.get(culture, {})
        for table in ("Resources.resw", "XamlText.resw"):
            path = STRINGS / culture / table
            count = patch_resw(path, keyed if table == "Resources.resw" else {}, values)
            print(f"{path.relative_to(REPOSITORY_ROOT)}: {count} fixes")
            total += count
        count = patch_translation_tsvs(culture, keyed, values)
        print(f"{culture} translations: {count} fixes")
        total += count
    print(f"Total fixes: {total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
