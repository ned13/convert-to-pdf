module ConvertToPdf.Utility

open System.Diagnostics
open FsToolkit.ErrorHandling

let internal withExecutionTime (aFun: _ -> Async<Result<'b, 'c>>) = asyncResult {
    let stopWatch = Stopwatch()
    stopWatch.Start()
    let! r = aFun ()
    stopWatch.Stop()
    return r, stopWatch.Elapsed
}

let internal withExecutionTime1Parameter (aFun: 'a -> Async<Result<'b, 'c>>) para1 =
    let noParaFun = fun () -> aFun para1
    withExecutionTime noParaFun

let internal withExecutionTime2Parameter (aFun: 'a -> 'b -> Async<Result<'c, 'd>>) para1 para2 =
    let noParaFun = fun () -> aFun para1 para2
    withExecutionTime noParaFun

let internal withExecutionTime3Parameter (aFun: 'a -> 'b -> 'c -> Async<Result<'d, 'e>>) para1 para2 para3 =
    let noParaFun = fun () -> aFun para1 para2 para3
    withExecutionTime noParaFun

let internal aFunReturnAsync f p1 p2 = async {
    return f p1 p2
}