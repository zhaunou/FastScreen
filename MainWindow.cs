using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LocalScreenshot
{
    sealed class MainWindow : Form
    {
        readonly Panel welcome = new Panel(), workspace = new Panel(), settings = new Panel();
        readonly Label folderLabel = new Label(), statusLabel = new Label(), shortcutHint = new Label(), settingsStatus = new Label();
        readonly Button openLast;
        readonly Timer captureTimer = new Timer();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        readonly Icon appIcon;
        readonly string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalScreenshot");
        readonly ShortcutBox fullBox, regionBox;
        readonly ToolStripMenuItem fullMenu, regionMenu;
        GlobalShortcuts shortcuts;
        Preferences preferences;
        string folder, lastFile;
        bool regionMode, capturing, returnToTray, exiting, modal;
        string HotkeyFile { get { return Path.Combine(settingsFolder, "hotkeys.txt"); } }

        public MainWindow()
        {
            Text = "Локальный скриншот";
            Font = new Font("Segoe UI", 10);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(920, 630);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 252);
            appIcon = Design.CreateIcon(); Icon = appIcon;
            preferences = Preferences.Read(HotkeyFile);
            folder = Path.Combine(settingsFolder, "Screenshots");
            try
            {
                string saved = File.ReadAllText(Path.Combine(settingsFolder, "folder.txt")).Trim();
                if (Path.IsPathRooted(saved) && !IsNetworkPath(saved)) folder = saved;
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }

            Controls.Add(Design.Text("▣   Локальный скриншот", 28, 24, 300, 32, 14, Design.Navy, true));
            Button home = Design.Button("Съёмка", 470, 19, 114, false);
            Button keys = Design.Button("Горячие клавиши", 596, 19, 174, false);
            Button hide = Design.Button("В трей  ↓", 782, 19, 110, false);
            home.Click += delegate { ShowPage(workspace); };
            keys.Click += delegate { OpenSettings(); };
            hide.Click += delegate { HideToTray(); };
            Controls.AddRange(new Control[] { home, keys, hide });
            foreach (Panel page in new Panel[] { welcome, workspace, settings })
            { page.SetBounds(0, 82, 920, 548); Controls.Add(page); }

            welcome.BackColor = Design.Navy;
            welcome.Controls.Add(Design.Text("ВАШ ЭКРАН. ВАШИ ФАЙЛЫ.", 42, 42, 460, 25, 10, Color.FromArgb(130, 237, 218), true));
            welcome.Controls.Add(Design.Text("Поймайте момент.\nСохраните у себя.", 38, 98, 490, 132, 32, Color.White, true));
            welcome.Controls.Add(Design.Text("Весь экран или только важная деталь.\nОдин снимок — и PNG уже на вашем компьютере.", 42, 251, 450, 65, 12, Color.FromArgb(177, 192, 218), false));
            Button start = Design.Button("Начать съёмку   →", 42, 346, 238, true);
            start.Click += delegate { ShowPage(workspace); };
            welcome.Controls.Add(start);
            HeroArt art = new HeroArt { Location = new Point(502, 85), Size = new Size(400, 340) };
            welcome.Controls.Add(art);
            welcome.Controls.Add(Design.Text("01   Выберите область", 42, 454, 250, 26, 11, Color.White, true));
            welcome.Controls.Add(Design.Text("02   Сделайте снимок", 332, 454, 250, 26, 11, Color.White, true));
            welcome.Controls.Add(Design.Text("03   Откройте свой PNG", 623, 454, 270, 26, 11, Color.White, true));
            welcome.Controls.Add(Design.Text("Без аккаунтов и серверов. Снимки остаются на локальном диске.", 42, 503, 800, 26, 10, Color.FromArgb(177, 192, 218), false));

            workspace.Controls.Add(Design.Text("Что сохраним?", 32, 20, 650, 50, 26, Design.Navy, true));
            workspace.Controls.Add(Design.Text("Выберите режим. Перед съёмкой окно автоматически скроется.", 35, 77, 850, 28, 11, Design.Muted, false));
            AddCaptureCard("01  /  ВЕСЬ ЭКРАН", "Всё в одном кадре", "Все подключённые мониторы", 34, false);
            AddCaptureCard("02  /  ВЫДЕЛЕННАЯ ОБЛАСТЬ", "Только то, что нужно", "Выделите прямоугольник мышью", 469, true);
            shortcutHint.SetBounds(35, 300, 850, 36); shortcutHint.ForeColor = Design.Muted;
            workspace.Controls.Add(shortcutHint);
            workspace.Controls.Add(Design.Text("ПАПКА СОХРАНЕНИЯ", 35, 358, 350, 24, 9, Design.Muted, true));
            folderLabel.SetBounds(35, 390, 650, 36); folderLabel.AutoEllipsis = true; folderLabel.Text = folder;
            workspace.Controls.Add(folderLabel);
            Button choose = Design.Button("Изменить…", 733, 377, 150, false);
            choose.Click += ChooseFolder; workspace.Controls.Add(choose);
            statusLabel.SetBounds(35, 439, 848, 28); statusLabel.Text = "Готово к съёмке. Всё сохраняется локально в PNG.";
            statusLabel.ForeColor = Design.Muted; statusLabel.AutoEllipsis = true; workspace.Controls.Add(statusLabel);
            openLast = Design.Button("Последний снимок", 35, 482, 218, false); openLast.Enabled = false;
            openLast.Click += delegate { try { System.Diagnostics.Process.Start(lastFile); } catch (Exception ex) { ShowError(ex); } };
            Button openFolder = Design.Button("Открыть папку", 267, 482, 196, false);
            openFolder.Click += delegate { try { EnsureLocalFolder(); Directory.CreateDirectory(folder); System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\""); } catch (Exception ex) { ShowError(ex); } };
            workspace.Controls.AddRange(new Control[] { openLast, openFolder });

            settings.Controls.Add(Design.Text("Снимок одним сочетанием", 32, 20, 850, 50, 26, Design.Navy, true));
            settings.Controls.Add(Design.Text("Работает из любой программы, даже когда скриншотер свёрнут в трей.", 35, 77, 850, 30, 11, Design.Muted, false));
            settings.Controls.Add(Design.Text("Весь экран", 35, 145, 370, 32, 15, Design.Navy, true));
            settings.Controls.Add(Design.Text("Выделенная область", 469, 145, 390, 32, 15, Design.Navy, true));
            fullBox = new ShortcutBox(preferences.Full); fullBox.SetBounds(35, 197, 412, 42);
            regionBox = new ShortcutBox(preferences.Region); regionBox.SetBounds(469, 197, 412, 42);
            settings.Controls.AddRange(new Control[] { fullBox, regionBox });
            settings.Controls.Add(Design.Text("Нажмите на поле и введите сочетание с Ctrl или Alt.\nМожно добавить Shift. Поддерживаются буквы, цифры, F1–F11, F13–F24 и PrintScreen.\nTab переключает поля. Изменения вступят в силу после сохранения.", 35, 264, 850, 94, 11, Design.Muted, false));
            Button save = Design.Button("Сохранить сочетания", 35, 389, 260, true);
            save.Click += SaveShortcuts;
            Button defaults = Design.Button("По умолчанию", 311, 389, 210, false);
            defaults.Click += delegate { Preferences original = new Preferences(); fullBox.SetShortcut(original.Full); regionBox.SetShortcut(original.Region); settingsStatus.Text = "Сочетания сброшены в полях. Нажмите «Сохранить сочетания»."; };
            settings.Controls.AddRange(new Control[] { save, defaults });
            settingsStatus.SetBounds(35, 459, 846, 68); settingsStatus.ForeColor = Design.Muted; settings.Controls.Add(settingsStatus);

            trayMenu.Items.Add("Открыть программу", null, delegate { RestoreWindow(); });
            fullMenu = new ToolStripMenuItem("Весь экран", null, delegate { BeginCapture(false); });
            regionMenu = new ToolStripMenuItem("Выделить область", null, delegate { BeginCapture(true); });
            trayMenu.Items.Add(fullMenu); trayMenu.Items.Add(regionMenu);
            trayMenu.Items.Add("Горячие клавиши", null, delegate { RestoreWindow(); OpenSettings(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Выйти", null, delegate { if (!capturing && !modal) { exiting = true; Close(); } });
            tray = new NotifyIcon { Icon = appIcon, Text = "Локальный скриншот", ContextMenuStrip = trayMenu, Visible = true };
            tray.DoubleClick += delegate { RestoreWindow(); };
            captureTimer.Interval = 300; captureTimer.Tick += CaptureNow;
            ShowPage(welcome);
            UpdateShortcutStatus();
        }

        void AddCaptureCard(string eyebrow, string title, string caption, int x, bool region)
        {
            Panel card = new Panel { Location = new Point(x, 125), Size = new Size(412, 159), BackColor = Color.White };
            card.Controls.Add(Design.Text(eyebrow, 20, 15, 375, 23, 9, Design.Accent, true));
            card.Controls.Add(Design.Text(title, 17, 44, 380, 33, 19, Design.Navy, true));
            card.Controls.Add(Design.Text(caption, 20, 83, 372, 26, 10, Design.Muted, false));
            Button button = Design.Button(region ? "Выделить область   →" : "Снять весь экран   →", 20, 113, 370, true);
            button.Height = 34; button.Click += delegate { BeginCapture(region); }; card.Controls.Add(button); workspace.Controls.Add(card);
        }

        void ShowPage(Panel page) { welcome.Visible = page == welcome; workspace.Visible = page == workspace; settings.Visible = page == settings; page.BringToFront(); }
        void OpenSettings() { fullBox.SetShortcut(preferences.Full); regionBox.SetShortcut(preferences.Region); UpdateShortcutStatus(); ShowPage(settings); }
        void UpdateShortcutStatus()
        {
            shortcutHint.Text = preferences.Full + "  /  весь экран     ·     " + preferences.Region + "  /  область";
            if (shortcuts == null) return;
            settingsStatus.Text = shortcuts.FullActive && shortcuts.RegionActive ? "Сочетания активны. Можно свернуть приложение в трей." :
                "Не удалось включить: " + (!shortcuts.FullActive ? "весь экран — " + preferences.Full + ". " : "") +
                (!shortcuts.RegionActive ? "область — " + preferences.Region + ". " : "") + "Выберите свободные сочетания.";
            if (!shortcuts.FullActive || !shortcuts.RegionActive) statusLabel.Text = "Есть занятые сочетания. Проверьте раздел «Горячие клавиши».";
        }

        void SaveShortcuts(object sender, EventArgs e)
        {
            Preferences next = new Preferences { Full = fullBox.Shortcut, Region = regionBox.Shortcut };
            if (next.Full.Encode() == next.Region.Encode()) { settingsStatus.Text = "Для двух режимов нужны разные сочетания."; return; }
            string error;
            if (!shortcuts.TryApply(next, preferences, out error)) { UpdateShortcutStatus(); settingsStatus.Text = error + " Предыдущие сочетания сохранены. " + settingsStatus.Text; return; }
            try { next.Save(HotkeyFile); preferences = next; UpdateShortcutStatus(); settingsStatus.Text = "Сохранено. Сочетания работают, пока приложение запущено."; }
            catch (Exception ex) { shortcuts.Register(preferences); UpdateShortcutStatus(); settingsStatus.Text = "Не удалось сохранить настройки: " + ex.Message; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (preferences != null) { shortcuts = new GlobalShortcuts(Handle); shortcuts.Register(preferences); UpdateShortcutStatus(); }
        }
        protected override void OnHandleDestroyed(EventArgs e) { if (shortcuts != null) { shortcuts.Dispose(); shortcuts = null; } base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312 && !modal && !(settings.Visible && ContainsFocus))
            { int id = message.WParam.ToInt32(); if (id == 1 || id == 2) BeginCapture(id == 2); }
            base.WndProc(ref message);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); if (WindowState == FormWindowState.Minimized && tray != null) HideToTray(); }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; if (!capturing && !modal) HideToTray(); }
            base.OnFormClosing(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                captureTimer.Dispose();
                if (shortcuts != null) shortcuts.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                trayMenu.Dispose(); if (appIcon != null) appIcon.Dispose();
            }
            base.Dispose(disposing);
        }
        void HideToTray() { if (!capturing && !modal) { Hide(); ShowInTaskbar = false; } }
        void RestoreWindow() { if (capturing || modal) return; ShowInTaskbar = true; WindowState = FormWindowState.Normal; Show(); Activate(); }

        internal static bool IsNetworkPath(string path)
        {
            if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).DriveType == DriveType.Network; } catch { return true; }
        }
        void EnsureLocalFolder() { if (IsNetworkPath(folder)) throw new IOException("Выберите папку на локальном диске. Сетевые папки не поддерживаются."); }
        void ChooseFolder(object sender, EventArgs e)
        {
            modal = true;
            try
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Папка для скриншотов на локальном диске", SelectedPath = folder })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (IsNetworkPath(dialog.SelectedPath)) throw new IOException("Выберите папку на локальном диске.");
                    Directory.CreateDirectory(settingsFolder); File.WriteAllText(Path.Combine(settingsFolder, "folder.txt"), dialog.SelectedPath);
                    folder = dialog.SelectedPath; folderLabel.Text = folder;
                }
            }
            catch (Exception ex) { ShowError(ex); }
            finally { modal = false; }
        }
        void BeginCapture(bool region)
        {
            if (capturing || modal) return;
            try { EnsureLocalFolder(); } catch (Exception ex) { ShowError(ex); return; }
            returnToTray = !Visible || WindowState == FormWindowState.Minimized;
            capturing = true; regionMode = region; Hide(); captureTimer.Start();
        }
        void CaptureNow(object sender, EventArgs e)
        {
            captureTimer.Stop();
            try
            {
                using (Bitmap desktop = LocalScreenshot.Capture.Desktop())
                {
                    if (regionMode)
                    {
                        using (SelectionWindow selection = new SelectionWindow(desktop))
                        {
                            if (selection.ShowDialog() != DialogResult.OK) { statusLabel.Text = "Съёмка отменена."; return; }
                            using (Bitmap crop = desktop.Clone(selection.SelectedArea, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) SaveShot(crop);
                        }
                    }
                    else SaveShot(desktop);
                }
            }
            catch (Exception ex) { returnToTray = false; ShowError(ex); }
            finally { capturing = false; if (!returnToTray) { ShowPage(workspace); RestoreWindow(); } }
        }
        void SaveShot(Bitmap bitmap)
        {
            EnsureLocalFolder(); lastFile = LocalScreenshot.Capture.Save(bitmap, folder); openLast.Enabled = true;
            statusLabel.Text = "Сохранено: " + Path.GetFileName(lastFile);
            if (returnToTray) tray.ShowBalloonTip(2500, "Снимок сохранён локально", Path.GetFileName(lastFile), ToolTipIcon.Info);
        }
        void ShowError(Exception ex)
        {
            bool wasModal = modal; modal = true;
            try { ShowInTaskbar = true; WindowState = FormWindowState.Normal; Show(); MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { modal = wasModal; }
        }
    }
}
