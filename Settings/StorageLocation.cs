using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PlayniteCustomImporter.Settings
{
    /// <summary>
    /// A named destination folder that imported game folders can be moved into.
    /// </summary>
    public class StorageLocation : INotifyPropertyChanged
    {
        private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        private string name = string.Empty;
        private string path = string.Empty;

        public string Name
        {
            get => name;
            set { name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string Path
        {
            get => path;
            set
            {
                path = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(FreeSpaceDisplay));
            }
        }

        /// <summary>
        /// Friendly label used in the wizard's storage list.
        /// </summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name) ? Path : $"{Name}  ({Path})";

        /// <summary>
        /// Human-readable amount of free space available on the drive that hosts <see cref="Path"/>,
        /// e.g. "123.4 GB free". Returns a short explanation when the drive cannot be queried.
        /// </summary>
        public string FreeSpaceDisplay
        {
            get
            {
                var free = GetFreeSpaceBytes();
                return free.HasValue ? $"{FormatBytes(free.Value)} free" : "Free space unavailable";
            }
        }

        /// <summary>
        /// Re-reads the free space for this location. Call before showing the list so the value
        /// reflects the disk's current state rather than whatever it was when the location was added.
        /// </summary>
        public void RefreshFreeSpace()
        {
            OnPropertyChanged(nameof(FreeSpaceDisplay));
        }

        /// <summary>
        /// Free bytes available to the current user on the volume that hosts <see cref="Path"/>,
        /// or null when the path is empty, malformed or the drive is not ready.
        /// </summary>
        private long? GetFreeSpaceBytes()
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return null;
            }

            try
            {
                var root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(Path));
                if (string.IsNullOrEmpty(root))
                {
                    return null;
                }

                var drive = new DriveInfo(root);
                return drive.IsReady ? drive.AvailableFreeSpace : (long?)null;
            }
            catch (Exception)
            {
                // Bad path, unmapped drive, permission issues, etc. Treat as "unknown".
                return null;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {
                return "0 B";
            }

            double size = bytes;
            var unit = 0;
            while (size >= 1024 && unit < SizeUnits.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return unit == 0 ? $"{size:0} {SizeUnits[unit]}" : $"{size:0.#} {SizeUnits[unit]}";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
