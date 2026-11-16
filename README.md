# PlayBrwsr Portable — YouTube Video & Music (Browsec VPN)

Aplikasi media player **portable untuk Windows** berisi **YouTube Video** dan **YouTube Music** dengan tampilan desktop media player (tanpa address bar/toolbar browser) dan ekstensi **Browsec VPN** yang sudah **terpasang, aktif (ON), privacy disetujui, dan terhubung otomatis ke server Singapore** secara default.

Dirancang khusus untuk jaringan yang memblokir VPN desktop, sehingga seluruh trafik YouTube Video & Music langsung melewati koneksi ekstensi Browsec VPN.

## Cara Menggunakan

1. Jalankan **`PlayBrwsr.exe`** (tidak perlu instalasi).
2. Jendela media player akan langsung terbuka dengan dua mode di bagian atas:
   - **YouTube** (`https://www.youtube.com`)
   - **YouTube Music** (`https://music.youtube.com`)
3. Koneksi **Browsec VPN (Singapore)** sudah aktif otomatis di latar belakang—tidak perlu klik ekstensi, menyetujui privacy policy, atau memilih negara secara manual.

## Opsi Baris Perintah (Opsional)

- `PlayBrwsr.exe` — langsung membuka Media Player (YouTube Video & YouTube Music).
- `PlayBrwsr.exe --video` — membuka YouTube Video saja.
- `PlayBrwsr.exe --music` — membuka YouTube Music saja.
- `PlayBrwsr.exe --launcher` — membuka jendela launcher pilihan mode.
- `PlayBrwsr.exe --hub` — membuka halaman hub lokal & pengecek IP VPN.

## Sifat Portable

Seluruh file aplikasi, runtime Firefox ESR (`runtime\firefox\`), ekstensi Browsec (`extensions\browsec@browsec.com.xpi`), dan profil teraktivasi (`data\profile\`) berada dalam satu folder ini, sehingga dapat langsung disalin ke flashdisk atau PC Windows lain.
