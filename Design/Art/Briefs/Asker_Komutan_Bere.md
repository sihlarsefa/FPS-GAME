# Brief — Asker Komutan Bere Varyantı

**Kimlik:** `char_soldier_commander_beret`  
**Taban:** [`Asker_Dijital.md`](Asker_Dijital.md) · Kamuflaj: [`../TSK_Kamuflaj_Rehberi.md`](../TSK_Kamuflaj_Rehberi.md)

---

## 1. Kullanım
Tim komutanı 3P / portre. Gövde aynı ADD asker; **kask yerine** matar bordo bere (`#4A1C28`). Rozet pad boş (runtime amblem).

## 2. Kapsam
- Yeni mesh: bere (+ saç/ense düzeni kasksız).
- Kask LOD’dan çıkar veya hide flag.
- Yelek / silah socket’leri aynı.
- Opsiyon: hafif rütbe omuz askısı **stilize** (MilitaryRank ikonları C3-4 ile; resmi nişan kopyası değil).

## 3. Poligon
Bere + kafa düzeni +2–4k LOD0. Toplam vücut bütçesi taban + %10.

## 4. Doku
Bere: düz kumaş 1K–2K; normal twill. ADD vücut aynı atlas (paylaşımlı).

## 5. Pivot / rig
Aynı Humanoid. Bere `Helmet` socket’e veya `Head` child `Beret`.

## 6. Teslim
Skin varyant FBX veya aynı FBX’te blendshape/mesh swap talimatı. README: “Commander=Beret visible, Helmet hidden”.

## 7. Kabul
- [ ] Bere rengi rehbere uyuyor
- [ ] Resmi nişan / hilal-yıldız rozet yok
- [ ] Taban asker ile oran tutarlı
- [ ] Mixamo uyumu bozulmamış

## 8. Referans
Komuta görünürlüğü: bere silueti uzaktan okunur; kamuflaj aynı kalır.

## 9. Süre / fiyat
3–5 gün $250–500 (taban model varsa) · doku $100–200

---

## UZATMA — İş ilanı

### TR
**Karakter varyant — Komutan beresi.** Mevcut asker rig’ine bere mesh + malzeme. Resmi arma yok.

### EN
**Character variant — Commander beret.** Beret mesh/material on existing soldier rig. No official insignia.

### Puan
Uyumluluk 30 · Görsel netlik 25 · PBR 20 · Teknik swap 25 · ≥ 4.0
