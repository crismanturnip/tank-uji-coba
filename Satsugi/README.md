# Satsugi

`Satsugi` adalah bot alternatif ketiga yang dibuat dari dasar strategi `Hebi` milik `Tubes1_PolicyGradientStrategist/src/alternative-bots/Hebi`, lalu disesuaikan agar tetap membawa karakter agresif Hebi.

Strategi utamanya adalah **aggressive ramming and close-range bullet damage**:

- radar melakukan sweep saat belum ada target,
- setelah target ditemukan, radar mengunci dengan overshoot kecil,
- bot mengejar prediksi posisi musuh dengan kecepatan tinggi,
- jika target sudah dekat, bot mengarahkan badan langsung ke musuh untuk ramming,
- tembakan besar diprioritaskan pada jarak dekat,
- jika berada dekat dinding, bot keluar sebentar ke tengah agar tidak kehilangan momentum karena wall damage,
- fire power tetap adaptif agar agresif tetapi tidak membuang energi saat target jauh.

## Cara Build

```powershell
dotnet build Satsugi/Satsugi.csproj
```

## Cara Run

Pastikan GUI/server Robocode Tank Royale sudah berjalan, lalu:

```powershell
cd Satsugi
dotnet run
```
