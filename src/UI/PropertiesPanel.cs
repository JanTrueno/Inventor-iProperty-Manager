using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Inventor;
using IPropertyManager.Core;
using Color = System.Drawing.Color;
using Environment = System.Environment;
using Materials = IPropertyManager.Core.Materials;

namespace IPropertyManager.UI
{
    /// <summary>
    /// The docked iProperties panel, laid out like Inventor's property panels (and the Tab and Slot panel): section bars
    /// with chevrons, flat fields, Save / Revert at the bottom. Rows are added from the "+" menu (grouped like Inventor's
    /// iProperties dialog, or a custom name) and moved or removed by right-clicking them. It shows the documents chosen by
    /// <see cref="Targets"/> and writes through <see cref="SaveJob"/>. While there are unsaved edits it stays on the
    /// documents they belong to instead of following the selection.
    /// </summary>
    internal sealed class PropertiesPanel : UserControl
    {
        private const int LabelWidth = 96;
        private const string MixedHint = "(multiple values)";

        private readonly Inventor.Application _app;
        private readonly Theme _theme;
        private readonly List<FieldSpec> _fields;

        private readonly Label _caption = new Label { AutoSize = false, Height = 20, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        private readonly Label _subCaption = new Label { AutoSize = true, Tag = "keep" };
        private readonly Label _materialNote = new Label { AutoSize = true, Tag = "keep" };
        private readonly Label _status = new Label { AutoSize = false, Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8, 2, 8, 0), Tag = "keep", Visible = false };
        private readonly Dictionary<FieldSpec, Control> _inputs = new Dictionary<FieldSpec, Control>();
        private IconButton _copy, _folder, _add;
        private ThemedCombo _material;
        private FieldBox _mass;
        private InventorButton _save, _revert;

        private List<_Document> _docs = new List<_Document>();
        private readonly HashSet<FieldSpec> _dirty = new HashSet<FieldSpec>();
        private bool _materialDirty;
        private bool _loading;

        public PropertiesPanel(Inventor.Application app, Theme theme)
        {
            _app = app;
            _theme = theme;
            _fields = FieldStore.Load();
            Font = new System.Drawing.Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = theme.Back;
            ForeColor = theme.Text;
            Dock = DockStyle.Fill;
            Build();
        }

        public bool HasEdits => _dirty.Count > 0 || _materialDirty;

        // ----- public API ---------------------------------------------------------------------------------------

        /// <summary>Shows the documents behind the selection; while there are unsaved edits it stays where it is.</summary>
        public void FollowSelection()
        {
            List<_Document> docs = Targets.Resolve(_app, out bool fromSelection);
            if (HasEdits)
            {
                if (!SameDocs(docs, _docs)) SetStatus("Unsaved changes. Save or Revert to follow the selection.", false);
                return;
            }
            Show(docs, fromSelection);
        }

        /// <summary>A document changed outside the panel: show its new values unless the user is editing.</summary>
        public void ReloadValues()
        {
            if (HasEdits || _docs.Count == 0) return;
            Show(_docs, _docs.Count > 1);
        }

        public void Clear()
        {
            _dirty.Clear();
            _materialDirty = false;
            Show(new List<_Document>(), false);
        }

        // ----- showing values -----------------------------------------------------------------------------------

        private void Show(List<_Document> docs, bool fromSelection)
        {
            _docs = docs;
            _loading = true;
            try
            {
                if (docs.Count == 0) { _caption.Text = "No document"; _subCaption.Text = ""; }
                else if (docs.Count == 1)
                {
                    _caption.Text = SafeName(docs[0]);
                    _subCaption.Text = fromSelection ? "Selected component" : "Active document";
                }
                else
                {
                    _caption.Text = docs.Count + " documents";
                    _subCaption.Text = string.Join(", ", docs.Take(3).Select(SafeName)) + (docs.Count > 3 ? ", ..." : "");
                }

                foreach (var kv in _inputs) ShowField(kv.Key, kv.Value);
                ShowMaterial();
            }
            finally { _loading = false; }

            _dirty.Clear();
            _materialDirty = false;
            UpdateButtons();
            SetStatus(null, false);
        }

