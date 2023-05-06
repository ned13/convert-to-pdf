module ConvertToPdf.Workflow.Types.Tests

open System
open System.Collections.Generic
open ConvertToPdf.Workflow.Types
open System.IO
open Xunit
open FsUnit.Xunit
open FsUnit.CustomMatchers
open FsCheck
open FsCheck.Xunit



let verifyEquivalentFileInfo (expected: FileInfo) (actual: FileInfo) =
    actual.FullName |> should equal expected.FullName
    actual.DirectoryName |> should equal expected.DirectoryName
    actual.Name |> should equal expected.Name
    actual.Extension |> should equal expected.Extension

module SupportedFileInfo =

    module Create =
        type SuccessfulSample =
            static member Data() : IEnumerable<obj[]> =
                seq {
                    yield [| "/a/abc.docx" |]
                    yield [| "/gg/aa/kkka870l.xlsx" |]
                    yield [| "/1a/2b/3c/agooz3.pptx" |]
                    yield [| "Iam Windows\Path\Pa/ago soz3.csv" |]
                }
                    
        [<Theory>]
        [<MemberData(nameof SuccessfulSample.Data, MemberType=typeof<SuccessfulSample>)>]
        let ``should return a SupportedFileInfo`` (sampleFilePathName: string) =        
            let result = SupportedFileInfo.create sampleFilePathName            
            match result with
            | Ok r ->            
                let expectedFi = FileInfo(sampleFilePathName)
                let actualFi = r |> SupportedFileInfo.value            
                actualFi |> verifyEquivalentFileInfo expectedFi 
            | Error e ->
                Assert.Fail("This should not happen")
                
        type NotSupportedExtensionSample =
            static member Data() : IEnumerable<obj[]> =
                seq {
                    yield [| "/a/abc.xab" |]
                    yield [| "/gg/aa/kkka870l.ggk" |]
                    yield [| "/1a/2b/3c/agooz3.ppt" |]
                    yield [| "Iam Windows\Path\Pa/ago soz3.csvaa" |]
                    yield [| " " |]
                    yield [| "   " |]                
                }            

        [<Theory>]
        [<MemberData(nameof NotSupportedExtensionSample.Data, MemberType=typeof<NotSupportedExtensionSample>)>]           
        let ``should return an error with not supported extension`` (sampleFilePathName: string) =
            let result = SupportedFileInfo.create sampleFilePathName
            match result with
            | Ok _ -> Assert.Fail("This should not happen")
            | Error e -> e |> should haveSubstring $"{sampleFilePathName} is not in supported format"


        type InvalidPathSample =
            static member Data() : IEnumerable<obj[]> =
                seq {
                    yield [| "" |]
                    yield [| null |]
                }            

        [<Theory>]
        [<MemberData(nameof InvalidPathSample.Data, MemberType=typeof<InvalidPathSample>)>]
        let ``should return an error with invalid path`` (sampleFilePathName: string) =
            let result = SupportedFileInfo.create sampleFilePathName
            match result with
            | Ok _ -> Assert.Fail("This should not happen")
            | Error e -> e |> should haveSubstring $"{sampleFilePathName} is not valid file path name."

module ExistingFileInfo =
    module CreateFromFileInfo =
        [<Fact>]
        let ``should return an ExistingFileInfo`` () =
            let tempFile = Path.GetTempFileName()
            let sampleFileInfo = FileInfo(tempFile)
            let result = ExistingFileInfo.createFromFileInfo sampleFileInfo
            match result with
            | Ok existFi ->
                let expectedFi = sampleFileInfo
                let actualFi = existFi |> ExistingFileInfo.value
                actualFi |> verifyEquivalentFileInfo expectedFi                    
            | Error e -> Assert.Fail("This should not happen")
            
        [<Fact>]
        let ``should return an error with non existing file`` () =
            let tempFile = Path.GetTempFileName()
            File.Delete(tempFile)
            let nonExistingFileInfo = FileInfo(tempFile)
            let result = ExistingFileInfo.createFromFileInfo nonExistingFileInfo
            match result with
            | Ok _ -> Assert.Fail("This should not happen")
            | Error e -> e |> should haveSubstring $"File {nonExistingFileInfo.FullName} doesn't exist."
            
    module Create =
        [<Fact>]
        let ``should return an ExistingFileInfo`` () =
            let tempFile = Path.GetTempFileName()
            let result = ExistingFileInfo.create tempFile
            match result with
            | Ok existFi ->
                let expectedFi = FileInfo(tempFile)
                let actualFi = existFi |> ExistingFileInfo.value
                actualFi |> verifyEquivalentFileInfo expectedFi                    
            | Error e -> Assert.Fail("This should not happen")

        
        type InvalidPathSample =
            static member Data() : IEnumerable<obj[]> =
                seq {
                    yield [| "" |]
                    yield [| null |]
                }                    
        [<Theory>]
        [<MemberData(nameof InvalidPathSample.Data, MemberType=typeof<InvalidPathSample>)>]
        let ``should return an error with invalid file path name`` (sampleFilePathName: string) =
            let result = ExistingFileInfo.create sampleFilePathName
            match result with
            | Ok _ -> Assert.Fail("This should not happen")
            | Error e -> e |> should haveSubstring $" is not valid file path name."
            