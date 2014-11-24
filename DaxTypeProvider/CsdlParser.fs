namespace LinqToDAX.TypeProvider

open System
open System.Data
open System.Data.Linq
open System.Xml
open System.Xml.Linq
open Microsoft.FSharp.Linq
open Microsoft.AnalysisServices

module StringExtensions = 
    type System.String
        with 
            member x.Capitalize = 
                    x.[0].ToString().ToUpper() + x.ToLower().Substring(1)

            member x.toIdentifier  =
                let c = x.TrimStart('[').TrimEnd(']').Replace(" ", "_").Replace("-", "_").Replace("__", "_");
                if c.Contains("_") then
                    c.Split('_')
                    |> Array.map (fun s -> s.Capitalize)
                    |> Array.fold (fun s y -> s + y) ""
                else 
                    c

module CsdlParser = 
    open StringExtensions
    type PropertyRecord =
        {
            Name : string
            refName : string
            TypeName : string
            IsHidden : bool
        }

    type EntityRecord = 
        {
            Ref : string
            Name : string
            Properties : PropertyRecord seq
        }
        static member Default = 
            {
                Ref  = "Invalid"
                Name = ""
                Properties = Seq.empty
            }

    

    let ns = @"{http://schemas.microsoft.com/ado/2008/09/edm}"
    let bi = @"{http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions}"
    
    let firstToOption xs = 
        xs
        |> List.ofSeq
        |> function
            | []   -> None
            | x::_ -> Some(x)  
     
    let firstOfOption xs = 
        xs
        |> List.ofSeq
        |> function
            | []   -> None
            | x::_ -> x
    
    let csdlSchema serverName databaseName = 
        let server = new Server()
        
        let soapCommandXml =
             @" <Envelope xmlns='http://schemas.xmlsoap.org/soap/envelope/'>
                        <Body>
                        <Discover xmlns='urn:schemas-microsoft-com:xml-analysis'>
                        <RequestType>DISCOVER_CSDL_METADATA</RequestType>
                        <Restrictions>
                        <RestrictionList>
                        <CATALOG_NAME>" + databaseName + @"</CATALOG_NAME>
                        </RestrictionList>
                        </Restrictions>
                        <Properties>
                        <PropertyList>
                        <FORMAT>Tabular</FORMAT>
                        </PropertyList>
                        </Properties>
                        </Discover>
                        </Body>
                        </Envelope>"
        do server.Connect(serverName)
        let xmlReader = server.SendXmlaRequest(XmlaRequestType.Discover, new System.IO.StringReader(soapCommandXml))
        XDocument.Load(xmlReader)

    let getProperty name typeName (element : XElement option)  =
        
        element
        |> Option.map 
            (fun e ->
                let hidden =
                    if e.Attribute(XName.Get("Stability")) <> null then
                        e.Attribute(XName.Get("Stability")).Value = "RowNumber"
                    else 
                        typeName = "Binary" || false         
                {
                    Name = name
                    refName = 
                        if e.Attribute(XName.Get("ReferenceName")) <> null then
                             e.Attribute(XName.Get("ReferenceName")).Value
                        else name
                    TypeName = typeName
                    IsHidden = hidden
                })

    let properties (element : XElement option) =
        match element with
        | Some e -> 
            let ps = 
                e.Descendants(XName.Get(ns + "Property"))
            ps
            |> Seq.map (fun p ->
                 p.Attribute(XName.Get("Name")),
                 p.Attribute(XName.Get("Type")),
                 p.Descendants(XName.Get(bi + "Property")) 
                 |> Seq.filter (fun x -> x <> null)
                 |> firstToOption
                 )
            |> Seq.choose 
                ( fun (n,t,e) -> 
                    if n <> null && t <> null then
                        getProperty n.Value t.Value e 
                    else None
                    )
        | None -> 
            Seq.empty


    let associations (csdl : XDocument) = 
        query {
             for a in csdl.Descendants(XName.Get(ns + "AssociationSet")) do
                let ends = a.Descendants(XName.Get(ns + "End")) |> Seq.toArray
                select 
                      ((ends.[0].Attribute(XName.Get("EntitySet")).Value)
                      , (ends.[1].Attribute(XName.Get("EntitySet")).Value))
            }
            |> Map.ofSeq

    let entities (csdl : XDocument) =
        
        seq {
                for e in csdl.Descendants(XName.Get(ns + "EntityType")) do
                    let name = e.Attribute(XName.Get("Name")).Value
                    let entitySet = 
                        csdl.Descendants(XName.Get(ns + "EntitySet"))
                        |> Seq.filter (fun x -> x <> null)
                        |> Seq.filter (fun x -> x.Attribute(XName.Get("Name")) <> null && x.Attribute(XName.Get("Name")).Value = name )
                        |> List.ofSeq
                    let refName = 
                        entitySet.Descendants(XName.Get(bi + "EntitySet")) 
                        |> Seq.map(fun x -> x.Attribute(XName.Get("ReferenceName")))
                        |> Seq.choose (fun x -> if x = null then None else Some(x))
                        |> firstToOption
                    yield 
                        {
                            Name = name
                            Ref = 
                                refName 
                                |> function 
                                    | Some(x) -> if x <> null then (x.Value) else name
                                    | None -> name
                            Properties = 
                                Some(e)
                                |> properties
                                
                        }
        }|> List.ofSeq   
     
