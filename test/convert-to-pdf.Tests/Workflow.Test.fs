module ConvertToPdf.WorkflowTest

open System
open System.Collections.Generic
open System.IO
open ConverToPdf
open Xunit
open Workflow
open FsUnit.Xunit
open FsUnit.CustomMatchers
open FsToolkit.ErrorHandling
open FsCheck
open FsCheck.Xunit
open ConvertToPdf.Workflow.Types
open FluentAssertions


module DetermineContentType =
    type SuccessfulSample =
        static member Data() : IEnumerable<obj[]> =
            seq {
                yield [| "abc.docx"; mediaTypeNameDocx |]
                yield [| "kkka870l.xlsx"; mediaTypeNameXlsx |]
                yield [| "agooz3.pptx"; mediaTypeNamePptx |]
                yield [| "ago soz3.csv"; mediaTypeNameCsv |]
                yield [| "iam-a-pdf-file.pdf"; mediaTypeNamePdf |]
            }

    [<Theory>]
    [<MemberData(nameof SuccessfulSample.Data, MemberType = typeof<SuccessfulSample>)>]
    let ``should return content type for supported file extension`` (fileName: string, expectedContentType: string) =
        let result = determineContentType fileName

        match result with
        | Ok contentType -> contentType |> should equal expectedContentType
        | Error err -> Assert.Fail("This should not happen")

    type NotSupportedSample =
        static member Data() : IEnumerable<obj[]> =
            seq {
                yield [| "invalid1.g"; ".g" |]
                yield [| "invalid2.gg"; ".gg" |]
                yield [| "invalid3.azi"; ".azi" |]
            }

    [<Theory>]
    [<MemberData(nameof NotSupportedSample.Data, MemberType = typeof<NotSupportedSample>)>]
    let ``should return an error for unsupported file extension`` (fileName: string, ext: string) =
        let result = determineContentType fileName

        match result with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err -> err |> should equal $"Unsupported file extension {ext}"


    type InvalidSample =
        static member Data() : IEnumerable<obj[]> =
            seq {
                yield [| "" |]
                yield [| null |]
            }

    [<Theory>]
    [<MemberData(nameof InvalidSample.Data, MemberType = typeof<InvalidSample>)>]
    let ``should return an error for invalid file name`` (fileName: string) =
        let result = determineContentType fileName

        match result with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err -> err |> should equal "No file name"

module GenConvertedFileName =
    type FileNameSample =
        static member Data() : IEnumerable<obj[]> =
            seq {
                yield [| "fileName1.abc"; "fileName1"; ".abc" |]
                yield [| "file-Name2.def"; "file-Name2"; ".def" |]
                yield [| "f i l eName3"; "f i l eName3"; "" |]
                yield [| "doubleDot.notExt.ext"; "doubleDot.notExt"; ".ext" |]
            }

    [<Theory>]
    [<MemberData(nameof FileNameSample.Data, MemberType = typeof<FileNameSample>)>]
    let ``should return converted file name`` (fileName: string, start: string, ext: string) =
        let fileName = FileInfo(fileName)

        let convertedFileName = genConvertedFileName fileName

        convertedFileName |> should startWith start
        convertedFileName |> should endWith ext

module RenameExistingFile =
    [<Fact>]
    let ``should return an ExistingFileInfo`` () = result {
        let srcFilePathName = Path.GetTempFileName()
        let! srcExiFi = ExistingFileInfo.create srcFilePathName

        let dstTempDirPathName = Path.GetTempPath ()
        let dstTempDir = DirectoryInfo(dstTempDirPathName)
        let dstFilePathName = $"{dstTempDir.FullName}test-dst.ggg"

        let! dstExiFi = renameExistingFile srcExiFi dstFilePathName
        dstExiFi
            |> ExistingFileInfo.getFilePathName
            |> should equal dstFilePathName
    }

    [<Fact>]
    let ``should return error if IOException`` () = result {
        let srcFilePathName = Path.GetTempFileName()
        let! srcExiFi = ExistingFileInfo.create srcFilePathName
        let result = renameExistingFile srcExiFi "not/existing/dir"
        match result with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err -> err |> should haveSubstring "File move operation failed with exception"
    }

let mockLogFunc = fun _ -> ()
let getTempFileName = Path.GetTempFileName

