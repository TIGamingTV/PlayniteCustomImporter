using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.SDK;
using PlayniteCustomImporter.Settings;

namespace PlayniteCustomImporter.Import
{
    public enum WizardStep
    {
        SelectSource,
        ChooseStorage,
        PickExecutable
    }

    /// <summary>
    /// A discovered executable, exposing a friendly file name for display while keeping the full path.
    /// </summary>
    public class ExecutableItem
    {
        public string FullPath { get; }
        public string FileName => System.IO.Path.GetFileName(FullPath);

        public ExecutableItem(string fullPath)
        {
            FullPath = fullPath;
        }
    }

    /// <summary>
    /// A candidate game folder shown in step 1, exposing its folder name for display while keeping the
    /// full path.
    /// </summary>
    public class SourceFolderItem
    {
        public string FullPath { get; }
        public string Name => System.IO.Path.GetFileName(FullPath.TrimEnd(
            System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));

        public SourceFolderItem(string fullPath)
        {
            FullPath = fullPath;
        }
    }

    public class ImportWizardViewModel : INotifyPropertyChanged
    {
        private readonly IPlayniteAPI api;
        private readonly PlayniteCustomImporterSettings settings;
        private readonly GameImporter importer;

        private WizardStep currentStep = WizardStep.SelectSource;
        private string sourceRoot = string.Empty;
        private string selectedSourceFolder = string.Empty;
        private SourceFolderItem selectedSourceFolderItem;
        private StorageLocation selectedStorage;
        private string movedFolder = string.Empty;
        private ExecutableItem selectedExecutable;
        private string statusMessage = string.Empty;
        private string sourceStatusMessage = string.Empty;

        public ImportWizardViewModel(IPlayniteAPI api, PlayniteCustomImporterSettings settings)
        {
            this.api = api;
            this.settings = settings;
            importer = new GameImporter(api);

            StorageLocations = settings.StorageLocations;
            FoundExecutables = new ObservableCollection<ExecutableItem>();
            SourceFolders = new ObservableCollection<SourceFolderItem>();

            // Start from the configured source folder if one is set, otherwise the user's Downloads
            // folder. A manual selector lets the user point elsewhere.
            sourceRoot = ResolveInitialSourceRoot();

            NextFromSourceCommand = new RelayCommand<object>(_ => NextFromSource(), _ => CanNextFromSource());
            ChangeSourceRootCommand = new RelayCommand<object>(_ => ChangeSourceRoot());
            MoveAndNextCommand = new RelayCommand<object>(_ => MoveAndNext(), _ => CanMoveAndNext());
            BackCommand = new RelayCommand<object>(_ => CurrentStep = WizardStep.SelectSource, _ => CurrentStep == WizardStep.ChooseStorage);
            BrowseExeCommand = new RelayCommand<object>(_ => BrowseExe());
            AddGameCommand = new RelayCommand<object>(_ => AddGame(), _ => SelectedExecutable != null);
            CancelCommand = new RelayCommand<object>(_ => OnCloseRequested());

            ScanSourceFolders();
        }

        public ObservableCollection<StorageLocation> StorageLocations { get; }
        public ObservableCollection<ExecutableItem> FoundExecutables { get; }
        public ObservableCollection<SourceFolderItem> SourceFolders { get; }

        public RelayCommand<object> NextFromSourceCommand { get; }
        public RelayCommand<object> ChangeSourceRootCommand { get; }
        public RelayCommand<object> MoveAndNextCommand { get; }
        public RelayCommand<object> BackCommand { get; }
        public RelayCommand<object> BrowseExeCommand { get; }
        public RelayCommand<object> AddGameCommand { get; }
        public RelayCommand<object> CancelCommand { get; }

        public event EventHandler CloseRequested;

