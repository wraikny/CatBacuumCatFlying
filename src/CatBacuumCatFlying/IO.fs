module Cbcf.IO

open FSharp.Data

type TheCatApi = JsonProvider<"""[{ "url":"hoge" }]""">
type TheCatApiCategory = JsonProvider<"""[{ "id":0, "name":"aaa" }]""">

let private getTheCatApiCategoriesAsync apiKey =
  let url = "https://api.thecatapi.com/v1/categories"
  Http.AsyncRequestString
    ( url, httpMethod = "GET",
      query = [ "api_key", apiKey; "format", "json" ],
      headers = [ "Accept", "application/json" ]
    )

let private getTheCatApiAsync (categoryId: int) (limit: int) apiKey =
  let url = "https://api.thecatapi.com/v1/images/search"
  
  Http.AsyncRequestString
    (
      url, httpMethod = "GET",
      query = [
        "api_key", apiKey
        "format", "json"
        "limit", limit.ToString()
        "category_ids", categoryId.ToString()
      ],
      headers = [ "Accept", "application/json" ]
    )

open System.Net
open System.IO
open System.Drawing
open System.Text

let private downloadImage (url: string) = async {
  use client = new WebClient()
  return! client.OpenReadTaskAsync(url) |> Async.AwaitTask
}

let private urlToFilename url =
  sprintf "%s.png" (Path.GetFileNameWithoutExtension url)

let private saveImageStreamAsPng (filename: string) (stream: Stream) =
  use bitmap = new Bitmap(stream)
  if bitmap <> null then
    bitmap.Save(filename, Imaging.ImageFormat.Png)

let loadCategoryAsync apiKey = async {
  let! json = getTheCatApiCategoriesAsync apiKey
  return
    [| for x in TheCatApiCategory.Parse json -> (x.Id, x.Name) |]
}

open System.Collections.Generic

let downloadImages apiKey dir (category, categoryName) limit = async {
  let! json = getTheCatApiAsync category limit apiKey
  printfn "Json:\n%s" json
  
  let dirname = sprintf "%s/%s" dir categoryName

  if System.IO.Directory.Exists dirname |> not then
    System.IO.Directory.CreateDirectory(dirname) |> ignore

  let filenames = List<string>()

  for x in TheCatApi.Parse json do
    let filename =
      urlToFilename x.Url
      |> sprintf "%s/%s" dirname

    if System.IO.File.Exists filename |> not then
      use! stream = downloadImage x.Url
    
      saveImageStreamAsPng filename stream
      printfn "Saved %s" filename
      filenames.Add(filename)
    else
      printfn "%s has alreadly existed" filename
  return (category, [| for x in filenames -> x |])
}


module Altseed =
  let loadString(path) =
    asd.Engine.File.CreateStaticFile(path).Buffer
    |> Encoding.UTF8.GetString