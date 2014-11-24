namespace LinqToDAX.TypeProvider
open LinqToDAX.Query
open System
open System.Reflection
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes

[<TypeProvider>]
type DaxTypeProvider(cfg : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    
    let ns = "LinqToDAX.TypeProvider"

    let execAsm = System.Reflection.Assembly.GetExecutingAssembly()
    let path = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".dll")
    let tmpAsm = ProvidedAssembly path
    let provider = ProvidedTypeDefinition(execAsm, ns, "TabularContext", Some typeof<obj>, IsErased = false)

    let parameters = [
                        ProvidedStaticParameter("ServerName", typeof<string>)
                        ProvidedStaticParameter("DatabaseName", typeof<string>)
                     ]



    do
        provider.DefineStaticParameters(
            parameters,
            fun typeName args ->
                let serverName = args.[0] :?> string
                let databaseName = args.[1] :?> string
                let contextType = ProvidedTypeDefinition(execAsm, ns, typeName, Some typeof<obj>, IsErased = false)
                let csdl = CsdlParser.csdlSchema serverName databaseName
                //addRawProperty templateType template
                //let vars, expr = parseTemplate { ParserState.Empty with Rest = stringToChars template } []
                //addConstructor templateType vars expr
                tmpAsm.AddTypes [contextType]
                contextType)

    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        tmpAsm.AddTypes [provider]
        this.AddNamespace(ns, [provider])



[<TypeProviderAssembly>]
do()
