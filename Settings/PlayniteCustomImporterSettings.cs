using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Playnite.SDK;

namespace PlayniteCustomImporter.Settings
{
    /// <summary>
    /// Persisted plugin settings: the folder new games are imported from and the list of
    /// storage locations they can be moved into.
    /// </summary>
    public class PlayniteCustomImporterSettings : INotifyPropertyChanged
    {
        private string sourceFolder = string.Empty;
        private bool openMetadataAfterImport = true;
        private ObservableCollection<StorageLocation> storageLocations = new ObservableCollection<StorageLocation>();

        public string SourceFolder
        {
            get => sourceFolder;
            set { sourceFolder = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// When true, the game's edit dialog is opened after import so metadata can be downloaded.
        /// (The Playnite SDK has no fully-silent metadata download for plugins.)
        /// </summary>
        public bool OpenMetadataAfterImport
        {
            get => openMetadataAfterImport;
            set { openMetadataAfterImport = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StorageLocation> StorageLocations
        {
            get => storageLocations;
            set { storageLocations = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel bridging the persisted settings to the settings view. Implements the Playnite
    /// <see cref="ISettings"/> edit lifecycle so Save/Cancel behave correctly.
    /// </summary>
    public class PlayniteCustomImporterSettingsViewModel : ISettings, INotifyPropertyChanged
    {
        private readonly PlayniteCustomImporterPlugin plugin;
        private readonly IPlayniteAPI api;
        private PlayniteCustomImporterSettings editingClone;
        private PlayniteCustomImporterSettings settings;

        public PlayniteCustomImporterSettings Settings
        {
            get => settings;
            set { settings = value; OnPropertyChanged(); }
        }

        public RelayCommand<object> AddStorageLocationCommand => new RelayCommand<object>(_ =>
        {
            var newLocation = new StorageLocation { Name = "New location", Path = string.Empty };
            var selected = api.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(selected))
            {
                newLocation.Path = selected;
            }

            Settings.StorageLocations.Add(newLocation);
        });

        public RelayCommand<StorageLocation> RemoveStorageLocationCommand => new RelayCommand<StorageLocation>(location =>
        {
            if (location != null)
            {
                Settings.StorageLocations.Remove(location);
            }
        }, location => location != null);

        public RelayCommand<StorageLocation> BrowseStorageLocationCommand => new RelayCommand<StorageLocation>(location =>
        {
            if (location == null)
            {
                return;
            }

            var selected = api.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(selected))
            {
                location.Path = selected;
            }
        }, location => location != null);

        public RelayCommand<object> BrowseSourceFolderCommand => new RelayCommand<object>(_ =>
        {
            var selected = api.Dialogs.SelectFolder();
            if (!string.IsNullOrEmpty(selected))
            {
                Settings.SourceFolder = selected;
            }
        });

        public PlayniteCustomImporterSettingsViewModel(PlayniteCustomImporterPlugin plugin, IPlayniteAPI api)
        {
            this.plugin = plugin;
            this.api = api;

            var saved = plugin.LoadPluginSettings<PlayniteCustomImporterSettings>();
            Settings = saved ?? new PlayniteCustomImporterSettings();
        }

        public void BeginEdit()
        {
            // Snapshot the current state so CancelEdit can restore it.
            editingClone = new PlayniteCustomImporterSettings
            {
                SourceFolder = Settings.SourceFolder,
                OpenMetadataAfterImport = Settings.OpenMetadataAfterImport,
                StorageLocations = new ObservableCollection<StorageLocation>(
                    Settings.StorageLocations.Select(l => new StorageLocation { Name = l.Name, Path = l.Path }))
            };
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            foreach (var location in Settings.StorageLocations)
            {
                if (string.IsNullOrWhiteSpace(location.Path))
                {
                    errors.Add($"Storage location \"{location.Name}\" has no path set.");
                }
            }

            return errors.Count == 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
