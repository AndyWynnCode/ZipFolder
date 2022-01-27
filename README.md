# ZipFolder

C# Console application to zip or 7z the contents of a folder based on rules provided in a JSON, which will be created on first run.

This program works by first creating a filelist.txt which contains the files to be zipped, then it simply calls the 7z command line to zip up those files

7z is required to be present in your PATH statement.

## zipInstructions.json

- **rootPath 
- **zipDestPath": ".\\Build alpha%nextFileNumber%.7z",
- **zipFormat** - 7z or zip are supported. Technically, this is only checked for "zip", if any other string is provided, 7z will be used.
- **includeFiles** - 
- **includeFolders** - 
- **excludeFiles** - 
- **excludeFolders** - 
- **includeOnly** - 
- **nextFileNumber** - 
- **justMakeFileList** - 
- **lastException** - 
