FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Booked.sln ./
COPY src/gateway/Booked.Gateway.Api/Booked.Gateway.Api.csproj src/gateway/Booked.Gateway.Api/
COPY src/services/identity/Booked.Identity.Api/Booked.Identity.Api.csproj src/services/identity/Booked.Identity.Api/
COPY src/services/identity/Booked.Identity.Application/Booked.Identity.Application.csproj src/services/identity/Booked.Identity.Application/
COPY src/services/identity/Booked.Identity.Domain/Booked.Identity.Domain.csproj src/services/identity/Booked.Identity.Domain/
COPY src/services/identity/Booked.Identity.Infrastructure/Booked.Identity.Infrastructure.csproj src/services/identity/Booked.Identity.Infrastructure/
COPY src/services/organizations/Booked.Organizations.Api/Booked.Organizations.Api.csproj src/services/organizations/Booked.Organizations.Api/
COPY src/services/organizations/Booked.Organizations.Application/Booked.Organizations.Application.csproj src/services/organizations/Booked.Organizations.Application/
COPY src/services/organizations/Booked.Organizations.Domain/Booked.Organizations.Domain.csproj src/services/organizations/Booked.Organizations.Domain/
COPY src/services/organizations/Booked.Organizations.Infrastructure/Booked.Organizations.Infrastructure.csproj src/services/organizations/Booked.Organizations.Infrastructure/
COPY src/services/scheduling/Booked.Scheduling.Api/Booked.Scheduling.Api.csproj src/services/scheduling/Booked.Scheduling.Api/
COPY src/services/scheduling/Booked.Scheduling.Application/Booked.Scheduling.Application.csproj src/services/scheduling/Booked.Scheduling.Application/
COPY src/services/scheduling/Booked.Scheduling.Domain/Booked.Scheduling.Domain.csproj src/services/scheduling/Booked.Scheduling.Domain/
COPY src/services/scheduling/Booked.Scheduling.Infrastructure/Booked.Scheduling.Infrastructure.csproj src/services/scheduling/Booked.Scheduling.Infrastructure/
COPY src/services/subscriptions/Booked.Subscriptions.Api/Booked.Subscriptions.Api.csproj src/services/subscriptions/Booked.Subscriptions.Api/
COPY src/services/subscriptions/Booked.Subscriptions.Application/Booked.Subscriptions.Application.csproj src/services/subscriptions/Booked.Subscriptions.Application/
COPY src/services/subscriptions/Booked.Subscriptions.Domain/Booked.Subscriptions.Domain.csproj src/services/subscriptions/Booked.Subscriptions.Domain/
COPY src/services/subscriptions/Booked.Subscriptions.Infrastructure/Booked.Subscriptions.Infrastructure.csproj src/services/subscriptions/Booked.Subscriptions.Infrastructure/
COPY src/services/chatbot-book/Booked.Chatbot.Api/Booked.Chatbot.Api.csproj src/services/chatbot-book/Booked.Chatbot.Api/
COPY src/services/chatbot-book/Booked.Chatbot.Application/Booked.Chatbot.Application.csproj src/services/chatbot-book/Booked.Chatbot.Application/
COPY src/services/chatbot-book/Booked.Chatbot.Domain/Booked.Chatbot.Domain.csproj src/services/chatbot-book/Booked.Chatbot.Domain/
COPY src/services/chatbot-book/Booked.Chatbot.Infrastructure/Booked.Chatbot.Infrastructure.csproj src/services/chatbot-book/Booked.Chatbot.Infrastructure/
COPY src/services/admin/Booked.Admin.Api/Booked.Admin.Api.csproj src/services/admin/Booked.Admin.Api/
COPY src/services/admin/Booked.Admin.Application/Booked.Admin.Application.csproj src/services/admin/Booked.Admin.Application/
COPY src/services/admin/Booked.Admin.Domain/Booked.Admin.Domain.csproj src/services/admin/Booked.Admin.Domain/
COPY src/services/admin/Booked.Admin.Infrastructure/Booked.Admin.Infrastructure.csproj src/services/admin/Booked.Admin.Infrastructure/
COPY src/shared/Booked.Shared.BuildingBlocks/Booked.Shared.BuildingBlocks.csproj src/shared/Booked.Shared.BuildingBlocks/
COPY src/shared/Booked.Shared.Contracts/Booked.Shared.Contracts.csproj src/shared/Booked.Shared.Contracts/
COPY src/shared/Booked.Shared.Observability/Booked.Shared.Observability.csproj src/shared/Booked.Shared.Observability/

RUN dotnet restore src/services/identity/Booked.Identity.Api/Booked.Identity.Api.csproj

COPY . .
RUN dotnet publish src/services/identity/Booked.Identity.Api/Booked.Identity.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Booked.Identity.Api.dll"]
