// Import necessary modules
open IcedTasks
open Microsoft.SemanticKernel
open System
open System.Text.Json
open System.Text.Json.Serialization
open System.ComponentModel
open Microsoft.SemanticKernel.Connectors.OpenAI
// Define pipeline
open System
open System.Text.Json
// Define custom type for JSON values
type JsonValue =
    | JsonObject of Map<string, JsonValue>
    | JsonArray of JsonValue list
    | JsonString of string
    | JsonNumber of float
    | JsonBool of bool
    | JsonNull
// Define JSON Schema types
type JsonSchema =
    | SchemaObject of Map<string, JsonSchema>
    | SchemaArray of JsonSchema
    | SchemaString
    | SchemaNumber
    | SchemaBool
    | SchemaNull
    | SchemaRequired of JsonSchema
// Define the context type
type Context = { Input: JsonElement }
// Define result type to support railway track style
type Result<'T> =
    | Success of 'T
    | Failure of string
// Convert JSON Element to custom JsonValue type
let rec toJsonValue (jsonElement: JsonElement): JsonValue =
    match jsonElement.ValueKind with
    | JsonValueKind.Object ->
        jsonElement.EnumerateObject()
        |> Seq.map (fun prop -> prop.Name, toJsonValue prop.Value)
        |> Map.ofSeq
        |> JsonObject
    | JsonValueKind.Array ->
        jsonElement.EnumerateArray()
        |> Seq.map toJsonValue
        |> List.ofSeq
        |> JsonArray
    | JsonValueKind.String -> JsonString (jsonElement.GetString())
    | JsonValueKind.Number -> JsonNumber (jsonElement.GetDouble())
    | JsonValueKind.True -> JsonBool true
    | JsonValueKind.False -> JsonBool false
    | JsonValueKind.Null -> JsonNull
    | _ -> failwith "Unsupported JSON value kind"
// Validate JSON against the given schema
let rec validateJson (schema: JsonSchema) (json: JsonValue): bool =
    match schema, json with
    | SchemaObject properties, JsonObject obj ->
        properties |> Map.forall (fun key valueSchema ->
            match obj.TryFind key with
            | Some value -> validateJson valueSchema value
            | None -> match valueSchema with
                      | SchemaRequired _ -> false
                      | _ -> true // Non-required fields
        )
    | SchemaArray itemSchema, JsonArray items ->
        items |> List.forall (validateJson itemSchema)
    | SchemaString, JsonString _
    | SchemaNumber, JsonNumber _
    | SchemaBool, JsonBool _
    | SchemaNull, JsonNull -> true
    | SchemaRequired innerSchema, value -> validateJson innerSchema value
    | _ -> false
// Define a function for JSON Schema validation without third-party libraries
let validateSchema schema context =
    let jsonValue = toJsonValue context.Input
    if validateJson schema jsonValue then
        Success context
    else
        Failure "JSON Schema validation failed"
// Create context with new value
let createContextWithNewValue newValue =
    let json = JsonDocument.Parse($"{{ \"value\": {newValue} }}").RootElement
    { Input = json }
// JSON operation function
let jsonOperation opFunc (context: Context) : Result<Context> =
    try
        let value = context.Input.GetProperty("value").GetInt32()
        let newValue = opFunc value
        Success (createContextWithNewValue newValue)
    with
    | ex -> Failure ex.Message
// Define operation functions
let addOne = jsonOperation ((+) 1)
let multiplyByTwo = jsonOperation ((*) 2)
let square = jsonOperation (fun x -> x * x)
// Create a mapping from function names to actual functions
let functionMap =
    [ "addOne", addOne
      "multiplyByTwo", multiplyByTwo
      "square", square ]
    |> Map.ofList
// Convert function name to corresponding function
let toFunction name : Context -> Result<Context> =
    match Map.tryFind name functionMap with
    | Some func -> func
    | None -> fun _ -> Failure (sprintf "Function %s not found" name)
// Define pipeline function (based on function composition)
let pipeline schema context functions =
    match validateSchema schema context with
    | Failure msg -> Failure msg
    | Success validatedContext ->
        functions
        |> Array.fold (fun acc funcName ->
            match acc with
            | Failure msg -> Failure msg
            | Success ctx ->
                // Use the function obtained via toFunction which returns Result<Context>
                toFunction funcName ctx
        ) (Success validatedContext)
// Define JSON Schema
let schemaJson = SchemaObject (Map.ofList [ ("value", SchemaRequired SchemaNumber) ])
// Create initial context (including JSON input)
let initialContext = createContextWithNewValue 3
// Create function sequence
let functionNames = [| "addOne"; "multiplyByTwo"; "square" |]
// Use pipeline function
let resultContext = pipeline schemaJson initialContext functionNames
// Print results
printfn "This is a test."
match resultContext with
| Success ctx -> printfn "Result: %s" (ctx.Input.ToString())
| Failure msg -> printfn "Error: %s" msg
printfn "This is a SK plugin."
type Calculate() =
    [<KernelFunction>]
    [<Description("")>]    
    member this.Pipeline([<Description("input number")>] input:int , [<Description("choose from addOne multiplyByTwo square")>] functionNames:string[] ) = 
          
        // Define JSON Schema
        let schemaJson = SchemaObject (Map.ofList [ ("value", SchemaRequired SchemaNumber) ])
        // Create initial context (including JSON input)
        let initialContext = createContextWithNewValue input
        // Use pipeline function
        let resultContext = pipeline schemaJson initialContext functionNames
        // Print results
        match resultContext with
        | Success ctx -> sprintf "Result: %s" (ctx.Input.ToString())
        | Failure msg -> sprintf "Error: %s" msg
        
let key = "sk-"  
let model = "gpt-4o-mini" 
let chat_url ="https://api.openai.com/v1/chat/completions" 
let builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId = model, endpoint = Uri(chat_url), apiKey = key)
let plugins = builder.Plugins.AddFromType<Calculate>()
let kernel = builder.Build()
asyncEx {
    let settings = OpenAIPromptExecutionSettings()
    settings.ToolCallBehavior <- ToolCallBehavior.AutoInvokeKernelFunctions
    let arguments = new KernelArguments(settings)
    printfn "Plugins count : %d" kernel.Plugins.Count
    let! response2 = kernel.InvokePromptAsync("Calculate: input 3 to Pipeline [addOne addOne]", arguments) 
    Console.WriteLine(response2)
} |> Async.RunSynchronously |> ignore
