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

module XmlExtensions =
    type System.Xml.Linq.XElement
        with
            member x.GetAttribute (attributeName : string) =
                if x.Attribute(XName.Get(attributeName)) <> null then
                        x.Attribute(XName.Get(attributeName)).Value |> Some
                else None
            member x.GetAttributeWithDefault (attributeName : string) (defaultValue : string) =
                 if x.Attribute(XName.Get(attributeName)) <> null then
                        x.Attribute(XName.Get(attributeName)).Value 
                 else defaultValue
module CsdlParser = 
    open StringExtensions
    open XmlExtensions
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
   

    let ns = @"{http://schemas.microsoft.com/ado/2008/09/edm}"
    let bi = @"{http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions}"
    
    let firstToOption xs = 
        xs
        |> List.ofSeq
        |> function
            | []   -> None
            | x::_ -> Some(x)  
    
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
                    match e.GetAttribute "Stability" with
                    | Some v -> v = "RowNumber"
                    | None ->  typeName = "Binary" || false         
                {
                    Name = name
                    refName = 
                        e.GetAttributeWithDefault "ReferenceName" name                       
                    TypeName = typeName
                    IsHidden = hidden
                })

    let properties (element : XElement) =
        element.Descendants(XName.Get(ns + "Property"))
        |> Seq.map (fun p ->
                p.GetAttribute("Name"),
                p.GetAttribute("Type"),
                p.Descendants(XName.Get(bi + "Property")) 
                |> Seq.filter (fun x -> x <> null)
                |> firstToOption)
        |> Seq.choose 
            (function 
                | _, Some "Binary", _ -> None
                | Some n, Some t, e -> 
                    getProperty n ("System." + t) e 
                | _ -> None)


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
                        |> Seq.filter (fun x -> (x.GetAttributeWithDefault "Name" "DefaultValue") = name )
                        |> List.ofSeq
                    let refName = 
                        entitySet.Descendants(XName.Get(bi + "EntitySet")) 
                        |> Seq.choose(fun x -> x.GetAttribute("ReferenceName"))
                        |> firstToOption
                    yield 
                        {
                            Name = name
                            Ref =  refName |> function | Some(x) -> x | None -> name
                            Properties =  properties e    
                        }
        }|> List.ofSeq   
     
