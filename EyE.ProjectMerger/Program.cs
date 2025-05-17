using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EyE.ProjectMerger;

/// <summary>
/// Класс с настройками объединения файлов
/// </summary>
class MergeSettings
{
    /// <summary>
    /// Удалять комментарии из кода
    /// </summary>
    public bool RemoveComments { get; set; } = true;

    /// <summary>
    /// Удалять пустые строки
    /// </summary>
    public bool RemoveEmptyLines { get; set; } = true;

    /// <summary>
    /// Сжимать излишние пробелы
    /// </summary>
    public bool CompressWhitespace { get; set; } = true;

    /// <summary>
    /// Удалять отступы в начале строк
    /// </summary>
    public bool RemoveIndentation { get; set; } = true;
    
    /// <summary>
    /// Список директорий, которые следует исключить
    /// </summary>
    public List<string> ExcludedDirectories { get; set; } =
        ["bin", "obj", "Locale", "deb", "Driver", "wwwroot", "Linux", "Windows", "Migrations", "integrations", "legacy"];
    
    /// <summary>
    /// Список подстрок, при наличии которых в имени директории ее следует исключить
    /// </summary>
    public List<string> ExcludedDirectorySubstrings { get; set; } = [".Tests"];
    
    /// <summary>
    /// Список расширений файлов, которые следует исключить
    /// </summary>
    public List<string> ExcludedExtensions { get; set; } = new List<string> { ".dll", ".po" };
    
    /// <summary>
    /// Список расширений файлов, которые следует включить (если список не пуст, то включаются только файлы с указанными расширениями)
    /// </summary>
    public List<string> IncludedExtensions { get; set; } = new List<string>(){ ".cs", ".razor", "cshtml", "json", "props", "yml" };
    
    /// <summary>
    /// Игнорировать файлы и директории, начинающиеся с точки
    /// </summary>
    public bool IgnoreDotFiles { get; set; } = true;
    
    /// <summary>
    /// Полный путь к директории, где будут сохраняться объединенные файлы
    /// </summary>
    public string OutputDirectoryPath { get; set; } = @"E:\repos\EyE.ProjectMerger\EyE.ProjectMerger\MergedProjects";
    
