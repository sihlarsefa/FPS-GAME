# Satın Alınan Paket — Otomatik Karşılama

Asset Store paketi Package Manager (My Assets) ile `Assets/<PaketAdı>/` altına gelir. `Assets/_Project` ve `Assets/ThirdParty` dışındaki her yer taranır.

## Çalıştırma
- Menü: `HAREKÂT > Satın Alınan Paketi Bağla`
- Batch: `-executeMethod Project.EditorTools.BatchEntry.BindPurchasedPack` (`BindThirdParty` zincirinde de çalışır; Console'da Türkçe tablo + ContentOverrides doğrulaması).

## Nasıl çalışır
1. **Tarama** (`PurchasedPackBinder`, saf sezgiler `PurchasedPackRules`): prefab/model adayları.
   - Karakter: SkinnedMeshRenderer; puan = Humanoid avatar +40, >5k üçgen +20 (>20k +10), LODGroup +10, ad ipucu (soldier/military/swat/operator/character...) +25, boy 1.5-2.1 m +10, zombi/sivil vb. -40. En az 40 puan gerekir.
   - Silah: MeshRenderer, en uzun kenar 0.2-1.4 m ve ince; ad ipucu rifle/ak/m4/sniper/shotgun/pistol/smg/mg. Şarjör/dürbün/kutu adları elenir.
2. **Karakter**: Humanoid rig zorlanır, Standard malzemeler URP/Lit kopyasına çevrilir (`_MainTex→_BaseMap`, normal, metal/pürüz, AO, emisyon, alfa kesme; `Assets/ThirdParty/Materials/Purchased`). Sarmalayıcı prefab `Assets/ThirdParty/Characters/Purchased/` altına yazılır ve `ContentOverrides.soldier` doldurulur. Paketin Animator Controller'ı yoksa mevcut controller korunur/yeniden kullanılır.
3. **Silah**: `ThirdPartyWeaponBinder.BuildFromModel` ile Muzzle/Grip_R/Grip_L/Magazine/Bolt/Sight soketleri + ölçek; ad sınıfı weaponId'ye eşlenir (AR → ar_*, sniper → sr_jng90/dmr_*, shotgun → sg_*, pistol → pistol_*, smg → smg_*, MG → lmg_*). Öncelik: satın alınan paket > Quaternius; mevcut paket silahı yalnızca yeni model daha yüksek puanlıysa değişir (etiket `pk_score_N`).
4. **Rapor**: `prefab | tri | eşlenen | soket` tablosu.

## Sahibi görsel olarak kontrol etmeli
- Asker: T-pose/duruş, kamuflaj dokusu pembe/beyaz mı (URP malzeme), Humanoid avatar yeşil mi, boy ~1.8 m.
- Silahlar: namlu +Z'ye bakıyor mu, sol el (Grip_L) ve sağ el tutuşu, nişangah hizası, ölçek. Yanlış eşleşen model varsa `ContentOverrides` içinden elle değiştirin.
- Silah dışı (kutu, şarjör) yanlış seçilmiş mi; tabloda `eşlenen` sütununa bakın.
- Doğrulayıcı raporunda HATA/UYARI var mı.
