FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/MedicineScheduler.Api/MedicineScheduler.Api.csproj src/MedicineScheduler.Api/
RUN dotnet restore src/MedicineScheduler.Api/MedicineScheduler.Api.csproj

COPY src/MedicineScheduler.Api/ src/MedicineScheduler.Api/
RUN dotnet publish src/MedicineScheduler.Api/MedicineScheduler.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# SQLite database lives on a persistent volume mounted at /data
ENV ConnectionStrings__DefaultConnection="Data Source=/data/medicine_scheduler.db"
ENV ASPNETCORE_URLS="http://+:8080"

EXPOSE 8080
ENTRYPOINT ["dotnet", "MedicineScheduler.Api.dll"]
