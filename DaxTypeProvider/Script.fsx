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

let z  = entities x