let changeExtension = fun ext path ->
    Path.ChangeExtension(path, ext)

let moveFile = fun src dst ->
    File.Move(src, dst, true)
    dst

let getSupportedTempFilePathName () =
    let tmpFilePathName = getTempFileName ()
    tmpFilePathName
    |> changeExtension ".docx"
    |> moveFile tmpFilePathName


module ConvertToPdf =
    let mockToPdfFunc = fun srcFileName -> asyncResult {
        let! fakeSrcFi = ExistingFileInfo.create srcFileName
        return {
            SucceededMessage = "Ok"
            SrcFileInfo = fakeSrcFi
            DstFileInfo = fakeSrcFi
        }
    }

    let mockToPdfErrorFunc (_: string) : Async<Result<ConversionResult, string>> =
        Result.Error "mock error" |> Async.retn

    let makeSrcContent () = result {
        let! srcExiFi = getTempFileName () |> ExistingFileInfo.create
        return {
            ContentType = "do-not-care"
            ContentLength = 100
            RetrievedFileInfo = srcExiFi
        }
    }

    [<Fact>]
    let ``should return a conversion result`` () =
        let runTestingFunc () = asyncResult {
            let! mockedSrcContent = makeSrcContent ()
            let! conversionResult = convertToPdf mockLogFunc mockToPdfFunc mockedSrcContent
            return (conversionResult, mockedSrcContent.RetrievedFileInfo)
        }
        let runTestResult = runTestingFunc () |> Async.RunSynchronously
        match runTestResult with
        | Ok (conversionResult, srcExiFi) ->
            conversionResult.SucceededMessage |> should equal "Ok"
            conversionResult.SrcFileInfo
                |> ExistingFileInfo.getFilePathName
                |> should equal (ExistingFileInfo.getFilePathName srcExiFi)
            conversionResult.DstFileInfo
                |> ExistingFileInfo.getFilePathName
                |> should equal (ExistingFileInfo.getFilePathName srcExiFi)

        | Error r ->
            Assert.Fail($"This should not happen, reason: {r}")

    [<Fact>]
    let ``should return an error`` () =
        let runTestingFunc () = asyncResult {
            let! mockedSrcContent = makeSrcContent ()
            let! conversionResult = convertToPdf mockLogFunc mockToPdfErrorFunc mockedSrcContent
            return (conversionResult, mockedSrcContent.RetrievedFileInfo)
        }
        let runTestResult = runTestingFunc () |> Async.RunSynchronously
        match runTestResult with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err -> err |> should equal "mock error"

module RenameConvertedToDstFile =
    let runTestingFun renameConvertedToDstFile =
        result {
            let! srcFileInfo = getSupportedTempFilePathName ()
                               |> ExistingFileInfo.create
            let! dstFileInfo = getSupportedTempFilePathName ()
                               |> ExistingFileInfo.create
            let conversionResult = {
                SucceededMessage = "Ok"
                SrcFileInfo = srcFileInfo
                DstFileInfo = dstFileInfo
            }
            let! cr = renameConvertedToDstFile mockLogFunc conversionResult
            return cr, dstFileInfo
        }

    [<Fact>]
    let ``should return converted file content`` () =

        let result = runTestingFun renameConvertedToDstFile

        match result with
        | Ok (cfc, dstFi) ->
            let expectedFilePathName = $"/tmp/{dstFi |> ExistingFileInfo.getFileName}"
            let filePathName = cfc.ConvertedFileInfo |> ExistingFileInfo.getFilePathName
            filePathName
                .Should()
                .MatchRegex(@"/tmp/tmp\w{6}-\d{18}\.docx", ?because=None)
            |> ignore
        | Error err -> Assert.Fail(err)

    [<Fact>]
    let ``should return an error of renaming to destination file.`` () =
        let renameExistingFileWithError = fun _ _ ->
            result {
                return! Result.Error "mock error"
            }
        let renameConvertedToDstFile = renameConvertedToDstFileImpl
                                           renameExistingFileWithError
                                           determineContentType

        let result = runTestingFun renameConvertedToDstFile

        match result with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err ->
            err.Should()
                .Match("Rename to * failed.*", ?because=None) |> ignore




    [<Fact>]
    let ``should return an error of not supported destination file`` () =
        let determineContentTypeWithError = fun _ ->
            result {
                return! Result.Error "mock error"
            }
        let renameConvertedToDstFile = renameConvertedToDstFileImpl
                                           renameExistingFile
                                           determineContentTypeWithError

        let result = runTestingFun renameConvertedToDstFile

        match result with
        | Ok _ -> Assert.Fail("This should not happen")
        | Error err ->
            err |> should haveSubstring "Destination file is not supported."

