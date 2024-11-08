// For more information see https://aka.ms/fsharp-console-apps
open System.ServiceProcess
open System.Diagnostics
open System.Threading
open System.IO
open YamlDotNet.Serialization
open Fake.IO
open Fake.IO.FileSystemOperators
open System.Collections.Generic


let stopService serviceName =
    let serviceController = new ServiceController(serviceName)
    match serviceController.Status with
    | ServiceControllerStatus.Running ->
        serviceController.Stop()
        serviceController.WaitForStatus(ServiceControllerStatus.Stopped)
        printfn "Service '%s' stopped successfully." serviceName
    | _ ->
        printfn "Service '%s' is not running or is in an unknown state." serviceName


let startService serviceName =
    let serviceController = new ServiceController(serviceName)
    match serviceController.Status with
    | ServiceControllerStatus.Stopped ->
        serviceController.Start()
        serviceController.WaitForStatus(ServiceControllerStatus.Running)
        printfn "Service '%s' started successfully." serviceName
    | _ ->
        printfn "Service '%s' is not running or is in an unknown state." serviceName


let startProcess processName arguments =
    let psi = new ProcessStartInfo()
    psi.FileName <- processName
    psi.Arguments <- arguments
    psi.RedirectStandardOutput <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    use readProcess = new Process()
    readProcess.StartInfo <- psi
    readProcess.Start() |> ignore
    let output = readProcess.StandardOutput.ReadToEnd()
    readProcess.WaitForExit()
    output


let buildSolution solutionPath =
    let arguments = sprintf "build %s" solutionPath
    startProcess "dotnet" arguments


let runDotnetCommand arguments =
    let psi = new ProcessStartInfo()
    psi.FileName <- "dotnet"
    psi.Arguments <- arguments
    psi.RedirectStandardOutput <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    use dotnetProcess = new Process()
    dotnetProcess.StartInfo <- psi
    dotnetProcess.Start() |> ignore
    let output = dotnetProcess.StandardOutput.ReadToEnd()
    dotnetProcess.WaitForExit()
    output


let removeNuGetPackage packageName projectPath =
    let arguments = sprintf "remove %s package %s" projectPath packageName
    runDotnetCommand arguments


let addNuGetPackage packageName version projectPath =
    let arguments = sprintf "add %s package %s --version %s" projectPath packageName version
    runDotnetCommand arguments


let copyDllFiles sourceDir targetDir =
    let files = Directory.GetFiles(sourceDir, "*.dll", SearchOption.TopDirectoryOnly)
    for file in files do
        let fileName = Path.GetFileName(file)
        let targetPath = Path.Combine(targetDir, fileName)
        File.Copy(file, targetPath, true)
        printfn "Copied: %s to %s" file targetPath


// Define a helper class for deserialization 
type ServiceConfigHelper() = 
    member val ServiceName : string = "" with get, set 
    member val PackageSolutionPaths : List<string> = List<string>() with get, set 
    member val Workflow : Dictionary<int, Dictionary<string, string>> = Dictionary<int, Dictionary<string, string>>() with get, set
    member val SolutionPath : string = "" with get, set 
    member val SourceDirectory : string = "" with get, set 
    member val TargetDirectory : string = "" with get, set 


// Define the target type
type ServiceConfig = {
    ServiceName: string
    PackageSolutionPaths: List<string>
    Workflow: Dictionary<int, Dictionary<string, string>>
    SolutionPath: string
    SourceDirectory: string
    TargetDirectory: string
}


// Function to convert from ServiceConfigHelper to ServiceConfig 
let convertToServiceConfig (helper: ServiceConfigHelper) : ServiceConfig = {
    ServiceName = helper.ServiceName
    PackageSolutionPaths = helper.PackageSolutionPaths
    Workflow = helper.Workflow
    SolutionPath = helper.SolutionPath
    SourceDirectory = helper.SourceDirectory
    TargetDirectory = helper.TargetDirectory
}


// Function to read and deserialize the YAML
let readYamlFile filePath =
    let deserializer = (new DeserializerBuilder()).Build()
    let yaml = File.ReadAllText(filePath)
    deserializer.Deserialize<ServiceConfigHelper>(yaml)


let resolveDir str = 
    (DirectoryInfo.ofPath str).FullName


let requireFile str errMss =
    (FileInfo.ofPath str).FullName    


[<EntryPoint>]
let main args =
    let root = resolveDir (__SOURCE_DIRECTORY__ @@ "..") // Not sure, but we want to go up 1 from build
    let filePath = requireFile (root @@ "ServiceManifest.yml") "Build failed because there was no ServiceManifest.yaml."
    let configHelper = readYamlFile filePath
    let config = convertToServiceConfig configHelper 

    if (config.PackageSolutionPaths.Count > 0) then
        // Step 1
        printfn "Step 1 - Started"
        for packageSolutionPath in config.PackageSolutionPaths do
            let result = buildSolution packageSolutionPath
            printfn "%s" result
        printfn "Step 1 - Completed"

        // Step 2
        printfn "Step 2 - Started"
        for steps in config.Workflow do
            let mutable projectPath = ""
            let mutable packageName = ""
            let mutable packageVersion = ""

            for step in steps.Value do
                match step.Key with
                | "PackageName" -> packageName <- step.Value
                | "ProjectPath" -> projectPath <- step.Value
                | "PackageVersion" -> packageVersion <- step.Value
                | _ -> ()

            printfn "Removing package: %s" projectPath
            let removeResult = removeNuGetPackage packageName projectPath
            printfn "%s" removeResult

            printfn "Adding package: %s" packageName
            let addResult = addNuGetPackage packageName packageVersion projectPath
            printfn "%s" addResult
        printfn "Step 2 - Completed"
        
    // Step 3
    printfn "Step 3 - Started"
    let result = buildSolution config.SolutionPath
    printfn "%s" result
    printfn "Step 3 - Completed"

    // Step 4
    printfn "Step 4 - Started"
    stopService config.ServiceName
    Thread.Sleep(20000)
    copyDllFiles config.SourceDirectory config.TargetDirectory
    startService config.ServiceName
    printfn "Step 4 - Completed"
    0 // Return 0 to indicate successful execution

