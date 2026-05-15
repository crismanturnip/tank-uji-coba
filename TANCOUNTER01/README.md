# TANCOUNTER01

`TANCOUNTER01` adalah bot alternatif untuk Robocode Tank Royale yang dibuat sebagai tandingan dari `TANTRUMP01`. Bot ini tetap memakai pendekatan **algoritma greedy**, tetapi heuristic yang digunakan berbeda dari bot utama.

Strategi utama bot ini adalah:

> **Greedy Safe Distance Counter Attack**

Artinya, pada setiap turn bot memilih aksi lokal terbaik berdasarkan kondisi saat itu, dengan prioritas menjaga posisi aman, mempertahankan jarak efektif, lalu menyerang ketika kondisi tembak cukup menguntungkan.

## Tujuan Bot

Bot ini dibuat untuk menjadi alternatif strategi dalam tugas besar Strategi Algoritma. Fokus utamanya bukan memilih target dengan target score seperti `TANTRUMP01`, melainkan mengambil keputusan greedy berdasarkan:

- keamanan posisi terhadap dinding,
- jarak terhadap musuh,
- energi sendiri,
- energi musuh,
- kestabilan radar lock,
- kesiapan senjata untuk menembak.

Dengan kata lain, `TANCOUNTER01` mencoba bertahan lebih aman dulu, lalu melakukan counter attack ketika jarak dan lock target sudah cukup baik.

## Teknik Greedy Yang Digunakan

Teknik greedy yang diterapkan adalah **greedy by safe distance and local combat condition**.

Pada setiap turn, bot tidak mencari solusi global untuk seluruh battle. Bot hanya mengevaluasi kondisi lokal saat ini, lalu memilih aksi yang dianggap paling menguntungkan untuk turn tersebut.

Urutan prioritas greedy:

1. Jika dekat dinding, keluar dari area dinding.
2. Jika musuh terlalu dekat, bergerak diagonal menjauh.
3. Jika musuh berada pada jarak ideal, lakukan circling dan counter attack.
4. Jika musuh terlalu jauh, dekati secara aman dengan arah miring.
5. Jika target hilang, lakukan radar sweep 360 derajat.
6. Jika senjata siap dan target valid, pilih fire power terbaik berdasarkan heuristic.

## Fungsi Objektif Greedy

Secara konseptual, fungsi objektif lokal yang ingin dimaksimalkan adalah:

```text
nilai_aksi =
    safety_position
  + safe_distance
  + attack_opportunity
  + energy_efficiency
  + radar_confidence
```

Penjelasan:

- `safety_position`: tinggi jika bot tidak dekat dinding.
- `safe_distance`: tinggi jika jarak bot berada di zona aman/ideal.
- `attack_opportunity`: tinggi jika musuh berada pada jarak tembak efektif.
- `energy_efficiency`: tinggi jika fire power sesuai dengan energi sendiri.
- `radar_confidence`: tinggi jika target baru saja ter-scan dan lock masih stabil.

Implementasinya tidak menghitung rumus matematis eksplisit untuk semua aksi, tetapi menerjemahkan fungsi objektif tersebut menjadi aturan greedy berbasis prioritas.

## Heuristic Yang Digunakan

### 1. Heuristic Jarak Musuh

Bot membagi jarak musuh menjadi beberapa kondisi:

- `TooCloseDistance`: musuh terlalu dekat dan dianggap berbahaya.
- `IdealMinDistance` sampai `IdealMaxDistance`: jarak ideal untuk counter attack.
- Di atas `IdealMaxDistance`: musuh terlalu jauh, bot mendekat secara aman.

Keputusan:

- Terlalu dekat: bot bergerak diagonal menjauh.
- Jarak menengah: bot melakukan gerakan menyamping/circling.
- Terlalu jauh: bot mendekat miring agar tidak mudah ditembak lurus.

### 2. Heuristic Dinding

Jika bot berada dekat dinding, bot langsung memprioritaskan keluar menuju tengah arena.

Alasannya:

- dinding meningkatkan risiko wall damage,
- ruang gerak menjadi sempit,
- bot lebih mudah diprediksi,
- sulit melakukan dodge atau circling.

Karena itu, kondisi dekat dinding memiliki prioritas lebih tinggi daripada menyerang.

### 3. Heuristic Energi Sendiri

Jika energi bot rendah, bot menjadi lebih defensif:

- fire power dikurangi,
- movement tetap aktif untuk bertahan,
- bot menghindari pemborosan energi pada tembakan besar.

Parameter yang digunakan:

```csharp
LowEnergyThreshold
```

### 4. Heuristic Energi Musuh

Jika energi musuh rendah dan target masih valid, bot dapat memakai fire power lebih besar untuk mencoba menghabisi musuh.

Namun, tembakan besar hanya digunakan jika:

- target berada pada jarak yang cukup baik,
- radar lock masih segar,
- gun heat sudah siap,
- energi sendiri masih cukup.

### 5. Heuristic Target Lock

Bot tidak menembak besar jika target sudah lama tidak terlihat. Jika target hilang terlalu lama, bot menghapus target dan kembali melakukan radar sweep.

Parameter yang digunakan:

```csharp
RadarLostTurnLimit
```

## Radar Sweep dan Lock Radar

Bot memiliki dua mode radar:

### Radar Sweep

Jika belum ada musuh atau target hilang, radar melakukan sweep 360 derajat.

Tujuannya:

- menemukan musuh sejak awal battle,
- mendapatkan informasi posisi musuh,
- mengembalikan tracking jika target hilang.

### Lock Radar

Jika musuh terdeteksi, data musuh disimpan:

- id musuh,
- posisi `X` dan `Y`,
- bearing,
- distance,
- energy,
- speed,
- direction,
- turn terakhir terlihat.

Radar kemudian diarahkan ke posisi target dengan sedikit overshoot agar tracking lebih stabil.

## Greedy Movement

Movement bot berada di fungsi:

```csharp
MoveGreedily()
```

Aturan movement:

- dekat dinding: keluar ke tengah arena,
- musuh terlalu dekat: diagonal menjauh,
- musuh jarak ideal: circling/strafe,
- musuh jauh: mendekat secara miring,
- tidak ada target: patroli sambil radar sweep.

Gerakan diagonal dan miring sengaja dipakai agar bot tidak terlalu mudah diprediksi oleh tembakan linear.

## Greedy Fire Power

Pemilihan kekuatan tembakan berada di fungsi:

```csharp
ChooseFirePower()
```

Aturan fire power:

- jarak dekat: power cukup besar,
- jarak menengah: power sedang,
- jarak jauh: power kecil,
- energi sendiri rendah: hemat energi,
- energi musuh rendah: power dinaikkan untuk finishing,
- target lock tidak stabil: power dibatasi kecil.

Bot juga hanya menembak jika:

- `GunHeat == 0`,
- target masih valid,
- arah gun sudah cukup dekat ke target,
- energi sendiri cukup.

## Perbedaan Dengan TANTRUMP01

| Aspek | TANTRUMP01 | TANCOUNTER01 |
| --- | --- | --- |
| Fokus utama | Target score dan adaptive attack | Safe distance dan counter attack |
| Pemilihan target | Menggunakan score target | Menggunakan target terakhir yang valid |
| Movement | Menjaga jarak dan anti ditempel | Prioritas keluar dinding, diagonal escape, circling |
| Fire power | Dipengaruhi jarak, energi, dan akurasi | Dipengaruhi jarak aman, energi, dan stabilitas lock |
| Radar | Sweep dan lock target | Sweep 360 lalu lock stabil dengan target freshness |
| Gaya strategi | Lebih agresif berbasis target | Lebih defensif-posisional berbasis jarak aman |

## Parameter Yang Bisa Diubah

Parameter utama ada di awal file `TANCOUNTER01.cs`:

```csharp
private const double TooCloseDistance = 180;
private const double IdealMinDistance = 260;
private const double IdealMaxDistance = 430;
private const double FarDistance = 560;
private const double WallMargin = 90;
private const double LowEnergyThreshold = 22;
private const int RadarLostTurnLimit = 14;
```

Catatan tuning:

- Naikkan `TooCloseDistance` jika ingin bot lebih cepat kabur.
- Turunkan `TooCloseDistance` jika ingin bot lebih berani bertarung dekat.
- Perbesar `IdealMinDistance` dan `IdealMaxDistance` jika ingin bot menjaga jarak lebih jauh.
- Perbesar `WallMargin` jika ingin bot lebih takut dinding.
- Perbesar `LowEnergyThreshold` jika ingin bot lebih cepat hemat energi.
- Perkecil `RadarLostTurnLimit` jika ingin bot lebih cepat kembali ke radar sweep.

## Cara Build

Dari folder project:

```powershell
cd TANCOUNTER01
dotnet build
```

Atau dari root repository:

```powershell
dotnet build TANCOUNTER01/TANCOUNTER01.csproj
```

## Cara Run

Pastikan server atau GUI Robocode Tank Royale sudah berjalan terlebih dahulu.

Lalu jalankan:

```powershell
cd TANCOUNTER01
dotnet run
```

Jika server belum aktif, bot akan gagal connect ke:

```text
ws://localhost:7654/
```

Itu bukan error logic bot, melainkan karena engine Robocode belum berjalan.

## File Konfigurasi Bot

File konfigurasi berada di:

```text
TANCOUNTER01/TANCOUNTER01.json
```

Isi penting:

```json
{
  "name": "TANCOUNTER01",
  "version": "1.0",
  "authors": ["Crisman Turnip"],
  "description": "Greedy safe distance counter attack bot with wall escape, radar sweep, and adaptive fire power"
}
```

## Kesimpulan

`TANCOUNTER01` adalah bot greedy alternatif yang original dan berbeda dari `TANTRUMP01`. Bot ini cocok dijelaskan sebagai penerapan greedy berbasis heuristic jarak aman, posisi terhadap dinding, energi, dan kualitas target lock.

Keputusan bot bersifat lokal per turn, sehingga sesuai dengan karakteristik greedy: memilih aksi yang terlihat paling baik pada kondisi saat ini tanpa melakukan pencarian global terhadap seluruh kemungkinan battle.
