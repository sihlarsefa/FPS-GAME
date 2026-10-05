# Ayaz Geçidi — Ses Ortamı

Kurgu tatbikat coğrafyası. Atmosfer kanalı ayrı ses ayarı; kritik olaylar metin/ikonla da verilir.

| Bölge | Katman | Not |
|-------|--------|-----|
| Üst sırt | Rüzgâr | Yönlü, seyrek; iletişimi maskelemeyecek dinamik aralık |
| Kayak evi | Ahşap | Gıcırtı; iç/dış geçişte düşük geçiren filtre |
| Radar üssü | Mekanik | Düşük uğultu; yapı dışında hızla söner |
| Kar yüzeyi | Ayak | Kuru/sert kar için iki örnek; hız ve duruşa göre seçim |
| Geçit | Yankı | Kısa kaya yankısı; uzaktan atış yönü korunur |
| Buz göleti | Su | Yalnızca kıyı; güvenli buz üstü yürüyüşü bu konseptte yok |

## Teknik öneriler

- Kar ayak izi: yüzey tag’ine bağlı; sprint/crouch ayrı clip.
- Rüzgâr: 3D ambient emitter sırt hattında; LOD ile uzak kes.
- Radar: loop + distance attenuation; iç mekân low-pass.