        private void ShowField(FieldSpec spec, Control input)
        {
            Combined c = _docs.Count > 0 ? PropertyIO.Read(_docs, spec) : new Combined();
            switch (input)
            {
                case FieldBox box:
                    box.Text = c.Mixed ? "" : PropertyIO.Display(c.Value);
                    box.Hint = c.Mixed ? MixedHint : spec.Kind == FieldKind.Date ? DateHint : "";
                    box.Invalid = false;
                    break;
                case ThemedCombo combo:
                    string v = c.Mixed || !(c.Value is bool) ? null : PropertyIO.Display(c.Value);
                    combo.SetItems(new[] { "Yes", "No" }, v, c.Mixed ? MixedHint : "");
                    break;
            }
            input.Enabled = _docs.Count > 0;
        }

        private static string DateHint => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern.ToLowerInvariant();

        private void ShowMaterial()
        {
            string mass = null;
            try { mass = _docs.Count > 0 ? MassInfo.Total(_app, _docs) : null; }
            catch (Exception ex) { Log.Write("Mass failed: " + ex.Message); }
            _mass.Text = mass ?? "";
            _mass.Hint = mass == null ? "n/a" : "";

            List<PartDocument> parts = _docs.OfType<PartDocument>().ToList();
            bool any = parts.Count > 0;
            _material.Visible = any;
            _materialNote.Text = _docs.Count == 0 ? "" : !any ? "Only parts have a material." :
                parts.Count < _docs.Count ? "Applies to the " + parts.Count + " part(s) only." : "";
            _materialNote.Visible = _materialNote.Text.Length > 0;
            if (!any) return;

            string[] current = parts.Select(Materials.Current).Distinct().ToArray();
            string shared = current.Length == 1 ? current[0] : null;
            _material.SetItems(Materials.Available(_app, parts), shared, shared == null ? MixedHint : null);
        }

        private static string SafeName(_Document d)
        {
            try { return d.DisplayName; } catch { return "?"; }
        }

        private static bool SameDocs(List<_Document> a, List<_Document> b) => a.Count == b.Count && a.All(b.Contains);

        // ----- saving -------------------------------------------------------------------------------------------

        private void Save()
        {
            if (_docs.Count == 0 || !HasEdits) return;

            var edits = new List<KeyValuePair<FieldSpec, object>>();
            foreach (FieldSpec spec in _fields.Where(_dirty.Contains))
            {
                Control input = _inputs[spec];
                string text = input is ThemedCombo combo ? combo.Value ?? "" : input.Text;
                if (!PropertyIO.TryParse(text, spec.Kind, out object value))
                {
                    if (input is FieldBox box) box.Invalid = true;
                    SetStatus("\"" + text + "\" is not a valid " + KindWord(spec.Kind) + " for " + spec.Name + ".", true);
                    return;
                }
                edits.Add(new KeyValuePair<FieldSpec, object>(spec, value));
            }
            string material = _materialDirty ? _material.Value : null;

            SaveJob.Outcome outcome;
            try
            {
                Cursor = Cursors.WaitCursor;
                outcome = SaveJob.Run(_app, _docs, edits, material);
            }
            catch (Exception ex)
            {
                Log.Write("Save failed: " + ex);
                SetStatus("Saving failed: " + ex.Message, true);
                return;
            }
            finally { Cursor = Cursors.Default; }

            _dirty.Clear();
            _materialDirty = false;
            Show(_docs, _docs.Count > 1);
            SetStatus(outcome.Summary(), outcome.Failed > 0);
        }

        private static string KindWord(FieldKind k) =>
            k == FieldKind.Number ? "number" : k == FieldKind.Date ? "date" : k == FieldKind.YesNo ? "yes/no value" : "value";

        private void Revert()
        {
            _dirty.Clear();
            _materialDirty = false;
            FollowSelection();
        }

        // ----- rows: add, move, remove ---------------------------------------------------------------------------

        private void AddField(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (_fields.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                SetStatus(name + " is already shown.", false);
                return;
            }
            _fields.Add(FieldSpec.For(name));
            FieldsChanged();
        }

        private void RemoveField(FieldSpec spec)
        {
            _fields.Remove(spec);
            _dirty.Remove(spec);
            FieldsChanged();
        }

        private void MoveField(FieldSpec spec, int delta)
        {
            int i = _fields.IndexOf(spec), j = i + delta;
            if (i < 0 || j < 0 || j >= _fields.Count) return;
            _fields.RemoveAt(i);
            _fields.Insert(j, spec);
            FieldsChanged();
        }

