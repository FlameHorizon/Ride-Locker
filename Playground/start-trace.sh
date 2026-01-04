#! /bin/bash

dotnet build -c Release
dotnet trace collect -o trace.netrace -- ./bin/Release/net10.0/Playground
dotnet trace convert trace.netrace --format speedscope
