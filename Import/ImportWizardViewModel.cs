using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteCustomImporter.Settings;

namespace PlayniteCustomImporter.Import
{
    public enum WizardStep
    {
        SelectSource,
        PickExecutable,
        ChooseStorage
    }

    /// <summary>
    /// A discovered executable, exposing a friendly display for the list while keeping the full path.
    /// When a base folder is supplied the display is the path relative to it, so nested launchers are
    /// distinguishable (e.g. "Some Game\bin\game.exe").
    /// </summary>
    public class ExecutableItem
    {
        private readonly string basePath;

        public string FullPath { get; }
        public string FileName => System.IO.Path.GetFileName(FullPath);

        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(basePath))
                {
                    try
                    {
                        var root = System.IO.Path.GetFullPath(basePath).TrimEnd(
                            System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                            + System.IO.Path.DirectorySeparatorChar;
                        var full = System.IO.Path.GetFullPath(FullPath);
                        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        {
                            return full.Substring(root.Length);
                        }
                    }
                    catch
                    {
                        // Fall through to the plain file name.
                    }
                }

                return FileName;
            }
        }

        public ExecutableItem(string fullPath, string basePath = null)
        {
            FullPath = fullPath;
            this.basePath = basePath;
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
        private ExecutableItem selectedExecutable;
        private string statusMessage = string.Empty;
        private string sourceStatusMessage = string.Empty;
        private bool isImporting;
        private string importStatus = string.Empty;
        private double importPercent;
        private bool importIsIndeterminate = true;

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
            PickExeNextCommand = new RelayCommand<object>(_ => PickExeNext(), _ => SelectedExecutable != null);
            BrowseExeCommand = new RelayCommand<object>(_ => BrowseExe());
            BackCommand = new RelayCommand<object>(_ => Back(), _ => CurrentStep != WizardStep.SelectSource);
            FinishCommand = new RelayCommand<object>(_ => Finish(), _ => CanFinish());
            CancelCommand = new RelayCommand<object>(_ => OnCloseRequested(), _ => !IsImporting);

            ScanSourceFolders();
        }

        public ObservableCollection<StorageLocation> StorageLocations { get; }
        public ObservableCollection<ExecutableItem> FoundExecutables { get; }
        public ObservableCollection<SourceFolderItem> SourceFolders { get; }

        public RelayCommand<object> NextFromSourceCommand { get; }
        public RelayCommand<object> ChangeSourceRootCommand { get; }
        public RelayCommand<object> PickExeNextCommand { get; }
        public RelayCommand<object> BrowseExeCommand { get; }
        public RelayCommand<object> BackCommand { get; }
        public RelayCommand<object> FinishCommand { get; }
        public RelayCommand<object> CancelCommand { get; }

        public event EventHandler CloseRequested;

        /// <summary>
        /// The game created by a successful import, or null if none was created. The host reads this
        /// after the window closes to optionally open the metadata editor.
        /// </summary>
        public Game ImportedGame { get; private set; }

        public WizardStep CurrentStep
        {
            get => currentStep;
            set
            {
                currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSelectSourceStep));
                OnPropertyChanged(nameof(IsPickExecutableStep));
                OnPropertyChanged(nameof(IsChooseStorageStep));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsSelectSourceStep => CurrentStep == WizardStep.SelectSource;
        public bool IsPickExecutableStep => CurrentStep == WizardStep.PickExecutable;
        public bool IsChooseStorageStep => CurrentStep == WizardStep.ChooseStorage;

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
        /// True while a game folder is being moved. The window shows a blocking progress overlay and
        /// disables navigation while this is set.
        /// </summary>
        public bool IsImporting
        {
            get => isImporting;
            set { isImporting = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>The current phase of the import, shown above the progress bar.</summary>
        public string ImportStatus
        {
            get => importStatus;
            set { importStatus = value; OnPropertyChanged(); }
        }

        /// <summary>Completion percentage (0–100) of the folder move.</summary>
        public double ImportPercent
        {
            get => importPercent;
            set { importPercent = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True when the current phase has no measurable percentage (the progress bar animates instead
        /// of filling to a value).
        /// </summary>
        public bool ImportIsIndeterminate
        {
            get => importIsIndeterminate;
            set { importIsIndeterminate = value; OnPropertyChanged(); }
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

        /// <summary>
        /// Moves to the executable-picking step, listing the game executables found anywhere inside the
        /// selected download folder (so the real game exe is offered even when it sits in a subfolder
        /// alongside junk).
        /// </summary>
        private void NextFromSource()
        {
            FoundExecutables.Clear();
            foreach (var exe in GameImporter.FindGameExecutables(SelectedSourceFolder))
            {
                FoundExecutables.Add(new ExecutableItem(exe, SelectedSourceFolder));
            }

            SelectedExecutable = FoundExecutables.FirstOrDefault();
            StatusMessage = FoundExecutables.Count == 0
                ? "No .exe files found in this folder. Use \"Browse for another .exe...\" to pick one manually."
                : $"Found {FoundExecutables.Count} executable(s). Select the one that launches the game — its folder is what gets kept.";

            CurrentStep = WizardStep.PickExecutable;
        }

        private void BrowseExe()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Select the game executable";
                dialog.Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*";
                if (Directory.Exists(SelectedSourceFolder))
                {
                    dialog.InitialDirectory = SelectedSourceFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrEmpty(dialog.FileName))
                {
                    var existing = FoundExecutables.FirstOrDefault(
                        e => string.Equals(e.FullPath, dialog.FileName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        existing = new ExecutableItem(dialog.FileName, SelectedSourceFolder);
                        FoundExecutables.Add(existing);
                    }

                    SelectedExecutable = existing;
                }
            }
        }

        private void PickExeNext()
        {
            RefreshStorageFreeSpace();
            CurrentStep = WizardStep.ChooseStorage;
        }

        private void Back()
        {
            if (CurrentStep == WizardStep.ChooseStorage)
            {
                CurrentStep = WizardStep.PickExecutable;
            }
            else if (CurrentStep == WizardStep.PickExecutable)
            {
                CurrentStep = WizardStep.SelectSource;
            }
        }

        private void RefreshStorageFreeSpace()
        {
            foreach (var location in StorageLocations)
            {
                location.RefreshFreeSpace();
            }
        }

        private bool CanFinish()
        {
            return !IsImporting
                && SelectedExecutable != null
                && !string.IsNullOrWhiteSpace(SelectedSourceFolder)
                && Directory.Exists(SelectedSourceFolder)
                && SelectedStorage != null
                && !string.IsNullOrWhiteSpace(SelectedStorage.Path);
        }

        /// <summary>
        /// Performs the import: moves only the real game folder into storage, sends the leftover download
        /// wrapper to the Recycle Bin, and registers the game. The metadata editor (if enabled) is opened
        /// by the host once this window has closed.
        ///
        /// The folder move can take a long time (large games, cross-volume copies), so it runs on a
        /// background thread while a progress overlay keeps the window responsive. <see cref="Progress{T}"/>
        /// marshals the reports back to the UI thread, so the bound properties can be set directly here.
        /// </summary>
        private async void Finish()
        {
            var sourceFolder = SelectedSourceFolder;
            var exePath = SelectedExecutable.FullPath;
            var storagePath = SelectedStorage.Path;

            ImportStatus = "Preparing...";
            ImportIsIndeterminate = true;
            ImportPercent = 0;
            IsImporting = true;

            var progress = new Progress<ImportProgress>(update =>
            {
                ImportStatus = update.Status;
                if (update.Percent.HasValue)
                {
                    ImportIsIndeterminate = false;
                    ImportPercent = update.Percent.Value;
                }
                else
                {
                    ImportIsIndeterminate = true;
                }
            });

            try
            {
                var result = await Task.Run(() =>
                    importer.ImportGameFolder(sourceFolder, exePath, storagePath, progress));

                // Registering the game touches the Playnite database, which expects to be called from
                // the UI thread, so do it here after the background move has finished.
                ImportStatus = "Adding game to your library...";
                ImportIsIndeterminate = true;
                var game = importer.AddGame(result.NewExePath);
                ImportedGame = game;

                var summary = $"Added \"{game.Name}\" to your library.";
                if (result.WrapperRemoved)
                {
                    summary += "\n\nThe game folder was moved to your storage location and the leftover " +
                               "download folder was sent to the Recycle Bin.";
                }

                if (settings.OpenMetadataAfterImport)
                {
                    summary += "\n\nOpening the game editor so you can download metadata.";
                }

                api.Dialogs.ShowMessage(summary, "Custom Importer");
                OnCloseRequested();
            }
            catch (Exception ex)
            {
                api.Dialogs.ShowErrorMessage(ex.Message, "Custom Importer");
            }
            finally
            {
                IsImporting = false;
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
