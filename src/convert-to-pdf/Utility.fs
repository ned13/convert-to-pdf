module ConvertToPdf.Utility

open System.Diagnostics
open FsToolkit.ErrorHandling

let internal withExecutionTime1Parameter (aFun: 'a -> Async<Result<'b, 'c>>) para1 = asyncResult {
    let stopWatch = Stopwatch()
    stopWatch.Start()
    let! r = aFun para1
    stopWatch.Stop()
    return r, stopWatch.Elapsed
}

let internal withExecutionTime2Parameter (aFun: 'a -> 'b -> Async<Result<'c, 'd>>) para1 para2 = asyncResult {
    let stopWatch = Stopwatch()
    stopWatch.Start()
    let! r = aFun para1 para2
    stopWatch.Stop()
    return r, stopWatch.Elapsed
}

let internal withExecutionTime3Parameter (aFun: 'a -> 'b -> 'c -> Async<Result<'d, 'e>>) para1 para2 para3 = asyncResult {
    let stopWatch = Stopwatch()
    stopWatch.Start()
    let! r = aFun para1 para2 para3
    stopWatch.Stop()
    return r, stopWatch.Elapsed
}

let internal aFunReturnAsync f p1 p2 = async {
    return f p1 p2
}