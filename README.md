# WhatsAppDesktop-Installer-Updater-NoStore
remake of [github.com/DuvyDev/WhatsAppDesktop-NoStore](https://github.com/DuvyDev/WhatsAppDesktop-NoStore)

# WhatsApp Tray Updater

**This app is entirely vibecoded.**

**WhatsApp Tray Updater** is a lightweight Windows utility that runs silently in the system tray and automatically monitors for new WhatsApp Desktop versions — without requiring the Microsoft Store. When an update (or a fresh installation) is available, it notifies you via a balloon notification and lets you download and install it with a single click.

## Features

- **System Tray** — Runs silently in the background with no window, no taskbar entry. Right-click or left-click the tray icon to open the menu.
- **Automatic Checks** — Checks for a new version on startup and every hour thereafter.
- **No Microsoft Store Required** — Fetches the latest `.msixbundle` directly from Microsoft's CDN via [store.rg-adguard.net](https://store.rg-adguard.net).
- **Fresh Install Support** — If WhatsApp is not installed at all, the tool detects this and offers to download and install it.
- **Download Progress Window** — A popup shows live download progress (MB received, percentage) with a cancel button. Installation runs silently in the background via `Add-AppxPackage`.
- **Balloon Notifications** — Notified the moment a new version is found. Clicking the balloon opens the install dialog directly.
- **Toggleable Notifications** — Right-click the tray icon and click **Notifications** to enable or disable balloon popups. The setting is remembered across restarts.
- **Single EXE, Zero Dependencies** — No installer, no .NET runtime to ship, no third-party libraries. Requires only .NET Framework 4.x, which is built into every Windows 10 and 11 installation.
- **Minimal Memory Footprint** — ~8–10 MB RAM at idle.

## Usage

1. Download `WhatsAppTrayUpdater.exe`.
2. Run it — a tray icon appears immediately.
3. After a few seconds the first version check runs automatically.
4. If an update or fresh install is available, a balloon notification appears. Click it, or right-click the tray icon and choose **Download && Install**.
5. A progress window opens. When the download finishes, WhatsApp is installed silently. The window closes automatically on success.

To have the updater start with Windows, place a shortcut to the EXE in your startup folder (`Win + R` → `shell:startup`).

## Requirements

- Windows 10 or Windows 11
- .NET Framework 4.x (included in Windows 10/11 — no separate install needed)

## Building from Source

No IDE required. Use the C# compiler that ships with Windows:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe /optimize /win32manifest:app.manifest ^
  /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:WhatsAppTrayUpdater.exe WhatsAppTrayUpdater.cs
```

## Known Issues

- **WhatsApp must be closed before updating.** If WhatsApp is running when you click Download && Install, `Add-AppxPackage` will fail because the package is in use. Close WhatsApp first, then proceed with the installation.

## Credits

Inspired by [DuvyDev/WhatsAppDesktop-NoStore](https://github.com/DuvyDev/WhatsAppDesktop-NoStore).

Version lookup powered by [store.rg-adguard.net](https://store.rg-adguard.net).

**This app is entirely vibecoded**, so I'm not gonna take credit for this, but if you need something similar and aren't able to get AI to do this for you, you can contact me at info@techwiz-services.eu

## License

MIT License — free to use, modify, and distribute.
