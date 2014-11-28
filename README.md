DaxTypeProvider
===============

F# TypeProvider for SSAS tabular

Usage
-----

At the moment it is only usable like this:

```F#
#r @"c:\users\gfarkas\documents\visual studio 2013\Projects\DaxTypeProvider\DaxTypeProvider\bin\Debug\DaxTypeProvider.dll" ;;
#r @"c:\users\gfarkas\documents\visual studio 2013\Projects\DaxTypeProvider\DaxTypeProvider\bin\Debug\TabularTableExtensions.dll" ;;
#r @"C:\Users\gfarkas\Source\Repos\codeplex\linqtodax\LinqToDAX\bin\Debug\LinqToDAX.dll"
#r @"C:\Users\gfarkas\Source\Repos\codeplex\linqtodax\LinqToDAX\bin\Debug\TabularEntities.dll"
open LinqToDAX.TypeProvider
open LinqToDAX.Query
open LinqToDAX
open LinqToDAX.Helper.TabularTable
open System
open System.Linq
open System.Collections
open Microsoft.FSharp.Linq
open Microsoft.FSharp.Quotations
type AdventureWorks = LinqToDAX.TypeProvider.TabularContext<"LDDEVCUBEDB2","AdventureWorks Tabular Model SQL 2012">
// Define your library scripting code here

let db = new AdventureWorks()


let provider = new TabularQueryProvider("Provider=MSOLAP;Data Source=LDDEVCUBEDB2;Initial Catalog=AdventureWorks Tabular Model SQL 2012;")
let p = new TabularTable<AdventureWorks.Currency>(provider)
let sales = new TabularTable<AdventureWorks.InternetSales>(provider)
let q = 
    (query{
        for c in p do
        for s in sales do
          where (c.CurrencyCode = "GBP")
          select (s.SalesAmount.Sum())
    })
    |> Seq.head

```
