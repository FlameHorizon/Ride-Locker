#! /bin/bash

dotnet build -c Release
dotnet trace collect -o trace.netrace -- dotnet run ./bin/Release/net10.0/Website
dotnet trace convert trace.netrace --format speedscope

