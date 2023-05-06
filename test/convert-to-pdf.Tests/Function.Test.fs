namespace ConvertToPdf


open System
open System.IO
open ConvertToPdf.Workflow.Types
open Xunit
open FsToolkit.ErrorHandling
open Amazon.Runtime.CredentialManagement
open Amazon.Lambda.TestUtilities
open Amazon
open Amazon.S3
open Amazon.S3.Model


open ConverToPdf.Workflow
open ConverToPdf.ShellCommand

module FunctionTest =
    
           
    [<Fact>]
    let ``Test functions`` () = taskResult {
        let chain = CredentialProfileStoreChain()        
        let (result, awsCredentials) = chain.TryGetAWSCredentials("")
        Assert.True(result)
        use s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.APNortheast1)
        let lambdaContext = TestLambdaContext()
        let logFunc = lambdaContext.Logger.LogInformation
        let! extractedFileInfo = validateSupportedFileName "Financial Sample.xlsx"        
        let aFunction = Function(s3Client)
        let logFunc = lambdaContext.Logger.LogInformation
        let! srcContent = aFunction.ReadFileFromS3 s3Client logFunc extractedFileInfo
        let srcFilePathName = srcContent.RetrievedFileInfo |> ExistingFileInfo.getFilePathName
        
        let convertedFileName = genConvertedFileName (extractedFileInfo |> SupportedFileInfo.value)
        // let! dstFileContent = aFunction.ToPdfNothing logFunc srcFilePathName 
        // let! conversionResult = aFunction.WritePdfFileToS3 s3Client logFunc dstFileContent.DstFileInfo        
        // return conversionResult
        return ""        
    }
    
    [<Fact>]
    let ``Test getting object from S3`` () = task {
        let chain = CredentialProfileStoreChain()        
        let (result, awsCredentials) = chain.TryGetAWSCredentials("")
        Assert.True(result)        
        use s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.APNortheast1)               
        let req = GetObjectRequest(
                BucketName = "iqc-convert-to-pdf",
                Key = "in/Financial Sample.xlsx"                               
            )        
        use! aObjRes = s3Client.GetObjectAsync(req) |> Async.AwaitTask
        use reader = new StreamReader(aObjRes.ResponseStream)
        let contents = reader.ReadToEnd();
        Console.WriteLine("Object - " + aObjRes.Key);
        Console.WriteLine(" Version Id - " + aObjRes.VersionId);
        Console.WriteLine(" Contents - " + contents.Length.ToString());        
    }
    
    
    // [<Fact>]
    // let ``Test getting content type for an event``() = task {
    //     use s3Client = new AmazonS3Client(RegionEndpoint.USWest2)
    //     let bucketName = sprintf "lambda-blueprint-basename-%i" DateTime.Now.Ticks
    //     let key = "text.txt"
    //
    //     let! putBucketResponse =
    //         s3Client.PutBucketAsync(bucketName)
    //         |> Async.AwaitTask
    //
    //     try
    //         let! putObjectResponse =
    //             PutObjectRequest(
    //                 BucketName = bucketName,
    //                 Key = key,
    //                 ContentBody = "sample data"
    //             )
    //             |> s3Client.PutObjectAsync
    //             |> Async.AwaitTask
    //
    //         let eventRecords = [
    //             S3Event.S3EventNotificationRecord(
    //                 S3 = S3Event.S3Entity(
    //                     Bucket = S3Event.S3BucketEntity (Name = bucketName),
    //                     Object = S3Event.S3ObjectEntity (Key = key)
    //                 )
    //             )
    //         ]
    //
    //         let s3Event = S3Event(Records = List(eventRecords))
    //         let lambdaContext = TestLambdaContext()
    //         let lambdaFunction = Function(s3Client)
    //         let! contentType = lambdaFunction.FunctionHandler s3Event lambdaContext |> Async.AwaitTask
    //
    //         Assert.Equal("text/plain", contentType)
    //
    //     finally
    //          AmazonS3Util.DeleteS3BucketWithObjectsAsync(s3Client, bucketName)
    //             |> Async.AwaitTask |> Async.RunSynchronously
    // }
        
    [<Fact>]
    let ``Try execute shell command`` () = async {
        let! r = executeBashShellCommand "ls -alh" 
        if r.ExitCode = 0 then
            printfn "%s" r.StandardOutput
        else
            eprintfn "%s" r.StandardError
            // Environment.Exit(r.ExitCode)        
    }
        
    
    
    // [<EntryPoint>]
    // let main _ = 0