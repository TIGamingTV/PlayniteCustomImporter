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

            if (string.Equals(Path.GetFullPath(sourceFolder), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                // Already in place; nothing to move.
                return destination;
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
                CopyDirectory(sourceFolder, destination);
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
