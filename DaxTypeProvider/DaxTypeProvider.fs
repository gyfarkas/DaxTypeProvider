namespace LinqToDAX.TypeProvider
open LinqToDAX.Query
open System
open System.Reflection
open System.Linq
open System.Data
open System.Data.Linq
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open ProviderImplementation.ProvidedTypes
open StringExtensions
open LinqToDAX
open LinqToDAX.Query
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

    let addProperty (record : CsdlParser.PropertyRecord) (t : ProvidedTypeDefinition) tableName = 
        let fieldName = "_" + record.Name.toIdentifier.ToLower()
        let prpName = record.Name.toIdentifier
        let pt = Type.GetType(record.TypeName)
        let f = ProvidedField(fieldName, pt)
        t.AddMember f
        let p = ProvidedProperty(prpName, pt, [] )
        p.GetterCode <-  fun args -> Expr.FieldGet(args.[0],f)
        let colName = "'" + tableName + "'"  + "[" + record.refName + "]"
        let tma = typeof<TabularMappingAttribute>
        let ca = 
            { new CustomAttributeData() with 
                    member __.Constructor =  tma.GetConstructors().[0]
                    member __.ConstructorArguments = 
                        upcast 
                            [| 
                                CustomAttributeTypedArgument(typeof<string>, colName) 
                                CustomAttributeTypedArgument(typeof<string>, "'" + tableName + "'")                             
                            |]

                    member __.NamedArguments = upcast [| |]
                    }
        
        p.AddCustomAttribute(ca)
        p.AddXmlDoc("<summary> Represents " + colName + " column </summary>")
        t.AddMember p
        ()

    let tableClassType (record : CsdlParser.EntityRecord) (contextType : ProvidedTypeDefinition) = 
        let t = ProvidedTypeDefinition(record.Name.toIdentifier, Some typeof<obj>, IsErased = false)
        let ca = (new LinqToDAX.TabularMappingAttribute(record.Ref))
        let tma = typeof<TabularTableMappingAttribute>
        let ca = 
            { new CustomAttributeData() with 
                    member __.Constructor =  tma.GetConstructors().[0]
                    member __.ConstructorArguments = upcast [| CustomAttributeTypedArgument(typeof<string>, "'" + record.Ref + "'") |]
                    member __.NamedArguments = upcast [| |]
                    }
        
        t.AddCustomAttribute(ca)
        t.AddXmlDoc("<summary> Represents " + record.Ref + " table</summary>")
        Seq.iter (fun p -> addProperty p t record.Ref) record.Properties
        contextType.AddMember t
        t

    let connectionString = sprintf "Provider=MSOLAP;Data Source=%s;Initial Catalog=%s;";

    let genField  (contextType : ProvidedTypeDefinition) fieldName (fieldType : Type) = 
        let f = ProvidedField(fieldName, fieldType)
        contextType.AddMember(f)
        f

    let genCtor fieldsProperties (conn : string) =        
        let ctor = ProvidedConstructor([])
        ctor.InvokeCode <- (fun ctorArgs -> 
            let this = ctorArgs.[0]
            
            let providerT = typeof<TabularQueryProvider>
            let p = Expr.NewObject( providerT.GetConstructors().[0], [Expr.Value(conn)])
            let provider =  new TabularQueryProvider(conn)
            let newTable (t : ProvidedTypeDefinition) (f:ProvidedField) = 
                let iqt = typeof<System.Linq.IQueryable<_>>.GetGenericTypeDefinition().MakeGenericType(t)
                let ttt = typeof<TabularTable<_>>.GetGenericTypeDefinition().MakeGenericType(t)
                let i = null 
                //System.Activator.CreateInstance(ttt, [|provider|]) 
                //let tctor = ttt.GetConstructors().[0]
                //Expr.Value(())
                let arg = 
                    Expr.Coerce(Expr.Value(i), iqt)
                Expr.FieldSet(this, f, arg)
            
            let init  = 
                fieldsProperties
                |> List.map (fun (t,_,f,p) -> newTable t f )
                |> List.fold (fun expr state -> Expr.Sequential(expr, state)) (Expr.Value(())) 
           
            Expr.Value(()) 
            //init
        )
        ctor

    let addQueryableProperties (contextType : ProvidedTypeDefinition) (classes : ProvidedTypeDefinition seq) =        
            classes
            |> Seq.map (fun c -> c,(typeof<System.Linq.IQueryable<_>>.GetGenericTypeDefinition().MakeGenericType(c), c.Name + "Set"))
            |> Seq.map (fun (c,(t,n)) -> 
                let fieldName = "_" + n.toIdentifier.ToLower()
                let f = genField contextType fieldName t
                let p = ProvidedProperty(n.toIdentifier,t,IsStatic = false)
                p.GetterCode <- fun args -> Expr.FieldGet(args.[0], f)
                //p.SetterCode <- fun args -> <@@ f.SetValue(args.[0], args.[1]) @@> // Expr.FieldSet(args.[0],f, args.[1])
                contextType.AddMember p
                (c,n,f,p)
            ) |> List.ofSeq

    do
        provider.DefineStaticParameters(
            parameters,
            fun typeName args ->
                let serverName = args.[0] :?> string
                let databaseName = args.[1] :?> string
                let contextType = ProvidedTypeDefinition(execAsm, ns, typeName, Some typeof<obj>, IsErased = false)
                let csdl = CsdlParser.csdlSchema serverName databaseName
                let entities = CsdlParser.entities csdl
                let associations = CsdlParser.associations csdl
                let classes =
                    entities
                    |> Seq.map (fun t-> tableClassType t contextType)
                    |> List.ofSeq
               
                let fp = addQueryableProperties contextType classes 
                
                let ctor = genCtor fp (connectionString serverName databaseName)
                contextType.AddMemberDelayed (fun () -> ctor)
                
                tmpAsm.AddTypes [contextType]
                contextType)
    do
        this.RegisterRuntimeAssemblyLocationAsProbingFolder cfg
        tmpAsm.AddTypes [provider]
        this.AddNamespace(ns, [provider])



[<TypeProviderAssembly>]
do()
