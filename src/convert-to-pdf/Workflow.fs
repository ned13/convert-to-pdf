module ConverToPdf.Workflow

open System
open System.IO
open ConvertToPdf.Workflow.Types
open FsToolkit.ErrorHandling

let supportedExtensions = [ ".docx"; ".xlsx"; "pptx"; ".csv" ]
let createSupportedInfo = SupportedFileInfo.create supportedExtensions
let mediaTypeNameDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
let mediaTypeNameXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"

let mediaTypeNamePptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
let mediaTypeNameCsv = "text/csv"

let mediaTypeNamePdf = "application/pdf"

let validateSupportedFileName fileName = result {
    match String.IsNullOrEmpty(fileName) with
    | true -> return! Error "No file name"
    | false ->        
        let! supportedFi = createSupportedInfo fileName 
        return supportedFi
}
        
let determineContentType fileName =
    match fileName with
    | null -> Error "No file name"
    | fn ->
        let fi = FileInfo(fn)
        let ext = fi.Extension
        match ext.ToLower() with
        | ".docx" -> Ok mediaTypeNameDocx
        | ".xlsx" -> Ok mediaTypeNameXlsx
        | ".csv" -> Ok mediaTypeNameCsv
        | ".pptx" -> Ok mediaTypeNamePptx
        | ".pdf" -> Ok mediaTypeNamePdf
        | notSupported -> Error $"Not supported extension {notSupported}"
    
let getNoExtFileName: string -> string = Path.GetFileNameWithoutExtension

let genConvertedFileName (fileInfo: FileInfo) =
    let fileName = getNoExtFileName fileInfo.Name
    $"{fileName}-{DateTime.Now.Ticks}{fileInfo.Extension}"

let mapError errorMessage = 
    Result.mapError (fun err -> $"{errorMessage} reason: {err}")    

let convertToPdfThenRename (logFunc: LogFunc) (toPdfFunc: ToPdfFunc) (srcFileContent: RetrievedFileContent) = asyncResult {
    let srcFilePathName = srcFileContent.RetrievedFileInfo |> ExistingFileInfo.getFilePathName 
    let! toPdfResult = toPdfFunc srcFilePathName 
    let convertedFilePathName = toPdfResult.DstFileInfo |> ExistingFileInfo.getFilePathName
    logFunc $"Checking converted file {convertedFilePathName}..."
        
    // Rename to given file name
    let convertedFileInfo = toPdfResult.DstFileInfo
    let convertedFilePathName = convertedFileInfo |> ExistingFileInfo.getFilePathName
    let dstFileName = genConvertedFileName (convertedFileInfo |> ExistingFileInfo.value)
    let dstFilePathName = $"/tmp/{dstFileName}"
    logFunc $"Renaming converted file name from {convertedFilePathName} to {dstFilePathName}."
    File.Move(convertedFilePathName, dstFilePathName)         
    // let! _ = executeBashShellCommandWithResult $"mv {convertedFileInfo.FullName} {dstFilePathName}"
        
    // Check rename result
    let! dstFileInfo = ExistingFileInfo.create dstFilePathName
                        |> mapError $"Rename to {dstFilePathName} failed."
                                                
    let! contentType = dstFileInfo
                       |> ExistingFileInfo.getFileName
                       |> determineContentType
    return {
        ContentType = contentType
        ContentLength = dstFileInfo |> ExistingFileInfo.getFileLength
        ConvertedFileInfo = dstFileInfo
    }           
}

let createWorkflow
    (logFunc: LogFunc)
    (retrieveSrcFileFunc: RetrieveSrcFileFunc)
    (toPdfFunc: ToPdfFunc)
    (writeToStorageFunc: WritePdfFileToStorage) : Workflow =
    
    let convertSubWorkflowFunc = convertToPdfThenRename logFunc toPdfFunc
    let workflow = fun srcFileName -> asyncResult {
        
        let! supportedFileInfo = validateSupportedFileName srcFileName
        let! retrievedFileContent = retrieveSrcFileFunc supportedFileInfo                
        let! convertedFileContent = convertSubWorkflowFunc retrievedFileContent
        let! wroteFileContentInfo = writeToStorageFunc convertedFileContent
        
        return {
            SrcContentType = retrievedFileContent.ContentType
            SrcContentLength = retrievedFileContent.ContentLength
            DstContentType = wroteFileContentInfo.ContentType
            DstContentLength = wroteFileContentInfo.ContentLength
        }                 
    }
    
    workflow

