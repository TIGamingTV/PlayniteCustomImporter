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

    public class ImportWizardViewModel : INotifyPropertyChanged
    {
        private readonly IPlayniteAPI api;
        private readonly PlayniteCustomImporterSettings settings;
        private readonly GameImporter importer;

        private WizardStep currentStep = WizardStep.SelectSource;
        private string selectedSourceFolder = string.Empty;
        private StorageLocation selectedStorage;
        private string movedFolder = string.Empty;
        private ExecutableItem selectedExecutable;
        private string statusMessage = string.Empty;

        public ImportWizardViewModel(IPlayniteAPI api, PlayniteCustomImporterSettings settings)
        {
            this.api = api;
            this.settings = settings;
            importer = new GameImporter(api);

            StorageLocations = settings.StorageLocations;
            FoundExecutables = new ObservableCollection<ExecutableItem>();
            selectedSourceFolder = settings.SourceFolder ?? string.Empty;

            ImportCommand = new RelayCommand<object>(_ => Import());
            MoveAndNextCommand = new RelayCommand<object>(_ => MoveAndNext(), _ => CanMoveAndNext());
            BackCommand = new RelayCommand<object>(_ => CurrentStep = WizardStep.SelectSource, _ => CurrentStep == WizardStep.ChooseStorage);
            BrowseExeCommand = new RelayCommand<object>(_ => BrowseExe());
            AddGameCommand = new RelayCommand<object>(_ => AddGame(), _ => SelectedExecutable != null);
            CancelCommand = new RelayCommand<object>(_ => OnCloseRequested());
        }

        public ObservableCollection<StorageLocation> StorageLocations { get; }
        public ObservableCollection<ExecutableItem> FoundExecutables { get; }

        public RelayCommand<object> ImportCommand { get; }
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

        private void Import()
        {
            // Open a native folder browser starting at the configured source folder.
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select the game folder to import";
                if (Directory.Exists(settings.SourceFolder))
                {
                    dialog.SelectedPath = settings.SourceFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    SelectedSourceFolder = dialog.SelectedPath;
                    StatusMessage = string.Empty;
                    RefreshStorageFreeSpace();
                    CurrentStep = WizardStep.ChooseStorage;
                }
            }
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
                foreach (var exe in GameImporter.FindTopLevelExes(movedFolder))
                {
                    FoundExecutables.Add(new ExecutableItem(exe));
                }

                SelectedExecutable = FoundExecutables.FirstOrDefault();
                StatusMessage = FoundExecutables.Count == 0
                    ? "No .exe files found in the top level of the folder. Use \"Browse...\" to pick one manually."
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
                api.Dialogs.ShowMessage($"Added \"{game.Name}\" to your library.", "Custom Importer");
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
