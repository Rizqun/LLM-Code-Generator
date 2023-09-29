# LLM-Code-Generator
## Overview

LLM Code Generator is a console application built in .NET that empowers users to generate code effortlessly based on Jira requirements and API Documentation URLs. This tool also supports embedding using Qdrant, a vector database, allowing users to create new collections in Qdrant by providing an API Documentation URL.

## Dependencies

- [HtmlAgilityPack](https://html-agility-pack.net) - Version 1.11.52
- [Microsoft.Extensions.Configuration.Json](https://dot.net) - Version 8.0.0-preview.7.23375.6
- [Microsoft.ML](https://dot.net/ml) - Version 2.0.1
- [Microsoft.SemanticKernel](https://aka.ms/semantic-kernel) - Version 0.24.230918.1-preview
- [Microsoft.SemanticKernel.Connectors.Memory.Qdrant](https://aka.ms/semantic-kernel) - Version 0.24.230918.1-preview
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - Version 0.47.0

## Getting Started
**1. Clone the Repository**
```
git clone https://github.com/Rizqun/LLM-Code-Generator.git
cd llm-code-generator
```
**2. Configure `appsettings.json`**
```
You can create Jira API token here [Create Jira API Token](https://id.atlassian.com/manage-profile/security/api-tokens) and Qdrant cloud account here [Qdrant Cloud](https://cloud.qdrant.io/login)

{
  "OpenAI": {
    "Model": "YOUR_GPT_MODEL", //gpt-3.5-turbo
    "Embedding": "YOUR_EMBEDDING_MODEL" //text-embedding-ada-002
    "Key": "YOUR_GPT_KEY"
  },
  "Jira": {
    "Host": "YOUR_JIRA_HOST"
    "Username": "YOUR_USERNAME",
    "Key": "YOUR_JIRA_API_TOKEN"
  },
  "Qdrant": {
    "Host": "YOUR_QDRANT_CLOUD_HOST"
    "Key": "YOUR_QDRANT_CLOUD_KEY"
  }
}
```
**3. Build and Run**

Build and run the application using the following commands:
```
dotnet build
dotnet run
```

## Usage
LLM Code Generator has two main features, embedding and generate. In embedding, user able to list collection from Qdrant, add new collection, and delete collection. In generate, user able to generate code using Jira, generate code using API documentation URL, and generate code using Jira and API documentation URL. Please note that you need to have a collection in Qdrant to be able to generate using API documentation URL.

![Screenshot 2023-09-28 201609](https://github.com/Rizqun/LLM-Code-Generator/assets/50146188/6de8e5aa-3ed7-453f-8f7c-8bebc36e5813)

**1. List Collection**

List collection will list all of your collection in Qdrant, including the total of vectors for each collection.

![Screenshot 2023-09-28 202103](https://github.com/Rizqun/LLM-Code-Generator/assets/50146188/0f172ff9-b7d6-4916-9ad5-32a3395cfa61)

**2. Add Collection**

Add new collection require you to input the name of collection and the API documentation URL that you want to embed. You can add multiple API documentation URL by separating it using comma. Some API documentation URL has its properties, request body, authentication method, and the other important things in different URL, so it would be helpful in that case!

![Screenshot 2023-09-28 202745](https://github.com/Rizqun/LLM-Code-Generator/assets/50146188/d949ff82-8cdf-4ce5-9cc1-795a360d3dfb)

**3. Delete Collection**

Delete collection require you to input the name of collection that you wanted to delete from Qdrant.

**4. Generate**

There are 3 options in generate feature. The input required depends on the option that you choose. Basically it needs you to input your prompt, the jira key that will give the application detailed information about requirement, the collection name from Qdrant, project name for the generated solution, and the project location.

![Screenshot 2023-09-28 203835](https://github.com/Rizqun/LLM-Code-Generator/assets/50146188/fcfe21a9-df58-4680-8071-9f4a2ed7231e)
