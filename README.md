DaxTypeProvider
===============

F# TypeProvider for SSAS tabular

Usage
-----

At the moment it is only usable like this:

```F#
#r @"c:\users\gfarkas\documents\visual studio 2013\Projects\DaxTypeProvider\DaxTypeProvider\bin\Debug\DaxTypeProvider.dll" ;;
#r @"C:\Users\gfarkas\Documents\Visual Studio 2013\Projects\Library1\packages\LinqToDax.0.0.0.6\lib\net45\LinqToDAX.dll"
open LinqToDAX.TypeProvider
open LinqToDAX.Query
open LinqToDAX
open System.Linq
open Microsoft.FSharp.Linq
open Microsoft.FSharp.Quotations
type MyT = LinqToDAX.TypeProvider.TabularContext<"LDDEVCUBEDB2","AdventureWorks Tabular Model SQL 2012">
// Define your library scripting code here

//let db = new MyT()

let p = new TabularTable<MyT.Currency>(new TabularQueryProvider("Provider=MSOLAP;Data Source=LDDEVCUBEDB2;Initial Catalog=AdventureWorks Tabular Model SQL 2012;"))

let q = 
    query{
        for c in p do
        select c.CurrencyCode
    } 
    |> Seq.toList
```
