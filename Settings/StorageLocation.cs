using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayniteCustomImporter.Settings
{
    /// <summary>
    /// A named destination folder that imported game folders can be moved into.
    /// </summary>
    public class StorageLocation : INotifyPropertyChanged
    {
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
            set { path = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
        }

        /// <summary>
        /// Friendly label used in the wizard's storage list.
        /// </summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name) ? Path : $"{Name}  ({Path})";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