        public WizardStep CurrentStep
        {
            get => currentStep;
            set
            {
                currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectSourceStep));
                OnPropertyChanged(nameof(IsChooseStorageStep));
                OnPropertyChanged(nameof(IsPickExecutableStep));
            }
        }

        public bool IsSelectSourceStep => CurrentStep == WizardStep.SelectSource;
        public bool IsChooseStorageStep => CurrentStep == WizardStep.ChooseStorage;
        public bool IsPickExecutableStep => CurrentStep == WizardStep.PickExecutable;

        public string SourceRoot
        {
            get => sourceRoot;
            private set { sourceRoot = value; OnPropertyChanged(); }
        }

        public string SourceStatusMessage
        {
            get => sourceStatusMessage;
            set { sourceStatusMessage = value; OnPropertyChanged(); }
        }

        public SourceFolderItem SelectedSourceFolderItem
        {
            get => selectedSourceFolderItem;
            set
            {
                selectedSourceFolderItem = value;
                SelectedSourceFolder = value?.FullPath ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string SelectedSourceFolder
        {
            get => selectedSourceFolder;
            set { selectedSourceFolder = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public StorageLocation SelectedStorage
        {
            get => selectedStorage;
            set { selectedStorage = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public ExecutableItem SelectedExecutable
        {
            get => selectedExecutable;
            set { selectedExecutable = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string StatusMessage
        {
            get => statusMessage;
            set { statusMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Determines where the folder list starts: the configured source folder when set and valid,
        /// otherwise the current user's Downloads folder.
        /// </summary>
        private string ResolveInitialSourceRoot()
        {
            if (!string.IsNullOrWhiteSpace(settings.SourceFolder) && Directory.Exists(settings.SourceFolder))
            {
                return settings.SourceFolder;
            }

            return GetDownloadsFolder();
        }

        /// <summary>
        /// The current user's Downloads folder. .NET Framework has no special-folder entry for it, so
        /// it is derived from the user profile (the usual location).
        /// </summary>
        private static string GetDownloadsFolder()
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(profile) ? string.Empty : Path.Combine(profile, "Downloads");
        }

        /// <summary>
        /// (Re)populates the list of candidate game folders under <see cref="SourceRoot"/>.
        /// </summary>
        private void ScanSourceFolders()
        {
            SourceFolders.Clear();
            SelectedSourceFolderItem = null;
            OnPropertyChanged(nameof(SourceRoot));

            if (!Directory.Exists(sourceRoot))
            {
                SourceStatusMessage = string.IsNullOrWhiteSpace(sourceRoot)
                    ? "No source folder is available. Use \"Change folder...\" to pick one."
                    : $"The folder \"{sourceRoot}\" does not exist. Use \"Change folder...\" to pick another.";
                return;
            }

            foreach (var folder in GameImporter.FindGameFolders(sourceRoot))
            {
                SourceFolders.Add(new SourceFolderItem(folder));
            }

            SelectedSourceFolderItem = SourceFolders.FirstOrDefault();
            SourceStatusMessage = SourceFolders.Count == 0
                ? "No subfolders containing an .exe were found here. Use \"Change folder...\" to look elsewhere."
                : $"Found {SourceFolders.Count} folder(s) with an executable. Select the game to import.";
        }

        /// <summary>
        /// Lets the user point the scan at a different folder (the manual selector).
        /// </summary>
        private void ChangeSourceRoot()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the folder to look for games in";
                if (Directory.Exists(sourceRoot))
                {
                    dialog.SelectedPath = sourceRoot;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    SourceRoot = dialog.SelectedPath;
                    ScanSourceFolders();
                }
            }
        }

        private bool CanNextFromSource()
        {
            return !string.IsNullOrWhiteSpace(SelectedSourceFolder) && Directory.Exists(SelectedSourceFolder);
        }

        private void NextFromSource()
        {
            StatusMessage = string.Empty;
            RefreshStorageFreeSpace();
            CurrentStep = WizardStep.ChooseStorage;
        }

        private void RefreshStorageFreeSpace()
        {
            foreach (var location in StorageLocations)
            {
                location.RefreshFreeSpace();
            }
        }

        private bool CanMoveAndNext()
        {
            return !string.IsNullOrWhiteSpace(SelectedSourceFolder)
                && Directory.Exists(SelectedSourceFolder)
                && SelectedStorage != null
                && !string.IsNullOrWhiteSpace(SelectedStorage.Path);
        }

        private void MoveAndNext()
        {
            try
            {
                movedFolder = importer.MoveFolder(SelectedSourceFolder, SelectedStorage.Path);

                FoundExecutables.Clear();
                foreach (var exe in GameImporter.FindExecutables(movedFolder))
                {
                    FoundExecutables.Add(new ExecutableItem(exe));
                }

                SelectedExecutable = FoundExecutables.FirstOrDefault();
                StatusMessage = FoundExecutables.Count == 0
                    ? "No .exe files found in the folder. Use \"Browse for another .exe...\" to pick one manually."
                    : $"Found {FoundExecutables.Count} executable(s). Select the one to launch the game.";

                CurrentStep = WizardStep.PickExecutable;
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage(ex.Message, "Custom Importer");
            }
        }

        private void BrowseExe()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Select the game executable";
                dialog.Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*";
                var initialDir = Directory.Exists(movedFolder) ? movedFolder : SelectedSourceFolder;
                if (Directory.Exists(initialDir))
                {
                    dialog.InitialDirectory = initialDir;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrEmpty(dialog.FileName))
                {
                    var existing = FoundExecutables.FirstOrDefault(
                        e => string.Equals(e.FullPath, dialog.FileName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        existing = new ExecutableItem(dialog.FileName);
                        FoundExecutables.Add(existing);
                    }

                    SelectedExecutable = existing;
                }
            }
        }

        private void AddGame()
        {
            try
            {
                var game = importer.AddGame(SelectedExecutable.FullPath);
                api.Dialogs.ShowMessage(
                    $"Added \"{game.Name}\" to your library.\n\n" +
                    "To fetch cover art and details, right-click the game and choose " +
                    "\"Download Metadata\" (the name has been cleaned up so the search matches better).",
                    "Custom Importer");
                OnCloseRequested();
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage(ex.Message, "Custom Importer");
            }
        }

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
