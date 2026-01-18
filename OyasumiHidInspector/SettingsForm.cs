using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace OyasumiHidInspector
{
    public partial class SettingsForm : Form
    {
        private Form1 mainForm;
        private bool _isRecording = false;
        private string _currentCombination = "";
        private Keys _lastKeyDown = Keys.None;

        public SettingsForm(Form1 form)
        {
            InitializeComponent();
            this.KeyPreview = true;
            this.KeyDown += SettingsForm_KeyDown;
            this.KeyUp += SettingsForm_KeyUp;
            mainForm = form;


            LoadSavedCombination();
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(Keys vKey);

        private void LoadSavedCombination()
        {
            try
            {
                if (!string.IsNullOrEmpty(Properties.Settings.Default.HotkeyCombination))
                {
                    txtForceBlock.Text = Properties.Settings.Default.HotkeyCombination;
                    if (mainForm != null)
                    {
                        mainForm.SavedHotkey = Properties.Settings.Default.HotkeyCombination;
                    }
                }
            }
            catch
            {
            }
        }

        private void SaveCombination(string combination)
        {
            try
            {
                Properties.Settings.Default.HotkeyCombination = combination;
                Properties.Settings.Default.Save();

                txtForceBlock.Text = combination;
                if (mainForm != null)
                {
                    mainForm.UpdateHotkeyCombination(combination);
                }

                MessageBox.Show($"Saved: {combination}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            if (e.KeyCode == Keys.Tab)
            {
                _isRecording = false;
                _currentCombination = "";
                _lastKeyDown = Keys.None;

                if (!string.IsNullOrEmpty(Properties.Settings.Default.HotkeyCombination))
                {
                    txtForceBlock.Text = Properties.Settings.Default.HotkeyCombination;
                }
                else
                {
                    txtForceBlock.Text = "(not set)";
                }

                button1.Text = "Record combination";
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                _isRecording = false;
                _currentCombination = "";
                _lastKeyDown = Keys.None;

                if (!string.IsNullOrEmpty(Properties.Settings.Default.HotkeyCombination))
                {
                    txtForceBlock.Text = Properties.Settings.Default.HotkeyCombination;
                }
                else
                {
                    txtForceBlock.Text = "(not set)";
                }

                button1.Text = "Record combination";
                e.Handled = true;
                return;
            }

            _lastKeyDown = e.KeyCode;
            UpdateDisplay();
        }

        private void SettingsForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_isRecording) return;

            if (_lastKeyDown != Keys.None &&
                _lastKeyDown != Keys.ControlKey && _lastKeyDown != Keys.LControlKey && _lastKeyDown != Keys.RControlKey &&
                _lastKeyDown != Keys.Menu && _lastKeyDown != Keys.LMenu && _lastKeyDown != Keys.RMenu &&
                _lastKeyDown != Keys.ShiftKey && _lastKeyDown != Keys.LShiftKey && _lastKeyDown != Keys.RShiftKey &&
                _lastKeyDown != Keys.LWin && _lastKeyDown != Keys.RWin)
            {
                _currentCombination = "";

                bool ctrlDown = (Control.ModifierKeys & Keys.Control) != 0;
                bool altDown = (Control.ModifierKeys & Keys.Alt) != 0 && (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;
                bool shiftDown = (Control.ModifierKeys & Keys.Shift) != 0;

                if (ctrlDown) _currentCombination += "Ctrl+";
                if (altDown) _currentCombination += "Alt+";
                if (shiftDown) _currentCombination += "Shift+";

                _currentCombination += _lastKeyDown.ToString();

                SaveCombination(_currentCombination);

                _isRecording = false;
                _currentCombination = "";
                _lastKeyDown = Keys.None;
                button1.Text = "Record combination";
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (!_isRecording) return;

            string display = "";

            bool ctrlDown = (Control.ModifierKeys & Keys.Control) != 0;
            bool altDown = (Control.ModifierKeys & Keys.Alt) != 0 && (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;
            bool shiftDown = (Control.ModifierKeys & Keys.Shift) != 0;

            if (ctrlDown) display += "Ctrl+";
            if (altDown) display += "Alt+";
            if (shiftDown) display += "Shift+";

            if (_lastKeyDown != Keys.None &&
                _lastKeyDown != Keys.ControlKey && _lastKeyDown != Keys.LControlKey && _lastKeyDown != Keys.RControlKey &&
                _lastKeyDown != Keys.Menu && _lastKeyDown != Keys.LMenu && _lastKeyDown != Keys.RMenu &&
                _lastKeyDown != Keys.ShiftKey && _lastKeyDown != Keys.LShiftKey && _lastKeyDown != Keys.RShiftKey &&
                _lastKeyDown != Keys.LWin && _lastKeyDown != Keys.RWin)
            {
                display += _lastKeyDown.ToString();
            }
            else if (!string.IsNullOrEmpty(display))
            {
                display = display.TrimEnd('+') + " + ...";
            }
            else
            {
                display = "Press keys... (Esc - cancel)";
            }

            txtForceBlock.Text = display;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!_isRecording)
            {
                txtForceBlock.Clear();
                txtForceBlock.Text = "Press keys... (Esc - cancel)";
                _isRecording = true;
                _currentCombination = "";
                _lastKeyDown = Keys.None;
                button1.Text = "Recording...";
            }
            else
            {
                _isRecording = false;
                _currentCombination = "";
                _lastKeyDown = Keys.None;

                if (!string.IsNullOrEmpty(Properties.Settings.Default.HotkeyCombination))
                {
                    txtForceBlock.Text = Properties.Settings.Default.HotkeyCombination;
                }
                else
                {
                    txtForceBlock.Text = "(not set)";
                }

                button1.Text = "Record combination";
            }
        }

        private void btnPenMode_Click(object sender, EventArgs e)
        {
            mainForm.isPen = !mainForm.isPen;

            if (mainForm.isPen)
            {
                lblStatus.Text = "Status: ON";
                lblStatus.ForeColor = Color.Green;
            }
            else
            {
                lblStatus.Text = "Status: OFF";
                lblStatus.ForeColor = Color.Red;
            }

            MessageBox.Show($"Pen mode changed to: {(mainForm.isPen ? "ON" : "OFF")}",
                           "Mode Changed",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Information);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_isRecording && keyData == Keys.Tab)
            {
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}