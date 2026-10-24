# 🌾 StardewBridge Client Mod

[![SMAPI](https://img.shields.io/badge/SMAPI-4.0%2B-brightgreen)](https://smapi.io)
[![Stardew Valley](https://img.shields.io/badge/Stardew%20Valley-1.6%2B-orange)](https://www.stardewvalley.net/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/ijplabs/litevalley-bridge?include_prereleases&color=7c3aed)](https://github.com/ijplabs/litevalley-bridge/releases)

**StardewBridge** adalah mod client resmi SMAPI untuk menghubungkan game **Stardew Valley (PC)** Anda ke **LiteValley Headless Dedicated Server** (baik server *Self-Host* mandiri maupun *Cloud SaaS Farms*).

Mod ini menyediakan integrasi antarmuka mulus (*seamless UI patches*), sinkronisasi status server secara langsung, pemilihan server mudah, *Smart Toolbelt*, serta lokalisasi penuh Bahasa Indonesia.

---

## ✨ Fitur Utama

- 🌐 **Universal Server Connectivity:** Mendukung koneksi langsung ke server mandiri (*Self-Host IP:Port*) maupun server awan LiteValley (*Subdomain SaaS*).
- 🛠️ **Smart Toolbelt System:** Pintasan cerdas pergantian alat secara otomatis atau via menu vertikal yang responsif.
- 🇮🇩 **Dukungan Penuh Bahasa Indonesia:** Menerjemahkan antarmuka menu, item, dialog, dan interaksi server ke Bahasa Indonesia standar tanpa merusak berkas asli game.
- 🎨 **Title Screen Integration:** Menambahkan tombol akses cepat server dan pemilih bahasa langsung di layar judul game dengan gaya visual otentik Stardew Valley.
- ⚡ **Ringan & Hemat Resource:** Dibangun murni menggunakan .NET 6 dan SMAPI 4.x tanpa menambah beban performa game Anda.

---

## 📥 Panduan Instalasi (Untuk Pemain)

### Prasyarat
1. Game **Stardew Valley (PC)** resmi (versi 1.6 atau lebih baru) dari Steam, GOG, atau PC Game Pass.
2. Telah menginstal **[SMAPI](https://smapi.io/)** (versi 4.0.0 ke atas).

### Langkah Pemasangan
1. Buka halaman **[Releases](https://github.com/ijplabs/litevalley-bridge/releases)** dan unduh berkas ZIP rilis terbaru (`StardewBridge-vX.Y.Z.zip`).
2. Ekstrak isi ZIP tersebut ke dalam folder `Mods` pada instalasi Stardew Valley Anda:
   - **Steam (Windows):** `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\`
   - **GOG (Windows):** `C:\Program Files (x86)\GOG Galaxy\Games\Stardew Valley\Mods\`
   - **Linux:** `~/.local/share/Steam/steamapps/common/Stardew Valley/Mods/`
   - **macOS:** `~/Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS/Mods/`
3. Pastikan struktur folder menjadi: `Mods/StardewBridge/manifest.json`.
4. Jalankan game melalui `StardewModdingAPI.exe`.

---

## 💻 Panduan Kompilasi dari Source Code (Untuk Pengembang)

Jika Anda ingin memodifikasi atau berkontribusi pada mod ini:

### 1. Prasyarat
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- Salinan instalasi Stardew Valley lokal dengan SMAPI terpasang

### 2. Clone Repositori
```bash
git clone git@github.com:ijplabs/litevalley-bridge.git
cd litevalley-bridge
```

### 3. Build Project
```bash
dotnet build LiteValley.Bridge.sln -c Release
```
> Berkas paket distribusi `.zip` akan otomatis digenerate di folder `_releases/` dan terpasang ke folder `Mods/StardewBridge` game lokal Anda.

---

## 📜 Kepatuhan & Lisensi (Tribute)

Proyek ini dibangun dengan komitmen penuh terhadap lisensi dan komunitas:
- **Eric Barone (ConcernedApe):** Terima kasih tak terhingga atas karya agung Stardew Valley. Mod ini tidak mendistribusikan aset komersial berhak cipta.
- **Pathoschild & Tim SMAPI:** Terima kasih atas ekosistem modding yang luar biasa dan alat bantu `Pathoschild.Stardew.ModBuildConfig`.
- **Lisensi:** Mod ini dirilis di bawah lisensi terbuka [MIT License](LICENSE).

Dibuat dengan ❤️ oleh **[IJP Labs](https://github.com/ijplabs)**.
