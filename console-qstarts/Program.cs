﻿using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using ConsoleApp;

// Create a configuration object from the json file
IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appSettings.json", optional: true)
    .AddUserSecrets<Program>()
    .Build();

var openAI = config.GetSection("OpenAI").Get<OpenAISettings>();

var type = "conversationwithfunctions";
switch(type)
{
    case "ask":
        await Ask(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "chat1":
        await Chat1(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "chat2":
        Chat2(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "nonchat":
        NonChat(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "conversation":
        await Conversation(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "conversationwithfunctions":
        await ConversationWithFunctions(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    default:
        Console.WriteLine("Bad choice!!!");
        break;
}
//Console.ReadLine();

async Task Ask(string endpoint, string key, string deploymentOrModelName)
{
    // Enter the deployment name you chose when you deployed the model.
    //string engine = "text-davinci-003";
    var engine = openAI.Deployment;

    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    string prompt = "When was Microsoft founded?";
    Console.Write($"Input: {prompt}\n");

    Response<Completions> completionsResponse =
        await client.GetCompletionsAsync(engine, prompt);
    string completion = completionsResponse.Value.Choices[0].Text;
    Console.WriteLine($"Chatbot: {completion}");
}


async Task Chat1(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var chatCompletionsOptions = new ChatCompletionsOptions()
    {
        Messages =
    {
        new ChatMessage(ChatRole.System, "You are a helpful assistant."),
        new ChatMessage(ChatRole.User, "Does Azure OpenAI support customer managed openAI.Keys?"),
        new ChatMessage(ChatRole.Assistant, "Yes, customer managed openAI.Keys are supported by Azure OpenAI."),
        new ChatMessage(ChatRole.User, "Do other Azure AI services support this too?"),
    },
        MaxTokens = 100
    };

    Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
        deploymentOrModelName: deploymentOrModelName,
        chatCompletionsOptions);
    using StreamingChatCompletions streamingChatCompletions = response.Value;

    await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
    {
        await foreach (ChatMessage message in choice.GetMessageStreaming())
        {
            Console.Write(message.Content);
        }
        Console.WriteLine();
    }

    Console.WriteLine();
}

void Chat2(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var response = client.GetChatCompletions(
               deploymentOrModelName: deploymentOrModelName,
                      new ChatCompletionsOptions()
                      {
            Messages =
                          {
                new ChatMessage(ChatRole.System, "You are a helpful assistant."),
                new ChatMessage(ChatRole.User, "Does Azure OpenAI support customer managed openAI.Keys?"),
                new ChatMessage(ChatRole.Assistant, "Yes, customer managed openAI.Keys are supported by Azure OpenAI."),
                new ChatMessage(ChatRole.User, "Do other Azure AI services support this too?"),
            },
            MaxTokens = 100
        });

    Console.WriteLine(response.Value.Choices[0].Message.Content);

    Console.WriteLine();
}

void NonChat(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var response = client.GetChatCompletions(
               deploymentOrModelName: deploymentOrModelName,
                      new ChatCompletionsOptions()
                      {
                          Messages =
                          {
                            new ChatMessage(ChatRole.System, @"""You are an assistant designed to extract entities from text. Users will paste in a string of text and you will respond with entities you've extracted from the text as a JSON object. Here's an example of your output format:
                            {
                               ""name"": """",
                               ""company"": """",
                               ""phone_number"": """",
                               ""subject"": """",
                            }"""),
                            new ChatMessage(ChatRole.User, "Hello. My name is Robert Smith. I'm calling from Contoso Insurance, Delaware. My colleague mentioned that you are interested in learning about our comprehensive benefits policy. Please call me back at (555) 346-9322. I would like to go over the benefits."),
                          },
                          MaxTokens = 100
                      });

    Console.WriteLine(response.Value.Choices[0].Message.Content);

    Console.WriteLine();
}

async Task Conversation(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var messages = new ChatCompletionsOptions()
    {
        Messages =
        {
            new ChatMessage(ChatRole.System, "Users will ask you questions. Answer in Danish."),
        },
        MaxTokens = 100
    };

    do
    {
        Console.Write("User: ");
        string? userMessage = Console.ReadLine();
        if (string.IsNullOrEmpty(userMessage))
            break;
        messages.Messages.Add(new ChatMessage(ChatRole.User, userMessage));
        Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
            deploymentOrModelName: deploymentOrModelName,
            messages);
        using StreamingChatCompletions streamingChatCompletions = response.Value;

        await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
        {
            await foreach (ChatMessage message in choice.GetMessageStreaming())
            {
                Console.Write(message.Content);
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    } while (true);

    Console.WriteLine();
}

async Task ConversationWithFunctions(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));
    var functions = new FunctionDefinition[]
    {
        new FunctionDefinition()
        {
            Name = "getHotels",
            Description = "Retrieves hotels from the search index based on the parameters provided",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties =  new Dictionary<string,FunctionArgument>{
                    { "location", new FunctionArgument{ type = "string", description = "The location of the hotel (i.e. Seattle, WA)" } },
                    { "max_price", new FunctionArgument{ type = "number", description = "The maximum price for the hotel" } },
                    { "features", new FunctionArgument{ type = "string", description = "A comma separated list of features (i.e. beachfront, free wifi, etc.)" } }
                }
            })
        }
    };
    var messages = new ChatCompletionsOptions()
    {
        Messages =
        {
            new ChatMessage(ChatRole.System, "Please use functions whenever possible."),
        },
        Functions = functions,
        MaxTokens = 100
    };

    do
    {
        Console.Write("User: ");
        string? userMessage = Console.ReadLine();
        if (string.IsNullOrEmpty(userMessage))
            break;
        messages.Messages.Add(new ChatMessage(ChatRole.User, userMessage));
        Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
            deploymentOrModelName: deploymentOrModelName,
            messages
            );
        using StreamingChatCompletions streamingChatCompletions = response.Value;

        await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
        {
            await foreach (ChatMessage message in choice.GetMessageStreaming())
            {
                if(message.Role == ChatRole.Assistant)
                {
                    var function = message.FunctionCall;
                    if(function != null)
                    {
                        Console.WriteLine($"Function: {function.Name}");
                        //Console.WriteLine($"Description: {function.Description}");
                        Console.WriteLine($"Parameters: {function.Arguments}");
                    }
                }
                else
                    Console.Write(message.Content);
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    } while (true);

    Console.WriteLine();
}

public class FunctionArgument
{
    public string description { get; set; }
    public string type { get; set; }
}