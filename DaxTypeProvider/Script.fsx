// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.
#r "System.Data.dll"
#r "System.Data.Linq.dll"
#r "System.Xml"
#r "System.Xml.Linq"
#r "Microsoft.AnalysisServices.dll"
#load "CsdlParser.fs"
open LinqToDAX.TypeProvider.CsdlParser
open System.Xml
open System.Xml.Linq
let x = csdlSchema "LDDEVCUBEDB2" "AdventureWorks Tabular Model SQL 2012"

// Define your library scripting code here

let y =  
    query {
            for a in x.Descendants(XName.Get(ns + "AssociationSet")) do
                let ends = a.Descendants(XName.Get(ns + "End")) |> Seq.toArray
                select 
                      ((ends.[0].Attribute(XName.Get("EntitySet")).Value)
                      , (ends.[1].Attribute(XName.Get("EntitySet")).Value))
                   
            }          
            |> Map.ofSeq

let z  = 
    query {
           for e in x.Descendants(XName.Get(ns + "EntitySet")) do 
                let name = e.Attribute(XName.Get("Name")).Value
                let refName = 
                    e.Descendants(XName.Get(bi + "EntitySet")) 
                    |> Seq.map(fun x -> x.Attribute(XName.Get("ReferenceName")))
                    |> Seq.choose (fun x -> if x = null then None else Some(x))
                    |> List.ofSeq
                    |> function
                        | [] -> None
                        | x::_ -> Some(x)
                    
                select 
                    {
                        EntityRecord.Default with
                            Name = name
                            Ref = refName |> function Some(x) -> (x.Value) | None -> name
                    }
        } |> List.ofSeq