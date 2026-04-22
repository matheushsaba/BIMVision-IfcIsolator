using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ApiLayer
{
    internal sealed class CopyableErrorDialog : Form
    {
        readonly TextBox _messageTextBox;

        CopyableErrorDialog(string title, string text)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Error" : title;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowIcon = true;
            ShowInTaskbar = false;
            Size = new Size(720, 420);
            MinimumSize = new Size(520, 280);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var icon = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Dock = DockStyle.Top,
                Width = 32,
                Height = 32,
                Margin = new Padding(0, 4, 12, 0),
            };

            _messageTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Text = text ?? string.Empty,
                Font = new Font(FontFamily.GenericMonospace, 9F),
                HideSelection = false,
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
            };

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                MinimumSize = new Size(84, 28),
                Margin = new Padding(8, 6, 0, 0),
            };

            var copyButton = new Button
            {
                Text = "Copy",
                AutoSize = true,
                MinimumSize = new Size(84, 28),
                Margin = new Padding(8, 6, 0, 0),
            };
            copyButton.Click += (sender, args) => Clipboard.SetText(_messageTextBox.Text);

            buttons.Controls.Add(closeButton);
            buttons.Controls.Add(copyButton);

            layout.Controls.Add(icon, 0, 0);
            layout.Controls.Add(_messageTextBox, 1, 0);
            layout.Controls.Add(buttons, 1, 1);

            Controls.Add(layout);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }

        public static void Show(string title, string message, string details, IntPtr ownerHandle)
        {
            var text = BuildText(message, details);

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                using (var dialog = new CopyableErrorDialog(title, text))
                {
                    ShowDialog(dialog, ownerHandle);
                }

                return;
            }

            Exception dialogException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var dialog = new CopyableErrorDialog(title, text))
                    {
                        ShowDialog(dialog, ownerHandle);
                    }
                }
                catch (Exception ex)
                {
                    dialogException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (dialogException != null)
                throw dialogException;
        }

        static string BuildText(string message, string details)
        {
            if (string.IsNullOrWhiteSpace(details))
                return message ?? string.Empty;

            if (string.IsNullOrWhiteSpace(message))
                return details;

            return message + Environment.NewLine + Environment.NewLine + "Details:" + Environment.NewLine + details;
        }

        static void ShowDialog(CopyableErrorDialog dialog, IntPtr ownerHandle)
        {
            if (ownerHandle == IntPtr.Zero)
            {
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.ShowDialog();
                return;
            }

            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(new DialogOwner(ownerHandle));
        }

        sealed class DialogOwner : IWin32Window
        {
            readonly IntPtr _handle;

            public DialogOwner(IntPtr handle)
            {
                _handle = handle;
            }

            public IntPtr Handle
            {
                get { return _handle; }
            }
        }
    }
}
