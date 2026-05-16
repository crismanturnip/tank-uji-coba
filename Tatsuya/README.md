# Tatsuya

`Tatsuya` adalah bot alternatif keempat yang dibuat dari dasar `LowestEnergyChaser` di `Tubes1_PolicyGradientStrategist/src/alternative-bots/LowestEnergyChaser`.

Karakter bot ini tetap dekat dengan sumbernya: mencari musuh dengan energi paling rendah, mengejar musuh tersebut, lalu menghabisinya dengan tembakan atau tabrakan.

Penyempurnaan yang ditambahkan:

- nama class dan konfigurasi dipisah menjadi `Tatsuya`,
- warna bot dibuat berbeda,
- radar dan gun tetap dikunci ke target terlemah,
- tembakan hanya dilakukan saat gun siap,
- prediksi posisi musuh dibatasi agar tidak keluar arena,
- wall handling dibuat sedikit lebih aman tanpa mengubah gaya chase.

## Cara Build

```powershell
dotnet build Tatsuya/Tatsuya.csproj
```

## Cara Run

Pastikan server atau GUI Robocode Tank Royale sudah berjalan, lalu:

```powershell
cd Tatsuya
dotnet run
```
