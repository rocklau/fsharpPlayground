
//C# version https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/GettingStarted/Step2_Add_Plugins.cs
open IcedTasks
open Microsoft.SemanticKernel
open FSharp.Data
open System
open System.ComponentModel

open Microsoft.SemanticKernel.Connectors.OpenAI
open System.Globalization
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open FSharp.Control
open System.Text.Json
open System.Text.Json.Serialization


type TimeInformationPlugin() =
    [<KernelFunction>]
    [<Description("Retrieves the current time in UTC.")>]
    member this.GetCurrentUtcTime() = DateTime.UtcNow.ToString("R")




[<JsonConverter(typeof<JsonStringEnumConverter>)>]
type WidgetType =
    | [<Description("A widget that is useful.")>] Useful  = 0 
    | [<Description("A widget that is decorative.")>] Decorative =1 
[<JsonConverter(typeof<JsonStringEnumConverter>)>]
type WidgetColor =
    | [<Description("Use when creating a red item.")>] Red =0 
    | [<Description("Use when creating a green item.")>] Green = 1
    | [<Description("Use when creating a blue item.")>] Blue =2






  
type WidgetDetails =
    {
        SerialNumber: string
        Type: WidgetType
        Colors: WidgetColor[]
    }

type WidgetFactoryPlugin() =

    [<KernelFunction>]
    [<Description("Creates a new widget of the specified type and colors")>]    
    member   this.CreateWidget([<Description("The type of widget to be created")>] widgetType:WidgetType , [<Description("The colors of the widget to be created")>] widgetColors:WidgetColor[] ) = 
        let number = $"{widgetType}-{String.Join('-',widgetColors)}-{Guid.NewGuid()}"
        Console.WriteLine $"Call a widget {number}"
        {
            SerialNumber = number
            Type = widgetType
            Colors = widgetColors
        }
    

 


let key = "sk-"  
let model = "gpt-4o-mini" 
let chat_url ="https://api.openai.com/v1/chat/completions" 

let builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId = model, endpoint = Uri(chat_url), apiKey = key)
let plugins =builder.Plugins.AddFromType<TimeInformationPlugin>().AddFromType<WidgetFactoryPlugin>()
let kernel = builder.Build()

    


let example1 =
    fun () ->
        task {
            printfn "example1"

            let! response = kernel.InvokePromptAsync("What color is the sky?")
            Console.WriteLine response

            let arguments = new KernelArguments()
            arguments.Add("topic", "sea")

            do!
                kernel.InvokePromptStreamingAsync("What color is the {{$topic}}?", arguments)
                |> TaskSeq.iter (fun item -> Console.Write item)
                |> Async.AwaitTask

        }

Console.WriteLine ""



let example2 =
    fun () ->
        asyncEx {
            printfn "example2"

            let arguments = new KernelArguments()
            arguments.Add("topic", "forest")

            let stream = kernel.InvokePromptStreamingAsync("{What color is {$topic}} ?", arguments)

            for item in stream do
                Console.Write item

        } 



let example3 =
    fun () ->
        
            printfn "example3"


            let ChatPrompt =
                """
                    <message role="user">What is Seattle?</message>
                    <message role="system">Respond with JSON.</message>
                    """

            let chatSemanticFunction = kernel.CreateFunctionFromPrompt(ChatPrompt)
           
            Console.WriteLine("Chat Prompt:")
            Console.WriteLine(ChatPrompt)
            Console.WriteLine("Chat Prompt Streaming Result:")
            let chatPromptResult = kernel.InvokeAsync(chatSemanticFunction)
            Console.WriteLine("Chat Prompt Result:")
            Console.WriteLine(chatPromptResult.Result)

           

            // for message in kernel.InvokeStreamingAsync<StreamingKernelContent>(chatSemanticFunction) do
            //     Console.Write  message 
              



        


let example4 = fun()->
     asyncEx{
         let! response = kernel.InvokePromptAsync("The current time is {{TimeInformationPlugin.GetCurrentUtcTime}}. How many days until Christmas?")
         Console.WriteLine response
     }

let example5 = fun()->
  asyncEx{
 // Example 3: Invoke the kernel with a prompt and allow the AI to automatically invoke functions
        let settings = OpenAIPromptExecutionSettings()
        settings.ToolCallBehavior <- ToolCallBehavior.AutoInvokeKernelFunctions
        let arguments = new KernelArguments(settings)
        printfn "Plugins count : %d" kernel.Plugins.Count
    

         
        // Example 4: Invoke the kernel with a prompt and allow the AI to automatically invoke functions that use enumerations
        let! response2 = kernel.InvokePromptAsync("""Create a beautiful scarlet colored widget for me.""", arguments) 
        Console.WriteLine(response2)
    


     
    }

let examplex = fun()->
  asyncEx{
     printfn "ssss"
  }

[examplex;example5] |> Seq.map (fun f -> f()) |> Async.Sequential  |>Async.RunSynchronously|> ignore



