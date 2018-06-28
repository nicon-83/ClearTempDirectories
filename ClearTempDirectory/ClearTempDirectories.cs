using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace MVA.ConsoleApp
{
    class ClearTempDirectories
    {
        private static int filesCount = 0;
        private static int dirsCount = 0;

        private class Type
        {
            public static String success = "success";
            public static String information = "information";
            public static String error = "error";
        }

        static void Main(string[] args)
        {
            try
            {
                string tempDirectory = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMDRIVE"), @"\Temp");
                string logFilePath = Path.Combine(tempDirectory, DateTime.Now.ToString("dd.MM.yyyy_H.mm.ss") + ".log");
                StreamWriter logFile = null;

                PrintMessage("Будет выполнена очистка временных директорий компьютера", Type.information);
                Console.WriteLine();
                Console.WriteLine("Для продолжения нажмите Enter или любую другую клавишу для выхода...");
                Console.WriteLine();
                ConsoleKeyInfo key = Console.ReadKey();
                if (key.Key.ToString() != "Enter")
                {
                    return;
                }

                if (!Directory.Exists(tempDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(tempDirectory);
                    }
                    catch (Exception ex)
                    {
                        PrintMessage(ex.Message, Type.error);
                    }
                }

                try
                {
                    logFile = File.CreateText(logFilePath);
                    logFile.WriteLine();
                    logFile.WriteLine($"{DateTime.Now} Запущено удаление");
                    logFile.WriteLine(new String('-', 60));
                }
                catch (Exception ex)
                {
                    PrintMessage(ex.Message, Type.error);
                }

                Console.SetWindowSize((int)(Console.LargestWindowWidth * 0.7), (int)(Console.LargestWindowHeight * 0.7));

                List<string> FilesAndFoldersList = CreateDirectoriesList();

                DeleteFilesAndFolders(FilesAndFoldersList, logFile);

                logFile.WriteLine(new String('-', 60));
                logFile.WriteLine($"{DateTime.Now} Удаление завершено. Удалено {dirsCount} папок и {filesCount} файлов");
                logFile.WriteLine();
                logFile.Dispose();
                PrintMessage($"Удаление завершено. Удалено {dirsCount} папок и {filesCount} файлов, см. лог {Environment.GetEnvironmentVariable("SYSTEMDRIVE")}{logFilePath}", Type.information);
                Console.WriteLine();
                Console.WriteLine("Для выхода нажмите любую клавишу...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                PrintMessage(ex.Message, Type.error);
            }
        }

        private static List<String> CreateDirectoriesList()
        {
            #region Формируем список директорий в которых будет производиться удаление

            List<String> FilesAndFoldersList = new List<string>()
            {
                Path.Combine(Environment.GetEnvironmentVariable("SYSTEMDRIVE"), @"\$RECYCLE.BIN"),
                Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "Temp")
            };

            SelectQuery query = new SelectQuery("Select * from win32_userprofile");
            ManagementObjectSearcher searcher = null;
            try
            {
                searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject userProfile in searcher.Get())
                {
                    if (!userProfile["localpath"].ToString().ToLower().Contains("windows"))
                    {
                        FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\Local\Temp");
                        FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\LocalLow\Sun\Java\Deployment\cache");
                        FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\Local\Microsoft\Windows\INetCache");
                        if (Environment.OSVersion.ToString().Contains("6.1")) //Windows 7
                        {
                            FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\Local\Microsoft\Windows\Temporary Internet Files");
                        }
                        FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\Local\Mozilla\Firefox\Profiles\cache2");
                        FilesAndFoldersList.Add((userProfile["localpath"] as String) + @"\AppData\Local\Google\Chrome\User Data\Default\Cache");
                        if (Directory.Exists((userProfile["localpath"] as String) + @"\AppData\Local\Packages\Microsoft.MicrosoftEdge_8wekyb3d8bbwe\AC"))
                        {
                            IEnumerable<string> dirs = Directory.EnumerateDirectories((userProfile["localpath"] as String) + @"\AppData\Local\Packages\Microsoft.MicrosoftEdge_8wekyb3d8bbwe\AC", "*", SearchOption.AllDirectories);
                            foreach (var dir in dirs)
                            {
                                if (dir.ToLower().Contains("inetcache"))
                                {
                                    FilesAndFoldersList.Add(dir);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PrintMessage(ex.Message, Type.error);
            }

            return FilesAndFoldersList;

            #endregion
        }

        private static void DeleteFilesAndFolders(List<string> FilesAndFoldersList, StreamWriter logFile)
        {
            #region Удаляем директории и файлы
            IEnumerable<string> files = null;
            List<string> dirs = null;
            ProgressBarOptions options = new ProgressBarOptions
            {
                ProgressCharacter = '_',
                ProgressBarOnBottom = true,
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGray,
            };

            try
            {
                foreach (string currentPath in FilesAndFoldersList)
                {
                    if (Directory.Exists(currentPath))
                    {
                        PrintMessage($@"Сканирование файлов в директории {currentPath}", Type.information);

                        files = GetAllFiles(currentPath, "*");

                        PrintMessage($@"найдено файлов: {files.Count()}", Type.information);
                        filesCount += files.Count();
                        if (files.Count() > 0)
                        {
                            using (ProgressBar pbar = new ProgressBar(files.Count(), "", options))
                            {
                                foreach (string file in files)
                                {
                                    pbar.Tick($@"Delete file: {file}");
                                    try
                                    {
                                        File.Delete(file);
                                        logFile.WriteLine(file);
                                    }
                                    catch (Exception ex)
                                    {
                                        logFile.WriteLine("Ошибка при удалении файла: " + ex.Message);
                                        filesCount--;
                                    }
                                }
                            }
                        }

                        PrintMessage($@"Сканирование папок в директории {currentPath}", Type.information);

                        dirs = GetAllDirectories(currentPath, "*");

                        PrintMessage($@"найдено папок: {dirs.Count()}", Type.information);
                        dirsCount += dirs.Count();
                        if (dirs.Count() > 0)
                        {
                            using (ProgressBar pbar = new ProgressBar(dirs.Count(), "", options))
                            {
                                foreach (string dir in dirs)
                                {
                                    pbar.Tick($@"Delete directory: {dir}");
                                    try
                                    {
                                        Directory.Delete(dir);
                                        logFile.WriteLine(dir);
                                    }
                                    catch (Exception ex)
                                    {
                                        logFile.WriteLine($"Ошибка при удалении папки: {dir}: " + ex.Message);
                                        dirsCount--;
                                    }
                                }
                            }
                        }
                    }
                }
                Console.WriteLine(new String('-', 60));
            }
            catch (Exception ex)
            {
                PrintMessage(ex.Message, Type.error);
            }

            #endregion
        }

        private static void PrintMessage(string messageText, string messageType)
        {
            #region Метод для печати сообщений
            switch (messageType)
            {
                case "success":
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(messageText);
                    Console.ResetColor();
                    Console.WriteLine();
                    break;
                case "information":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(messageText);
                    Console.ResetColor();
                    break;
                case "error":
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(messageText);
                    Console.ResetColor();
                    Console.WriteLine();
                    break;
                default:
                    Console.WriteLine(messageText);
                    Console.WriteLine();
                    break;
            }
            #endregion
        }

        private static IEnumerable<String> GetAllFiles(string path, string searchPattern)
        {
            #region Формируем список файлов директории для удаления, поиск ведется рекурсивно
            return Directory.EnumerateFiles(path, searchPattern).Union(Directory.EnumerateDirectories(path).SelectMany(d =>
                {
                    try
                    {
                        return GetAllFiles(d, searchPattern);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return Enumerable.Empty<String>();
                    }
                }));
            #endregion
        }

        private static List<string> GetAllDirectories(string path, string searchPattern)
        {
            #region Формируем список директорий для удаления, поиск ведется рекурсивно
            List<string> dirs = new List<string>();

            try
            {
                dirs.AddRange(Directory.GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly));
                foreach (string directory in Directory.GetDirectories(path))
                {
                    dirs.AddRange(GetAllDirectories(directory, searchPattern));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex)
            {
                PrintMessage(ex.Message, Type.error);
            }

            return dirs;
            #endregion
        }
    }
}
