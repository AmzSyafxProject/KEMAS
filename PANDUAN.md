# KEMAS 3.0: Panduan Release, Update & Anti-Virus

## 1. Kenapa exe baharu "nampak macam virus"

Windows (SmartScreen) dan antivirus menilai exe berdasarkan dua perkara:
1. **Siapa penerbit**, iaitu digital signature (code signing).
2. **Reputasi**, iaitu berapa ramai orang dah muat turun dan jalankan fail yang sama.

KEMAS 3.0 dah dibina untuk kurangkan risiko false-positive:
- Exe native .NET, **tak lagi jalankan PowerShell tersembunyi**. Ini punca utama antivirus syak versi 2.
- Version info yang lengkap (nama produk, penerbit, versi, copyright).
- Manifest yang jelas: minta admin secara terbuka, dan sokong DPI.
- Tiada packer, obfuscation, atau muat turun kod luar.

### Apa awak boleh buat (ikut keutamaan)

| Langkah | Kos | Kesan |
|---|---|---|
| Hantar exe ke **Microsoft Defender** untuk analisis false-positive: https://www.microsoft.com/wdsi/filesubmission (pilih "Software developer") | Percuma | Defender berhenti flag biasanya dalam 1–3 hari |
| Imbas di **VirusTotal** (virustotal.com). Kalau ada vendor yang flag, hantar false-positive report ke vendor tu | Percuma | Bersihkan amaran antivirus lain |
| Edar melalui **GitHub Releases** (HTTPS, URL tetap). Jangan tukar nama fail antara versi | Percuma | Reputasi SmartScreen terkumpul |
| **Code signing certificate (OV)** daripada Sectigo, DigiCert, SSL.com atau Certum. Certum ada pakej murah untuk individu / open source | ~USD 150–300/tahun (Certum lebih murah) | Nama awak keluar sebagai penerbit, dan amaran "Unknown publisher" hilang |
| **SignPath Foundation**, percuma untuk projek open-source di GitHub yang layak | Percuma | Signed tanpa beli sijil |

Nota:
- **Azure Artifact Signing** (USD 9.99/bulan) buat masa ni hanya untuk individu di AS/Kanada, jadi belum boleh digunakan di Malaysia.
- Sijil **EV tak lagi bypass SmartScreen** sejak 2024, jadi tak berbaloi bayar lebih.
- Walaupun dah signed, SmartScreen mungkin masih beri amaran pada awalnya. Ia hilang bila makin ramai orang guna. Sign setiap versi dengan identiti yang sama supaya reputasi tak bermula semula.

## 2. Sistem auto-update (GitHub)

KEMAS semak `https://api.github.com/repos/AmzSyafxProject/KEMAS/releases/latest` ketika dibuka, dan bila pengguna tekan "Check for updates".

### Setup sekali sahaja (sudah ditetapkan: `AmzSyafxProject/KEMAS`)
1. Di GitHub, buat repo baharu bernama **KEMAS** (mesti **Public**, nama tepat).
2. Buat release pertama: tag **`v3.0.1`**, upload `KEMAS.exe` dan `KEMAS.exe.sha256` dari pakej ini, kemudian Publish.

### Setiap kali nak release versi baharu
1. Naikkan nombor versi (contoh 3.0.0 → 3.1.0) dan build `KEMAS.exe`. Minta Claude, atau ubah `AppInfo.Version` dan `AssemblyVersion` dalam kod.
2. Buat fail hash (dalam PowerShell):
   ```powershell
   (Get-FileHash .\KEMAS.exe -Algorithm SHA256).Hash.ToLower() | Out-File -Encoding ascii KEMAS.exe.sha256
   ```
3. Di GitHub: **Releases → Draft a new release**
   - Tag: `v3.1.0` (mesti lebih tinggi daripada versi semasa)
   - Title: `KEMAS 3.1.0`
   - Notes: senarai perubahan. Teks ni dipaparkan dalam apps pengguna.
   - Upload **dua fail**: `KEMAS.exe` dan `KEMAS.exe.sha256` (nama mesti tepat)
   - Jangan tanda "pre-release", kemudian tekan **Publish**.
4. Selesai. Semua pengguna akan nampak "Update available". Bila mereka tekan **Update now**, KEMAS:
   - muat turun fail baharu,
   - semak SHA-256 dan pastikan ia program Windows yang sah,
   - tukar fail, kemudian restart sendiri.

## 3. Install untuk client

- Client cuma perlu jalankan `KEMAS.exe`. Kali pertama, apps akan tawarkan **Install**:
  - salin ke `C:\Program Files\KEMAS`,
  - tambah pintasan Start menu dan desktop,
  - daftar dalam **Settings → Apps** (boleh uninstall dari situ).
- Untuk guna tanpa install, jalankan `KEMAS.exe --portable`.
- Data, tetapan dan log: `%LOCALAPPDATA%\KEMAS`

## 4. Pilihan command-line

| Arahan | Fungsi |
|---|---|
| `KEMAS.exe --tray` | Mula senyap di system tray (digunakan oleh "Start with Windows") |
| `KEMAS.exe --autoclean` | Bersih tanpa tetingkap (digunakan oleh jadual auto clean) |
| `KEMAS.exe --uninstall` | Nyahpasang |
| `KEMAS.exe --portable` | Jangan tawar install |
