using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.WindowsAPICodePack.Internal;

namespace Microsoft.WindowsAPICodePack.Taskbar
{
    public class ThumbnailToolbarButton : IDisposable
    {
        private static uint _nextId = 101;
        public readonly uint Id;
        private THBFLAGS _flags;
        private THUMBBUTTON _win32ThumbButton;
        internal bool AddedToTaskbar;
        internal IntPtr WindowHandle;

        public ThumbnailToolbarButton(Icon icon, string tooltip)
        {
            // Set our id
            Id = _nextId;

            // increment the ID
            if (_nextId == int.MaxValue)
                _nextId = 101; // our starting point
            else
                _nextId++;

            // Set user settings
            Icon = icon;
            Tooltip = tooltip;

            // Create a native 
            _win32ThumbButton = new THUMBBUTTON();
        }

        /// <summary>
        ///     The window manager should call this method to raise the public click event to all
        ///     the subscribers.
        /// </summary>
        internal void FireClick(TaskbarWindow window)
        {
            var e = Click;
            if (e != null)
                e(this, EventArgs.Empty);
        }

        public event EventHandler Click;

        #region Enabled Property

        public bool Enabled
        {
            get { return (_flags & THBFLAGS.THBF_DISABLED) == 0; }
            set
            {
                if (Enabled == value) return;

                if (value)
                    _flags &= ~(THBFLAGS.THBF_DISABLED);
                else
                    _flags |= THBFLAGS.THBF_DISABLED;

                UpdateThumbnailButton();
            }
        }

        #endregion

        #region Visible Property

        public bool Visible
        {
            get { return (_flags & THBFLAGS.THBF_HIDDEN) == 0; }
            set
            {
                if (Visible == value) return;

                if (value)
                    _flags &= ~(THBFLAGS.THBF_HIDDEN);
                else
                    _flags |= THBFLAGS.THBF_HIDDEN;

                UpdateThumbnailButton();
            }
        }

        #endregion

        internal void UpdateThumbnailButton()
        {
            if (!AddedToTaskbar) return;

            // Get the array of thumbnail buttons in native format
            THUMBBUTTON[] nativeButtons = {Win32ThumbButton};

            var hr = TaskbarManager.Instance.TaskbarList.ThumbBarUpdateButtons(WindowHandle, 1, nativeButtons);

            if (!CoreErrorHelper.Succeeded((int) hr))
                Marshal.ThrowExceptionForHR((int) hr);
        }

        /// <summary>
        ///     Native representation of the thumbnail button
        /// </summary>
        internal THUMBBUTTON Win32ThumbButton
        {
            get
            {
                _win32ThumbButton.iId = Id;
                _win32ThumbButton.szTip = _tooltip;
                _win32ThumbButton.dwFlags = _flags;
                _win32ThumbButton.dwMask = THBMASK.THB_FLAGS;

                if (_tooltip != null)
                    _win32ThumbButton.dwMask |= THBMASK.THB_TOOLTIP;

                if (_icon != null)
                {
                    _win32ThumbButton.dwMask |= THBMASK.THB_ICON;
                    _win32ThumbButton.hIcon = _icon.Handle;
                }

                return _win32ThumbButton;
            }
        }

        #region Icon Property

        private Icon _icon;

        public Icon Icon
        {
            get { return _icon; }
            set
            {
                if (_icon == value) return;
                _icon = value;
                UpdateThumbnailButton();
            }
        }

        #endregion

        #region Tooltip Property

        private string _tooltip;

        public string Tooltip
        {
            get { return _tooltip; }
            set
            {
                if (_tooltip == value) return;
                _tooltip = value;
                UpdateThumbnailButton();
            }
        }

        #endregion

        #region IDisposable logic

        public void Dispose()
        {
            Dispose(true);
        }

        ~ThumbnailToolbarButton()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;

            if (_icon != null)
                _icon.Dispose();
            _tooltip = null;
        }

        #endregion
    }
}