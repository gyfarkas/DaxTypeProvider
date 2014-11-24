namespace LinqToDAX.TypeProvider

open System.Xml
open System.Xml.Linq

open System.Data
open System.Data.Linq
open Microsoft.FSharp.Linq
open Microsoft.AnalysisServices

module CsdlParser = 
    type EntityRecord = 
        {
            Ref : string
            Name : string
        }
        static member Default = 
            {
                Ref  = "Invalid"
                Name = ""
            }

    let ns = @"{http://schemas.microsoft.com/ado/2008/09/edm}"
    let bi = @"{http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions}"
    
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
        query {
                for e in csdl.Descendants(XName.Get(ns + "EntitySet")) do 
                let name = e.Attribute(XName.Get("Name")).Value
                let refName = 
                    e.Descendants(XName.Get(bi + "EntitySet")) 
                    |> Seq.map(fun x -> x.Attribute(XName.Get("ReferenceName")))
                    |> Seq.choose (fun x -> if x = null then None else Some(x))
                    |> List.ofSeq
                    |> function
                        | []   -> None
                        | x::_ -> Some(x)
                    
                select 
                    {
                        Name = name
                        Ref = 
                            refName 
                            |> function 
                                | Some(x) -> (x.Value) 
                                | None -> name
                    }
        } 
        |> List.ofSeq   
     
        