    /// <summary>
    /// Проверяет, должна ли директория быть исключена
    /// </summary>
    public bool ShouldExcludeDirectory(string directoryName)
    {
        if (ExcludedDirectories.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
            return true;
            
        foreach (var pattern in ExcludedDirectorySubstrings)
        {
            if (directoryName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        if (IgnoreDotFiles && directoryName.StartsWith("."))
            return true;
            
        return false;
    }
    
    /// <summary>
    /// Проверяет, должен ли файл быть исключен
    /// </summary>
    public bool ShouldExcludeFile(string fileName)
    {
        if (IgnoreDotFiles && fileName.StartsWith("."))
            return true;
        
        string extension = Path.GetExtension(fileName);
        
        // Проверяем, исключается ли расширение файла
        if (!string.IsNullOrEmpty(extension) && ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return true;
        
        // Если указан список включаемых расширений, то включаем только файлы с этими расширениями
        if (IncludedExtensions.Count > 0)
        {
            if (string.IsNullOrEmpty(extension) || !IncludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return true;
        }
            
        return false;
    }
}

class Program
{
    //dddddd
    private static readonly MergeSettings Settings = new MergeSettings();

    static async Task Main(string[] args)
    {
        while (true)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Приложение для объединения файлов проекта .NET");
            Console.WriteLine("=============================================");

            // Получаем путь к папке проекта
            string projectPath;
            if (args.Length > 0)
            {
                projectPath = args[0];
            }
            else
            {
                Console.Write("Введите полный путь к папке проекта: ");
                projectPath = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            {
                Console.WriteLine("Указанный путь не существует или некорректен.");
                return;
            }

            // Получаем путь для выходного файла
            string outputPath;
            if (args.Length > 1)
            {
                outputPath = args[1];
            }
            else
            {
                // Создаем директорию для сохранения файлов, если она не существует
                if (!Directory.Exists(Settings.OutputDirectoryPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Settings.OutputDirectoryPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"Не удалось создать директорию {Settings.OutputDirectoryPath}: {ex.Message}");
                        Console.WriteLine("Будет использована текущая директория.");
                        Settings.OutputDirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
                    }
                }

                // Генерируем имя файла на основе имени проекта и текущей даты/времени
                string projectName = Path.GetFileName(projectPath);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"{projectName}_{timestamp}.txt";

                outputPath = Path.Combine(Settings.OutputDirectoryPath, fileName);
                Console.WriteLine($"Выходной файл будет сохранен в: {outputPath}");
            }

            try
            {
                // Создаем объект директорий для корневой папки проекта
                var rootDirectory = new DirectoryNode(projectPath, projectPath);

                // Строим дерево файлов и директорий
                BuildDirectoryTree(rootDirectory);

                int totalFiles = CountFiles(rootDirectory);
                Console.WriteLine($"Найдено {totalFiles} файлов для объединения.");

                // Объединяем файлы с сохранением структуры директорий
                await MergeFilesAsync(rootDirectory, outputPath);

                Console.WriteLine($"Файлы успешно объединены в: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Рекурсивно строит дерево директорий и файлов
    /// </summary>
    private static void BuildDirectoryTree(DirectoryNode directoryNode)
    {
        // Добавляем файлы из текущей директории, применяя фильтрацию
        foreach (var filePath in Directory.GetFiles(directoryNode.FullPath))
        {
            var fileName = Path.GetFileName(filePath);
            
            // Пропускаем файлы, которые должны быть исключены
            if (Settings.ShouldExcludeFile(fileName))
                continue;
                
            var fileNode = new FileNode(filePath, directoryNode.RootPath);
            directoryNode.Files.Add(fileNode);
        }

        // Рекурсивно обрабатываем поддиректории, применяя фильтрацию
        foreach (var subDirPath in Directory.GetDirectories(directoryNode.FullPath))
        {
            var dirName = Path.GetFileName(subDirPath);
            
            // Пропускаем директории, которые должны быть исключены
            if (Settings.ShouldExcludeDirectory(dirName))
                continue;
            
            var subdirectoryNode = new DirectoryNode(subDirPath, directoryNode.RootPath);
            BuildDirectoryTree(subdirectoryNode);
            directoryNode.Subdirectories.Add(subdirectoryNode);
        }
    }

    /// <summary>
    /// Подсчитывает общее количество файлов в дереве директорий
    /// </summary>
    private static int CountFiles(DirectoryNode directory)
    {
        int count = directory.Files.Count;
        foreach (var subdir in directory.Subdirectories)
        {
            count += CountFiles(subdir);
        }
        return count;
    }

    /// <summary>
    /// Объединяет файлы в один, сохраняя структуру директорий
    /// </summary>
    private static async Task MergeFilesAsync(DirectoryNode rootDirectory, string outputPath)
    {
        using (var outputFile = new StreamWriter(outputPath, false, Encoding.UTF8))
        {
            // Заголовок файла
            await outputFile.WriteLineAsync("=================================================================");
            await outputFile.WriteLineAsync($"ПРОЕКТ: {Path.GetFileName(rootDirectory.FullPath)}");
            await outputFile.WriteLineAsync($"ДАТА ОБЪЕДИНЕНИЯ: {DateTime.Now}");
            await outputFile.WriteLineAsync("=================================================================");
            await outputFile.WriteLineAsync();
            
            // Записываем содержимое всех файлов, сохраняя структуру директорий
            await ProcessDirectoryNodeAsync(rootDirectory, outputFile);
        }
    }

    /// <summary>
    /// Рекурсивно обрабатывает директорию и ее содержимое для записи в выходной файл
    /// </summary>
    private static async Task ProcessDirectoryNodeAsync(DirectoryNode directory, StreamWriter writer)
    {
        // Выводим название директории (кроме корневой)
        // if (indentLevel > 0)
        // {
        //     string indent = new string(' ', (indentLevel - 1) * 2);
        //     await writer.WriteLineAsync();
        //     await writer.WriteLineAsync($"{indent}=================================================================");
        //     await writer.WriteLineAsync($"{indent}ДИРЕКТОРИЯ: {Path.GetFileName(directory.FullPath)}");
        //     await writer.WriteLineAsync($"{indent}=================================================================");
        //     await writer.WriteLineAsync();
        // }

        // Выводим файлы в текущей директории
        foreach (var file in directory.Files)
        {
            await WriteFileContentAsync(file, writer);
        }

        // Рекурсивно обрабатываем все поддиректории
        foreach (var subdirectory in directory.Subdirectories)
        {
            await ProcessDirectoryNodeAsync(subdirectory, writer);
        }
    }

    /// <summary>
    /// Записывает содержимое файла в выходной файл
    /// </summary>
    private static async Task WriteFileContentAsync(FileNode file, StreamWriter writer)
    {
        // Записываем заголовок файла
        await writer.WriteLineAsync($"ФАЙЛ: {file.RelativePath}");
        Console.WriteLine("===");

        // Записываем содержимое файла
        try
        {
            string content = await File.ReadAllTextAsync(file.FullPath, Encoding.UTF8);
            content = MinifyContent(content, file.FullPath);
            await writer.WriteLineAsync(content);
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync($"[Ошибка чтения файла: {ex.Message}]");
        }

        await writer.WriteLineAsync($"====");
    }
    
    /// <summary>
/// Минифицирует содержимое файла в зависимости от его типа и настроек
/// </summary>
private static string MinifyContent(string content, string filePath)
{
    string extension = Path.GetExtension(filePath).ToLowerInvariant();
    
    if (Settings.RemoveComments)
    {
        switch (extension)
        {
            case ".cs":
            case ".razor":
            case ".js":
            case ".ts":
                // Удаляем однострочные комментарии для C#, JS, TS
                content = Regex.Replace(content, @"//.*?$", "", RegexOptions.Multiline);
                // Удаляем многострочные комментарии /* ... */
                content = Regex.Replace(content, @"/\*[\s\S]*?\*/", "", RegexOptions.Singleline);
                // Удаляем XML-комментарии для C# (///...)
                content = Regex.Replace(content, @"///.*?$", "", RegexOptions.Multiline);
                break;
                
            case ".cshtml":
            case ".html":
            case ".xml":
            case ".xaml":
                // Удаляем HTML/XML комментарии
                content = Regex.Replace(content, @"<!--[\s\S]*?-->", "", RegexOptions.Singleline);
                break;
                
            case ".json":
                // JSON не имеет комментариев в стандарте, но некоторые реализации поддерживают
                content = Regex.Replace(content, @"//.*?$", "", RegexOptions.Multiline);
                break;
        }
    }
    
    if (Settings.RemoveEmptyLines)
    {
        // Удаляем пустые строки (включая строки только с пробелами)
        content = Regex.Replace(content, @"^\s*$\n", "", RegexOptions.Multiline);
        
        // Удаляем несколько пустых строк подряд, оставляя только одну
        content = Regex.Replace(content, @"\n{3,}", "\n\n", RegexOptions.Singleline);
    }
    
    if (Settings.RemoveIndentation)
    {
        // Удаляем отступы в начале каждой строки
        content = Regex.Replace(content, @"^[ \t]+", "", RegexOptions.Multiline);
    }
    
    if (Settings.CompressWhitespace)
    {
        // Сжимаем последовательности пробелов в один пробел
        content = Regex.Replace(content, @"[ \t]+", " ", RegexOptions.Singleline);
        
        // Удаляем пробелы перед знаками пунктуации
        content = Regex.Replace(content, @"\s+([\.,;:\)\]}])", "$1", RegexOptions.Singleline);
        
        // Удаляем пробелы после открывающих скобок
        content = Regex.Replace(content, @"([\(\[\{])\s+", "$1", RegexOptions.Singleline);
    }
    
    return content;
}
}

/// <summary>
/// Класс, представляющий директорию в дереве файловой системы
/// </summary>
class DirectoryNode
{
    public string FullPath { get; }
    public string RootPath { get; }
    public string RelativePath { get; }
    public List<FileNode> Files { get; } = new List<FileNode>();
    public List<DirectoryNode> Subdirectories { get; } = new List<DirectoryNode>();

    public DirectoryNode(string fullPath, string rootPath)
    {
        FullPath = fullPath;
        RootPath = rootPath;
        RelativePath = GetRelativePath(fullPath, rootPath);
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        // Убедимся, что пути заканчиваются разделителем
        if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            basePath += Path.DirectorySeparatorChar;
        }

        if (fullPath.Equals(basePath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            return ".";
        }

        Uri baseUri = new Uri(basePath);
        Uri fullUri = new Uri(fullPath + Path.DirectorySeparatorChar);

        Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Заменяем прямые слеши на обратные для Windows
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        // Убираем завершающий разделитель
        if (relativePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            relativePath = relativePath.Substring(0, relativePath.Length - 1);
        }

        return relativePath;
    }
}

/// <summary>
/// Класс, представляющий файл в дереве файловой системы
/// </summary>
class FileNode
{
    public string FullPath { get; }
    public string RelativePath { get; }

    public FileNode(string fullPath, string rootPath)
    {
        FullPath = fullPath;
        RelativePath = GetRelativePath(fullPath, rootPath);
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        // Убедимся, что базовый путь заканчивается разделителем
        if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            basePath += Path.DirectorySeparatorChar;
        }

        Uri baseUri = new Uri(basePath);
        Uri fullUri = new Uri(fullPath);

        Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Заменяем прямые слеши на обратные для Windows
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

        return relativePath;
    }
}