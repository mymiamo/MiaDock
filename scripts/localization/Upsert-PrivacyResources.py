from pathlib import Path
import re

root = Path(r"D:\Masaüstü Kısayolları\Uygulamalar\MiaDock\src\MiaDock.App\Strings")
entries = {
  "tr-TR": {
    "Module.privacy.Name": "Gizlilik",
    "Privacy_Title": "Gizlilik",
    "Privacy_MicrophoneInUse": "Mikrofon kullanılıyor",
    "Privacy_CameraInUse": "Kamera kullanılıyor",
    "Privacy_CameraAndMicrophoneInUse": "Kamera ve mikrofon kullanılıyor",
    "Privacy_NoActiveDevices": "Aktif gizlilik kullanımı yok",
    "Privacy_ActiveApplications": "Aktif uygulamalar",
    "Onboarding.Module.Privacy.Title": "Gizlilik",
    "Onboarding.Module.Privacy.Description": "Mikrofon ve kamerayı kullanan uygulamaları gösterir.",
    "System.Call.IdleDetail": "Yerel arama çıkarımı",
    "Module.system-activity.Name": "Arama",
    "Onboarding.Module.System.Title": "Arama etkinliği",
    "Onboarding.Module.System.Description": "Yerel arama çıkarımını izler; görüşme içeriği okunmaz.",
  },
  "en-US": {
    "Module.privacy.Name": "Privacy",
    "Privacy_Title": "Privacy",
    "Privacy_MicrophoneInUse": "Microphone in use",
    "Privacy_CameraInUse": "Camera in use",
    "Privacy_CameraAndMicrophoneInUse": "Camera and microphone in use",
    "Privacy_NoActiveDevices": "No active privacy usage",
    "Privacy_ActiveApplications": "Active applications",
    "Onboarding.Module.Privacy.Title": "Privacy",
    "Onboarding.Module.Privacy.Description": "Shows which apps are using the microphone and camera.",
    "System.Call.IdleDetail": "Local call inference",
    "Module.system-activity.Name": "Call",
    "Onboarding.Module.System.Title": "Call activity",
    "Onboarding.Module.System.Description": "Monitors local call inference; call content is never read.",
  },
  "es-ES": {
    "Module.privacy.Name": "Privacidad",
    "Privacy_Title": "Privacidad",
    "Privacy_MicrophoneInUse": "Micrófono en uso",
    "Privacy_CameraInUse": "Cámara en uso",
    "Privacy_CameraAndMicrophoneInUse": "Cámara y micrófono en uso",
    "Privacy_NoActiveDevices": "Sin uso activo de privacidad",
    "Privacy_ActiveApplications": "Aplicaciones activas",
    "Onboarding.Module.Privacy.Title": "Privacidad",
    "Onboarding.Module.Privacy.Description": "Muestra qué aplicaciones usan el micrófono y la cámara.",
    "System.Call.IdleDetail": "Inferencia local de llamadas",
    "Module.system-activity.Name": "Llamada",
    "Onboarding.Module.System.Title": "Actividad de llamadas",
    "Onboarding.Module.System.Description": "Supervisa la inferencia local de llamadas; el contenido no se lee.",
  },
  "es-MX": {
    "Module.privacy.Name": "Privacidad",
    "Privacy_Title": "Privacidad",
    "Privacy_MicrophoneInUse": "Micrófono en uso",
    "Privacy_CameraInUse": "Cámara en uso",
    "Privacy_CameraAndMicrophoneInUse": "Cámara y micrófono en uso",
    "Privacy_NoActiveDevices": "Sin uso activo de privacidad",
    "Privacy_ActiveApplications": "Aplicaciones activas",
    "Onboarding.Module.Privacy.Title": "Privacidad",
    "Onboarding.Module.Privacy.Description": "Muestra qué aplicaciones usan el micrófono y la cámara.",
    "System.Call.IdleDetail": "Inferencia local de llamadas",
    "Module.system-activity.Name": "Llamada",
    "Onboarding.Module.System.Title": "Actividad de llamadas",
    "Onboarding.Module.System.Description": "Supervisa la inferencia local de llamadas; el contenido no se lee.",
  },
  "pt-BR": {
    "Module.privacy.Name": "Privacidade",
    "Privacy_Title": "Privacidade",
    "Privacy_MicrophoneInUse": "Microfone em uso",
    "Privacy_CameraInUse": "Câmera em uso",
    "Privacy_CameraAndMicrophoneInUse": "Câmera e microfone em uso",
    "Privacy_NoActiveDevices": "Nenhum uso ativo de privacidade",
    "Privacy_ActiveApplications": "Aplicativos ativos",
    "Onboarding.Module.Privacy.Title": "Privacidade",
    "Onboarding.Module.Privacy.Description": "Mostra quais aplicativos estão usando o microfone e a câmera.",
    "System.Call.IdleDetail": "Inferência local de chamadas",
    "Module.system-activity.Name": "Chamada",
    "Onboarding.Module.System.Title": "Atividade de chamada",
    "Onboarding.Module.System.Description": "Monitora a inferência local de chamadas; o conteúdo nunca é lido.",
  },
  "az-Latn-AZ": {
    "Module.privacy.Name": "Məxfilik",
    "Privacy_Title": "Məxfilik",
    "Privacy_MicrophoneInUse": "Mikrofon istifadə olunur",
    "Privacy_CameraInUse": "Kamera istifadə olunur",
    "Privacy_CameraAndMicrophoneInUse": "Kamera və mikrofon istifadə olunur",
    "Privacy_NoActiveDevices": "Aktiv məxfilik istifadəsi yoxdur",
    "Privacy_ActiveApplications": "Aktiv tətbiqlər",
    "Onboarding.Module.Privacy.Title": "Məxfilik",
    "Onboarding.Module.Privacy.Description": "Mikrofon və kameradan istifadə edən tətbiqləri göstərir.",
    "System.Call.IdleDetail": "Yerli zəng çıxarımı",
    "Module.system-activity.Name": "Zəng",
    "Onboarding.Module.System.Title": "Zəng fəaliyyəti",
    "Onboarding.Module.System.Description": "Yerli zəng çıxarımını izləyir; zəng məzmunu oxunmur.",
  },
}

