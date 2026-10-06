using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>
    /// DONANIM sayfası: birincil/ikincil silah ve 5 yuvalı eklenti seçici, kamuflaj ve silah kaplaması (sahip olunanlar),
    /// dönen 3B silah önizlemesi ve animasyonlu istatistik çubukları (eklentisiz taban değeri soluk iz olarak gösterilir).
    /// </summary>
    public sealed class LoadoutPage : MonoBehaviour
    {
        private sealed class Row
        {
            public Text Value;
            public Image Chip;
        }

        private LoadoutPreview _preview;
        private LoadoutSelection _sel;
        private bool _showSecondary;
        private Text _weaponName;
        private Text _weaponMeta;
        private Row _primaryRow, _secondaryRow, _camoRow, _skinRow;
        private readonly Row[] _attRows = new Row[AttachmentCatalog.SlotCount];
        private const int Bars = LoadoutStats.BarCount;
        private readonly UiProgressBar[] _bars = new UiProgressBar[Bars];
        private readonly Image[] _ghost = new Image[Bars];
        private readonly Text[] _barValues = new Text[Bars];
        private readonly Text[] _barDeltas = new Text[Bars];
        private readonly float[] _barTarget = new float[Bars];
        private readonly float[] _barShown = new float[Bars];
        private readonly float[] _ghostTarget = new float[Bars];
        private readonly float[] _ghostShown = new float[Bars];
        private readonly float[] _lastValues = new float[Bars];
        private readonly int[] _deltaPoints = new int[Bars];
        private bool _hasLast;
        private float _deltaTimer;
        private const float DeltaHold = 4f;
        private Text _weaponDesc;
        private RectTransform _nodeRoot;
        private RectTransform _previewRect;
        private RectTransform _swatchRoot;
        private string _swatchSignature;
        private sealed class Node
        {
            public RectTransform Box;
            public Text Caption;
            public Text Name;
            public Text Effects;
            public Image Line;
            public Image Dot;
            public Image Back;
            public bool Active;
        }

        private readonly Node[] _nodes = new Node[AttachmentCatalog.SlotCount];

        // Düğümlerin önizleme içindeki sabit yerleşimi (0..1): nişangâh, namlu, tutamak, şarjör, dipçik.
        private static readonly Vector2[] NodeLayout =
        {
            new Vector2(0.60f, 0.86f), new Vector2(0.88f, 0.64f), new Vector2(0.68f, 0.17f), new Vector2(0.40f, 0.17f), new Vector2(0.11f, 0.52f)
        };
        private Text _extraStats;
        private RectTransform _barsRoot;

        private static readonly string[] SlotNames = { "NİŞANGÂH", "NAMLU", "TUTAMAK", "ŞARJÖR", "DİPÇİK" };

        public static LoadoutPage Create(RectTransform page)
        {
            var host = page.gameObject.AddComponent<LoadoutPage>();
            host._sel = LoadoutSelection.Shared;
            host.Build(page);
            return host;
        }

        private void Build(RectTransform page)
        {
            var title = UiFactory.Label(page, "DONANIM", 56, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-300f, 0f));
            UiFactory.AddShadow(title, UiTheme.TextShadow, new Vector2(3f, -3f));
            var line = UiFactory.Image(page, null, UiTheme.Accent);
            UiFactory.SetRect(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -80f), new Vector2(180f, -76f));

            // Sol sütun: yuva satırları.
            var column = UiFactory.VerticalList(page, 6f);
            UiFactory.SetRect(column, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(430f, -100f));
            var vlg = column.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.childControlHeight = true;
                vlg.childForceExpandHeight = false;
            }

            _primaryRow = AddRow(column, "BİRİNCİL SİLAH", d => CycleWeapon(true, d), () => { _showSecondary = false; Refresh(false); });
            _secondaryRow = AddRow(column, "İKİNCİL SİLAH", d => CycleWeapon(false, d), () => { _showSecondary = true; Refresh(false); });
            UiFactory.Spacer(column, 6f);
            for (var s = 0; s < AttachmentCatalog.SlotCount; s++)
            {
                var slot = (AttachmentSlot)s;
                _attRows[s] = AddRow(column, SlotNames[s], d => CycleAttachment(slot, d), null, 40f, 21);
            }

            UiFactory.Spacer(column, 6f);
            _camoRow = AddRow(column, "KAMUFLAJ", d => CycleCosmetic(CosmeticsService.SlotCamo, d), null, 44f, 22, true);
            _skinRow = AddRow(column, "SİLAH KAPLAMASI", d => CycleCosmetic(CosmeticsService.SlotWeaponSkin, d), null, 44f, 22, true);

            // Sağ: önizleme + isim + çubuklar.
            var previewHolder = UiFactory.CreateRect("PreviewHolder", page);
            UiFactory.SetRect(previewHolder, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(450f, 168f), new Vector2(0f, -100f));
            _previewRect = previewHolder;
            var back = UiFactory.Image(previewHolder, UiSprites.Vignette, new Color(0.02f, 0.03f, 0.02f, 0.78f));
            back.raycastTarget = false;
            var frame = UiFactory.Image(previewHolder, UiSprites.GetRoundedRectOutline(UiTheme.CornerRadius), UiTheme.WithAlpha(UiTheme.PanelBorder, 0.7f));
            frame.raycastTarget = false;
            var raw = UiFactory.RawImage(previewHolder, null);
            raw.raycastTarget = true;
            UiFactory.Stretch(raw);
            raw.color = new Color(1f, 1f, 1f, 0f);
            _preview = LoadoutPreview.Create(raw, 1280, 720);
            BuildNodes(previewHolder);

            _weaponName = UiFactory.Label(previewHolder, string.Empty, 40, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            _weaponName.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_weaponName, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -12f));
            UiFactory.AddShadow(_weaponName, UiTheme.TextShadow, new Vector2(2f, -2f));
            _weaponMeta = UiFactory.Label(previewHolder, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Khaki, FontStyle.Bold);
            _weaponMeta.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_weaponMeta, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -92f), new Vector2(-20f, -60f));
            _weaponDesc = UiFactory.Label(previewHolder, string.Empty, UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.TextDim);
            _weaponDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            _weaponDesc.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.SetRect(_weaponDesc, new Vector2(0f, 1f), new Vector2(0.48f, 1f), new Vector2(22f, -156f), new Vector2(0f, -98f));
            var hint = UiFactory.Label(previewHolder, "sürükle: döndür", UiTheme.FontTiny, TextAnchor.LowerRight, UiTheme.TextMuted);
            UiFactory.SetRect(hint, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(-14f, 30f));

            _barsRoot = UiFactory.CreateRect("Bars", page);
            UiFactory.SetRect(_barsRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(450f, 0f), new Vector2(0f, 156f));
            for (var i = 0; i < Bars; i++)
                BuildBar(_barsRoot, i);

            _extraStats = UiFactory.Label(_barsRoot, string.Empty, UiTheme.FontSmall, TextAnchor.LowerLeft, UiTheme.TextDim, FontStyle.Bold);
            _extraStats.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(_extraStats, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f), new Vector2(0f, 26f));
        }

        private void BuildBar(RectTransform parent, int index)
        {
            var col = index / 3;
            var xa = col * 0.5f;
            var xb = xa + 0.5f;
            var gap = col == 0 ? 14f : 0f;
            var y = -(index % 3) * 34f;
            var cap = UiFactory.Label(parent, LoadoutStats.Labels[index], UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextDim, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Anchor(cap, new Vector2(xa, 1f), new Vector2(0f, 1f), new Vector2(2f + (col == 1 ? 10f : 0f), y - 17f), new Vector2(110f, 28f));

            var bar = UiFactory.ProgressBar(parent, UiTheme.Accent, UiTheme.Track);
            bar.TrailEnabled = false;
            UiFactory.SetRect(bar, new Vector2(xa, 1f), new Vector2(xb, 1f), new Vector2(116f + (col == 1 ? 10f : 0f), y - 30f), new Vector2(-96f - gap, y - 6f));
            _bars[index] = bar;

            var ghost = UiFactory.Image(bar.transform, null, new Color(1f, 1f, 1f, 0.55f));
            ghost.raycastTarget = false;
            UiFactory.SetRect(ghost, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -5f), new Vector2(0f, -2f));
            _ghost[index] = ghost;

            var val = UiFactory.Label(parent, "0", UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(val, new Vector2(xb, 1f), new Vector2(xb, 1f), new Vector2(-40f - gap, y - 30f), new Vector2(-gap, y - 6f));
            _barValues[index] = val;

            var delta = UiFactory.Label(parent, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleRight, UiTheme.Success, FontStyle.Bold);
            delta.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.SetRect(delta, new Vector2(xb, 1f), new Vector2(xb, 1f), new Vector2(-94f - gap, y - 30f), new Vector2(-42f - gap, y - 6f));
            _barDeltas[index] = delta;
        }

        private void BuildNodes(RectTransform holder)
        {
            _nodeRoot = UiFactory.CreateRect("Nodes", holder);
            UiFactory.Stretch(_nodeRoot);

            // Önce çizgiler/noktalar (düğüm kutularının altında kalsın), sonra kutular.
            for (var s = 0; s < _nodes.Length; s++)
            {
                _nodes[s] = new Node();
                var line = UiFactory.Image(_nodeRoot, UiSprites.White, UiTheme.WithAlpha(UiTheme.Accent, 0.8f));
                line.raycastTarget = false;
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                line.rectTransform.anchorMin = line.rectTransform.anchorMax = Vector2.zero;
                _nodes[s].Line = line;
                var dot = UiFactory.Image(_nodeRoot, UiSprites.White, UiTheme.Accent);
                dot.raycastTarget = false;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
                dot.rectTransform.sizeDelta = new Vector2(11f, 11f);
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                _nodes[s].Dot = dot;
            }

            for (var s = 0; s < _nodes.Length; s++)
            {
                var slot = (AttachmentSlot)s;
                var n = _nodes[s];
                var box = UiFactory.Panel(_nodeRoot, UiTheme.WithAlpha(UiTheme.PanelDark, 0.92f), UiSprites.ChamferRect);
                box.gameObject.name = "Node_" + SlotNames[s];
                box.anchorMin = box.anchorMax = Vector2.zero;
                box.sizeDelta = new Vector2(190f, 58f);
                n.Box = box;
                n.Back = box.GetComponent<Image>();
                var btn = box.gameObject.AddComponent<Button>();
                btn.targetGraphic = n.Back;
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() => CycleAttachment(slot, 1));

                n.Caption = UiFactory.Label(box, SlotNames[s], UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
                n.Caption.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(n.Caption, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -20f), new Vector2(-6f, -3f));
                n.Name = UiFactory.Label(box, string.Empty, UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
                n.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.SetRect(n.Name, new Vector2(0f, 0.28f), new Vector2(1f, 0.72f), new Vector2(10f, 0f), new Vector2(-6f, 0f));
                n.Effects = UiFactory.Label(box, string.Empty, UiTheme.FontTiny, TextAnchor.LowerLeft, UiTheme.TextDim, FontStyle.Bold);
                n.Effects.horizontalOverflow = HorizontalWrapMode.Overflow;
                n.Effects.supportRichText = true;
                UiFactory.SetRect(n.Effects, new Vector2(0f, 0f), new Vector2(1f, 0.4f), new Vector2(10f, 3f), new Vector2(-6f, 0f));
            }

            _swatchRoot = UiFactory.CreateRect("Swatches", holder);
            UiFactory.SetRect(_swatchRoot, new Vector2(0f, 0f), new Vector2(0.6f, 0f), new Vector2(18f, 10f), new Vector2(0f, 44f));
        }

        private static Row AddRow(RectTransform parent, string caption, Action<int> cycle, Action focus, float height = 66f, int valueSize = 26, bool chip = false)
        {
            var row = UiFactory.Panel(parent, UiTheme.WithAlpha(UiTheme.PanelDark, 0.88f), UiSprites.ChamferRect);
            row.gameObject.name = "Row_" + caption;
            UiFactory.LayoutSize(row, -1f, height, 1f);

            var cap = UiFactory.Label(row, caption, UiTheme.FontTiny, TextAnchor.UpperLeft, UiTheme.TextMuted, FontStyle.Bold);
            cap.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (height > 50f)
                UiFactory.SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -26f), new Vector2(-60f, -6f));
            else
                UiFactory.SetRect(cap, new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(16f, 0f), new Vector2(0f, 0f));
            if (height <= 50f)
                cap.alignment = TextAnchor.MiddleLeft;

            var value = UiFactory.Label(row, string.Empty, valueSize, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (height > 50f)
                UiFactory.SetRect(value, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 4f), new Vector2(-96f, -22f));
            else
                UiFactory.SetRect(value, new Vector2(0.38f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-92f, 0f));

            Image chipImage = null;
            if (chip)
            {
                chipImage = UiFactory.Image(row, UiSprites.White, Color.gray);
                chipImage.raycastTarget = false;
                UiFactory.Anchor(chipImage, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(24f, 24f));
            }

            if (focus != null)
            {
                var img = row.GetComponent<Image>();
                var focusBtn = row.gameObject.AddComponent<Button>();
                focusBtn.targetGraphic = img;
                focusBtn.transition = Selectable.Transition.None;
                focusBtn.navigation = new Navigation { mode = Navigation.Mode.None };
                focusBtn.onClick.AddListener(() => focus());
            }

            var left = SmallArrow(row, "‹", -1, cycle, -62f);
            var right = SmallArrow(row, "›", 1, cycle, -10f);
            return new Row { Value = value, Chip = chipImage };
        }

        private static Button SmallArrow(RectTransform row, string glyph, int dir, Action<int> cycle, float x)
        {
            var b = UiFactory.Button(row, glyph, () => cycle(dir), UiButtonStyle.Default);
            UiFactory.Anchor(b, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(44f, 36f));
            var l = UiFactory.GetButtonLabel(b);
            if (l != null)
            {
                l.fontSize = UiTheme.FontLarge;
                UiFactory.Stretch(l, 0f);
            }

            return b;
        }

        // ------------------------------------------------------------------ Eylemler

        private void CycleWeapon(bool primary, int dir)
        {
            var list = primary ? LoadoutSelection.PrimaryCandidates() : LoadoutSelection.SecondaryCandidates();
            var current = primary ? _sel.PrimaryId : _sel.SecondaryId;
            var next = LoadoutSelection.Cycle(list, current, dir);
            if (primary)
                _sel.PrimaryId = next;
            else
                _sel.SecondaryId = next;
            _showSecondary = !primary;
            UiWidgets.PlaySound(SoundId.WeaponEquip);
            Refresh(true);
        }

        private void CycleAttachment(AttachmentSlot slot, int dir)
        {
            if (!WeaponCatalog.TryGet(_sel.PrimaryId, out var weapon))
                return;
            var options = LoadoutSelection.CompatibleAttachments(slot, weapon.Category);
            if (options.Count <= 1)
                return;
            _sel.SetAttachment(slot, LoadoutSelection.Cycle(options, _sel.GetAttachment(slot), dir));
            _showSecondary = false;
            UiWidgets.PlaySound(SoundId.UiClick);
            Refresh(false);
        }

        private void CycleCosmetic(string slot, int dir)
        {
            var service = CosmeticsRuntime.Service;
            if (service == null)
                return;
            var owned = new List<string>();
            foreach (var def in service.InSlot(slot))
                if (service.IsOwned(def.id))
                    owned.Add(def.id);
            if (owned.Count == 0)
                return;
            service.Equip(LoadoutSelection.Cycle(owned, service.GetEquipped(slot), dir));
            UiWidgets.PlaySound(SoundId.UiClick);
            Refresh(slot == CosmeticsService.SlotWeaponSkin);
        }

        /// <summary>Sayfa gösterilince çağrılır (sahip olunan kozmetikler değişmiş olabilir).</summary>
        public void OnShown() => Refresh(true);

        private void OnEnable() => Refresh(true);

        // ------------------------------------------------------------------ Yenileme

        private void Refresh(bool rebuildModel)
        {
            if (_sel == null || _primaryRow == null)
                return;

            var primaryId = _sel.PrimaryId;
            var secondaryId = _sel.SecondaryId;
            WeaponCatalog.TryGet(primaryId, out var primary);
            WeaponCatalog.TryGet(secondaryId, out var secondary);
            _primaryRow.Value.text = primary != null ? primary.DisplayName : "—";
            _secondaryRow.Value.text = secondary != null ? secondary.DisplayName : "—";

            for (var s = 0; s < _attRows.Length; s++)
            {
                var id = _sel.GetAttachment((AttachmentSlot)s);
                var options = primary != null ? LoadoutSelection.CompatibleAttachments((AttachmentSlot)s, primary.Category) : null;
                var def = id != null ? AttachmentCatalog.Get(id) : null;
                _attRows[s].Value.text = options == null || options.Count <= 1 ? "uyumsuz" : (def != null ? def.DisplayName : "YOK");
            }

            RefreshCosmetic(_camoRow, CosmeticsService.SlotCamo);
            RefreshCosmetic(_skinRow, CosmeticsService.SlotWeaponSkin);

            var shown = _showSecondary ? secondary : primary;
            if (shown != null)
            {
                _weaponName.text = MenuText.ToUpperTr(shown.DisplayName);
                _weaponMeta.text = WeaponCatalog.GetCategoryName(shown.Category) + "  ·  " + WeaponCatalog.GetAmmoName(shown.AmmoType)
                                   + (_showSecondary ? "  ·  İKİNCİL" : "  ·  BİRİNCİL");
                _weaponDesc.text = LoadoutStats.Describe(shown.Category) + "\nKalibre: " + WeaponCatalog.GetAmmoName(shown.AmmoType);
            }

            if (_preview != null && (rebuildModel || _lastShownId != (shown != null ? shown.WeaponId : null)))
            {
                _lastShownId = shown != null ? shown.WeaponId : null;
                _preview.SetWeapon(shown);
            }

            // Çubuklar: önizlenen silah; eklentiler yalnızca birincil için geçerli.
            var attachments = _showSecondary ? null : _sel.AttachmentIds();
            var withMods = LoadoutStats.Compute(shown, attachments);
            var baseSet = LoadoutStats.Compute(shown);
            var changed = !_hasLast;
            for (var i = 0; i < Bars; i++)
            {
                if (_hasLast && Mathf.Abs(LoadoutStats.Value(withMods, i) - _lastValues[i]) >= 0.005f)
                    changed = true;
            }

            for (var i = 0; i < Bars; i++)
            {
                var v = LoadoutStats.Value(withMods, i);
                var b = LoadoutStats.Value(baseSet, i);
                if (changed && _hasLast)
                    _deltaPoints[i] = LoadoutStats.DeltaPoints(_lastValues[i], v);
                _lastValues[i] = v;
                _barTarget[i] = v;
                _ghostTarget[i] = Mathf.Clamp01(b);
                if (!_hasLast)
                {
                    _barShown[i] = 0f;
                    _ghostShown[i] = Mathf.Clamp01(b);
                }

                _barValues[i].text = Mathf.RoundToInt(v * 100f).ToString();
                var delta = v - b;
                _ghost[i].color = Mathf.Abs(delta) < 0.005f ? new Color(1f, 1f, 1f, 0.25f) : (delta > 0f ? new Color(UiTheme.Success.r, UiTheme.Success.g, UiTheme.Success.b, 0.9f) : new Color(UiTheme.Amber.r, UiTheme.Amber.g, UiTheme.Amber.b, 0.9f));
            }

            if (changed && _hasLast)
                _deltaTimer = DeltaHold;
            _hasLast = true;

            RefreshNodes(primary);
            RefreshSwatches();
            ApplyTint(shown);

            if (shown != null)
                _extraStats.text = Mathf.RoundToInt(withMods.RoundsPerMinute) + " d/dk   ·   " + withMods.MagazineSize + " mermi   ·   "
                                   + withMods.WeightKg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " kg";
        }

        private string _lastShownId;

        private void RefreshNodes(WeaponDefinitionData primary)
        {
            for (var s = 0; s < _nodes.Length; s++)
            {
                var n = _nodes[s];
                if (n == null)
                    continue;
                var slot = (AttachmentSlot)s;
                var options = primary != null ? LoadoutSelection.CompatibleAttachments(slot, primary.Category) : null;
                var usable = !_showSecondary && options != null && options.Count > 1;
                n.Active = usable;
                n.Box.gameObject.SetActive(!_showSecondary);
                n.Line.gameObject.SetActive(usable);
                n.Dot.gameObject.SetActive(usable);
                n.Back.color = UiTheme.WithAlpha(UiTheme.PanelDark, usable ? 0.92f : 0.5f);
                var id = _sel.GetAttachment(slot);
                var def = id != null ? AttachmentCatalog.Get(id) : null;
                n.Name.text = !usable ? "uyumsuz" : (def != null ? def.DisplayName : "YOK");
                n.Name.color = usable ? UiTheme.Text : UiTheme.TextMuted;
                n.Effects.text = EffectsRich(def);
            }
        }

        private static string EffectsRich(AttachmentDefinition def)
        {
            var raw = LoadoutStats.AttachmentEffects(def);
            var extra = AttachmentCatalog.ExtraEffectTags(def);
            if (!string.IsNullOrEmpty(extra))
                raw = string.IsNullOrEmpty(raw) ? extra : raw + " " + extra;
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var parts = raw.Split(' ');
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < parts.Length; i++)
            {
                var good = parts[i].StartsWith("+");
                if (i > 0)
                    sb.Append(' ');
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(good ? UiTheme.Success : UiTheme.Danger)).Append('>').Append(parts[i]).Append("</color>");
            }

            return sb.ToString();
        }

        private void RefreshSwatches()
        {
            var service = CosmeticsRuntime.Service;
            if (_swatchRoot == null || service == null)
                return;
            var defs = new List<CosmeticDefinition>();
            var sig = new System.Text.StringBuilder(service.GetEquipped(CosmeticsService.SlotCamo) ?? "-").Append('|');
            foreach (var d in service.InSlot(CosmeticsService.SlotCamo))
            {
                defs.Add(d);
                sig.Append(d.id).Append(service.IsOwned(d.id) ? '1' : '0');
            }

            if (sig.ToString() == _swatchSignature)
                return;
            _swatchSignature = sig.ToString();
            for (var c = _swatchRoot.childCount - 1; c >= 0; c--)
                Destroy(_swatchRoot.GetChild(c).gameObject);

            var equipped = service.GetEquipped(CosmeticsService.SlotCamo);
            for (var i = 0; i < defs.Count && i < 14; i++)
            {
                var def = defs[i];
                var owned = service.IsOwned(def.id);
                var frame = UiFactory.Image(_swatchRoot, UiSprites.White, def.id == equipped ? UiTheme.Accent : UiTheme.WithAlpha(UiTheme.PanelBorder, 0.7f));
                UiFactory.Anchor(frame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 36f, 0f), new Vector2(32f, 32f));
                var fill = UiFactory.Image(frame.transform, UiSprites.White, owned ? CosmeticsRuntime.PreviewColor(def) : new Color(0.15f, 0.15f, 0.15f, 1f));
                UiFactory.Stretch(fill, 3f);
                fill.raycastTarget = false;
                if (!owned)
                {
                    var lockMark = UiFactory.Label(frame.transform, "×", UiTheme.FontSmall, TextAnchor.MiddleCenter, UiTheme.TextMuted, FontStyle.Bold);
                    UiFactory.Stretch(lockMark);
                    lockMark.raycastTarget = false;
                }

                var id = def.id;
                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = frame;
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() =>
                {
                    var svc = CosmeticsRuntime.Service;
                    if (svc == null || !svc.IsOwned(id))
                        return;
                    svc.Equip(id);
                    UiWidgets.PlaySound(SoundId.UiClick);
                    Refresh(false);
                });
            }
        }

        /// <summary>Kuşanılmış kaplama/kamuflaj rengini önizleme modeline canlı uygular.</summary>
        private void ApplyTint(WeaponDefinitionData shown)
        {
            if (_preview == null || shown == null)
                return;
            var service = CosmeticsRuntime.Service;
            if (service == null)
            {
                _preview.SetTint(null);
                return;
            }

            if (CosmeticsRuntime.TryGetWeaponTint(shown.WeaponId, out var skinTint))
            {
                _preview.SetTint(skinTint);
                return;
            }

            var id = service.GetEquipped(CosmeticsService.SlotCamo);
            if (id != null && service.TryGet(id, out var def) && def.unlockMethod != CosmeticsService.MethodDefault)
                _preview.SetTint(CosmeticsRuntime.PreviewColor(def));
            else
                _preview.SetTint(null);
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (_deltaTimer > 0f)
                _deltaTimer -= dt;
            var fade = Mathf.Clamp01(_deltaTimer / 1f);
            for (var i = 0; i < Bars; i++)
            {
                if (_bars[i] == null)
                    continue;
                _barShown[i] = MainMenuMotion.Approach(_barShown[i], _barTarget[i], 9f, dt);
                _ghostShown[i] = MainMenuMotion.Approach(_ghostShown[i], _ghostTarget[i], 9f, dt);
                _bars[i].SetValue(_barShown[i], false);
                _ghost[i].rectTransform.anchorMax = new Vector2(_ghostShown[i], 0f);
                var pts = _deltaPoints[i];
                var text = _deltaTimer > 0f ? LoadoutStats.DeltaText(pts) : string.Empty;
                if (_barDeltas[i].text != text)
                    _barDeltas[i].text = text;
                var c = pts >= 0 ? UiTheme.Success : UiTheme.Danger;
                c.a = fade;
                _barDeltas[i].color = c;
            }
        }

        private void LateUpdate()
        {
            if (_preview == null || _nodeRoot == null || !_preview.IsLive)
                return;
            var w = _previewRect.rect.width;
            var h = _previewRect.rect.height;
            for (var s = 0; s < _nodes.Length; s++)
            {
                var n = _nodes[s];
                if (n == null || _showSecondary)
                    continue;
                var boxPos = new Vector2(NodeLayout[s].x * w, NodeLayout[s].y * h);
                n.Box.anchoredPosition = boxPos;
                if (!n.Active)
                    continue;
                if (!_preview.TryGetMountViewport((AttachmentSlot)s, out var vp))
                {
                    n.Line.gameObject.SetActive(false);
                    n.Dot.gameObject.SetActive(false);
                    continue;
                }

                n.Line.gameObject.SetActive(true);
                n.Dot.gameObject.SetActive(true);
                var dotPos = new Vector2(vp.x * w, vp.y * h);
                n.Dot.rectTransform.anchoredPosition = dotPos;
                var d = dotPos - boxPos;
                var len = d.magnitude;
                var lr = n.Line.rectTransform;
                lr.anchoredPosition = boxPos;
                lr.sizeDelta = new Vector2(len, 2f);
                lr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
        }

        private static void RefreshCosmetic(Row row, string slot)
        {
            var service = CosmeticsRuntime.Service;
            if (service == null)
            {
                row.Value.text = "—";
                return;
            }

            var id = service.GetEquipped(slot);
            if (id != null && service.TryGet(id, out var def))
            {
                row.Value.text = def.name;
                if (row.Chip != null)
                    row.Chip.color = CosmeticsRuntime.PreviewColor(def);
            }
            else
            {
                row.Value.text = "VARSAYILAN";
                if (row.Chip != null)
                    row.Chip.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            }
        }
    }
}
