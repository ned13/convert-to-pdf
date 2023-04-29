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
    SrcContentType: string
    SrcContentLength: int64
    DstContentType: string
    DstContentLength: int64
}

type LogFunc = string -> unit
type RetrieveSrcFileFunc = SupportedFileInfo -> Async<Result<RetrievedFileContent, string>>
type ToPdfFunc = string -> Async<Result<ConversionResult, string>>
type WritePdfFileToStorage = ConvertedFileContent -> Async<Result<StoredFileContent, string>>
type Workflow = string -> Async<Result<WorkflowResult, string>>


module SupportedFileInfo =
    let value (SupportedFileInfo fi) = fi
    let create supportedFileExts filePathName  =
        let fi = FileInfo(filePathName)        
        if supportedFileExts |> List.exists (fun ext -> ext = fi.Extension) then
            Ok (SupportedFileInfo fi)
        else
            Error $"{filePathName} is not in supported format {supportedFileExts}"
            
    let getFileName (SupportedFileInfo fi) = fi.Name            

module ExistingFileInfo =
    let value (ExistingFileInfo fi) = fi
        
    let createFromFileInfo (fileInfo: FileInfo) =
        match fileInfo.Exists with
        | true -> Ok (ExistingFileInfo fileInfo)
        | false -> Error $"File {fileInfo.FullName} doesn't exist. {nameof(ExistingFileInfo)} can't be created."        

    let create filePathName =
        let fileInfo = FileInfo(filePathName)
        createFromFileInfo fileInfo    
            
    let getFilePathName (ExistingFileInfo fi) = fi.FullName
                
    let getFileName (ExistingFileInfo fi) = fi.Name
                
    let getFileLength (ExistingFileInfo fi) = fi.Length
    
    let getFileDir (ExistingFileInfo fi) = fi.Directory
    
    let getFileExt (ExistingFileInfo fi) = fi.Extension
        
        
            
          
        