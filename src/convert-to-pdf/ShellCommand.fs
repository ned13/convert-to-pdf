module ConverToPdf.ShellCommand

open System.Diagnostics
open System.Threading.Tasks

// From https://alexn.org/blog/2020/12/06/execute-shell-command-in-fsharp/

type CommandResult =
    { ExitCode: int
      StandardOutput: string
      StandardError: string }

let executeCommand executable (args: string list) = async {
    let startInfo = ProcessStartInfo()
    startInfo.FileName <- executable

    for a in args do
        startInfo.ArgumentList.Add(a)

    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true
    startInfo.UseShellExecute <- false
    startInfo.CreateNoWindow <- true
    use p = new Process()
    p.StartInfo <- startInfo
    p.Start() |> ignore

    let outTask =
        Task.WhenAll([| p.StandardOutput.ReadToEndAsync(); p.StandardError.ReadToEndAsync() |])

    do! p.WaitForExitAsync() |> Async.AwaitTask
    let! out = outTask |> Async.AwaitTask

    return {
        ExitCode = p.ExitCode
        StandardOutput = out.[0]
        StandardError = out.[1]
    }
}

let executeBashShellCommand command =
    // executeCommand "/usr/bin/env" [ "-S"; "bash"; "-c"; command ]
    executeCommand "/usr/bin/env" [ "bash"; "-c"; command ]

let executeBashShellCommandWithResult command = async {
    let! cmdResult = executeBashShellCommand command

    match cmdResult.ExitCode with
    | 0 -> return Ok cmdResult.StandardOutput
    | _ -> return Error cmdResult.StandardError
}

let executeShellCommand command =
    // AWS Lambda doesn't support -S option
    executeCommand command []
