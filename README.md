# ZipFolder

C# Console application to zip or 7z the contents of a folder based on rules provided in a JSON, which will be created on first run.

This program works by first creating a filelist.txt which contains the files to be zipped, then it simply calls the 7z command line to zip up those files

7z is required to be present in your PATH statement.

## zipInstructions.json

- **rootPath** - The path to use as the root folder for the zip file. If not provided, it will be set to the current directory.
- **zipDestPath** - The path to the destination zip file, relative to the rootPath. Will replace the string %nextFileNumber% with the value in *nextFileNumber*, or the next file number determined by setting *increment* to `False`
- **zipFormat** - 7z or zip are supported. Technically, this is only checked for "zip", if any other string is provided, 7z will be used.
- **includeFiles** - JSON array of relative paths which can include the * and ? wildcards in the *filename* part of the path. These will be included regardless of the *excludeFiles* and *excludeFolders* options.
- **includeFolders** - JSON array of relative paths to folders to be included. These will be included regardless of the *excludeFolders* option.
- **excludeFiles** - As per *includeFiles*, but this will exclude files that may exist in the includeFolders.
- **excludeFolders** - As per *includeFolders*.
- **includeOnly** - `False` will include * by default, `True` will only include files and folders in the includes.
- **nextFileNumber** - Keeps track of the next file number to use. Will be incremented by 1 if *increment* is `True`, or will be set to the current latest file + 1 otherwise
- **justMakeFileList** - Just makes a file list, doesnt zip the files.
- **lastException** - Stores the last exception for debugging purposes. The program will not read the value stored here.
- **increment** - `True` indicates that nextFileNumber should be incremented by 1, `False` will search for the next file number in zipDestPath and add 1 to the largest found.

