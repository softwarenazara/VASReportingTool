$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe"
$project = "G:\sam ofc pc\sam\d drive\Samadhan\sam_project\VASReportingTool\VASReportingTool.sln"
& $msbuild $project /p:Configuration=Debug /verbosity:minimal
