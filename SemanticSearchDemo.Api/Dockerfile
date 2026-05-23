FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY SemanticSearchDemo.Api/SemanticSearchDemo.Api.csproj SemanticSearchDemo.Api/
COPY SemanticSearchDemo.ServiceDefaults/SemanticSearchDemo.ServiceDefaults.csproj SemanticSearchDemo.ServiceDefaults/
RUN dotnet restore SemanticSearchDemo.Api/SemanticSearchDemo.Api.csproj -a $TARGETARCH

COPY SemanticSearchDemo.Api/ SemanticSearchDemo.Api/
COPY SemanticSearchDemo.ServiceDefaults/ SemanticSearchDemo.ServiceDefaults/
RUN dotnet publish SemanticSearchDemo.Api/SemanticSearchDemo.Api.csproj \
    -a $TARGETARCH -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SemanticSearchDemo.Api.dll"]
