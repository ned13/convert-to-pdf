module ConvertToPdf.Workflow.Types

open System.IO

type SupportedFileInfo =
    private SupportedFileInfo of FileInfo
    
type ExistingFileInfo =
    private ExistingFileInfo of FileInfo
        
type RetrievedFileContent = {
    ContentType: string
    ContentLength: int64    
    RetrievedFileInfo: ExistingFileInfo
}

type ConvertedFileContent = {
    ContentType: string
    ContentLength: int64    
    ConvertedFileInfo: ExistingFileInfo
}

type StoredFileContent = {
    ContentType: string
    ContentLength: int64    
    StoredFileInfo: ExistingFileInfo
    StoredPath: string
}
    
type ConversionResult = {
    SucceededMessage: string
    SrcFileInfo: ExistingFileInfo
    DstFileInfo: ExistingFileInfo
}
    
type WorkflowResult = {
    SrcFileContentType: string
    SrcFileContentLength: int64
    SrcFileName: string
    DstFileContentType: string
    DstFileContentLength: int64
    DstFileName: string
    RetrieveSrcFileElapsed: string
    ConvertSubWorkflowElapsed: string
    WriteToStorageElapsed: string
    TotalElapsed: string
}

type LogFunc = string -> unit
type RetrieveSrcFileFunc = SupportedFileInfo -> Async<Result<RetrievedFileContent, string>>
type ToPdfFunc = string -> Async<Result<ConversionResult, string>>
type WritePdfFileToStorage = ConvertedFileContent -> Async<Result<StoredFileContent, string>>
type Workflow = string -> Async<Result<WorkflowResult, string>>


module SupportedFileInfo =
    let supportedExtensions = [ ".docx"; ".xlsx"; ".pptx"; ".csv" ]
    let value (SupportedFileInfo fi) = fi
    let create filePathName  =
        try 
            let fi = FileInfo(filePathName)
            if supportedExtensions |> List.exists (fun ext -> ext = fi.Extension) then
                Ok (SupportedFileInfo fi)
            else
                Error $"{filePathName} is not in supported format {supportedExtensions}"            
        with
        |  ex -> Error $"{filePathName} is not valid file path name. reason: {ex.Message}"
        

            
    let getFileName (SupportedFileInfo fi) = fi.Name            

module ExistingFileInfo =
    let value (ExistingFileInfo fi) = fi
        
    let createFromFileInfo (fileInfo: FileInfo) =
        match fileInfo.Exists with
        | true -> Ok (ExistingFileInfo fileInfo)
        | false -> Error $"File {fileInfo.FullName} doesn't exist. {nameof(ExistingFileInfo)} can't be created."        

    let create filePathName =
        try
            let fileInfo = FileInfo(filePathName)
            createFromFileInfo fileInfo
        with
        | ex -> Error $"{filePathName} is not valid file path name. reason: {ex.Message}"
            
    let getFilePathName (ExistingFileInfo fi) = fi.FullName
                
    let getFileName (ExistingFileInfo fi) = fi.Name
                
    let getFileLength (ExistingFileInfo fi) = fi.Length
    
    let getFileDir (ExistingFileInfo fi) = fi.Directory
    
    let getFileExt (ExistingFileInfo fi) = fi.Extension
        
        
            
          
        