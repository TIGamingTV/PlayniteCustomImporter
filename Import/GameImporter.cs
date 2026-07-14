using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        // Scene / repacker tags and packaging noise that hurt Playnite's metadata name matching.
        private static readonly HashSet<string> noiseTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "repack", "multi", "codex", "plaza", "skidrow", "reloaded", "flt", "tenoke", "razor1911",
            "hoodlum", "empress", "goldberg", "rune", "doge", "prophet", "cpy", "fitgirl", "dodi",
            "elamigos", "gog", "proper", "incl", "dlc", "update", "crackfix", "readnfo", "x64", "x86",
            "win64", "win32"
        };

        private readonly IPlayniteAPI api;

        public GameImporter(IPlayniteAPI api)
        {
            this.api = api;
        }

        /// <summary>
        /// Returns the immediate subfolders of <paramref name="root"/> that contain at least one
        /// <c>.exe</c> file anywhere inside them. Used to show the user only folders that look like
        /// importable games. The root itself is never returned (importing it would move the whole
        /// download folder), and inaccessible subfolders are skipped rather than aborting the scan.
        /// </summary>
        public static List<string> FindGameFolders(string root)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return result;
            }

            string[] subDirectories;
            try
            {
                subDirectories = Directory.GetDirectories(root);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Could not enumerate subfolders of {root}.");
                return result;
            }

            foreach (var directory in subDirectories.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (ContainsExecutable(directory))
                {
                    result.Add(directory);
                }
            }

            return result;
        }

        /// <summary>
        /// True when <paramref name="folder"/> holds at least one <c>.exe</c> at any depth. Enumeration
        /// is lazy so it stops at the first match, and access errors are treated as "no exe found"
        /// rather than propagating.
        /// </summary>
        private static bool ContainsExecutable(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories).Any();
            }
            catch (Exception)
            {
                // Permission issues, symlink loops, etc. Treat the folder as not scannable.
                return false;
            }
        }

        // Executables that are almost never the game itself; used to keep the recursive fallback tidy.
        private static readonly string[] nonGameExeMarkers =
        {
            "unins", "uninstall", "vcredist", "vc_redist", "dxsetup", "directx", "dotnet",
            "dotnetfx", "redist", "crashreport", "crashhandler", "setup", "install"
        };

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
        /// Lists candidate game executables in <paramref name="folder"/>. Top-level exes are preferred;
        /// when there are none (the game lives in a subfolder), it falls back to a recursive search with
        /// obvious installers/redistributables filtered out so the user still gets a useful list.
        /// </summary>
        public static List<string> FindExecutables(string folder)
        {
            var topLevel = FindTopLevelExes(folder);
            if (topLevel.Count > 0)
            {
                return topLevel;
            }

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return topLevel;
            }

            try
            {
                return Directory
                    .EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories)
                    .Where(p => !IsLikelyNonGameExe(p))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Recursive executable search failed under {folder}.");
                return topLevel;
            }
        }

        private static bool IsLikelyNonGameExe(string exePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(exePath);
            return nonGameExeMarkers.Any(marker =>
                fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
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

            // Avoid importing the same executable twice: Playnite happily stores duplicate games, so a
            // second import of the same exe silently creates a second library entry.
            var existing = FindExistingGameByExe(exePath);
            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"\"{existing.Name}\" already points at this executable and is already in your library.");
            }

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

        /// <summary>
        /// Finds an already-imported game whose play action launches <paramref name="exePath"/>, or
        /// null when none exists. Comparison is case-insensitive on the full path.
        /// </summary>
        private Game FindExistingGameByExe(string exePath)
        {
            var normalised = Path.GetFullPath(exePath);

            return api.Database.Games.FirstOrDefault(g =>
                g.GameActions != null &&
                g.GameActions.Any(a =>
                    a.Type == GameActionType.File &&
                    !string.IsNullOrEmpty(a.Path) &&
                    PathsEqual(a.Path, normalised)));
        }

        private static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a), b, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Stored action paths can contain variables like {InstallDir}; a plain compare is enough.
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Derives a display name for the game, preferring the containing folder when the exe name is
        /// generic (game.exe, launcher.exe...). The chosen name is cleaned of scene/repacker tags and
        /// separators so Playnite's metadata search has the best chance of matching.
        /// </summary>
        private static string BuildGameName(string exePath, string installDir)
        {
            var exeName = Path.GetFileNameWithoutExtension(exePath);

            var rawName = exeName;
            if (!string.IsNullOrEmpty(installDir) && genericExeNames.Contains(exeName))
            {
                var folderName = new DirectoryInfo(installDir).Name;
                if (!string.IsNullOrWhiteSpace(folderName))
                {
                    rawName = folderName;
                }
            }

            var cleaned = CleanGameName(rawName);
            // If cleaning stripped everything (e.g. the name was only tags), fall back to the raw value
            // so the game is never added with an empty name.
            return string.IsNullOrWhiteSpace(cleaned) ? rawName : cleaned;
        }

        /// <summary>
        /// Normalises a scene-style folder/exe name into something a metadata provider can match:
        /// drops bracketed groups, turns dot/underscore/dash separators into spaces, and removes
        /// version numbers and known release-group / packaging tokens.
        /// </summary>
        internal static string CleanGameName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            // Drop anything from the first bracket onwards, e.g. "Game (2021) [FitGirl Repack]".
            var name = Regex.Replace(raw, @"[\[(].*$", string.Empty);

            // Common separators in scene releases -> spaces.
            name = name.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ');

            var kept = name
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !noiseTokens.Contains(token) && !IsVersionToken(token))
                .ToList();

            return string.Join(" ", kept).Trim();
        }

        /// <summary>
        /// True for tokens that look like a version or build marker (v1, v1.2.3, 2021, build) rather
        /// than part of the title.
        /// </summary>
        private static bool IsVersionToken(string token)
        {
            if (string.Equals(token, "build", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Regex.IsMatch(token, @"^v?\d+(\.\d+)+$", RegexOptions.IgnoreCase);
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
