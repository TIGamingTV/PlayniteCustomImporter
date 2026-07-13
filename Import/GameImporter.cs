using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace PlayniteCustomImporter.Import
{
    /// <summary>
    /// Non-UI helper that performs the folder move, executable discovery and game registration.
    /// </summary>
    public class GameImporter
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly HashSet<string> genericExeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "game", "launcher", "launch", "start", "play", "run", "setup", "install", "main"
        };

        private readonly IPlayniteAPI api;

        public GameImporter(IPlayniteAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Returns the .exe files that sit directly inside <paramref name="folder"/> (non-recursive).
        /// </summary>
        public static List<string> FindTopLevelExes(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return new List<string>();
            }

            return Directory
                .GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Moves <paramref name="sourceFolder"/> into <paramref name="storageRoot"/>, keeping the
        /// original folder name. Falls back to copy+delete for cross-volume moves. Returns the new
        /// full path of the moved folder.
        /// </summary>
        public string MoveFolder(string sourceFolder, string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                throw new DirectoryNotFoundException($"Source folder does not exist: {sourceFolder}");
            }

            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("Storage location path is not set.", nameof(storageRoot));
            }

            Directory.CreateDirectory(storageRoot);

            var folderName = new DirectoryInfo(sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
            var destination = Path.Combine(storageRoot, folderName);

            var sourceFull = Path.GetFullPath(sourceFolder);
            var destinationFull = Path.GetFullPath(destination);

            if (string.Equals(sourceFull, destinationFull, StringComparison.OrdinalIgnoreCase))
            {
                // Already in place; nothing to move.
                return destination;
            }

            // Reject moving a folder into itself. If the storage location lives inside the source
            // folder, Directory.Move throws and the copy fallback below would recurse into the
            // destination as it grows, filling the disk. Fail fast with a clear message instead.
            if (IsSameOrSubPath(sourceFull, destinationFull))
            {
                throw new IOException(
                    "The selected storage location is inside the folder being imported. Choose a storage location outside the game folder.");
            }

            if (Directory.Exists(destination) || File.Exists(destination))
            {
                throw new IOException($"A folder named \"{folderName}\" already exists in the selected storage location.");
            }

            try
            {
                Directory.Move(sourceFolder, destination);
            }
            catch (IOException ex)
            {
                // Directory.Move fails across volumes; fall back to a recursive copy then delete.
                logger.Warn(ex, "Directory.Move failed, falling back to copy+delete.");
                try
                {
                    CopyDirectory(sourceFolder, destination);
                }
                catch
                {
                    // The copy failed part way through. Remove the partial destination so the
                    // source stays the single copy of the data and a retry is not blocked by a
                    // half-written folder. The source is left untouched (it is only deleted after
                    // a fully successful copy below).
                    TryDeleteDirectory(destination);
                    throw;
                }

                Directory.Delete(sourceFolder, true);
            }

            return destination;
        }

        /// <summary>
        /// Registers the selected executable as a game, mirroring Playnite's "add game manually by
        /// exe" behaviour: install directory set to the exe's folder, a File play action and the
        /// game marked as installed. Returns the created game.
        /// </summary>
        public Game AddGame(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable not found: {exePath}");
            }

            var installDir = Path.GetDirectoryName(exePath);
            var name = BuildGameName(exePath, installDir);

            var game = new Game(name)
            {
                InstallDirectory = installDir,
                IsInstalled = true,
                GameActions = new ObservableCollection<GameAction>
                {
                    new GameAction
                    {
                        Name = "Play",
                        Type = GameActionType.File,
                        Path = exePath,
                        WorkingDir = installDir,
                        IsPlayAction = true
                    }
                }
            };

            api.Database.Games.Add(game);
            logger.Info($"Added game \"{name}\" from {exePath}");
            return game;
        }

        private static string BuildGameName(string exePath, string installDir)
        {
            var exeName = Path.GetFileNameWithoutExtension(exePath);

            // Prefer the containing folder name when the exe name is generic (game.exe, launcher.exe...).
            if (!string.IsNullOrEmpty(installDir) && genericExeNames.Contains(exeName))
            {
                var folderName = new DirectoryInfo(installDir).Name;
                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    return folderName;
                }
            }

            return exeName;
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is the same directory as, or a descendant of,
        /// <paramref name="root"/>. Both are expected to be absolute paths.
        /// </summary>
        private static bool IsSameOrSubPath(string root, string candidate)
        {
            var rootWithSeparator = AppendDirectorySeparator(root);
            var candidateWithSeparator = AppendDirectorySeparator(candidate);
            return candidateWithSeparator.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to clean up partial copy at {path}.");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.GetFiles(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
            }
        }
    }
}
