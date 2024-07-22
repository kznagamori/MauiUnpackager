@echo off
dotnet pack
dotnet tool install --global --add-source ./nupkg MauiUnpackager