        /// <summary>Saves the row list and rebuilds the panel, keeping any unsaved edits that are still shown.</summary>
        private void FieldsChanged()
        {
            FieldStore.Save(_fields);
            var keep = _dirty.Where(_inputs.ContainsKey).ToDictionary(s => s, s => _inputs[s] is ThemedCombo c ? c.SelectedItem : (object)_inputs[s].Text);
            bool materialDirty = _materialDirty;
            object material = _material.SelectedItem;

            SuspendLayout();
            // the labels live for the panel's lifetime; take them out before their containers are disposed
            foreach (Control keepAlive in new Control[] { _caption, _subCaption, _materialNote, _status })
                keepAlive.Parent?.Controls.Remove(keepAlive);
            foreach (Control c in Controls.Cast<Control>().ToList()) c.Dispose();
            Controls.Clear();
            _inputs.Clear();
            Build();
            ResumeLayout(true);

            _loading = true;
            foreach (var kv in _inputs) ShowField(kv.Key, kv.Value);
            ShowMaterial();
            foreach (var kv in keep)
            {
                if (_inputs[kv.Key] is ThemedCombo combo) combo.SelectedItem = kv.Value;
                else _inputs[kv.Key].Text = kv.Value as string;
            }
            if (materialDirty) _material.SelectedItem = material;
            _loading = false;

            _dirty.Clear();
            foreach (FieldSpec s in keep.Keys) _dirty.Add(s);
            _materialDirty = materialDirty;
            UpdateButtons();
        }

        /// <summary>The "+" menu: a submenu per tab of Inventor's iProperties dialog, and a Custom submenu.</summary>
        private ContextMenuStrip AddMenu()
        {
            var menu = new ThemedMenu(_theme);
            foreach (string tab in Catalog.Tabs)
            {
                var sub = new ToolStripMenuItem(tab);
                foreach (KnownProperty k in Catalog.All.Where(p => p.Tab == tab))
                {
                    bool shown = _fields.Any(f => f.Name == k.Name);
                    var item = new ToolStripMenuItem(k.Name) { Enabled = !shown, ForeColor = shown ? _theme.Dim : _theme.Text };
                    string name = k.Name;
                    item.Click += (s, e) => AddField(name);
                    sub.DropDownItems.Add(item);
                }
                ThemedMenu.Style(sub, _theme);
                menu.Items.Add(sub);
            }

            var custom = new ToolStripMenuItem("Custom");
            var typed = new ToolStripTextBox { AutoSize = false, Width = 180, BackColor = _theme.Back, ForeColor = _theme.Text, BorderStyle = BorderStyle.FixedSingle, ToolTipText = "Type a name and press Enter" };
            typed.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                string name = typed.Text;
                menu.Close();
                AddField(name);
            };
            custom.DropDownItems.Add(new ToolStripLabel("New name (Enter to add):") { ForeColor = _theme.Dim });
            custom.DropDownItems.Add(typed);

            // custom properties the shown documents already have
            string[] existing = ExistingCustomNames();
            if (existing.Length > 0)
            {
                custom.DropDownItems.Add(new ToolStripSeparator());
                foreach (string n in existing)
                {
                    string name = n;
                    var item = new ToolStripMenuItem(n);
                    item.Click += (s, e) => AddField(name);
                    custom.DropDownItems.Add(item);
                }
            }
            custom.DropDownOpened += (s, e) => typed.Focus();
            ThemedMenu.Style(custom, _theme);
            menu.Items.Add(custom);
            return menu;
        }

