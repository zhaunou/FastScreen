using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LocalScreenshot
{
    sealed class MainWindow : Form
    {
        readonly Panel welcome = new FluentPanel { Gradient = true }, workspace = new Panel(), settings = new Panel();
        readonly Button navHome, navKeys;
        readonly Label folderLabel = new Label(), statusLabel = new Label(), fullShortcutHint = new Label(), regionShortcutHint = new Label(), settingsStatus = new Label();
        readonly Button openLast;
        readonly Timer captureTimer = new Timer();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        readonly Icon appIcon;
        readonly ScreenshotSound screenshotSound = new ScreenshotSound();
        readonly string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalScreenshot");
        readonly ShortcutBox fullBox, regionBox;
        readonly ToolStripMenuItem fullMenu, regionMenu;
        GlobalShortcuts shortcuts;
        Preferences preferences;
        string folder, lastFile;
        bool regionMode, capturing, returnToTray, exiting, modal, activationPending;
        string HotkeyFile { get { return Path.Combine(settingsFolder, "hotkeys.txt"); } }

        public MainWindow()
        {
            Text = AppInfo.DisplayName;
            Font = new Font("Segoe UI", 10);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(920, 630);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Design.Surface;
            appIcon = Design.CreateIcon(); Icon = appIcon;
            preferences = Preferences.Read(HotkeyFile);
            folder = FolderSettings.Read(Path.Combine(settingsFolder, "folder.txt"), Path.Combine(settingsFolder, "Screenshots"));

            Controls.Add(Design.Text("▣", 28, 24, 28, 32, 17, Design.Accent, true));
            Controls.Add(Design.Text("FastScreen", 63, 21, 290, 26, 13, Design.Navy, true));
            Controls.Add(Design.Text("Версия " + AppInfo.Version, 63, 49, 290, 20, 9, Design.Muted, false));
            Button home = Design.Button("Создать скриншот", 396, 19, 188, false);
            Button keys = Design.Button("Горячие клавиши", 596, 19, 174, false);
            Button hide = Design.Button("В трей  ↓", 782, 19, 110, false);
            navHome = home; navKeys = keys;
            home.Click += delegate { ShowPage(workspace); };
            keys.Click += delegate { OpenSettings(); };
            hide.Click += delegate { HideToTray(); };
            Controls.AddRange(new Control[] { home, keys, hide });
            foreach (Panel page in new Panel[] { welcome, workspace, settings })
            { page.SetBounds(0, 82, 920, 548); Controls.Add(page); }

            welcome.SetBounds(20, 82, 880, 528);
            welcome.Controls.Add(Design.Text("Скриншоты всего\nэкрана или области\nв один клик.", 28, 61, 450, 169, 27, Design.Navy, true));
            welcome.Controls.Add(Design.Text("Скриншоты хранятся только\nна вашем компьютере.", 32, 251, 425, 65, 12, Design.Muted, false));
            Button start = Design.Button("Создать скриншот   →", 32, 332, 245, true);
            start.Click += delegate { ShowPage(workspace); };
            welcome.Controls.Add(start);
            HeroArt art = new HeroArt { Location = new Point(465, 80), Size = new Size(385, 320) };
            welcome.Controls.Add(art);
            AddWelcomeStep("01", "Выберите область или весь экран", 32);
            AddWelcomeStep("02", "Сделайте скриншот", 450);

            workspace.Controls.Add(Design.Text("Что сохраним?", 32, 20, 650, 50, 26, Design.Navy, true));
            workspace.Controls.Add(Design.Text("Выберите режим. Перед съёмкой окно автоматически скроется.", 35, 77, 850, 28, 11, Design.Muted, false));
            AddCaptureCard("Весь экран", "Вся область экрана", 34, false);
            AddCaptureCard("Выделенная область", "Выделите прямоугольник мышью", 469, true);
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
            openFolder.Click += delegate { try { using (LocalFolderGuard guard = LocalFolderGuard.Acquire(folder, true)) System.Diagnostics.Process.Start(SystemPrograms.ExplorerStartInfo(guard.Folder)); } catch (Exception ex) { ShowError(ex); } };
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
            trayMenu.Font = new Font("Segoe UI", 10);
            trayMenu.BackColor = Color.FromArgb(247, 250, 254);
            trayMenu.Renderer = new ToolStripProfessionalRenderer(new FluentMenuColors());
            trayMenu.ShowImageMargin = false;
            trayMenu.Padding = new Padding(5);
            fullMenu = new ToolStripMenuItem("Весь экран", null, delegate { BeginCapture(false); });
            regionMenu = new ToolStripMenuItem("Выделить область", null, delegate { BeginCapture(true); });
            trayMenu.Items.Add(fullMenu); trayMenu.Items.Add(regionMenu);
            trayMenu.Items.Add("Горячие клавиши", null, delegate { RestoreWindow(); OpenSettings(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Выйти", null, delegate { if (!capturing && !modal) { exiting = true; Close(); } });
            tray = new NotifyIcon { Icon = appIcon, Text = AppInfo.DisplayName, ContextMenuStrip = trayMenu, Visible = true };
            tray.DoubleClick += delegate { RestoreWindow(); };
            captureTimer.Interval = 300; captureTimer.Tick += CaptureNow;
            ShowPage(welcome);
            UpdateShortcutStatus();
        }

        void AddCaptureCard(string title, string caption, int x, bool region)
        {
            Panel card = new FluentPanel { Location = new Point(x, 125), Size = new Size(412, 190), BackColor = Color.FromArgb(252, 253, 255) };
            card.Controls.Add(Design.Text(title, 17, 18, 380, 35, 19, Design.Navy, true));
            card.Controls.Add(Design.Text(caption, 20, 60, 372, 26, 10, Design.Muted, false));
            Label shortcut = region ? regionShortcutHint : fullShortcutHint;
            shortcut.SetBounds(20, 95, 372, 26); shortcut.ForeColor = Design.Accent;
            card.Controls.Add(shortcut);
            Button button = Design.Button(region ? "Выделить область   →" : "Снять весь экран   →", 20, 134, 370, true);
            button.Height = 38; button.Click += delegate { BeginCapture(region); }; card.Controls.Add(button); workspace.Controls.Add(card);
        }

        void AddWelcomeStep(string number, string text, int x)
        {
            Panel card = new FluentPanel { Location = new Point(x, 426), Size = new Size(402, 64), BackColor = Color.FromArgb(252, 253, 255) };
            Label stepNumber = Design.Text(number, 0, 0, 32, card.Height, 11, Design.Accent, true);
            Label stepText = Design.Text(text, 0, 0, 340, card.Height, 10, Design.Navy, false);
            stepText.Width = TextRenderer.MeasureText(text, stepText.Font).Width + 4;
            int left = (card.Width - stepNumber.Width - 12 - stepText.Width) / 2;
            stepNumber.Left = left;
            stepText.Left = left + stepNumber.Width + 12;
            stepNumber.TextAlign = stepText.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.AddRange(new Control[] { stepNumber, stepText });
            welcome.Controls.Add(card);
        }
        void ShowPage(Panel page)
        {
            welcome.Visible = page == welcome; workspace.Visible = page == workspace; settings.Visible = page == settings;
            ((FluentButton)navHome).Selected = page == workspace; ((FluentButton)navKeys).Selected = page == settings;
            navHome.Invalidate(); navKeys.Invalidate(); page.BringToFront();
        }
        void OpenSettings() { fullBox.SetShortcut(preferences.Full); regionBox.SetShortcut(preferences.Region); UpdateShortcutStatus(); ShowPage(settings); }
        void UpdateShortcutStatus()
        {
            fullShortcutHint.Text = preferences.Full.ToString();
            regionShortcutHint.Text = preferences.Region.ToString();
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
            Design.WindowStyle(Handle);
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
                screenshotSound.Dispose();
                if (shortcuts != null) shortcuts.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                trayMenu.Dispose(); if (appIcon != null) appIcon.Dispose();
            }
            base.Dispose(disposing);
        }
        void HideToTray() { if (!capturing && !modal) { Hide(); ShowInTaskbar = false; } }
        void RestoreWindow() { if (capturing || modal) return; ShowInTaskbar = true; WindowState = FormWindowState.Normal; Show(); Activate(); }
        internal void RequestActivation()
        {
            if (capturing) { activationPending = true; return; }
            if (modal) { Activate(); return; }
            RestoreWindow();
        }

        internal static bool IsNetworkPath(string path)
        {
            if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).DriveType == DriveType.Network; } catch { return true; }
        }
        void EnsureLocalFolder() { using (LocalFolderGuard guard = LocalFolderGuard.Acquire(folder, false)) { } }
        void ChooseFolder(object sender, EventArgs e)
        {
            modal = true;
            try
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Папка для скриншотов на локальном диске", SelectedPath = folder })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (IsNetworkPath(dialog.SelectedPath)) throw new IOException("Выберите папку на локальном диске.");
                    folder = FolderSettings.Save(Path.Combine(settingsFolder, "folder.txt"), dialog.SelectedPath);
                    folderLabel.Text = folder;
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
            finally
            {
                capturing = false;
                if (activationPending) { returnToTray = false; activationPending = false; }
                if (!returnToTray) { ShowPage(workspace); RestoreWindow(); }
            }
        }
        void SaveShot(Bitmap bitmap)
        {
            EnsureLocalFolder(); lastFile = LocalScreenshot.Capture.Save(bitmap, folder); openLast.Enabled = true;
            screenshotSound.Play();
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
