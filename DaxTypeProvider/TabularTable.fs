namespace LinqToDAX.Helper
module TabularTable = 
    open LinqToDAX
    open LinqToDAX.Query
    open System
    open System.Reflection

    let newTable (t:Type) (connectionString:string) = 
        let ttt = typedefof<TabularTable<_>>.MakeGenericType(t)
        let i = null 
        let tctor = ttt.GetConstructors().[0]
        let p = new TabularQueryProvider(connectionString)
        tctor.Invoke([|p|])
    