module PrettyFormatTimeSpan =
    [<Theory>]
    [<InlineData(0, "00:00:00.000")>]
    [<InlineData(1000, "00:00:01.000")>]
    [<InlineData(6030, "00:00:06.030")>]
    [<InlineData(360099, "00:06:00.099")>]
    let ``should return formatted time span`` (milliSeconds: int, expected: string) =
        let timeSpan = TimeSpan.FromMilliseconds(float milliSeconds)
        let actual = prettyFormatTimeSpan timeSpan
        actual |> should equal expected

module CreateWorkflow =
    let srcFileName = "abc.xlsx"
    let srcFileContentLength = 100L
    let dstFileName = "abc.pdf"
    let dstFileContentLength = 200

    let mockRetrieveSrcFileFunc = fun _ -> asyncResult {
        let! srcTmpExiFi = getTempFileName () |> ExistingFileInfo.create
        let srcTmpFilePathName = srcTmpExiFi |> ExistingFileInfo.getFilePathName
        let srcFileDir = srcTmpExiFi |> ExistingFileInfo.getFileDir
        let srcFilePathName = srcFileDir.FullName + "/" + srcFileName
        let srcFilePathName' = moveFile srcTmpFilePathName srcFilePathName
        let! srcFileExiFi = srcFilePathName' |> ExistingFileInfo.create
        return {
            ContentType = mediaTypeNameXlsx
            ContentLength = srcFileContentLength
            RetrievedFileInfo = srcFileExiFi
        }
    }

    let mockToPdfFunc = fun srcFileName -> asyncResult {
        let! fakeSrcFi = ExistingFileInfo.create srcFileName
        let srcFileName = fakeSrcFi |> ExistingFileInfo.getFileName
        let! dstTmpExiFi = getTempFileName () |> ExistingFileInfo.create
        let dstTmpFilePathName = dstTmpExiFi |> ExistingFileInfo.getFilePathName
        let dstFileDir = dstTmpExiFi |> ExistingFileInfo.getFileDir
        let dstFilePathName = dstFileDir.FullName + "/" + srcFileName
        let dstFilePathName' = moveFile dstTmpFilePathName dstFilePathName
        let dstFilePathName'' = changeExtension "pdf" dstFilePathName'
        let dstFilePathName''' = moveFile dstFilePathName' dstFilePathName''
        let! dstFileExiFi = dstFilePathName''' |> ExistingFileInfo.create
        return {
            SucceededMessage = "Ok"
            SrcFileInfo = fakeSrcFi
            DstFileInfo = dstFileExiFi
        }
    }



    let mockWriteToStorageFunc = fun (convertedFileContent: ConvertedFileContent) -> asyncResult {
        return {
            ContentType = convertedFileContent.ContentType
            ContentLength = convertedFileContent.ContentLength
            StoredFileInfo = convertedFileContent.ConvertedFileInfo
            StoredPath = "do-not-care"
        }
    }


    [<Fact>]
    let ``should return a successful result`` () =
        let workflow = createWorkflow
                           mockLogFunc
                           mockRetrieveSrcFileFunc
                           mockToPdfFunc
                           mockWriteToStorageFunc

        let runTestingFunc () = asyncResult {
            let! r = workflow srcFileName
            return r
        }

        let result = runTestingFunc () |> Async.RunSynchronously
        match result with
        | Ok wr ->
            wr.SrcFileContentType |> should equal mediaTypeNameXlsx
            wr.SrcFileContentLength |> should equal srcFileContentLength
            wr.SrcFileName |> should equal srcFileName
            wr.DstFileContentType |> should equal mediaTypeNamePdf
            wr.DstFileContentLength |> should equal dstFileContentLength
            wr.DstFileName |> should equal dstFileName
        | Error err -> Assert.Fail($"This should not happen, error={err}")
