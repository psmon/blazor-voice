# Use the official .NET 8 SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["BlazorVoice.csproj", "."]
RUN dotnet restore "BlazorVoice.csproj"
COPY . .
RUN dotnet publish "BlazorVoice.csproj" -c Release -o /app/publish

# Use the official ASP.NET Core runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BlazorVoice.dll"]