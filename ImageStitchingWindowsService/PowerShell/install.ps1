$path=Join-Path -Path $PSScriptRoot -ChildPath "..\bin\Release\net5.0" | Resolve-Path
$path .\ImageStitchingWindowsServiceTopshelf.exe install