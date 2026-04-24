using System;
using System.Windows.Forms;

namespace CoreLayer
{
    public partial class FindByGuidWindow : Form
    {
        readonly Action<string> _requestGuidSearch;
        string _userInput = string.Empty;

        public FindByGuidWindow(Action<string> requestGuidSearch)
        {
            if (requestGuidSearch == null)
                throw new ArgumentNullException(nameof(requestGuidSearch));

            _requestGuidSearch = requestGuidSearch;
            InitializeComponent();
        }

        public void ShowOwnedWindow(IntPtr ownerHandle)
        {
            if (!Visible)
            {
                if (ownerHandle != IntPtr.Zero)
                {
                    try
                    {
                        Show(new WindowHandleWrapper(ownerHandle));
                    }
                    catch (ArgumentException)
                    {
                        Show();
                    }
                }
                else
                {
                    Show();
                }
            }

            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            Activate();
            BringToFront();
            _guidTextBox.Focus();
            _guidTextBox.SelectAll();
        }

        public void HandleSearchCompleted(int matchesCount)
        {
            if (matchesCount <= 0)
                return;

            if (matchesCount > 1)
            {
                MessageBox.Show(
                    this,
                    "There is more than object with the same Guid.",
                    "Alert",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            Hide();
        }

        void FindGuid_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_userInput))
                return;

            _requestGuidSearch(_userInput.Trim());
        }

        void FindGuidWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        void GuidTextBox_TextChanged(object sender, EventArgs e)
        {
            _userInput = _guidTextBox.Text;
        }

        sealed class WindowHandleWrapper : IWin32Window
        {
            public WindowHandleWrapper(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
