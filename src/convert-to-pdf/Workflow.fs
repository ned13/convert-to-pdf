module ConverToPdf.Workflow

open System
open System.Diagnostics
open System.IO
open ConvertToPdf.Workflow.Types
open FsToolkit.ErrorHandling



let mediaTypeNameDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
let mediaTypeNameXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

let mediaTypeNamePptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
let mediaTypeNameCsv = "text/csv"

let mediaTypeNamePdf = "application/pdf"

let parseSupportedFileInfo = SupportedFileInfo.create


let internal determineContentType fileName =
    match fileName with
    | null | "" -> Error "No file name"
    | fn ->
        let fi = FileInfo(fn)
        let ext = fi.Extension
        match ext.ToLower() with
        | ".docx" -> Ok mediaTypeNameDocx
        | ".xlsx" -> Ok mediaTypeNameXlsx
        | ".pptx" -> Ok mediaTypeNamePptx
        | ".csv" -> Ok mediaTypeNameCsv
        | ".pdf" -> Ok mediaTypeNamePdf
        | unSupported -> Error $"Unsupported file extension {unSupported}"

let internal getNoExtFileName: string -> string = Path.GetFileNameWithoutExtension

let internal genConvertedFileName (fileInfo: FileInfo) =
    let fileName = getNoExtFileName fileInfo.Name
    $"{fileName}-{DateTime.Now.Ticks}{fileInfo.Extension}"

let mapError errorMessage =
    Result.mapError (fun err -> $"{errorMessage} reason: {err}")






let internal renameExistingFile srcExistingFileInfo dstFilePathName =
    let srcFilePathName = srcExistingFileInfo |> ExistingFileInfo.getFilePathName
    let moveFunc dstFilePathName srcFilePathName =
        // Use command line to rename file, but use F# File.Move instead
        // let! _ = executeBashShellCommandWithResult $"mv {dstFilePathName} {dstFilePathName'}"
        File.Move(srcFilePathName, dstFilePathName)
        dstFilePathName

    try
        srcFilePathName
            |> moveFunc dstFilePathName
            |> ExistingFileInfo.create
    with
    | ex -> Result.Error $"File move operation failed with exception: {ex.Message}"

let internal convertToPdf (logFunc: LogFunc) (toPdfFunc: ToPdfFunc) (srcFileContent: RetrievedFileContent) =
    asyncResult {
        let srcFilePathName = srcFileContent.RetrievedFileInfo |> ExistingFileInfo.getFilePathName
        let! toPdfResult = toPdfFunc srcFilePathName
        let convertedFilePathName = toPdfResult.DstFileInfo |> ExistingFileInfo.getFilePathName
        logFunc $"Checking converted file {convertedFilePathName}..."
        return toPdfResult
    }

let internal renameConvertedToDstFileImpl renameExistingFile determineContentType
    (logFunc: LogFunc) (toPdfResult: ConversionResult) =
     result {
        let convertedFileInfo = toPdfResult.DstFileInfo
        let convertedFilePathName = convertedFileInfo |> ExistingFileInfo.getFilePathName
        let dstFileName = convertedFileInfo
                          |> ExistingFileInfo.value
                          |> genConvertedFileName
        let dstFilePathName = $"/tmp/{dstFileName}"
        logFunc $"Renaming converted file name from {convertedFilePathName} to {dstFilePathName}."

        let! dstFileInfo = dstFilePathName
                            |> renameExistingFile convertedFileInfo
                            |> mapError $"Rename to {dstFilePathName} failed."

        let! contentType = dstFileInfo
                           |> ExistingFileInfo.getFileName
                           |> determineContentType
                           |> mapError $"Destination file is not supported."
        return {
            ContentType = contentType
            ContentLength = dstFileInfo |> ExistingFileInfo.getFileLength
            ConvertedFileInfo = dstFileInfo
        }
    }

let internal renameConvertedToDstFile (logFunc: LogFunc) (toPdfResult: ConversionResult) =
     renameConvertedToDstFileImpl renameExistingFile determineContentType logFunc toPdfResult

let internal prettyFormatTimeSpan (timeSpan: TimeSpan) =
    $"%02d{timeSpan.Hours}:%02d{timeSpan.Minutes}:%02d{timeSpan.Seconds}.%03d{timeSpan.Milliseconds}"

let private withExecutionTime (aFunc: unit -> Async<Result<'a, 'b>>) =
    fun () -> asyncResult {
        let stopWatch = Stopwatch()
        stopWatch.Start()
        let! r = aFunc ()
        stopWatch.Stop()
        return r, stopWatch.Elapsed
    }

let createWorkflow
    (logFunc: LogFunc)
    (retrieveSrcFileFunc: RetrieveSrcFileFunc)
    (toPdfFunc: ToPdfFunc)
    (writeToStorageFunc: WritePdfFileToStorage) : Workflow =

    let workflow = fun srcFileName -> asyncResult {

        let! supportedFileInfo = parseSupportedFileInfo srcFileName

        // Use function decorator technique
        let retrieveSrcFileFunc' = (fun () -> retrieveSrcFileFunc supportedFileInfo) |> withExecutionTime
        let! retrievedFileContent, retrieveSrcFileElapsed =  retrieveSrcFileFunc' ()

        let convertToPdf' = (fun () -> convertToPdf logFunc toPdfFunc retrievedFileContent) |> withExecutionTime
        let! toPdfResult, convertToPdfElapsed = convertToPdf' ()

        let renameConvertedToDstFile' = (fun () -> renameConvertedToDstFile logFunc toPdfResult |> Async.retn)
                                        |> withExecutionTime
        let! convertedFileContent, renameConvertedToDstFileElapsed = renameConvertedToDstFile' ()

        let writeToStorageFunc' = (fun () -> writeToStorageFunc convertedFileContent) |> withExecutionTime
        let! wroteFileContentInfo, writeToStorageElapsed = writeToStorageFunc' ()

        let totalElapsed = retrieveSrcFileElapsed
                           + convertToPdfElapsed
                           + renameConvertedToDstFileElapsed
                           + writeToStorageElapsed
        return {
            SrcFileContentType = retrievedFileContent.ContentType
            SrcFileContentLength = retrievedFileContent.ContentLength
            SrcFileName = retrievedFileContent.RetrievedFileInfo |> ExistingFileInfo.getFileName
            DstFileContentType = wroteFileContentInfo.ContentType
            DstFileContentLength = wroteFileContentInfo.ContentLength
            DstFileName = wroteFileContentInfo.StoredFileInfo |> ExistingFileInfo.getFileName
            RetrieveSrcFileElapsed = retrieveSrcFileElapsed |> prettyFormatTimeSpan
            ConvertSubWorkflowElapsed = convertToPdfElapsed + renameConvertedToDstFileElapsed
                                        |> prettyFormatTimeSpan
            WriteToStorageElapsed = writeToStorageElapsed |> prettyFormatTimeSpan
            TotalElapsed = totalElapsed |> prettyFormatTimeSpan
        }
    }

    workflow

