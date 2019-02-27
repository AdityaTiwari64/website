module Website.Main

open System
open System.IO
open WebSharper
open WebSharper.Sitelets
open WebSharper.UI
open WebSharper.UI.Notation

type EndPoint =
    | [<EndPoint "GET /">] Home
    | [<EndPoint "GET /docs"; Wildcard>] Docs of string
    | [<EndPoint "/blog">] BlogPage of slug: string

type MainTemplate = Templating.Template<"index.html">

module Site =
    open WebSharper.UI.Html
    open WebSharper.UI.Server
    open Website.Blogs
    open Website.Blogs.Jekyll

    let Menu = [
        "Home", "/"
        "Documentation", "/docs"
        "Blog", "/blog"
        "Try F#", "https://tryfsharp.fsbolero.io"
    ]

    let private head =
        __SOURCE_DIRECTORY__ + "/js/Client.head.html"
        |> File.ReadAllText
        |> Doc.Verbatim

    let Page (title: option<string>) (body: Doc) =
        MainTemplate()
#if !DEBUG
            .ReleaseMin(".min")
#endif
            .Head(head)
            .Title(
                match title with
                | None -> ""
                | Some t -> t + " | "
            )
            .TopMenu([for text, url in Menu -> MainTemplate.TopMenuItem().Text(text).Url(url).Doc()])
            .DrawerMenu([for text, url in Menu -> MainTemplate.DrawerMenuItem().Text(text).Url(url).Doc()])
            .Body(body)
            .Doc()
        |> Content.Page

    let HomePage () =
        MainTemplate.HomeBody()
            .Doc()
        |> Page None

    let PlainHtml html =
        div [Attr.Create "ws-preserve" ""] [Doc.Verbatim html]

    let DocPage (doc: Docs.Document) =
        MainTemplate.DocsBody()
            .Sidebar(PlainHtml Docs.Sidebar)
            .Content(PlainHtml doc.content)
            .Doc()
        |> Page doc.title

    [<Website>]
    let Main, BlogPages =
        let blogConfig =
            {
                PostsFolder = "_posts"
                LayoutsFolder = "_layouts"
            }
        let blogPages = Runtime.Paginator.BuildPostList blogConfig
        Application.MultiPage (fun ctx action ->
            let site =
                Path.Combine(__SOURCE_DIRECTORY__, "_config.yml")
                |> File.ReadAllText
                |> Yaml.OfYaml<Site>
            printfn "site=%A" site
            let paginator = Runtime.Paginator.Build(blogConfig, site)
            printfn "paginator=%A" paginator
            match action with
            | Home ->
                HomePage ()
            | BlogPage p ->
                Jekyll.BlogPage ctx blogConfig (site, paginator) (SlugType.BlogPost p)
            | Docs p ->
                DocPage Docs.Pages.[p]
        ), blogPages

[<Sealed>]
type Website() =
    interface IWebsite<EndPoint> with
        member this.Sitelet = Site.Main
        member this.Actions = [
            yield Home
            for p in Docs.Pages.Keys do
                yield Docs p
            for (path, filename, (y, m, d), slug, ext) in Site.BlogPages do
                yield BlogPage filename
        ]

[<assembly: Website(typeof<Website>)>]
do ()
