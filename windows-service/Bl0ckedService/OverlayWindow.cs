using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Bl0ckedService
{
    /// <summary>
    /// Full-screen overlay window that displays during lockdown.
    /// Shows chore list, timer, and provides emergency unlock option.
    /// </summary>
    public class OverlayWindow : Form
    {
        private readonly StateManager _stateManager;
        private readonly Screen _targetScreen;
        private System.Windows.Forms.Timer? _timerUpdate;
        private Label? _timerLabel;
        private Panel? _choreListPanel;
        private Panel? _infoModal;
        private List<Chore> _chores = new List<Chore>();
        private readonly object _choreUpdateLock = new object();

        // Colors matching planning.md dark theme
        private readonly Color BackgroundColor = ColorTranslator.FromHtml("#1a1a1a");
        private readonly Color ContainerColor = ColorTranslator.FromHtml("#2a2a2a");
        private readonly Color BorderColor = ColorTranslator.FromHtml("#444");
        private readonly Color TextColor = ColorTranslator.FromHtml("#e0e0e0");
        private readonly Color SubtleTextColor = ColorTranslator.FromHtml("#888");
        private readonly Color TimerColor = ColorTranslator.FromHtml("#ff9800");

        public OverlayWindow(StateManager stateManager, Screen screen)
        {
            _stateManager = stateManager;
            _targetScreen = screen;
            InitializeOverlayWindow();
            LoadChores();
            StartTimer();
        }

        /// <summary>
        /// Initializes the overlay window with all UI components
        /// </summary>
        private void InitializeOverlayWindow()
        {
            // Window settings for full-screen overlay
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;

            // Position on specific screen
            Bounds = _targetScreen.Bounds;

            // Dark background
            BackColor = BackgroundColor;

            // Prevent closing
            ControlBox = false;

            // Create UI
            CreateUI();
        }

        /// <summary>
        /// Creates all UI elements
        /// </summary>
        private void CreateUI()
        {
            // Main container panel (centered)
            Panel mainContainer = new Panel
            {
                Width = 600,
                Height = 500,
                BackColor = ContainerColor,
                Location = new Point((Width - 600) / 2, (Height - 500) / 2)
            };
            Controls.Add(mainContainer);

            // Info icon (top-right)
            Button infoButton = new Button
            {
                Text = "i",
                Width = 30,
                Height = 30,
                Location = new Point(Width - 50, 20),
                BackColor = ColorTranslator.FromHtml("#333"),
                ForeColor = ColorTranslator.FromHtml("#666"),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            infoButton.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#666");
            infoButton.Click += InfoButton_Click;
            Controls.Add(infoButton);

            // Header section
            Label header = new Label
            {
                Text = "Computer Locked - Complete Chores",
                Font = new Font("Arial", 18, FontStyle.Bold),
                ForeColor = TextColor,
                BackColor = ColorTranslator.FromHtml("#333"),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(10)
            };
            mainContainer.Controls.Add(header);

            // Timer display
            _timerLabel = new Label
            {
                Text = "Locked for: 00:00:00",
                Font = new Font("Arial", 16, FontStyle.Regular),
                ForeColor = TimerColor,
                BackColor = ContainerColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10)
            };
            mainContainer.Controls.Add(_timerLabel);

            // Chore list section
            Label choreHeader = new Label
            {
                Text = "CHORE LIST",
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = SubtleTextColor,
                BackColor = ContainerColor,
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(20, 5, 5, 5)
            };
            mainContainer.Controls.Add(choreHeader);

            // Scrollable chore list panel
            _choreListPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ContainerColor,
                AutoScroll = true,
                Padding = new Padding(20)
            };
            mainContainer.Controls.Add(_choreListPanel);

            // Footer message
            Label footer = new Label
            {
                Text = "Contact admin to unlock",
                Font = new Font("Arial", 10, FontStyle.Regular),
                ForeColor = SubtleTextColor,
                BackColor = ColorTranslator.FromHtml("#222"),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 40
            };
            mainContainer.Controls.Add(footer);

            // Emergency unlock button (bottom-left)
            Button emergencyButton = new Button
            {
                Text = "Emergency Unlock",
                Width = 150,
                Height = 30,
                Location = new Point(20, Height - 50),
                BackColor = ColorTranslator.FromHtml("#444"),
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            emergencyButton.FlatAppearance.BorderColor = BorderColor;
            emergencyButton.Click += EmergencyButton_Click;
            Controls.Add(emergencyButton);
        }

        /// <summary>
        /// Loads chores from the chores.json file
        /// </summary>
        private void LoadChores()
        {
            lock (_choreUpdateLock)
            {
                string choresPath = _stateManager.GetChoresFilePath();

                if (File.Exists(choresPath))
                {
                    try
                    {
                        string json = File.ReadAllText(choresPath);
                        var choreData = JsonConvert.DeserializeObject<ChoreData>(json);
                        _chores = choreData?.Chores ?? new List<Chore>();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading chores: {ex.Message}");
                        _chores = new List<Chore>();
                    }
                }
                else
                {
                    _chores = new List<Chore>();
                }

                RefreshChoreList();
            }
        }

        /// <summary>
        /// Refreshes the chore list UI
        /// </summary>
        private void RefreshChoreList()
        {
            if (_choreListPanel == null)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(RefreshChoreList));
                return;
            }

            _choreListPanel.Controls.Clear();

            if (_chores.Count == 0)
            {
                Label emptyLabel = new Label
                {
                    Text = "No chores assigned. Contact admin.",
                    Font = new Font("Arial", 12, FontStyle.Italic),
                    ForeColor = SubtleTextColor,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                _choreListPanel.Controls.Add(emptyLabel);
                return;
            }

            int yPos = 0;
            foreach (var chore in _chores)
            {
                Panel choreItem = new Panel
                {
                    Width = _choreListPanel.Width - 40,
                    Height = 50,
                    Location = new Point(0, yPos),
                    BackColor = ContainerColor
                };

                CheckBox checkbox = new CheckBox
                {
                    Checked = chore.Checked,
                    Location = new Point(10, 15),
                    Width = 20,
                    Height = 20,
                    BackColor = ContainerColor,
                    ForeColor = TextColor
                };
                checkbox.Tag = chore.Id;
                checkbox.CheckedChanged += Checkbox_CheckedChanged;
                choreItem.Controls.Add(checkbox);

                Label choreText = new Label
                {
                    Text = chore.Text,
                    Font = new Font("Arial", 11, FontStyle.Regular),
                    ForeColor = TextColor,
                    Location = new Point(40, 15),
                    Width = choreItem.Width - 50,
                    Height = 20,
                    BackColor = ContainerColor
                };
                choreItem.Controls.Add(choreText);

                _choreListPanel.Controls.Add(choreItem);
                yPos += 55;
            }
        }

        /// <summary>
        /// Handles checkbox state changes
        /// </summary>
        private void Checkbox_CheckedChanged(object? sender, EventArgs e)
        {
            if (sender is CheckBox checkbox && checkbox.Tag is int choreId)
            {
                lock (_choreUpdateLock)
                {
                    var chore = _chores.Find(c => c.Id == choreId);
                    if (chore != null)
                    {
                        chore.Checked = checkbox.Checked;
                        SaveChores();
                        // Note: In full implementation, this would also notify the Android app
                    }
                }
            }
        }

        /// <summary>
        /// Saves chores back to the chores.json file
        /// </summary>
        private void SaveChores()
        {
            try
            {
                string choresPath = _stateManager.GetChoresFilePath();
                var choreData = new ChoreData { Chores = _chores };
                string json = JsonConvert.SerializeObject(choreData, Formatting.Indented);
                File.WriteAllText(choresPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving chores: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the timer that updates the lock duration
        /// </summary>
        private void StartTimer()
        {
            _timerUpdate = new System.Windows.Forms.Timer
            {
                Interval = 1000 // Update every second
            };
            _timerUpdate.Tick += TimerUpdate_Tick;
            _timerUpdate.Start();
        }

        /// <summary>
        /// Updates the timer display
        /// </summary>
        private void TimerUpdate_Tick(object? sender, EventArgs e)
        {
            if (_timerLabel != null)
            {
                long duration = _stateManager.LockDuration;
                TimeSpan timeSpan = TimeSpan.FromSeconds(duration);
                _timerLabel.Text = $"Locked for: {timeSpan:hh\\:mm\\:ss}";
            }
        }

        /// <summary>
        /// Shows the info modal
        /// </summary>
        private void InfoButton_Click(object? sender, EventArgs e)
        {
            ShowInfoModal();
        }

        /// <summary>
        /// Creates and shows the info modal overlay
        /// </summary>
        private void ShowInfoModal()
        {
            _infoModal = new Panel
            {
                Width = Width,
                Height = Height,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(200, 0, 0, 0) // Semi-transparent black
            };

            Panel modalBox = new Panel
            {
                Width = 500,
                Height = 300,
                BackColor = ColorTranslator.FromHtml("#2a2a2a"),
                Location = new Point((Width - 500) / 2, (Height - 300) / 2)
            };
            modalBox.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(BorderColor, 2))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, modalBox.Width - 1, modalBox.Height - 1);
                }
            };

            Label modalHeader = new Label
            {
                Text = "Why is my computer locked?",
                Font = new Font("Arial", 14, FontStyle.Bold),
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(10)
            };
            modalBox.Controls.Add(modalHeader);

            Label modalContent = new Label
            {
                Text = "Your computer has been locked because you failed to complete your assigned chores when asked the first time.\n\n" +
                       "You will not be able to access your computer until all chores have been completed and verified.\n\n" +
                       "Please complete your chores and contact the administrator when finished.",
                Font = new Font("Arial", 11, FontStyle.Regular),
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };
            modalBox.Controls.Add(modalContent);

            Button closeButton = new Button
            {
                Text = "Close",
                Width = 100,
                Height = 35,
                BackColor = ColorTranslator.FromHtml("#444"),
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 10, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Dock = DockStyle.Bottom
            };
            closeButton.FlatAppearance.BorderColor = BorderColor;
            closeButton.Click += (s, e) => CloseInfoModal();
            modalBox.Controls.Add(closeButton);

            _infoModal.Controls.Add(modalBox);
            _infoModal.Click += (s, e) => CloseInfoModal();
            Controls.Add(_infoModal);
            _infoModal.BringToFront();
        }

        /// <summary>
        /// Closes the info modal
        /// </summary>
        private void CloseInfoModal()
        {
            if (_infoModal != null)
            {
                Controls.Remove(_infoModal);
                _infoModal.Dispose();
                _infoModal = null;
            }
        }

        /// <summary>
        /// Handles emergency unlock button click
        /// </summary>
        private void EmergencyButton_Click(object? sender, EventArgs e)
        {
            ShowEmergencyUnlockDialog();
        }

        /// <summary>
        /// Shows dialog to enter emergency unlock code
        /// </summary>
        private void ShowEmergencyUnlockDialog()
        {
            Form dialog = new Form
            {
                Text = "Emergency Unlock",
                Width = 350,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = ContainerColor,
                ForeColor = TextColor,
                TopMost = true
            };

            Label label = new Label
            {
                Text = "Enter 8-digit emergency unlock code:",
                Location = new Point(20, 20),
                Width = 300,
                ForeColor = TextColor
            };
            dialog.Controls.Add(label);

            TextBox codeInput = new TextBox
            {
                Location = new Point(20, 50),
                Width = 300,
                Font = new Font("Arial", 14, FontStyle.Regular),
                BackColor = ColorTranslator.FromHtml("#333"),
                ForeColor = TextColor,
                MaxLength = 8
            };
            dialog.Controls.Add(codeInput);

            Button unlockButton = new Button
            {
                Text = "Unlock",
                Location = new Point(20, 100),
                Width = 140,
                Height = 35,
                BackColor = ColorTranslator.FromHtml("#444"),
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            unlockButton.Click += (s, e) =>
            {
                if (_stateManager.ValidateEmergencyUnlockCode(codeInput.Text))
                {
                    _stateManager.Unlock();
                    MessageBox.Show("Computer unlocked successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    dialog.Close();
                    Close();
                }
                else
                {
                    MessageBox.Show("Invalid emergency unlock code.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            dialog.Controls.Add(unlockButton);

            Button cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(170, 100),
                Width = 140,
                Height = 35,
                BackColor = ColorTranslator.FromHtml("#444"),
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            cancelButton.Click += (s, e) => dialog.Close();
            dialog.Controls.Add(cancelButton);

            dialog.ShowDialog();
        }

        /// <summary>
        /// Public method to reload chores from file (called when chores are updated externally)
        /// </summary>
        public void ReloadChores()
        {
            LoadChores();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timerUpdate?.Stop();
                _timerUpdate?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Represents a single chore item
    /// </summary>
    public class Chore
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool Checked { get; set; }
    }

    /// <summary>
    /// Container for the chore list
    /// </summary>
    public class ChoreData
    {
        public List<Chore> Chores { get; set; } = new List<Chore>();
    }
}
