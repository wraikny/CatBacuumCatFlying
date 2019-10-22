#r "paket:
    storage: none
    source https://api.nuget.org/v3/index.json
    nuget Fake.DotNet.Cli
    nuget Fake.IO.FileSystem
    nuget Fake.Core.Target //"

#if !FAKE

#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"
#endif

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

let debugDir = "src/CatBacuumCatFlying/bin/x86/Debug/net472"

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    |> Shell.cleanDirs 
)

Target.create "CopyResources" <| fun _ ->
    let resources = "Resources"
    let targetDir = sprintf "%s/%s" debugDir resources

    Directory.create(debugDir)
    Directory.delete(targetDir) |> ignore
    Shell.copyDir (targetDir) resources (fun _ -> true)

Target.create "Build" (fun _ ->
    !! "src/**/*.*proj"
    |> Seq.iter (DotNet.build id)
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "All"

Target.runOrDefault "All"