        private string[] ExistingCustomNames()
        {
            var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (_Document d in _docs)
            {
                try { foreach (Property p in d.PropertySets[Catalog.CustomSet]) names.Add(p.Name); } catch { }
            }
            return names.Where(n => !_fields.Any(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase))).ToArray();
        }

        /// <summary>Right-click on a row: move it or take it off the panel (the iProperty itself is not touched).</summary>
        private ContextMenuStrip RowMenu(FieldSpec spec)
        {
            var menu = new ThemedMenu(_theme);
            int i = _fields.IndexOf(spec);
            var up = new ToolStripMenuItem("Move up") { Enabled = i > 0, ForeColor = _theme.Text };
            var down = new ToolStripMenuItem("Move down") { Enabled = i < _fields.Count - 1, ForeColor = _theme.Text };
            var remove = new ToolStripMenuItem("Remove from panel") { ForeColor = _theme.Text };
            up.Click += (s, e) => MoveField(spec, -1);
            down.Click += (s, e) => MoveField(spec, 1);
            remove.Click += (s, e) => RemoveField(spec);
            menu.Items.AddRange(new ToolStripItem[] { up, down, new ToolStripSeparator(), remove });
            return menu;
        }

        // ----- layout -------------------------------------------------------------------------------------------

        private void Build()
        {
            _copy = new IconButton(_theme, "Copy the name", DrawCopy) { Size = new Size(26, 22) };
            _folder = new IconButton(_theme, "Open the folder in Explorer", DrawFolder) { Size = new Size(26, 22) };
            _add = new IconButton(_theme, "Add an iProperty", DrawPlus) { Size = new Size(26, 22) };
            _material = new ThemedCombo(_theme);
            _mass = new FieldBox(_theme) { ReadOnly = true };
            _save = new InventorButton(_theme, "Save", true);
            _revert = new InventorButton(_theme, "Revert", false);

            var stack = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddSection(stack, "Selection", SelectionTable());
            AddSection(stack, "iProperties", PropertiesTable());
            AddSection(stack, "Material", MaterialTable());

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(stack);

            // Save and Revert on the left, as OK / Cancel are in Inventor.
            var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 38, ColumnCount = 3, Padding = new Padding(8, 7, 8, 0) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.Controls.Add(_save, 0, 0);
            buttons.Controls.Add(_revert, 1, 0);
            _save.Margin = _revert.Margin = Padding.Empty;

            Controls.Add(scroll);
            Controls.Add(_status);
            Controls.Add(buttons);

            _theme.Apply(this);
            _subCaption.ForeColor = _materialNote.ForeColor = _theme.Dim;
            WireEvents();
            UpdateButtons();
        }

        private void AddSection(TableLayoutPanel stack, string title, Control body)
        {
            var header = new SectionHeader(_theme, title, true) { Font = Font, Dock = DockStyle.Fill, Margin = Padding.Empty };
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(8, 6, 8, 6);
            header.Toggled += isOpen => body.Visible = isOpen;
            int r = stack.RowCount++;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.Controls.Add(header, 0, r);
            r = stack.RowCount++;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            stack.Controls.Add(body, 0, r);
        }

        private static TableLayoutPanel Table()
        {
            var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }

        private Control SelectionTable()
        {
            var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            _caption.Margin = new Padding(0, 2, 0, 0);
            _subCaption.Margin = new Padding(0, 0, 0, 2);
            _copy.Margin = _folder.Margin = Padding.Empty;
            t.Controls.Add(_caption, 0, 0);
            t.Controls.Add(_copy, 1, 0);
            t.Controls.Add(_folder, 2, 0);
            t.Controls.Add(_subCaption, 0, 1);
            t.SetColumnSpan(_subCaption, 3);
            return t;
        }

        private Control PropertiesTable()
        {
            var t = Table();
            foreach (FieldSpec spec in _fields)
            {
                Control input;
                if (spec.Kind == FieldKind.YesNo) input = new ThemedCombo(_theme);
                else
                {
                    var box = new FieldBox(_theme);
                    box.Committed += Save;
                    input = box;
                }
                _inputs[spec] = input;
                Label label = Row(t, spec.Name, input);
                FieldSpec s = spec;
                label.MouseUp += (o, e) => { if (e.Button == MouseButtons.Right) RowMenu(s).Show(label, e.Location); };
                _tips.SetToolTip(label, spec.Name + " (right-click to move or remove)");
            }

            // "+" under the rows, aligned with the fields
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _add.Margin = new Padding(0, 3, 0, 0);
            t.Controls.Add(_add, 1, r);
            if (_fields.Count == 0)
            {
                var none = new Label { Text = "Add iProperties with +", AutoSize = true, Tag = "keep", ForeColor = _theme.Dim, Margin = new Padding(0, 7, 0, 0) };
                t.Controls.Add(none, 0, r);
            }
            return t;
        }

        private Control MaterialTable()
        {
            var t = Table();
            Row(t, "Material", _material);
            _materialNote.Margin = new Padding(0, 2, 0, 2);
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(_materialNote, 1, r);
            Row(t, "Mass", _mass);
            return t;
        }

        private Label Row(TableLayoutPanel t, string label, Control c)
        {
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = new Label { Text = label, AutoSize = false, Width = LabelWidth - 4, Height = 22, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Anchor = AnchorStyles.Left };
            c.Margin = new Padding(0, 3, 0, 3);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            t.Controls.Add(l, 0, r);
            t.Controls.Add(c, 1, r);
            return l;
        }

        private readonly ToolTip _tips = new ToolTip();

        // ----- behaviour ----------------------------------------------------------------------------------------

        private void WireEvents()
        {
            foreach (var kv in _inputs)
            {
                FieldSpec spec = kv.Key;
                Control input = kv.Value;
                EventHandler changed = (s, e) =>
                {
                    if (_loading) return;
                    _dirty.Add(spec);
                    if (input is FieldBox box) box.Invalid = !PropertyIO.TryParse(box.Text, spec.Kind, out _);
                    UpdateButtons();
                };
                if (input is FieldBox fb) fb.TextChanged += changed;
                else if (input is ThemedCombo cb) cb.SelectedIndexChanged += changed;
            }
            _material.SelectedIndexChanged += (s, e) =>
            {
                if (_loading) return;
                _materialDirty = _material.Value != null;
                UpdateButtons();
            };
            _add.Click += (s, e) => AddMenu().Show(_add, new System.Drawing.Point(0, _add.Height));
            _copy.Click += (s, e) =>
            {
                string text = string.Join(Environment.NewLine, _docs.Select(SafeName));
                if (text.Length == 0) return;
                try { Clipboard.SetText(text); SetStatus("Copied.", false); } catch { /* clipboard busy */ }
            };
            _folder.Click += (s, e) => OpenFolder();
            _save.Click += (s, e) => Save();
            _revert.Click += (s, e) => Revert();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S)) { Save(); return true; }
            if (keyData == Keys.Escape && HasEdits) { Revert(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Opens Explorer on the shown document's folder with the file selected (the first one when several are shown).</summary>
        private void OpenFolder()
        {
            string path = null;
            try { path = _docs.FirstOrDefault()?.FullFileName; } catch { }
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                SetStatus(_docs.Count == 0 ? "No document shown." : "This document has not been saved to a file yet.", false);
                return;
            }
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex) { SetStatus("Could not open Explorer: " + ex.Message, true); }
        }

        private void UpdateButtons()
        {
            _save.Enabled = HasEdits && _docs.Count > 0;
            _revert.Enabled = HasEdits;
        }

        private void SetStatus(string text, bool isError)
        {
            _status.Text = text ?? "";
            _status.Visible = !string.IsNullOrEmpty(text);
            _status.ForeColor = isError ? Color.FromArgb(240, 110, 100) : _theme.Dim;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }

        // ----- icons --------------------------------------------------------------------------------------------

        private static void DrawCopy(Graphics g, Rectangle b, Color ink, Color accent)
        {
            float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            using (var pen = new Pen(ink, 1.2f))
            {
                g.DrawRectangle(pen, cx - 5, cy - 3, 8, 9);
                g.DrawLines(pen, new[] { new PointF(cx - 2, cy - 3), new PointF(cx - 2, cy - 6), new PointF(cx + 6, cy - 6), new PointF(cx + 6, cy + 3), new PointF(cx + 3, cy + 3) });
            }
        }

        private static void DrawFolder(Graphics g, Rectangle b, Color ink, Color accent)
        {
            float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            using (var pen = new Pen(ink, 1.2f))
            {
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 7, cy + 5), new PointF(cx - 7, cy - 5), new PointF(cx - 2, cy - 5), new PointF(cx, cy - 3),
                    new PointF(cx + 7, cy - 3), new PointF(cx + 7, cy + 5), new PointF(cx - 7, cy + 5)
                });
                g.DrawLine(pen, cx - 7, cy - 1, cx + 7, cy - 1);
            }
        }

        private static void DrawPlus(Graphics g, Rectangle b, Color ink, Color accent)
        {
            float cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            using (var pen = new Pen(ink, 1.6f))
            {
                g.DrawLine(pen, cx - 6, cy, cx + 6, cy);
                g.DrawLine(pen, cx, cy - 6, cx, cy + 6);
            }
        }
    }
}
