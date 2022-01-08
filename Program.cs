using System.Text.Json;
using System.Text.Json.Serialization;

static class Program
{
    private static ZipInstructions config;
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
                string jsonFileName = cd + "\\" + configFileName;
                if (!File.Exists(jsonFileName)) throw new Exception();
                //Try to get the JSON file (zipInstructions.json)
                ReadJSONOptions(jsonFileName);
            }
            catch
            {
                //rebuild the JSON file
                BuildNewOptions(cd);
                Console.WriteLine("Config file not found, please fill in the blanks of the zipInstructions.json just created.\r\nPress any key to exit.");
                Console.ReadKey();
                Environment.Exit(1);
            }
            BuildFileList();
            if (!config.justMakeFileList)
            {
                ZipFileList();
                Console.WriteLine($"{config.zipFormat} file created successfully, Press any key to exit.");
            }
            else
            {
                Console.WriteLine("File List created successfully, Press any key to exit.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Some unknown problem occurred. Check the JSON file for the exception. Press any key to exit.");
            config.lastException = ex;
        }
        finally
        {
            WriteJSONOptions(config);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
    private static void BuildFileList()
    {
        fileList = new();
        GetFilesRecursive(config.rootPathNonNull, config.includeOnly);
        //fileList.RemoveAll(a => config.excludeFiles.Any(b => EnumerateString(b, true) == a));
        {
            List<string> ExcludeFiles = GetFileList(config.excludeFiles);
            fileList.RemoveAll(a => ExcludeFiles.Any(b => b == a));
            List<string> IncludeFiles = GetFileList(config.includeFiles);
            foreach (string s in IncludeFiles)
            {
                fileList.Add(EnumerateString(s, true));
            }
        }
        fileList = fileList.Distinct().ToList();
        fileList = fileList.Select(x => x.Replace(config.rootPathNonNull + "\\", "")).ToList();
        if (config.justMakeFileList) Console.WriteLine($"{fileList.Count} files found.");
        fileListPath = config.rootPathNonNull + "\\filelist.txt";
        File.Delete(fileListPath);
        File.WriteAllText(fileListPath, string.Join("\r\n", fileList));
        void GetFilesRecursive(string directory, bool exclude = false)
        {
            foreach (string dir in Directory.GetDirectories(directory))
            {
                /*if(new DirectoryInfo(dir).Name == ".vs")
                {
                    bool check = true;
                }*/
                bool subDirExclude = exclude;
                if (subDirExclude || config.excludeFolders.Any(x => EnumerateString(x, true) == dir))
                {
                    subDirExclude = true;
                }
                if (!subDirExclude || config.includeFolders.Any(x => EnumerateString(x, true) == dir))
                {
                    subDirExclude = false;
                }
                GetFilesRecursive(dir, subDirExclude);
            }
            if (!exclude || config.includeFolders.Any(x => EnumerateString(x, true) == directory))
            {
                fileList.AddRange(Directory.EnumerateFiles(directory));
            }
        }
        List<string> GetFileList(List<string> inList, bool checkExists = true)
        {
            List<string> rtn = new();
            foreach (string s in inList)
            {
                string[] pathInfo = GetPathAndFilePattern(config.rootPathNonNull, s);
                rtn.AddRange(Directory.GetFiles(pathInfo[0], pathInfo[1], SearchOption.TopDirectoryOnly));
            }
            return rtn;
        }
    }
    private static string[] GetPathAndFilePattern(string pathPart1, string pathPart2)
    {
        string[] rtn = new string[2];
        List<string> a = pathPart2.Split("\\").ToList();
        string startPath = pathPart1;
        if (a[0] == ".") a.RemoveAt(0);
        for (; a.Count > 1; a.RemoveAt(0))
        {
            startPath += "\\" + a[0];
        }
        rtn[0] = startPath;
        rtn[1] = a[0];
        return rtn;
    }
    private static void ZipFileList()
    {
        string args = $"/C 7z.exe a {(config.zipFormat == "zip" ? "-tzip" : "-t7z")} \"{EnumerateString(config.zipDestPath)}\" @filelist.txt -mx=9";
        Console.WriteLine(args);
        System.Diagnostics.ProcessStartInfo processInfo = new System.Diagnostics.ProcessStartInfo("cmd.exe", args);
        processInfo.WorkingDirectory = config.rootPathNonNull;
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
        config.nextFileNumber++;
        Console.Write(string.Join("\r\n", stdOut));
        Console.WriteLine("");
        Console.Write(string.Join("\r\n", stdErr));
        Console.WriteLine("");
    }
    private static void ReadJSONOptions(string file)
    {
        string json = File.ReadAllText(file);
        config = JsonSerializer.Deserialize<ZipInstructions>(json);

        config.lastException = null;

        config.includeFiles = CleanList(config.includeFiles);
        config.excludeFiles = CleanList(config.excludeFiles);
        config.includeFolders = CleanList(config.includeFolders);
        config.excludeFolders = CleanList(config.excludeFolders);

        if (string.IsNullOrWhiteSpace(config.zipDestPath)) config.zipDestPath = ".\\output.zip";

        if (!config.increment)
        {
            DetermineNextNumber();
        }

        List<string> CleanList(List<string> inList)
        {
            inList = inList.Distinct().ToList();
            inList.Remove("");
            return inList;
        }
        void DetermineNextNumber()
        {
            string[] pathInfo = GetPathAndFilePattern(config.rootPathNonNull, config.zipDestPath);
            string[] replaceString = pathInfo[1].Split("%nextFileNumber%");
            if (replaceString.Length != 2) throw new Exception();
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
                if (nextFileNumber == 0) throw new Exception();
                config.nextFileNumber = ++nextFileNumber;
            }
            catch
            {
                Console.WriteLine("Problem converting the numbers in the current list to parsable values - falling back on the current value of nextFileNumber - " + config.nextFileNumber);
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
        string outStr = input.Replace("%nextFileNumber%", config.nextFileNumber.ToString()).Replace("%date%", DateTime.Now.ToString("yyyy-MM-dd"));
        outStr = config.rootPathNonNull + "\\" + outStr.Replace(".\\", "");
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
    public string rootPath { get; set; }
    [JsonIgnore]
    public string rootPathNonNull
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
    public Exception? lastException { get; set; }
}
