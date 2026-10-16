# KEMAS – Microsoft Store listing (English)

## Product name
KEMAS

## Short description
Clean junk, organise your folders and free up memory in one click.

## Description
KEMAS keeps your Windows PC clean, organised and fast, without the clutter and upsell of traditional cleaners.

One click on Smart Clean clears temporary files, browser caches, the Recycle Bin, old logs and other leftovers that slowly fill your drive. KEMAS measures every item before it touches it, skips files that are in use, and never goes near your documents, photos or system folders.

Deep Clean goes further when you need it. It clears graphics shader caches, app caches from Discord, Teams, Slack, Spotify, VS Code, Steam and Office, Windows crash dumps, the Windows component store and the old Windows.old folder after a major upgrade. Logins and settings are always kept.

File Organizer turns a messy Downloads, Pictures, Music or Videos folder into tidy, named folders. You choose the folder names and the file types that go in each one, preview every move before it happens, and undo the last run with one click.

Memory Booster shows which apps use the most memory and frees RAM safely. Startup Apps lets you switch off programs that slow down boot, just like Task Manager.

Designed to feel at home on Windows 11:
• A clean dashboard with a live health score for storage, memory and junk
• Dark and light themes that follow Windows
• Smooth animation that matches your screen's refresh rate
• English and Bahasa Melayu
• Optional Auto Clean on a daily or weekly schedule
• Start with Windows and run quietly in the system tray

Private by design: no account, no ads, no tracking and no telemetry. Everything KEMAS does happens on your PC.

KEMAS asks for administrator permission when it opens so it can clean system locations such as Windows Temp, Prefetch and the component store.

## What's new in this version
New: Deep Clean. Clear graphics shader caches, app caches, crash dumps, the Windows component store and Windows.old in one go. Every update now opens with a short What's New summary.

## Product features
- Smart Clean for temp files, browser caches, the Recycle Bin and old logs
- Deep Clean for shader caches, app caches, crash dumps and Windows leftovers
- File Organizer with your own folder names, file types, preview and undo
- Memory Booster to free RAM and close heavy apps
- Startup Apps manager for faster boot
- Live health score for storage, memory and junk
- Dark and light themes, English and Bahasa Melayu
- Optional daily or weekly Auto Clean
- No account, no ads, no tracking

## Search terms (max 7)
pc cleaner, disk cleanup, junk cleaner, temp files, free up space, file organizer, memory booster

## Copyright and trademark info
© 2026 Amzar Syafiq. All rights reserved.

## Privacy policy URL
https://amzsyafxproject.github.io/kemas/privacy.html

## Website
https://amzsyafxproject.github.io/

## Support contact
https://github.com/AmzSyafxProject/KEMAS/issues

## Notes for certification (allowElevation / runFullTrust justification)
KEMAS is a system maintenance utility. It declares runFullTrust because it is a desktop (WPF/.NET Framework) application, and allowElevation because cleaning system locations requires administrator rights: C:\Windows\Temp, C:\Windows\Prefetch, Windows Error Reporting archives, Delivery Optimization cache, the Windows Update download cache, memory dumps, Windows.old and the Windows component store (via the built-in DISM tool). The user is shown the standard UAC prompt and every cleaning action is listed and confirmed before it runs. The app has no in-app updater in the Store edition, collects no personal data and requires no account.
Test steps: open KEMAS and accept the UAC prompt; the dashboard scans automatically. Open System Cleaner and select Clean selected, or Deep Clean, to run a clean. File Organizer > Preview shows planned moves without changing files.
