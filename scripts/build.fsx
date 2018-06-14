// include Fake lib
#r @"..\packages\FAKE\tools\FakeLib.dll"
#r @"..\packages\Steinpilz.DevFlow.Fake\tools\Steinpilz.DevFlow.Fake.Lib.dll"
//#load @"c:\data\work\github\fake-build\src\app\Steinpilz.DevFlow.Fake\lib.fs"

open Fake
open Steinpilz.DevFlow.Fake 

Lib.setup(fun p -> 
    { p with 
        PublishProjects = !!"src/app/**/*.csproj"
        UseDotNetCliToTest = true
        // UseDotNetCliToPack = true Temporary pack with msbuild since dotnet cli build not working with Fody
        NuGetFeed = 
            { p.NuGetFeed with 
                ApiKey = environVarOrFail <| "NUGET_API_KEY" |> Some
            }
    }
)

RunTargetOrDefault "Watch"