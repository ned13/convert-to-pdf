namespace ConvertToPdf


open System
open System.IO
open System.Threading
open Amazon.Lambda.Core
open Amazon.Lambda.S3Events

open Amazon.S3
open Amazon.S3.Model
open Amazon.S3.Util
open FsToolkit.ErrorHandling
open ConverToPdf.Workflow
open ConverToPdf.ShellCommand
open FsToolkit.ErrorHandling.Operator.AsyncResult
open ConvertToPdf.Workflow.Types



// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[<assembly: LambdaSerializer(typeof<Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer>)>]
()
type Function(s3Client: IAmazonS3) =
// type Function() =
    
    let retrieveSrcFileFromTemp supportedFileInfo  = result {
        let fileInfo = supportedFileInfo |> SupportedFileInfo.value
        let tmpFilePathName = $"/tmp/{fileInfo.Name}"        
        let! retrievedFileInfo = tmpFilePathName
                                    |> ExistingFileInfo.create 
                                    |> mapError $"{tmpFilePathName} doesn't exist"
                                    
        let retrievedFileName = retrievedFileInfo|> ExistingFileInfo.getFileName
                                
        let! contentType = determineContentType retrievedFileName 
        return {
            ContentType = contentType
            ContentLength = retrievedFileInfo |> ExistingFileInfo.getFileLength  
            RetrievedFileInfo = retrievedFileInfo 
        }
    }
    
    
    // TODO: Need handle exception... otherwise the error type can't be inferred
    let retrieveSrcFileFromS3 (s3Client: IAmazonS3) (logFunc: LogFunc) supportedFileInfo = taskResult {
            let fileName = supportedFileInfo |> SupportedFileInfo.getFileName
            let req = GetObjectRequest(
                BucketName = "iqc-convert-to-pdf",
                Key = $"in/{fileName}"                               
            )
            let s3Path = $"{req.BucketName}/{req.Key}"
            logFunc $"Retrieving {s3Path} from S3..."
            use! response = s3Client.GetObjectAsync(req, CancellationToken.None)    
            logFunc $"Content Type {response.Headers.ContentType}"
            
            let tmpFilePathName = $"/tmp/{fileName}"
            logFunc $"Writing temp file into {tmpFilePathName}..."
            let! _ = response.WriteResponseStreamToFileAsync(tmpFilePathName, false, CancellationToken.None)
            let! tempFileInfo =
                tmpFilePathName
                |> ExistingFileInfo.create
                |> mapError $"Download {s3Path} failed."
                
            logFunc $"Wrote {tempFileInfo}"            
            return {
                ContentType = response.Headers.ContentType
                ContentLength = tempFileInfo |> ExistingFileInfo.getFileLength
                RetrievedFileInfo = tempFileInfo
            }      
        }     
       
    let toPdfNothing logFunc srcFilePathName = result {
        logFunc $"Source file path name={srcFilePathName}"
        let! srcFileInfo = ExistingFileInfo.create srcFilePathName
                           |> mapError $"Source file={srcFilePathName} doesn't exist."
                           
        let srcFileDir = srcFileInfo |> ExistingFileInfo.getFileDir
        let srcFileName = srcFileInfo |> ExistingFileInfo.getFileName
        let srcFileExt = srcFileInfo |> ExistingFileInfo.getFileExt
        let dstFilePathName = $"{srcFileDir}/{getNoExtFileName srcFileName}-{DateTime.Now.Ticks}{srcFileExt}"
        logFunc $"No conversion, copying {srcFilePathName} to {dstFilePathName}"
        File.Copy(srcFilePathName, dstFilePathName)                
        let! dstFileInfo = ExistingFileInfo.create dstFilePathName
                            |> Result.mapError (fun error -> $"Copy file to {dstFilePathName} failed.")
                            
        logFunc $"Dst file path name={dstFilePathName}"
        return {
            SucceededMessage = "Convert nothing."
            SrcFileInfo = srcFileInfo
            DstFileInfo = dstFileInfo
        }
    }
        
    let toPdfByLibreOffice logFunc srcFilePathName = taskResult {
        let dstDirPathName = "/tmp"
        let! srcFileInfo = srcFilePathName
                           |> ExistingFileInfo.create
                           |> mapError $"No source file, can't convert to pdf."

        let srcFilePathName = srcFileInfo |> ExistingFileInfo.getFilePathName                           
        logFunc $"Source File: {srcFilePathName}"
        
        // The command is from https://madhavpalshikar.medium.com/converting-office-docs-to-pdf-with-aws-lambda-372c5ac918f
        // Don't know why exporting HOME=tmp is necessary, but it doesn't work without it.            
        let convertCommand = $"export HOME=/tmp && /opt/libreoffice7.4/program/soffice.bin --headless --norestore --invisible --nodefault --nofirststartwizard --nolockcheck --nologo --convert-to \"pdf:writer_pdf_Export\" --outdir {dstDirPathName} \"{srcFilePathName}\""
        logFunc $"Prepare to run 1st command={convertCommand}"                        
        let! firstTimeResult = executeBashShellCommand convertCommand
        logFunc $"Run 1st time result={firstTimeResult.ToString()}"

        // Try delay a period of time.        
        // let millisecondsDelay = 3000
        // logFunc $"Delay {millisecondsDelay} for next execution."
        // let! _ = System.Threading.Tasks.Task.Delay(3000)
        
        let srcFileName = srcFileInfo |> ExistingFileInfo.getFileName
        let expectedDstFilePathName = $"{dstDirPathName}/{getNoExtFileName srcFileName}.pdf"        
        match firstTimeResult.ExitCode with
        | 0 ->
            let! dstFileInfo = ExistingFileInfo.create expectedDstFilePathName
                                |> mapError $"1st time command succeeded, but file is not there."
            return {
                SucceededMessage = "Conversion succeed in 1st time."
                SrcFileInfo = srcFileInfo
                DstFileInfo = dstFileInfo
            }            
        | _ ->            
            // first time failed, need to execute again, guessing that the libreoffice server need to be started by first execution.
            logFunc $"Prepare to run 2nd command={convertCommand}"
            let! cmdResult = executeBashShellCommandWithResult convertCommand |> AsyncResult.mapError (fun err -> err)
            logFunc $"Run 2nd time result={cmdResult.ToString()}"
            let! dstFileInfo = ExistingFileInfo.create expectedDstFilePathName
                                |> mapError $"2nd time Conversion doesn't succeed."
            return {
                SucceededMessage = "Conversion succeed in 2nd time."
                SrcFileInfo = srcFileInfo
                DstFileInfo = dstFileInfo
            }                                
    }
    
    
    let writePdfFileToTmp (pdfFileContent: ConvertedFileContent) =
        {
            ContentType = pdfFileContent.ContentType
            ContentLength = pdfFileContent.ContentLength
            StoredFileInfo = pdfFileContent.ConvertedFileInfo
            StoredPath = pdfFileContent.ConvertedFileInfo |> ExistingFileInfo.getFilePathName
        }    
    
                
    // TODO: Need handle exception... otherwise the error type can't be inferred        
    let writePdfFileToS3 (s3Client: IAmazonS3) logFunc (convertedFileContent: ConvertedFileContent) = taskResult {
            let bucketName = "iqc-convert-to-pdf"
            let fileName = convertedFileContent.ConvertedFileInfo |> ExistingFileInfo.getFileName
            let key = $"out/{fileName}"
            
            let req = PutObjectRequest(
                BucketName = bucketName,
                Key = key,
                FilePath = (convertedFileContent.ConvertedFileInfo |> ExistingFileInfo.getFilePathName)
            )
            
            logFunc $"writing {req.FilePath} to S3 {req.BucketName}/{req.Key}..."
            let! response = s3Client.PutObjectAsync(req)
            logFunc $"Writing result={response.HttpStatusCode}"
            return {
                ContentType = convertedFileContent.ContentType
                ContentLength = convertedFileContent.ContentLength
                StoredFileInfo = convertedFileContent.ConvertedFileInfo
                StoredPath = $"{bucketName}/{key}"     
            }        
    }    
    
    
    new() = Function(new AmazonS3Client())
    
    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    // member __.FunctionHandler (event: S3Event) (context: ILambdaContext) = taskResult {
    member __.FunctionHandler (event: string) (context: ILambdaContext) = task {
        
        // Verify container can run, just ToUpper incoming string.
        // return event.ToUpper()
        
        // trying run command in Lambda for checking environment.
        // let tryCmd (cmd: string) = taskResult {
        //     context.Logger.LogInformation($"Executing command={cmd}")
        //     let! cmdResult = executeBashShellCommand cmd
        //     context.Logger.LogInformation($"{cmd} result: \n {cmdResult.ToString()}")
        // }
        // let! _ = tryCmd event
        // return event        
        
        
        let logInformation = context.Logger.LogInformation
        
        let retrieveSrcFileFromTemp' = retrieveSrcFileFromTemp >> Async.retn
        let retrieveSrcFileFromS3' = retrieveSrcFileFromS3 s3Client logInformation >> Async.AwaitTask
        
        let toPdfNothing' = toPdfNothing logInformation >> Async.retn 
        let toPdfByLibreOffice' = toPdfByLibreOffice logInformation >> Async.AwaitTask
        
        let writePdfFileToTmp' = writePdfFileToTmp >> AsyncResult.retn       
        let writePdfFileToS3' = writePdfFileToS3 s3Client logInformation >> Async.AwaitTask
        

                
        // "Financial Sample.xlsx"
        // let workflow = createWorkflow
        //                    logInformation
        //                    retrieveSrcFileFromS3'           
        //                    toPdfByLibreOffice'             
        //                    writePdfFileToS3'               
                           
        let workflow  = createWorkflow
                            logInformation
                            retrieveSrcFileFromTemp'
                            toPdfNothing'
                            writePdfFileToTmp'
         
        // Null string should not exist in F#, Correct it before invoking workflow. 
        let fileName = event |> Option.ofNull |> Option.defaultValue ""                                                
        let! workflowResult = workflow fileName                                                                          
        return match workflowResult with
                | Ok r -> r.ToString()
                | Error err -> err.ToString()
        
        // let s3Event = event.Records.Item(0).S3
        //
        // sprintf "Processing object %s from bucket %s" s3Event.Object.Key s3Event.Bucket.Name
        // |> context.Logger.LogInformation
        //
        // let! response =
        //     s3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key)
        //     |> Async.AwaitTask
        //
        // sprintf "Content Type %s" response.Headers.ContentType
        // |> context.Logger.LogInformation
        //
        // return response.Headers.ContentType
    }
    
    member __.ReadFileFromS3 = retrieveSrcFileFromS3
    member __.ToPdfNothing = toPdfNothing
    member __.WritePdfFileToS3 = writePdfFileToS3