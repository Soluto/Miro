FROM microsoft/dotnet:sdk
WORKDIR /app


# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . /app

# Build runtime image
WORKDIR /app
CMD ["dotnet", "test", "--logger", "trx;LogFileName=test_results.trx"]