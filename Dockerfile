FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AzureOpenAIAgentApi/AzureOpenAIAgentApi.csproj", "AzureOpenAIAgentApi/"]
RUN dotnet restore "AzureOpenAIAgentApi/AzureOpenAIAgentApi.csproj"
COPY . .
WORKDIR "/src/AzureOpenAIAgentApi"
RUN dotnet build "AzureOpenAIAgentApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AzureOpenAIAgentApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AzureOpenAIAgentApi.dll"]