def upsert(text: str, key: str, value: str, culture: str) -> str:
    pattern = rf'(<data name="{re.escape(key)}" xml:space="preserve">\s*<value>)(.*?)(</value>)'
    if re.search(pattern, text, re.S):
        return re.sub(pattern, rf"\g<1>{value}\g<3>", text, count=1, flags=re.S)

    if culture == "en-US":
        # Keep Turkish comment for keyed English resources when practical.
        block = (
            f'  <data name="{key}" xml:space="preserve">\n'
            f"    <value>{value}</value>\n"
            f"  </data>\n"
        )
    else:
        block = (
            f'  <data name="{key}" xml:space="preserve">\n'
            f"    <value>{value}</value>\n"
            f"  </data>\n"
        )

    if key.startswith("Onboarding.Module.Privacy"):
        anchor = '  <data name="Onboarding.Module.System.Title"'
    elif key == "Module.privacy.Name" or key.startswith("Privacy_") or key == "System.Call.IdleDetail":
        anchor = '  <data name="Module.system-activity.Name"'
    else:
        anchor = "</root>"

    idx = text.find(anchor)
    if idx < 0:
        return text.replace("</root>", block + "</root>")
    if anchor == "</root>":
        return text.replace("</root>", block + "</root>")
    return text[:idx] + block + text[idx:]

for culture, mapping in entries.items():
    path = root / culture / "Resources.resw"
    text = path.read_text(encoding="utf-8")
    for key, value in mapping.items():
        text = upsert(text, key, value, culture)
    path.write_text(text, encoding="utf-8")
    print(culture, "ok")
