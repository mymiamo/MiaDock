from pathlib import Path
import re

root = Path(r"D:\Masaüstü Kısayolları\Uygulamalar\MiaDock\src\MiaDock.App\Strings")
cultures = ["tr-TR", "en-US", "es-ES", "es-MX", "pt-BR", "az-Latn-AZ"]

# Update AutomationProperties name for system activity expanded view.
updates_0074 = {
    "tr-TR": ("Arama etkinliği", None),
    "en-US": ("Call activity", "Arama etkinliği"),
    "es-ES": ("Actividad de llamadas", "Arama etkinliği"),
    "es-MX": ("Actividad de llamadas", "Arama etkinliği"),
    "pt-BR": ("Atividade de chamada", "Arama etkinliği"),
    "az-Latn-AZ": ("Zəng fəaliyyəti", "Arama etkinliği"),
}

new_entries = {
    "tr-TR": [
        ("XamlText.0672", "Arama", None),
        ("XamlText.0673", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır.", None),
    ],
    "en-US": [
        ("XamlText.0672", "Call", "Arama"),
        ("XamlText.0673", "Scale and fade, slide and fade, or spring. Applied while the dock grows and shrinks.", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır."),
    ],
    "es-ES": [
        ("XamlText.0672", "Llamada", "Arama"),
        ("XamlText.0673", "Escala y fundido, deslizamiento y fundido, o resorte. Se aplica al crecer y reducir el dock.", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır."),
    ],
    "es-MX": [
        ("XamlText.0672", "Llamada", "Arama"),
        ("XamlText.0673", "Escala y fundido, deslizamiento y fundido, o resorte. Se aplica al crecer y reducir el dock.", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır."),
    ],
    "pt-BR": [
        ("XamlText.0672", "Chamada", "Arama"),
        ("XamlText.0673", "Escala e fade, deslize e fade ou mola. Aplicado ao crescer e reduzir o dock.", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır."),
    ],
    "az-Latn-AZ": [
        ("XamlText.0672", "Zəng", "Arama"),
        ("XamlText.0673", "Miqyas və solma, sürüşmə və solma və ya yay. Dock böyüyüb kiçilərkən tətbiq olunur.", "Ölçek ve solma, kayma ve solma veya yay. Dock büyüyüp küçülürken uygulanır."),
    ],
}

for culture in cultures:
    path = root / culture / "XamlText.resw"
    text = path.read_text(encoding="utf-8")
    value, comment = updates_0074[culture]
    if culture == "en-US" or comment:
        pattern = r'(<data name="XamlText\.0074" xml:space="preserve">\s*<value>)(.*?)(</value>\s*)(<comment>.*?</comment>)?'
        repl = rf'\g<1>{value}\g<3><comment>{comment or value}</comment>'
        text, n = re.subn(pattern, repl, text, count=1, flags=re.S)
        if n == 0:
            raise SystemExit(f"failed update 0074 for {culture}")
    else:
        pattern = r'(<data name="XamlText\.0074" xml:space="preserve">\s*<value>)(.*?)(</value>)'
        text, n = re.subn(pattern, rf'\g<1>{value}\g<3>', text, count=1, flags=re.S)
        if n == 0:
            raise SystemExit(f"failed update 0074 for {culture}")

    for name, value, comment in new_entries[culture]:
        if f'name="{name}"' in text:
            continue
        if comment:
            block = (
                f'  <data name="{name}" xml:space="preserve">\n'
                f'    <value>{value}</value>\n'
                f'    <comment>{comment}</comment>\n'
                f'  </data>\n'
            )
        else:
            block = (
                f'  <data name="{name}" xml:space="preserve">\n'
                f'    <value>{value}</value>\n'
                f'  </data>\n'
            )
        text = text.replace("</root>", block + "</root>")
    path.write_text(text, encoding="utf-8")
    print(culture, "ok")
