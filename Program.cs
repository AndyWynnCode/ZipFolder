using System.Text.Json;
using System.Text.Json.Serialization;

static class Program
{
    private static ZipInstructions cfg;
    private const string configFileName = "zipInstructions.json";
    private static List<string> fileList = new();
    private static string fileListPath = "";
    public static void Main()
    {
        try
        {
            string cd = Directory.GetCurrentDirectory();
            try
            {
                // Try to read the JSON file into the cfg variable
                string jsonFileName = cd + "\\" + configFileName;
                if (!File.Exists(jsonFileName)) throw new FileNotFoundException("Could not find the JSON file");
                ReadJSONOptions(jsonFileName);
            }
            catch
            {
                // Rebuild the JSON file using default values
                BuildNewOptions(cd);
                Console.WriteLine($"Config file not found, please fill in the blanks of the zipInstructions.json just created.{Environment.NewLine}Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(1);
            }
            BuildFileList();
            if (!cfg.justMakeFileList)
            {
                ZipFileList();
                Console.WriteLine($"{cfg.zipFormat} file created successfully, Press any key to exit.");
            }
            else
            {
                Console.WriteLine("File List created successfully, Press any key to exit.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Some unknown problem occurred. Check the JSON file for the exception. Press any key to exit.");
            cfg.lastException = $"{ex.GetType().FullName}{Environment.NewLine}Message: {ex.Message}{Environment.NewLine}StackTrace: {ex.StackTrace}";
        }
        finally
        {
            WriteJSONOptions(cfg);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
    private static void BuildFileList()
    {
        fileList = new();
        GetFilesRecursive(cfg.rootPathNonNull, cfg.includeOnly);
        {
            List<string> ExcludeFiles = GetFileList(cfg.excludeFiles);
            fileList.RemoveAll(a => ExcludeFiles.Any(b => b == a));
            List<string> IncludeFiles = GetFileList(cfg.includeFiles);
            foreach (string s in IncludeFiles)
            {
                fileList.Add(EnumerateString(s, true));
            }
        }
        fileList = fileList.Distinct().ToList();
        fileList = fileList.Select(x => x.Replace(cfg.rootPathNonNull + "\\", "")).ToList();
        if (cfg.justMakeFileList) Console.WriteLine($"{fileList.Count} files found.");
        fileListPath = cfg.rootPathNonNull + "\\filelist.txt";
        File.Delete(fileListPath);
        File.WriteAllText(fileListPath, string.Join(Environment.NewLine, fileList));
        void GetFilesRecursive(string directory, bool exclude = false)
        {
            foreach (string dir in Directory.GetDirectories(directory))
            {
                bool subDirExclude = exclude;
                if (subDirExclude || cfg.excludeFolders.Any(x => EnumerateString(x, true) == dir))
                {
                    subDirExclude = true;
                }
                if (!subDirExclude || cfg.includeFolders.Any(x => EnumerateString(x, true) == dir))
                {
                    subDirExclude = false;
                }
                GetFilesRecursive(dir, subDirExclude);
            }
            if (!exclude || cfg.includeFolders.Any(x => EnumerateString(x, true) == directory))
            {
                fileList.AddRange(Directory.EnumerateFiles(directory));
            }
        }
        List<string> GetFileList(List<string> inList)
        {
            List<string> rtn = new();
            foreach (string s in inList)
            {
                string[] pathInfo = GetPathAndFilePattern(cfg.rootPathNonNull, s);
                rtn.AddRange(Directory.GetFiles(pathInfo[0], pathInfo[1], SearchOption.TopDirectoryOnly));
            }
            return rtn;
        }
    }
    // Returns an array of strings where rtn[0] is the Path and rtn[1] is the file pattern
    // pathPart1 and pathPart2 should evaluate to a valid path when concatenated.
    // Intended for use with Directory.GetFiles(rtn[0],rtn[1])
    private static string[] GetPathAndFilePattern(string pathPart1, string pathPart2)
    {
        string[] rtn = new string[2];
        List<string> a = pathPart2.Split("\\").ToList();
        string startPath = pathPart1;
        if (a[0] == ".") a.RemoveAt(0);
        for (; a.Count > 1; a.RemoveAt(0))
        {
            startPath += $"\\{a[0]}";
        }
        rtn[0] = startPath;
        rtn[1] = a[0];
        return rtn;
    }
    private static void ZipFileList()
    {
        string args = $"/C 7z.exe a {(cfg.zipFormat == "zip" ? "-tzip" : "-t7z")} \"{EnumerateString(cfg.zipDestPath)}\" @filelist.txt -mx=9";
        //Console.WriteLine(args); // <- Debug
        System.Diagnostics.ProcessStartInfo processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", args);
        processInfo.WorkingDirectory = cfg.rootPathNonNull;
        processInfo.CreateNoWindow = true;
        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.StartInfo = processInfo;
        proc.Start();
        List<string> stdOut = new();
        List<string> stdErr = new();
        while (proc.StandardOutput.Peek() > -1)
        {
            string? s = proc.StandardOutput.ReadLine();
            if (!(s == null)) stdOut.Add(s);
        }
        while (proc.StandardOutput.Peek() > -1)
        {
            string? s = proc.StandardError.ReadLine();
            if (!(s == null)) stdErr.Add(s);
        }
        proc.WaitForExit();
        cfg.nextFileNumber++;
        Console.Write(string.Join(Environment.NewLine, stdOut));
        Console.WriteLine("");
        Console.Write(string.Join(Environment.NewLine, stdErr));
        Console.WriteLine("");
    }
    private static void ReadJSONOptions(string file)
    {
        string json = File.ReadAllText(file);
        cfg = JsonSerializer.Deserialize<ZipInstructions>(json);

        cfg.lastException = null;

        cfg.includeFiles = CleanList(cfg.includeFiles);
        cfg.excludeFiles = CleanList(cfg.excludeFiles);
        cfg.includeFolders = CleanList(cfg.includeFolders);
        cfg.excludeFolders = CleanList(cfg.excludeFolders);

        if (string.IsNullOrWhiteSpace(cfg.zipDestPath)) cfg.zipDestPath = ".\\output.zip";

        if (!cfg.increment)
        {
            DetermineNextNumber();
        }

        List<string> CleanList(List<string> inList)
        {
            inList = inList.Distinct().ToList();
            inList.Remove("");
            return inList;
        }
        // Loop through the destination directory to get a list of similarly named files, 
        // then parses them for numbers and adds one to the largest number found.
        void DetermineNextNumber()
        {
            string[] pathInfo = GetPathAndFilePattern(cfg.rootPathNonNull, cfg.zipDestPath);
            string[] replaceString = pathInfo[1].Split("%nextFileNumber%");
            if (replaceString.Length != 2) throw new InvalidDataException(
                $"There are {replaceString.Length} sections after splitting - expected 2. {Environment.NewLine}" +
                $"Likely cause is either %nextFileNumber% occurs multiple times in zipDestPath or not at all. {Environment.NewLine}" +
                "If increment is not required, change the increment flag to false.");
            List<string> fileList = Directory.GetFiles(pathInfo[0], $"{replaceString[0]}*{replaceString[1]}").ToList();
            replaceString[0] = pathInfo[0] + "\\" + replaceString[0];
            List<string> fileNumbers = fileList.Select(x => x.Replace(replaceString[0], "").Replace(replaceString[1], "")).ToList();
            List<float> fileFloats = new();
            try
            {
                foreach (string s in fileNumbers)
                {
                    float f;
                    if (float.TryParse(s, out f))
                    {
                        fileFloats.Add(f);
                    }
                    else
                    {
                        fileFloats.Add(0);
                    }
                }
                float maxFile = fileFloats.Max();
                int nextFileNumber = (int)maxFile;
                if (nextFileNumber == 0) throw new DivideByZeroException("This error should never be seen.");
                cfg.nextFileNumber = ++nextFileNumber;
            }
            catch
            {
                Console.WriteLine("Problem converting the numbers in the current list to parsable values - falling back on the current value of nextFileNumber - " + cfg.nextFileNumber);
                return;
            }
        }
    }
    private static void BuildNewOptions(string rootPath = "")
    {
        List<string> tmpList = Enumerable.Repeat(string.Empty, 5).ToList();
        ZipInstructions tmp = new ZipInstructions(rootPath, "", "zip", new List<string>(tmpList), new List<string>(tmpList), new List<string>(tmpList), new List<string>(tmpList));
        tmp.excludeFiles.Add(".\\filelist.txt");
        tmp.excludeFiles.Add(".\\zipInstructions.json");
        string? s = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (!(s == null)) tmp.excludeFiles.Add($".\\{new FileInfo(s).Name}");
        WriteJSONOptions(tmp);
    }
    private static void WriteJSONOptions(ZipInstructions zi)
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        string json = JsonSerializer.Serialize(zi, options);
        File.WriteAllText(Directory.GetCurrentDirectory() + "\\" + configFileName, json);
    }
    private static string EnumerateString(string input, bool includeRootPath = false)
    {
        string outStr = input.Replace("%nextFileNumber%", cfg.nextFileNumber.ToString()).Replace("%date%", DateTime.Now.ToString("yyyy-MM-dd"));
        outStr = cfg.rootPathNonNull + "\\" + outStr.Replace(".\\", "");
        return outStr;
    }
}
public struct ZipInstructions
{
    public ZipInstructions(string rootPath, string zipDestPath, string zipFormat, List<string> includeFiles, List<string> includeFolders, List<string> excludeFiles, List<string> excludeFolders, int nextFileNumber = 1, bool includeOnly = false, bool justMakeFileList = false, bool increment = true)
    {
        this.rootPath = rootPath;
        this.zipDestPath = zipDestPath;
        this.zipFormat = zipFormat;
        this.includeFiles = includeFiles;
        this.includeFolders = includeFolders;
        this.excludeFiles = excludeFiles;
        this.excludeFolders = excludeFolders;
        this.includeOnly = includeOnly;
        this.nextFileNumber = nextFileNumber;
        this.lastException = null;
        this.justMakeFileList = justMakeFileList;
        this.increment = increment;
    }
    public string rootPath { get; set; } // The root path where the application will run
    [JsonIgnore]
    public string rootPathNonNull // The root path, but if that is null, returns the current directory
    {
        get
        {
            if (string.IsNullOrWhiteSpace(rootPath) || rootPath.Trim().Replace("\\", "") == ".") return Directory.GetCurrentDirectory();
            return rootPath;
        }
    }
    public string zipDestPath { get; set; }
    public string zipFormat { get; set; }
    public List<string> includeFiles { get; set; }
    public List<string> includeFolders { get; set; }
    public List<string> excludeFiles { get; set; }
    public List<string> excludeFolders { get; set; }
    public bool includeOnly { get; set; }
    public bool increment { get; set; }
    public int nextFileNumber { get; set; }
    public bool justMakeFileList { get; set; }
    public string? lastException { get; set; }
}
