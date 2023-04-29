namespace ConvertToPdf

open System
open System.Collections.Generic
open Workflow.Types
open System.IO
open ConverToPdf
open Xunit
open Workflow
open FsUnit.Xunit
open FsUnit.CustomMatchers
open FsToolkit.ErrorHandling
open FsCheck
open FsCheck.Xunit


module WorkflowTest =
    
    [<Property>]
    let ``Test validateSupportedFileName with non-empty file name`` (nonEmptyFileName: NonEmptyString) =
        let fileName = nonEmptyFileName.Get
        let result = validateSupportedFileName fileName

        match result with
        | Ok supportedFi ->
            let supportedFiFileName = supportedFi |> SupportedFileInfo.getFileName 
            supportedFiFileName |> should equal fileName
        | Error errMsg -> failwith errMsg

    [<Fact>]
    let ``Test validateSupportedFileName with empty file name`` () =
        let result = validateSupportedFileName ""

        match result with
        | Ok _ -> failwith "Expected an error for an empty file name"
        | Error errMsg -> errMsg |> should equal "No file name"    
    
    
    [<Fact>]
    let ``convertToPdf success`` () = taskResult {
        let targetFileName = "abc.pdf"
        // let! conversionResult = convertToPdfThenRename "ls -la" srcFileContent targetFileName
        // conversionResult.FilePathName |> should equal $"/tmp/{targetFileName}"
        return ()
    }
    
    [<Fact>]
    let ``convertToPdf failure`` () = task {
        // let! result = convertToPdfThenRename exitCode1Command sourceFileContent "dstFileName.pdf"
        // result |> should be (ofCase <@ Result<ProcessingContent, string>.Error @>)
        return ()
    }
    

